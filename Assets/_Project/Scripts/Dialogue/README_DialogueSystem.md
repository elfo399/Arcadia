# Arcadia Dialogue System

Sistema dialoghi unico, data-driven e riutilizzabile per NPC, oggetti parlanti, boss e servizi dell'Hub. Compatibile con Unity 2022.3.62f3, New Input System, TextMeshPro e il salvataggio JSON esistente.

## 1. Architettura

Flusso dati e runtime:

```text
PlayerInteraction -> IInteractable -> NPCInteractable
                                      |
                                      v
DialogueProfile -> DialogueConversation -> DialogueNode -> DialogueChoice
                         |                    |                  |
                         +---------- DialogueConditionGroup -----+
                         +---------------- DialogueAction --------+

DialogueManager
  |- DialogueConditionEvaluator
  |- DialogueActionRunner -> PlayerStats / PlayerInventory / QuestManager
  |- DialogueUI
  |- DialogueActor registry
  |- NpcServiceRegistry
  `- DialogueTeleportTarget registry
```

`DialogueManager` coordina soltanto il flusso. La UI non valuta condizioni e non modifica gameplay; evaluator e runner sono separati. `PlayerStats` resta proprietario del player persistente e dell'unico snapshot di salvataggio. Prima di valutare un profile, il manager forza l'applicazione delle sezioni quest/inventory caricate in modo differito, evitando condizioni stale o reward poi sovrascritte.

Il manager puo essere `DontDestroyOnLoad` e applica il normale duplicate guard. A ogni apertura ricrea il `DialogueRuntimeContext`, quindi riferimenti a player, inventory, dungeon e NPC vengono riallineati dopo i cambi scena.

## 2. Creare uno speaker

Usare `Assets > Create > Arcadia > Dialogue > Speaker`.

- `speakerId`: ID stabile, non usare il testo visualizzato.
- `displayName`: nome mostrato per un NPC.
- `portrait`: opzionale; se null il contenitore portrait viene nascosto.
- `isPlayer`: per lo speaker speciale del player.

Per uno speaker player lasciare `displayName` vuoto e attivare `isPlayer`: il nome viene sempre risolto da `PlayerStats.PlayerName`.

## 3. Creare una conversazione

Usare `Assets > Create > Arcadia > Dialogue > Conversation`.

Una singola `DialogueConversation` contiene tutte le battute:

- `conversationId`: ID stabile e univoco;
- `startNodeId`: ID del primo node;
- `nodes`: lista inline di `DialogueNode`.

Non va creato uno ScriptableObject per ogni frase. Il custom Inspector mostra gli errori di authoring (ID vuoti/duplicati e riferimenti mancanti).

## 4. Configurare i node

Ogni `DialogueNode` espone:

- `nodeId`, `speaker`, `text`;
- portrait override opzionale;
- Animator trigger opzionale;
- `voiceClip` opzionale;
- conditions;
- actions on enter/on exit;
- `nextNodeId`;
- choices.

La conversazione puo alternare liberamente Fabbro, Mago, Player e altri `DialogueSpeakerData`. Per le animazioni, aggiungere `DialogueActor` agli attori presenti nella scena e associare lo speaker corrispondente. Un trigger mancante genera un warning e non interrompe il dialogo.

## 5. Configurare le choices

Campi principali:

- `choiceId`: stabile e univoco nel node;
- `text`;
- conditions e actions;
- `nextNodeId`;
- `returnNodeId`: ritorno automatico a un menu dopo la fine del ramo;
- `playerSpeaksChoice`: default true;
- `unavailableDisplay`: `Disabled` (default) oppure `Hidden`;
- `showReadIndicator`.

Con `playerSpeaksChoice`, il manager mostra automaticamente prima `PlayerStats.PlayerName: "testo choice"`, poi esegue le actions e passa al node successivo. Non serve duplicare la battuta in un secondo node.

Le choice non disponibili rimangono visibili con prefisso lucchetto e `Button.interactable = false`. Una choice gia selezionata mostra il prefisso di lettura ma resta selezionabile.

## 6. Condizioni AND, OR e NOT

`DialogueConditionGroup` contiene condizioni e sottogruppi. Impostare:

- `logic = And` o `Or`;
- `negate = true` per negare l'intero gruppo;
- `condition.negate = true` per negare una singola condizione.

Esempio:

```text
AND
  QuestState(mage_quest, Completed)
  NOT StoryFlag(betrayed_mage)
  OR
    Intelligence >= 20
    Faith >= 25
```

I tipi iniziali coprono attributi, livello, Karma/Benedetto/Malefico, quest state, story flag, item/quantita, coins della run, dungeon floor, node letto e choice selezionata. Le condizioni sugli attributi leggono i valori base persistenti (non bonus temporanei); `Has Coins` usa `runCoins`, coerentemente con il wallet gameplay attuale. Gli operatori numerici sono tipizzati (`Equal`, `NotEqual`, `Greater`, `GreaterOrEqual`, `Less`, `LessOrEqual`).

La mappatura quest segue lo stato esistente: `NotStarted` = ID assente, `Active` = presente/non completata, `ReadyToComplete` = completata con reward non riscossa, `Completed` = flag completed, `RewardClaimed` = reward riscossa.

## 7. Actions

Le actions disponibili sono:

- Modify Karma/Benedetto/Malefico;
- Give Attribute Point;
- Add/Remove Coins (wallet `runCoins`);
- Add/Remove Item per generic, weapon, armor, magic e usable;
- Start/Complete Quest;
- Fail Quest (warning controllato: il QuestManager attuale non possiede questo stato);
- Set/Clear Story Flag;
- Restore Health/Mana/Stamina/Flasks;
- Open Service;
- Teleport.

Gli attributi principali non vengono mai modificati da un'action. I premi attributo incrementano soltanto `unspentAttributePoints`. Il runner usa API controllate e richiede un unico `PlayerStats.SaveStats()` alla fine del batch persistente.

Per costi/acquisti impostare `stopOnFailure` sull'action critica (`RemoveCoins`, `RemoveItem`, ecc.). Il runner esegue prima un pre-check di tutte le action bloccanti, cosi un requisito sicuramente mancante non applica le action precedenti. Un fallimento sicuro ripresenta il node; se un servizio esterno fallisce dopo possibili effetti parziali, il dialogo viene invece chiuso per impedire retry e duplicazioni. Le action non sono un database transazionale: operazioni commerciali complesse appartengono a un unico `INpcService` atomico.

Per `StartQuest`, assegnare una `QuestDefinition`. Registrare la stessa definition nel campo `Quest Definition Catalog` del `QuestManager`, non tra le quest iniziali: il catalogo rende risolvibili reward e riferimenti dopo il reload senza attivare la quest prima del dialogo. Configurare il manager della scena bootstrap (quello che diventa persistente e vince sui duplicati delle scene successive). Le `Initial Quest Definitions` restano riservate alle quest gia attive all'inizio di una nuova partita.

Gli item usati da action e reward devono inoltre essere presenti nell'`ItemDatabase` del player, come gia richiesto dal save inventory esistente; gli asset vengono ripristinati tramite i nomi stabili registrati nel database.

Una choice `Rifiuta` senza action lascia la quest riproponibile (policy predefinita). Per un rifiuto permanente, impostare una Story Flag e usarla nelle condizioni del profile/choice.

## 8. DialogueProfile

Usare `Assets > Create > Arcadia > Dialogue > Profile`.

Ogni regola contiene `ruleId`, `priority`, conditions e conversation. Vince la prima regola valida con priorita numerica maggiore; a parita viene mantenuto l'ordine della lista. Assegnare sempre `fallbackConversation`.

Esempio:

```text
100  blacksmith_hammer_ready  -> Blacksmith_HammerReturn
90   forest_boss_defeated     -> Blacksmith_AfterForestBoss
80   NOT met_blacksmith       -> Blacksmith_Introduction
0    fallback                 -> Blacksmith_Default
```

## 9. Configurare NPCInteractable

Sul GameObject/collider interagibile:

1. aggiungere `DialogueActor` e assegnare speaker, Animator e Focus Transform;
2. aggiungere `NPCInteractable`;
3. assegnare prompt, profile e Main Speaker Actor;
4. assegnare eventualmente Look Target;
5. configurare rotazione player/NPC, velocita e `Allow Cancel`;
6. assicurarsi che il collider sia incluso nel `interactLayer` del `PlayerInteraction`.

`PlayerInteraction` continua a emettere l'evento quest `Interact`; `NPCInteractable` non lo duplica.

## 10. Configurare la Dialogue UI

Il menu Editor `Arcadia > Dialogue > Create UI Prefab` genera un prefab non distruttivo. In alternativa creare:

```text
DialogueSystem                    (DialogueManager, AudioSource)
`- Canvas                         (Canvas, CanvasScaler, GraphicRaycaster, DialogueUI)
   `- DialogueRoot                (Image/panel)
      |- ContentRow               (HorizontalLayoutGroup)
      |  |- PortraitContainer
      |  |  `- PortraitImage
      |  `- LineContent
      |     |- SpeakerName        (TMP)
      |     `- DialogueText       (TMP)
      |- ChoicesScrollView        (ScrollRect verticale)
      |  `- Viewport              (RectMask2D)
      |     `- ChoicesRoot         (VerticalLayoutGroup + ContentSizeFitter)
      |        `- ChoiceButtonTemplate (Button + TMP, inattivo)
      `- ContinueIndicator
```

Collegare nel `DialogueUI` root, portrait, testi, choices root, button template e indicatore. `Choices Scroll Rect` e opzionale per UI manuali; se assegnato, la lista riparte dall'alto e segue automaticamente la scelta selezionata da tastiera/gamepad. Il prefab generato usa una viewport mascherata e un content ad altezza dinamica, quindi menu lunghi scorrono senza sovrapporsi a `ContentRow`. Collegare la UI nel `DialogueManager`, più lo speaker player. Il portrait container deve stare in un layout che ridistribuisce lo spazio quando viene disattivato.

Il prefab/oggetto `DialogueSystem` va aggiunto una sola volta alla scena di avvio. Con `persistAcrossScenes` resta vivo tra Hub e Dungeon e distrugge eventuali duplicati.

## 11. Esempio Fabbro

Il menu `Arcadia > Dialogue > Create Blacksmith Example Assets` genera senza sovrascrivere:

- speaker Player e Blacksmith;
- conversazione introduction;
- conversazione/default menu e ramo lore;
- profile prioritario basato su `met_blacksmith`.

Con `HubSceneV1` aperta, il menu `Arcadia > Dialogue > Setup Active Hub Blacksmith` esegue l'intero setup in modo idempotente: genera prima gli asset e il prefab UI, configura `__NPC/city_dwellers_1` sul layer `Interactable` con collider/actor/profile e aggiunge `DialogueSystem` sotto `__SYSTEM` se manca.

Per usarlo:

1. eseguire il menu;
2. creare/individuare il Fabbro nella Hub;
3. aggiungere `DialogueActor` e assegnare lo speaker Blacksmith;
4. aggiungere `NPCInteractable` e assegnare il profile generato;
5. assegnare lo speaker Player nel `DialogueManager`;
6. verificare che il Fabbro abbia un collider sul layer interagibile.

L'esempio dimostra flag `met_blacksmith`, menu, choice Intelligence 20 disabilitata, ramo lore con ritorno al menu e choice oscura con Malefico +5, Karma -2 e `accepted_dark_power`.

`Dialogue_Blacksmith_Lore.asset` e anche un template standalone di authoring. Il ramo lore effettivamente raggiunto dal profile e incorporato in Introduction/Default, perche la prima versione naviga node della conversazione corrente e non effettua salti impliciti tra asset diversi.

## 12. Aggiungere un ConditionType

1. aggiungere il valore a `DialogueConditionType`;
2. aggiungere soltanto i campi dati strettamente necessari a `DialogueCondition`;
3. implementare il case in `DialogueConditionEvaluator` usando API tipizzate;
4. aggiornare `GetConfigurationError` e il PropertyDrawer Editor;
5. aggiungere un caso alla diagnostica/acceptance test.

Non usare reflection o nomi di campi runtime arbitrari.

## 13. Aggiungere un DialogueActionType

1. aggiungere il valore a `DialogueActionType`;
2. aggiornare `DialogueAction.GetConfigurationError`;
3. implementare l'adapter in `DialogueActionRunner`;
4. chiamare l'API autorevole del sistema Arcadia;
5. indicare correttamente se lo stato persistente e cambiato, così il batch salva una sola volta;
6. aggiornare il PropertyDrawer.

## 14. Registrare un NPC Service

Implementare `INpcService`, oppure ereditare da `NpcServiceBehaviour`:

```csharp
public sealed class BlacksmithUpgradeService : NpcServiceBehaviour
{
    public override bool Open(NpcServiceContext context)
    {
        // Apri la UI esistente del servizio.
        return true;
    }

    public override void Close()
    {
    }
}
```

Impostare un `ServiceId` univoco e usare lo stesso ID nell'action `OpenService`. La registrazione avviene in `OnEnable` e viene rimossa in `OnDisable`, quindi non restano riferimenti di scene precedenti. Il servizio decide autonomamente il proprio modal state; il Dialogue System non conosce Blacksmith/Merchant/Tavern concreti.

## 15. Input, typewriter e fast-forward

Il manager riusa `PlayerController.Controls`:

- `Interact`: completa la frase / avanza / conferma;
- `Move.y`: navigazione verticale;
- `SprintOrDodge`: cancel quando consentito.

Quando la scena possiede l'`InputSystemUIInputModule` gia usato da Arcadia, il manager sospende temporaneamente soltanto la sua action `UI/Navigate` e governa le choices con `PlayerControls.Move`. Questo evita il doppio movimento e impedisce al `rightStick` del modulo UI di cambiare scelta mentre controlla la camera. Submit/click dell'EventSystem restano disponibili e `UI/Navigate` viene ripristinata alla chiusura.

Non viene creata una seconda action map e non esistono subscription permanenti duplicate. Il lock owner-based blocca movimento, jump, sprint, roll, combat, flask, quick slot, menu, interaction e lock-on, lasciando attivi `Look` e la camera gameplay. All'apertura vengono inoltre annullati cast/ranged, parry e hitbox melee gia in corso, cosi un'animazione iniziata nel frame precedente non puo infliggere danno durante il dialogo.

Alla chiusura il lock viene rilasciato solo dopo il rilascio fisico dell'input: questo evita che `R` (Interact e UseFlask nella mappa attuale) consumi una fiaschetta o che il Cancel provochi un roll.

Il typewriter usa `Time.unscaledDeltaTime`. Una pressione completa la linea; la successiva avanza. Tenendo Interact, il fast-forward opera soltanto sui node gia letti e si arresta su node nuovi, player-choice sintetiche e qualsiasi node con choices.

## 16. Story Flags, History e Save

`GameData` salva:

- `string[] storyFlags`;
- `SavedDialogueHistoryData.readNodeKeys`;
- `SavedDialogueHistoryData.selectedChoiceKeys`.

Runtime usa HashSet case-insensitive. Le chiavi sono composte da ID stabili e non dal testo. Le choice non vengono consumate: lo storico modifica soltanto l'indicatore UI.

Un node viene marcato letto quando la linea e stata completamente rivelata, non al semplice ingresso: annullare durante il typewriter non abilita il fast-forward su testo non ancora letto.

`SaveSystem.CurrentSaveVersion` e 3. La migrazione 2 -> 3 inizializza collezioni vuote; save precedenti senza i nuovi campi restano validi. `PlayerStats.BuildGameDataSnapshot` include sempre flag e history, quindi gli autosave di inventory/quest non li sovrascrivono.

## 17. Teleport

Aggiungere `DialogueTeleportTarget` alla destinazione e assegnare un `targetId`. L'action Teleport usa quell'ID e, opzionalmente, il nome scena. Il teleport locale e immediato; quello tra scene richiede `DialogueManager.persistAcrossScenes` e una scena presente nei Build Settings.

## 18. File esistenti modificati

- `PlayerController`: gameplay input lock generico owner-based.
- `PlayerCombat`, `PlayerAnimationEvents`, `PlayerInteraction`, `TargetLockSystem`: rispettano il lock, annullano hitbox/azioni pendenti e non bloccano la camera.
- `PlayerStats`: API narrative controllate, Story Flags, History e snapshot.
- `PlayerInventory`: adapter item tipizzato e rimozione atomica.
- `GameData`, `SaveSystem`: schema persistente v3 e migrazione.
- `QuestManager`, `QuestManager.ObjectiveEvents`, `QuestDefinition`: catalogo non attivo, start/query quest, restore asset reward, guardia load e progressi saturati.

Nessuna scena o prefab esistente viene riscritto automaticamente.

## 19. Limiti intenzionali

- `FailQuest` resta un warning/no-op perché il QuestManager non possiede uno stato Failed.
- Non sono incluse implementazioni concrete Merchant/Blacksmith/Magic/Tavern: si collegano tramite `INpcService`.
- Audio e un singolo `AudioSource`: niente lip sync, mixer o localization audio.
- Nessuna Dialogue Camera obbligatoria; la camera gameplay resta attiva.
- Nessun graph editor: dati inline, custom Inspector e validation coprono la prima versione.
- Il comportamento visuale finale dipende dal prefab/UI configurato e deve essere verificato in Play Mode con il layout reale del progetto.
