# Arcadia - Handoff Dettagliato

Data: 2026-03-20
Workspace: `d:\Unity\Arcadia`
Documento pensato per riprendere il lavoro su un altro PC senza dover riaprire questa chat.

## 1. Obiettivo del refactor fatto finora

Il lavoro principale fatto in questa fase e' stato smontare la vecchia `InventoryUI` monolitica e distribuire la logica su manager piu' piccoli e specializzati.

L'obiettivo architetturale corretto, da mantenere, e':

- `MenuManager` come orchestratore generale del menu
- manager separati per ogni sezione UI
- `PlayerInventory` come fonte dati di equip/loadout
- `PlayerStats` come fonte dati di stats e runtime damage handling
- nessuna nuova logica grossa deve tornare dentro `InventoryUI.cs`

## 2. Stato attuale del progetto

Al momento risultano funzionanti le aree seguenti:

- apertura/chiusura menu
- tab menu: equipment, inventory, magic, attributes, journal
- equipment di:
  - weapon
  - usable
  - magic
  - armor
- detail panel:
  - item
  - weapon
  - shield
  - armor
  - magic
- drag & drop inventory
- cambio scena hub <-> dungeon con fix principali su:
  - camera
  - player references
  - minimap
  - compass
  - HUD bars
- dungeon generator con scelta tema tramite ScriptableObject
- armor che influisce sul danno ricevuto

## 3. Regole architetturali da non rompere

Queste sono le regole che hanno senso adesso:

- non riportare logica di business dentro `InventoryUI.cs`
- non duplicare la stessa logica sia in `InventoryUIManager` sia in `EquipmentManager`
- `PlayerInventory` e' la fonte dello stato equipaggiato
- `PlayerStats` e' la fonte del calcolo runtime del danno e delle stats aggregate
- `MenuManager` deve coordinare, non contenere tutta la logica di dettaglio
- i manager UI devono risolvere i riferimenti mancanti in modo robusto quando possibile
- per il cambio scena, i manager persistenti devono sapersi riallineare alla scena corrente

## 4. Mappa dei sistemi attuali

### 4.1 Menu / UI

- `Assets/Scripts/UI/MenuManager.cs`
  - orchestrazione generale del menu
  - apertura/chiusura
  - gestione tab
  - routing input pad/mouse
  - refresh dei manager figli

- `Assets/Scripts/UI/InventoryUIManager.cs`
  - inventario generale
  - grid slots
  - filtri
  - drag & drop
  - detail panel di item/weapon/shield/armor
  - equip button
  - wallet UI

- `Assets/Scripts/UI/MagicInventoryManager.cs`
  - inventario magie
  - detail panel magia
  - equip magie

- `Assets/Scripts/UI/EquipmentManager.cs`
  - equipment cross:
    - right
    - left
    - bottom
    - top
  - armor slots:
    - helmet
    - chestplate
    - leggings
    - boots
  - focus pad
  - apertura flussi equip

- `Assets/Scripts/UI/AttributesUIManager.cs`
  - tab attributes
  - valori attributi/stats
  - defense/load display

- `Assets/Scripts/UI/QuestJournalUI.cs`
  - journal
  - filtri quest
  - details quest
  - rewards/objectives

- `Assets/Scripts/UI/PlayerUI.cs`
  - HUD player vera per:
    - barre vita
    - barre stamina
    - barre mana
    - flask count
    - key count
    - slot armi in HUD

- `Assets/Scripts/UI/StatBarManager.cs`
  - ormai legacy
  - e' marcato `[Obsolete]`
  - in `Awake()` si disabilita da solo
  - le barre reali sono gestite da `PlayerUI`

- `Assets/Scripts/UI/MinimapManager.cs`
  - minimappa
  - rebind scene change
  - resolve references
  - gestione singleton/duplicati

- `Assets/Scripts/UI/CompassSystem.cs`
  - bussola
  - marker cardinali / marker target

### 4.2 Dati di gioco

- `Assets/Scripts/Player/PlayerInventory.cs`
  - inventario runtime
  - loadout
  - equip references
  - starting loadout
  - item database

- `Assets/Scripts/Player/PlayerStats.cs`
  - HP / stamina / mana
  - stats player
  - economia run
  - banca persistente
  - chiavi
  - damage taken
  - armor totals runtime

### 4.3 Dungeon

- `Assets/Scripts/Dungeon/CoreGenerator.cs`
  - generazione piano
  - seed
  - theme selection
  - build layout
  - spawn stanze
  - navmesh
  - minimap init
  - respawn player nello start

- `Assets/Scripts/Dungeon/DungeonFloorThemeTable.cs`
  - mappa floor -> lista temi pesati

- `Assets/Scripts/Dungeon/DungeonThemeDefinition.cs`
  - definizione tema
  - `themeId`
  - `displayName`
  - `roomSet`

- `Assets/Scripts/Dungeon/DungeonRoomSet.cs`
  - contiene i prefab stanza per categoria e taglia

## 5. Dungeon generator a temi

### 5.1 Architettura attuale

Il flusso corretto adesso e':

`DungeonFloorThemeTable -> DungeonThemeDefinition -> DungeonRoomSet`

### 5.2 Struttura dei dati

#### `DungeonFloorThemeTable`

Contiene una lista di `FloorThemeEntry`.

Ogni `FloorThemeEntry` ha:

- `floorNumber`
- `themes` (lista di `ThemeChoice`)

Ogni `ThemeChoice` ha:

- `theme`
- `weight`

Il `weight` e' relativo agli altri temi dello stesso piano.

Esempio:

- piano 1
  - `Forest`, weight 50
  - `DarkForest`, weight 50

risultato:

- 50% Forest
- 50% DarkForest

Esempio 2:

- 80 / 20

risultato:

- circa 80% / 20%

#### `DungeonThemeDefinition`

Campi reali:

- `themeId`
- `displayName`
- `roomSet`

Al momento non contiene ancora:

- musica
- skybox
- nemici per tema
- props
- lighting presets

Si possono aggiungere in futuro.

#### `DungeonRoomSet`

Categorie attuali:

- `Start`
- `Normal`
- `Boss`
- `Treasure`
- `Shop`
- `Curch`
- `EvilCurch`

Taglie attuali supportate:

- `1x1`
- `2x1`
- `1x2`
- `2x2`

Nota importante:

- `2x1` = long / orizzontale
- `1x2` = tall / verticale

### 5.3 Come il generator sceglie il tema

In `CoreGenerator.Generate()`:

1. costruisce `floorSeedString = gameSeedString-currentFloor`
2. calcola `currentMasterSeed`
3. chiama `ResolveActiveThemeForCurrentFloor()`
4. valida il tema attivo
5. inizializza il prefab lookup dal `DungeonRoomSet`
6. genera layout
7. spawna dungeon

La scelta del tema e' deterministica rispetto a:

- `gameSeedString`
- `currentFloor`
- config della `DungeonFloorThemeTable`
- pesi configurati

Quindi:

- stessa seed + stesso piano = stesso tema
- seed diversa = puo' cambiare tema e layout

### 5.4 Configurazione obbligatoria

Se il dungeon non genera, controllare prima qui:

- `CoreGenerator.floorThemeTable` assegnata
- esiste un `FloorThemeEntry` per `currentFloor`
- esiste almeno un `ThemeChoice` valido
- il `DungeonThemeDefinition` selezionato ha `roomSet`
- il `roomSet` contiene almeno:
  - `Start`
  - `Normal`
  - `Boss`
  - `Shop`
  - `Treasure`

`Curch` e `EvilCurch` possono rimanere opzionali.

### 5.5 CoreGenerator - campi principali

Campi importanti da ricordare:

- `playerTransform`
- `navMeshSurface`
- `hubSceneName`
- `floorThemeTable`
- `gameSeedString`
- `useRandomSeed`
- `currentMasterSeed`
- `currentFloor`
- `maxFloors`
- `playerSpawnOffset`
- `totalNormalRooms`
- `xOffset`
- `zOffset`
- `curchsRoomsChance`
- `normalBigRoomChance`
- `bossBigRoomChance`
- `shopBigRoomChance`
- `treasureBigRoomChance`
- `curchBigRoomChance`
- `evilCurchBigRoomChance`
- `minBossDistance`
- `avoidBossTouchingSpecials`
- `bossMustBeDeadEnd`
- `showRngLogs`

### 5.6 Note pratiche sul generator

- `maxFloors` al momento e' `4`
- `NextFloor()`:
  - se `currentFloor >= maxFloors` torna a `HubScene`
  - altrimenti incrementa piano e rigenera

### 5.7 Log utili gia' presenti

Il generator logga:

- seed del piano
- inizio generazione piano
- log RNG stanze se `showRngLogs` e' attivo
- tema scelto
- successo/fallimento generazione

## 6. Inventory / Equipment / Magic - stato reale

### 6.1 `InventoryUIManager`

Campi serializzati reali:

#### Slot Grid

- `slotPrefab`
- `slotParent`
- `initialSlotCount`

#### Drag & Drop

- `dragCanvas`
- `dragPreviewTemplate`

#### Detail Panel - Shared

- `detailRoot`
- `detailIcon`
- `detailTitle`
- `detailDescription`

#### Detail Panel - Weapon / Shield Display

- `weaponDetailRoot`
- `weaponImage`
- `weaponTitle`
- `weaponDesc`

#### Detail Panel - Weapon Stats

- `weaponDescriptionRoot`
- `weaponStatsRoot`
- `weaponDamageText`
- `weaponCriticalText`
- `weaponWeightText`
- `weaponScalingText`
- `weaponRequirementsText`

#### Detail Panel - Shield Stats

- `shieldDescriptionRoot`
- `shieldDamageText`
- `shieldCriticalText`
- `shieldWeightText`
- `shieldScalingText`
- `shieldRequirementsText`
- `weaponPhysicalDefenseText`
- `weaponMagicDefenseText`

Nota:

- i due campi `weaponPhysicalDefenseText` e `weaponMagicDefenseText` hanno ancora nome legacy ma, di fatto, servono per lo shield

#### Detail Panel - Armor Variant

- `armorDescriptionRoot`
- `armorWeightText`
- `armorPhysicalDefenseText`
- `armorMagicDefenseText`
- `armorEquipButton`

#### Detail Panel - Item / Usable

- `itemDetailRoot`
- `itemImage`
- `itemTitle`
- `itemDesc`

#### Action Buttons

- `equipWeaponButton`
- `equipUsableButton`

#### Wallet UI

- `goldValueText`
- `silverValueText`
- `copperValueText`
- `keyValueText`
- `walletSource`
- `autoRefreshWallet`

### 6.2 Comportamento corretto dei detail panel

#### Weapon normale

Usa:

- display comune weapon/shield
- `WeaponCollumn`

Mostra:

- damage
- critical
- weight
- scaling
- requirement

#### Shield

Usa:

- display comune weapon/shield
- `ShieldCollumn`

Mostra:

- damage
- critical
- weight
- scaling
- requirement
- physical defense
- magic defense

Bug storico gia' risolto:

- se `weaponStatsRoot == weaponDetailRoot` e si spegneva il root stats, spariva anche il parent `DescWeapon`
- fixato con guard nel codice

#### Armor

Usa:

- display comune weapon/shield:
  - image
  - title
  - desc
- `ArmorCollumn` / `ArmorColumn` per le stats specifiche

Mostra:

- weight
- physical defense
- magic defense

Non mostra:

- requirement

#### Item / Usable

Usa:

- pannello item dedicato

### 6.3 Setup UI atteso in scena per armor

Gerarchia attesa lato UI:

- `DescWeapon`
  - area comune immagine / titolo / descrizione
  - `WeaponCollumn`
  - `ShieldCollumn`
  - `ArmorCollumn` oppure `ArmorColumn`

Dentro `ArmorCollumn` / `ArmorColumn` attesi:

- `Weight`
- `Def Phy`
- `Def Mag`
- `EquipBTN`

Se non compare qualcosa, controllare prima i nomi.

### 6.4 `MagicInventoryManager`

Campi principali:

- `slotPrefab`
- `magicSlotParent`
- `magicInitialSlotCount`
- `magicDetailRoot`
- `magicImage`
- `magicTitle`
- `magicDesc`
- `magicDamageText`
- `magicCriticalText`
- `magicScalingText`
- `magicRequirementsText`
- `equipMagicButton`

Responsabilita':

- grid magie
- detail magia
- equip magia

### 6.5 `EquipmentManager`

Campi principali:

#### Equipment Slot Prefab

- `slotPrefab`

#### HUD Cross Icons

- `hudCrossTop`
- `hudCrossRight`
- `hudCrossBottom`
- `hudCrossLeft`

#### HUD Cross Containers

- `hudRightContainer`
- `hudLeftContainer`
- `hudBottomContainer`
- `hudTopContainer`

#### Equipment Slot Containers

- `rightEquipContainer`
- `rightEquipContainer2`
- `rightEquipContainer3`
- `leftEquipContainer`
- `leftEquipContainer2`
- `leftEquipContainer3`
- `bottomEquipContainer`
- `bottomEquipContainer2`
- `bottomEquipContainer3`
- `topEquipContainer`
- `topEquipContainer2`
- `topEquipContainer3`

#### Equipment Roots

- `equipmentBackground`
- `inventoryBackground`
- `magicBackground`

#### Armor Slot Containers

- `armorHelmetContainer`
- `armorChestplateContainer`
- `armorLeggingsContainer`
- `armorBootsContainer`

#### Dependencies

- `inventoryUIManager`
- `magicInventoryManager`

### 6.6 Focus pad equipment

Enum target pubblico:

- `None`
- `Right`
- `Left`
- `Bottom`
- `Top`
- `Armor`

Colonne focus interne:

- `Right`
- `Left`
- `Bottom`
- `Top`
- `Armor`

Ultima modifica importante:

- gli armor slot adesso sono davvero raggiungibili dal pad

Shortcut introdotti:

- da `Left` + `sinistra` -> vai a `Armor`
- da `Armor` + `destra` -> torni a `Left`
- in `Armor`:
  - `su` scorre verso slot precedente
  - `giu` scorre verso slot successivo

Motivo del fix:

- la navigazione geometrica pura vedeva gli slot armor ma quasi mai li selezionava come best candidate

## 7. Armor gameplay

### 7.1 Situazione attuale

L'armor ora non e' solo UI/loadout.
Le stats dell'armor entrano nel gameplay.

### 7.2 File coinvolti

- `Assets/Scripts/Player/PlayerStats.cs`
- `Assets/Scripts/Player/PlayerInventory.cs`

### 7.3 Totali runtime esposti in `PlayerStats`

Campi/proprieta':

- `totalArmorPhysicalDefense`
- `totalArmorMagicDefense`
- `totalArmorWeight`

Proprieta' pubbliche:

- `TotalArmorPhysicalDefense`
- `TotalArmorMagicDefense`
- `TotalArmorWeight`

### 7.4 Fonte dei dati

I totali vengono ricalcolati da:

- `playerInventory.armorLoadout`

### 7.5 Quando vengono aggiornati

Sono aggiornati quando:

- si equipaggia armor
- si riallineano gli equipped references
- il player stats refresha le UI/runtime

### 7.6 Danno ricevuto

Sequenza logica attuale quando il player subisce danno:

1. il danno entra in `PlayerStats.TakeDamage(...)`
2. prima passa da eventuale block/parry
3. poi viene rinfrescato il totale armor
4. si applica la mitigazione armor
5. si aggiorna la salute

Formula usata:

`finalDamage = damage * (100 / (100 + defense))`

Uso:

- se il danno e' fisico -> usa `TotalArmorPhysicalDefense`
- se il danno e' magico -> usa `TotalArmorMagicDefense`

### 7.7 Equip load

Il peso armor e' incluso nel carico equip.
Le armor gia' equipaggiate sono escluse dal conteggio inventario normale per evitare doppio conteggio.

### 7.8 Log debug gia' aggiunti

Quando cambi armor:

- log dei totali armor

Quando subisci danno:

- log di:
  - incoming
  - afterBlockParry
  - tipo danno
  - armorDef usata
  - totali armor phy/mag
  - danno finale

## 8. Weapon categories nuove

Sono state aggiunte le categorie:

- `Flail`
- `Hammer`

File:

- `Assets/Scripts/Items/WeaponCategory.cs`

Nota importante:

- l'enum e' stato esteso in coda per non rompere gli asset gia' serializzati
- le categorie esistono
- non hanno ancora comportamento dedicato completo

Quindi mancano ancora eventualmente:

- animation profile specifici
- asset reali
- moveset specifico
- tuning stamina/danno/scaling

## 9. Scena, persistenza, cambio scena

### 9.1 Problemi che erano successi

Durante il refactor si erano rotti:

- camera lock
- spawn corretto in stanza start
- HUD bars
- minimap
- compass
- UI che non rifletteva il player corretto dopo cambio scena

### 9.2 Stato attuale

I fix principali sono stati fatti.
In particolare:

- i sistemi persistenti si riallineano meglio alla scena corrente
- il player e la camera non devono esplodere al cambio scena
- minimap e compass sono stati resi piu' robusti

### 9.3 MinimapManager

Problema storico:

- `UnassignedReferenceException` su `mapContainer`

Fix fatti:

- gestione singleton piu' robusta
- distruzione duplicati
- resolve references a cambio scena
- null guards nelle operazioni critiche

Comunque in Unity conviene ancora controllare:

- esiste un solo `MinimapManager`
- `mapContainer` e' assegnato correttamente

### 9.4 HUD bars

Importante:

- `StatBarManager` e' legacy
- il sistema attuale delle barre sta in `PlayerUI`

`PlayerUI`:

- aggiorna fill amount ogni frame
- ridimensiona la frame in base ai max stats
- riaggancia i riferimenti a cambio scena

Campi principali:

- `playerStats`
- `playerInventory`
- `healthBarFill`
- `staminaBarFill`
- `manaBarFill`
- `healthBarFrame`
- `staminaBarFrame`
- `manaBarFrame`
- `flaskCountText`
- `keyCountText`
- `slotLeftIcon`
- `slotRightIcon`

Parametri di sizing:

- `healthBaseWidth`
- `staminaBaseWidth`
- `manaBaseWidth`
- `healthWidthPerPoint`
- `staminaWidthPerPoint`
- `manaWidthPerPoint`
- `barWidthScale`
- `minBarWidth`
- `maxBarWidth`
- `fillHorizontalPadding`

## 10. Journal / Quest

La classe reale e':

- `Assets/Scripts/UI/QuestJournalUI.cs`

Non `QuestUiManager`.

Campi principali:

### Dependencies

- `menuManager`
- `inventoryUIManager`
- `magicInventoryManager`
- `playerInventory`
- `playerStats`

### Quest UI

- `useQuestManager`
- `autoWireQuestUI`
- `questListContainer`
- `questItemPrefab`
- `questActiveFilterButton`
- `questCompletedFilterButton`
- `questActiveCountText`
- `questCompletedCountText`
- `questActiveFilterLabelText`
- `questCompletedFilterLabelText`
- `questFilterSelectedColor`
- `startingQuests`

### Quest Detail UI

- `questDetailTypeText`
- `questDetailRecommendedText`
- `questDetailTitleText`
- `questDetailLocationText`
- `questDetailLoreTitleText`
- `questDetailLoreDescriptionText`
- `questDetailLoreAuthorText`
- `questDetailLoreRoot`
- `questObjectivesSectionRoot`
- `questDetailPanelRoot`
- `showQuestDetailOnlyOnSelection`
- `collapseQuestLoreWhenEmpty`
- `questObjectivesLiftWhenNoLore`
- `questObjectivesContainer`
- `questObjectivePrefab`
- `questRewardsContainer`
- `questRewardPrefab`
- `questClaimRewardButton`
- `questRewardInventoryCapacity`
- `questRewardMagicCapacity`
- `questDetailScrollRect`
- `smoothQuestMouseWheel`
- `questMouseWheelStepNormalized`
- `questMouseWheelSmoothSpeed`
- `questPadRightStickScrollSpeed`
- `questPadFocusBorderColor`
- `questPadFocusBorderThickness`

## 11. Setup Unity da controllare quando apri il progetto sull'altro PC

Questa e' la checklist operativa minima.

### 11.1 Scene

Aprire:

- `HubScene`
- `GameScene`

Controllare che siano state salvate con gli ultimi assegnamenti Inspector.

### 11.2 `CoreGenerator`

Verificare:

- `playerTransform`
- `navMeshSurface`
- `hubSceneName`
- `floorThemeTable`
- `currentFloor`
- `maxFloors`
- `playerSpawnOffset`

Poi aprire gli asset:

- `DungeonFloorThemeTable`
- `DungeonThemeDefinition`
- `DungeonRoomSet`

e controllare che i set non siano incompleti.

### 11.3 `MenuManager`

Verificare:

- `inventoryPanel`
- `playerHudPanel`
- `inventoryUIManager`
- `magicInventoryManager`
- `equipmentManager`
- `attributesUIManager`
- `questJournalUI`
- `tabs`
- `defaultOpenTabKey`

### 11.4 `InventoryUIManager`

Verificare i blocchi:

#### Grid

- `slotPrefab`
- `slotParent`

#### Drag

- `dragCanvas`
- `dragPreviewTemplate`

#### Detail shared

- `detailRoot`
- `detailIcon`
- `detailTitle`
- `detailDescription`

#### Weapon / Shield display

- `weaponDetailRoot`
- `weaponImage`
- `weaponTitle`
- `weaponDesc`

#### Weapon stats

- `weaponDescriptionRoot`
- `weaponStatsRoot`
- `weaponDamageText`
- `weaponCriticalText`
- `weaponWeightText`
- `weaponScalingText`
- `weaponRequirementsText`

#### Shield stats

- `shieldDescriptionRoot`
- `shieldDamageText`
- `shieldCriticalText`
- `shieldWeightText`
- `shieldScalingText`
- `shieldRequirementsText`
- `weaponPhysicalDefenseText`
- `weaponMagicDefenseText`

#### Armor stats

- `armorDescriptionRoot`
- `armorWeightText`
- `armorPhysicalDefenseText`
- `armorMagicDefenseText`
- `armorEquipButton`

#### Item / Usable

- `itemDetailRoot`
- `itemImage`
- `itemTitle`
- `itemDesc`

#### Buttons

- `equipWeaponButton`
- `equipUsableButton`

#### Wallet

- `goldValueText`
- `silverValueText`
- `copperValueText`
- `keyValueText`

### 11.5 `EquipmentManager`

Verificare:

- `slotPrefab`
- tutti i container right/left/bottom/top
- `equipmentBackground`
- `inventoryBackground`
- `magicBackground`
- `armorHelmetContainer`
- `armorChestplateContainer`
- `armorLeggingsContainer`
- `armorBootsContainer`
- `inventoryUIManager`
- `magicInventoryManager`

Se i container armor non sono assegnati, il codice prova a trovarli con questi nomi esatti:

- `helmet`
- `chestplate`
- `leggings`
- `boots`

### 11.6 `PlayerUI`

Verificare:

- `healthBarFill`
- `staminaBarFill`
- `manaBarFill`
- `healthBarFrame`
- `staminaBarFrame`
- `manaBarFrame`
- `flaskCountText`
- `keyCountText`
- `slotLeftIcon`
- `slotRightIcon`

### 11.7 `MinimapManager`

Verificare:

- esiste un solo `MinimapManager`
- `mapContainer` assegnato

## 12. Test manuali consigliati

### 12.1 Inventory / Equipment

Testare in ordine:

1. apri il menu
2. vai su `Equipment`
3. spostati col pad sulle 4 direzioni equipment
4. equipaggia una weapon destra
5. equipaggia una weapon sinistra
6. equipaggia un usable
7. equipaggia una magic
8. vai sugli armor slot con il pad
9. equipaggia:
   - helmet
   - chestplate
   - leggings
   - boots
10. verifica detail panel corretto per:
   - weapon
   - shield
   - armor
   - item
   - magic

### 12.2 Armor gameplay

1. togli armor
2. fatti colpire
3. guarda il log danno
4. equipaggia armor con defense alta
5. fatti colpire di nuovo
6. verifica riduzione del danno nei log

### 12.3 Dungeon

1. entra nel dungeon dall'hub
2. verifica spawn in start room
3. apri console
4. controlla log di:
   - seed
   - floor
   - tema scelto
5. passa al piano successivo
6. verifica che il tema del nuovo piano sia coerente con la `FloorThemeTable`

### 12.4 Scene change

1. hub -> dungeon
2. dungeon -> hub
3. controlla:
   - camera
   - lock on
   - minimap
   - compass
   - HUD bars
   - menu

## 13. Problemi storici gia' incontrati e fix applicati

Questa sezione serve a non perdere tempo rifacendo debugging gia' fatto.

### 13.1 Shield column non visibile

Sintomo:

- cliccando shield non compariva `ShieldCollumn`

Causa:

- `weaponStatsRoot` e `weaponDetailRoot` assegnati entrambi a `DescWeapon`
- spegnendo `weaponStatsRoot` si spegneva anche il parent

Fix:

- guard nel codice di `InventoryUIManager`

### 13.2 Armor slot non raggiungibili col pad

Sintomo:

- col pad non si arrivava agli armor slot

Causa:

- la navigazione geometrica non selezionava in pratica mai gli armor slot come best candidate

Fix:

- shortcut espliciti `Left <-> Armor`
- navigazione verticale diretta dentro colonna armor

### 13.3 Minimap `mapContainer` nullo

Sintomo:

- `UnassignedReferenceException` su `MinimapManager.mapContainer`

Fix:

- singleton piu' robusto
- resolve references a scene load
- null guards

### 13.4 Barre HUD rotte / enormi

Sintomo:

- barre vita/mana/stamina fuori scala tra scene

Situazione attuale:

- la gestione vera e' in `PlayerUI`
- `StatBarManager` non e' piu' il punto corretto

## 14. Debito tecnico ancora aperto

Le cose non chiuse davvero sono queste:

- la UI non mostra ancora bene i totali armor aggregati; per ora il riferimento piu' affidabile resta il log
- `Flail` e `Hammer` non hanno ancora contenuto dedicato completo
- `weapon skill` non e' ancora un sistema chiuso
- gli `usable` oltre `Heal` e `Mana` non sono rifiniti
- si puo' continuare la pulizia dei manager UI, ma senza cambiare comportamento

## 15. Cose da NON fare

- non riportare nuova logica dentro `InventoryUI.cs`
- non creare di nuovo duplicazione tra manager UI
- non usare `StatBarManager` come sistema principale delle barre
- non rimettere fallback opachi nel dungeon generator senza capire se il set tema e' realmente configurato
- non cambiare nomi UI tipo `ShieldCollumn`, `ArmorCollumn`, `helmet`, `chestplate`, ecc. senza aggiornare anche l'auto-wire

## 16. Priorita' sensate se si continua da qui

Ordine consigliato:

1. consolidare scene e Inspector references
2. fare una passata di test manuali completa
3. se tutto regge, chiudere il gameplay armor:
   - eventuale display UI totale defense/load
4. poi aggiungere contenuto:
   - `Flail`
   - `Hammer`
   - temi dungeon aggiuntivi
5. solo dopo fare altra pulizia strutturale

## 17. Riassunto breve per riprendere subito

Se apri il progetto sull'altro PC e vuoi solo ripartire in 2 minuti:

1. apri `HubScene` e `GameScene`
2. controlla `CoreGenerator.floorThemeTable`
3. controlla `InventoryUIManager`
4. controlla `EquipmentManager` armor containers
5. controlla `PlayerUI`
6. lancia il gioco
7. testa:
   - equipment
   - armor
   - dungeon entry
   - scene change
   - damage con armor

## 18. Sistema Player completo

Questa parte mancava nel documento precedente.

### 18.1 `PlayerController`

File:

- `Assets/Scripts/Player/PlayerController.cs`

Responsabilita':

- movimento base
- sprint
- salto
- dodge / roll
- gravita'
- gestione input condiviso tramite `PlayerControls`
- apertura inventory/menu
- quick cycle dei loadout:
  - right weapon
  - left weapon
  - usable
  - magic

Campi principali:

- movement:
  - `moveSpeed`
  - `sprintMultiplier`
  - `rotationSpeed`
  - `gravity`
- equip load movement:
  - `lightLoadThreshold`
  - `heavyLoadThreshold`
  - `lightLoadSpeedMultiplier`
  - `heavyLoadSpeedMultiplier`
- jump:
  - `jumpHeight`
  - `coyoteTime`
- dodge / roll:
  - `dodgeDistance`
  - `dodgeDuration`
  - `dodgeCooldown`
  - `rollStartDelay`
  - `dodgeSpeedCurve`
  - `rollIFrameStartNormalized`
  - `rollIFrameEndNormalized`
- stamina:
  - `rollStaminaCost`
  - `jumpStaminaCost`
  - `sprintStaminaCostPerSecond`
- UI:
  - `menuManager`

Proprieta' importanti:

- `Controls`
- `IsInventoryOpen`
- `IsGrounded`
- `IsRolling`

Note:

- subscribe a `SceneManager.sceneLoaded`
- su scene load riallinea `Camera.main`, `MenuManager`, `PlayerInventory`, `PlayerStats`, `PlayerCombat`
- chiama `menuManager.RefreshEquipmentUI()` dopo cambio scena

### 18.2 `PlayerCombat`

File:

- `Assets/Scripts/Player/PlayerCombat.cs`

Responsabilita':

- light/heavy attack
- shield block
- parry
- ranged prototype:
  - wand
  - bow
- throw weapon
- flask use
- lock del movimento/combat durante le animazioni

Campi importanti:

#### Stato combattimento

- `isAttacking`
- `canAttack`
- `shieldBlockHoldThreshold`
- `blockFrontAngle`
- `minimumBlockStaminaCost`
- `blockStabilityScale`
- `guardBreakDuration`
- `blockingAnimatorParameter`
- `parryAnimatorTrigger`
- `parryTotalLockTime`
- `parryRecoveryTime`
- `parryStaggerDuration`

#### Magic Cast Prototype

- `enableMagicCastPrototype`
- `magicCastPoint`
- `magicCastPointHeightOffset`
- `magicCastKey`
- `fallbackMagicCooldown`
- `fallbackProjectileSpeed`
- `fallbackProjectileLifetime`
- `wandLightCastWindup`
- `wandHeavyCastWindup`
- `bowLightShotWindup`
- `bowHeavyShotWindup`
- `rangedRecoveryTime`
- `rangedSpawnBackOffset`
- `castPointForwardOffsetScale`
- `lockRangedUntilAnimationEnds`
- `rangedMinTotalLockTime`
- `fallbackMeleeUnlockTime`

#### Weapon Throw

- `enableWeaponThrow`
- `throwMinLockTime`
- `throwHitMask`
- `throwSpawnForwardOffset`
- `throwSpawnHeightOffset`
- `throwArcUpBias`

Stato attuale importante:

- shield block/parry e' gia' implementato
- bow e wand sparano projectile
- throw weapon e' implementato
- alcune parti sono ancora marcate come prototype/fallback

### 18.3 `PlayerAnimation`

File:

- `Assets/Scripts/Player/PlayerAnimation.cs`

Responsabilita':

- aggiorna i parametri locomotion dell'animator:
  - `Speed`
  - `IsSprinting`

Condizioni:

- non aggiorna locomotion se il player sta attaccando
- non aggiorna locomotion se il player sta rollando

### 18.4 `PlayerWeaponVisuals`

File:

- `Assets/Scripts/Player/PlayerWeaponVisuals.cs`

Responsabilita':

- istanzia il `modelPrefab` delle armi equipaggiate nelle mani
- ricostruisce i modelli quando cambia il weapon loadout
- distrugge collider e rigidbody dai modelli equipaggiati in mano

Campi principali:

- `rightHandSocket`
- `leftHandSocket`
- `autoResolveHumanoidHandBones`

Note:

- se `autoResolveHumanoidHandBones` e' attivo, prova a prendere le mani dal rig umanoide

### 18.5 `TargetLockSystem`

File:

- `Assets/Scripts/camera/TargetLockSystem.cs`

Responsabilita':

- lock-on target
- switching target destra/sinistra
- rotazione player verso il target
- gestione camera free look / lock-on
- target icon UI

Campi principali:

- camere:
  - `freeLookCamera`
  - `lockOnCamera`
  - `playerModel`
- ricerca:
  - `scanRadius`
  - `enemyLayer`
  - `maxLockDistance`
- switching:
  - `switchCooldown`
  - `switchThreshold`
- movimento:
  - `rotationSpeed`
- UI:
  - `targetIcon`

Note:

- subscribe a `SceneManager.sceneLoaded`
- su scene load rifà:
  - `mainCam`
  - `playerController`
  - `targetIcon`
- `StopLockOn()` viene chiamato anche per reset camera/rotazione

### 18.6 `CameraInputBlocker`

File:

- `Assets/Scripts/System/CameraInputBlocker.cs`

Responsabilita':

- utility statica per abilitare/disabilitare input sulle `CinemachineFreeLook`
- cerca tutte le free look in scena
- se c'e' `CinemachineInputProvider`, abilita/disabilita quello
- altrimenti svuota/rimette i nomi raw axis

## 19. Interaction, porte, room flow

### 19.1 `PlayerInteraction`

File:

- `Assets/Scripts/Player/PlayerInteraction.cs`

Responsabilita':

- subscribe all'input `Interact`
- cerca `IInteractable` davanti al player con `Physics.OverlapSphere`
- prende l'interagibile piu' vicino
- chiama `Interact(gameObject)`

Campi principali:

- `interactRange`
- `interactLayer`

### 19.2 `IInteractable`

File:

- `Assets/Scripts/Interface/IInteractable.cs`

Usato da:

- `InteractableDoor`
- `WeaponWorldPickup`
- altri interactable futuri

### 19.3 `InteractableDoor`

File:

- `Assets/Scripts/Rooms/InteractableDoor.cs`

Responsabilita':

- porta interagibile
- se la stanza e' locked, consuma una chiave con `PlayerStats.UseKey()`
- sblocca la stanza speciale
- spegne il `GameObject` porta per far passare il player

### 19.4 `Room`

File:

- `Assets/Scripts/Rooms/Room.cs`

Responsabilita':

- stato runtime stanza
- gestione porte collegate
- blocco/sblocco stanza in battaglia
- registrazione nemici
- reward spawn
- spawn/attivazione portale di fine piano nella boss room
- spawn point player consigliato

Campi chiave:

- `roomData`
- `internalRoomType`
- `doors`
- `isLocked`
- `roomCleared`
- `activeEnemies`
- `floorPortalPrefab`
- `preplacedFloorPortal`
- `portalSpawnOffset`
- `portalDistanceFromCenter`
- `playerSpawnPoint`

Punti importanti:

- shop/treasure/blessed/evil possono partire chiusi a chiave
- quando il player entra in stanza con nemici:
  - chiude la stanza
  - attiva i nemici
- quando gli enemy muoiono:
  - sblocca la stanza
  - se boss room, prova a spawnare il floor portal
- loot spawn e' deterministico rispetto a seed e posizione stanza

### 19.5 `RoomData`

File:

- `Assets/Scripts/Rooms/RoomData.cs`

Contiene:

- identita' stanza
- `roomPrefab`
- `size`
- flag tipo stanza:
  - `isBossRoom`
  - `isTreasureRoom`
  - `isStartRoom`
  - `isShopRoom`
  - `isBlessedRoom`
  - `isEvilRoom`
- rewards / loot table

### 19.6 `FloorPortal`

File:

- `Assets/Scripts/Dungeon/FloorPortal.cs`

Responsabilita':

- trigger che manda al piano successivo
- chiama `CoreGenerator.Instance.NextFloor()`

Campi:

- `playerTag`
- `disableAfterUse`

### 19.7 `SceneLoader`

File:

- `Assets/Scripts/Scene/SceneLoader.cs`

Responsabilita':

- trigger molto semplice per cambiare scena
- puo' anche fare `Application.Quit()` se `isExit = true`

Nota:

- e' separato dal `FloorPortal`
- `FloorPortal` e' il flow interno del dungeon
- `SceneLoader` e' un trigger generico di scena

## 20. Enemy system

### 20.1 `EnemyData`

File:

- `Assets/Scripts/Enemy/EnemyData.cs`

Contiene:

- `enemyName`
- `prefab`
- `maxHealth`
- `damage`
- `moveSpeed`
- `experienceReward`
- `spawnWeight`

### 20.2 `SpawnTable`

File:

- `Assets/Scripts/Enemy/SpawnTable.cs`

Responsabilita':

- lista di `EnemyData`
- scelta random pesata di un enemy
- supporta anche scelta deterministica se gli passi un `System.Random`

### 20.3 `EnemySpawner`

File:

- `Assets/Scripts/Enemy/EnemySpawner.cs`

Responsabilita':

- prende `CoreGenerator.currentMasterSeed`
- genera un seed locale con posizione spawner
- sceglie enemy dalla `SpawnTable`
- istanzia il prefab
- applica dati runtime:
  - health
  - xp reward
  - move speed
  - AI config
- registra il nemico nella `Room`

### 20.4 `SimpleEnemyAI`

File:

- `Assets/Scripts/Enemy/SimpleEnemyAI.cs`

Stati interni:

- `Idle`
- `Chase`
- `Windup`
- `Recovery`
- `Return`

Responsabilita':

- detection player
- chase
- melee attack
- leash/return to spawn
- linea di vista opzionale
- animator sync

Campi importanti:

- setup:
  - `agent`
  - `playerTarget`
  - `animator`
- base:
  - `sightRange`
  - `attackRange`
  - `leashRange`
  - `aggroDetectionMultiplier`
  - `disengageDistanceFromTarget`
  - `repathInterval`
  - `returnStopDistance`
  - `preferredCombatDistance`
  - `personalSpaceRadius`
  - `attackStartRangeMultiplier`
- melee:
  - `attackDamage`
  - `windupDuration`
  - `hitDelay`
  - `recoveryDuration`
  - `attackCooldown`
  - `attackHitRangeMultiplier`
  - `useAnimationEventForHit`
- sensing:
  - `requireLineOfSight`
  - `sightBlockMask`
  - `eyeHeight`
- animator:
  - `moveSpeedParameter`
  - `inCombatParameter`
  - `attackTriggerParameter`

### 20.5 `EnemyHealth`

File:

- `Assets/Scripts/Enemy/EnemyHealth.cs`

Responsabilita':

- implementa `IDamageable`
- health enemy
- xp reward on death
- notifica `Room.EnemyDied(gameObject)` quando muore

### 20.6 `EnemySetup` e `EnemyHealthBar`

File:

- `Assets/Scripts/Enemy/EnemySetup.cs`
- `Assets/Scripts/Enemy/EnemyHealthBar.cs`

Nota:

- `EnemySetup` costruisce/setup della health bar enemy
- sistema separato dal player HUD

## 21. Items, database, pickup, world representation

### 21.1 `ItemDatabase`

File:

- `Assets/Scripts/Items/ItemDatabase.cs`

Struttura:

- `weaponsByCategory`
- `magics`
- `armors`
- `usables`
- `items`

La parte weapon e' divisa per bucket categoria:

- `WeaponCategoryBucket`
  - `category`
  - `weapons`

Nota:

- esiste `BuildFlatWeaponList()`
- l'assegnazione dell'`ItemDatabase` in `PlayerInventory` resta importante per save/restore

### 21.2 `WeaponItem`

File:

- `Assets/Scripts/Items/WeaponItem.cs`

Contiene:

- info:
  - `weaponName`
  - `icon`
  - `modelPrefab`
  - `description`
- category / range:
  - `category`
  - `rangeType`
- damage:
  - `damageType`
  - `physicalDamage`
  - `magicDamage`
  - `criticalHit`
  - `criticalChance`
  - `lightDamageMultiplier`
  - `heavyDamageMultiplier`
  - `weight`
- scaling:
  - `scaling`
  - rank per STR/DEX/INT/FAI
- requirements:
  - `strengthRequirement`
  - `dexterityRequirement`
  - `intelligenceRequirement`
  - `faithRequirement`
  - `requirements` legacy/fallback label
- animation:
  - `animationProfile`
- stamina:
  - `lightAttackStaminaCost`
  - `heavyAttackStaminaCost`
- shield:
  - `canBlock`
  - `canParry`
  - `physicalBlockPercent`
  - `magicBlockPercent`
  - `stability`
  - `parryWindowStart`
  - `parryWindowDuration`
- future / special:
  - `hasRightSkill`
  - `hasLeftSkill`
  - `isSpecialWeapon`
- wand:
  - projectile prefab
  - mana cost / cooldown / speed / lifetime / spawnOffset / hitMask
- bow:
  - projectile prefab
  - ammo item
  - cooldown / speed / lifetime / spawnOffset / hitMask
- throw:
  - `canBeThrown`
  - `throwStrengthRequirement`
  - `throwProjectilePrefab`
  - `throwSpeed`
  - `throwLifetime`
  - `throwStaminaCost`
  - `throwBladeHitChance`
  - `throwHandleDamageMultiplier`
  - `throwBreakChance`
- dropped pickup physics:
  - collider center/size
  - mass/drag
  - impulses
  - local euler

Nota:

- esiste custom inspector: `Assets/Scripts/Editor/WeaponItemEditor.cs`
- l'inspector mostra campi condizionali in base a categoria

### 21.3 `ArmorItemData`

File:

- `Assets/Scripts/Items/ArmorItemData.cs`

Contiene:

- `itemName`
- `description`
- `icon`
- `slot`
- `weight`
- `physicalDefense`
- `magicDefense`

Nota:

- non ha requirement

### 21.4 `MagicItemData`

File:

- `Assets/Scripts/Items/MagicItemData.cs`

Contiene:

- info base
- stats:
  - `magicDamage`
  - `criticalHit`
  - `scaling`
  - `requirements`
- cast:
  - `manaCost`
  - `castTime`
  - `castCooldown`
- projectile:
  - `projectilePrefab`
  - `projectileSpeed`
  - `projectileLifetime`
  - `spawnOffset`
  - `hitMask`

### 21.5 `UsableItemData`

File:

- `Assets/Scripts/Items/UsableItemData.cs`

Contiene:

- info base
- `weight`
- `cooldownSeconds`
- `maxCharges`
- `effectType`
- `durationSeconds`
- `customEffectId`
- placeholder fields:
  - `healAmount`
  - `manaRestore`

Nota:

- effect types:
  - `Heal`
  - `Mana`
  - `Invisibility`
  - `Custom`
- al momento i casi oltre heal/mana non sono ancora rifiniti

### 21.6 `ItemData`

File:

- `Assets/Scripts/Items/ItemData.cs`

Contiene:

- `itemName`
- `description`
- `icon`
- `weight`

### 21.7 `WeaponWorldPickup`

File:

- `Assets/Scripts/Items/WeaponWorldPickup.cs`

Responsabilita':

- interactable world pickup per weapon instance
- conserva `weapon` + `instanceId`
- quando raccolto:
  - aggiunge la specifica istanza al `PlayerInventory`

### 21.8 `CoinPickup` / `KeyPickup`

File:

- `Assets/Scripts/Items/CoinPickup.cs`
- `Assets/Scripts/Items/KeyPickup.cs`

Responsabilita':

- pickup trigger semplici su `Player`
- `CoinPickup` aggiunge monete run wallet
- `KeyPickup` aggiunge chiavi

## 22. Save / persistence

### 22.1 `SaveSystem`

File:

- `Assets/Scripts/System/SaveSystem.cs`

Responsabilita':

- salva/carica `GameData` come JSON in:
  - `Application.persistentDataPath/gamedata.json`

Metodi:

- `SaveData(GameData data)`
- `LoadData()`
- `GetSaveFilePath()`

### 22.2 `GameData`

File:

- `Assets/Scripts/System/GameData.cs`

Struttura del salvataggio:

- leveling:
  - `playerLevel`
  - `levelExperience`
  - `experienceToNextLevel`
  - `unspentAttributePoints`
- attributi:
  - `vigor`
  - `mind`
  - `endurance`
  - `strength`
  - `dexterity`
  - `intelligence`
  - `faith`
- alignment/stat extra:
  - `karma`
  - `benedetto`
  - `malefico`
- banca:
  - `bankGold`
  - `bankSilver`
  - `bankCopper`
- quest:
  - `quests`
- inventory:
  - `playerInventory`

Nested types rilevanti:

- `SavedQuestObjectiveData`
- `SavedQuestRewardData`
- `SavedQuestData`
- `SavedInventoryItemData`
- `SavedLoadoutSlotData`
- `SavedPlayerInventoryData`

### 22.3 Persistenza runtime

Oggetti persistenti importanti:

- `PlayerStats`
  - usa `DontDestroyOnLoad(root.gameObject)`
  - singleton: `PlayerStats.instance`
- `QuestManager`
  - singleton: `QuestManager.Instance`
  - puo' fare persistence cross-scene

Nota:

- `PlayerStats` resetta il run wallet quando entra in `GameScene`
- la banca resta persistente

## 23. Quest system runtime

### 23.1 `QuestManager`

File:

- `Assets/Scripts/System/QuestManager.cs`
- `Assets/Scripts/System/QuestManager.JournalRuntime.cs`

Responsabilita':

- mantiene la lista runtime delle quest
- seed iniziale / merge da inventory quest entries
- notifica cambi alla UI
- puo' persistere cross-scene

Campi principali:

- `persistAcrossScenes`
- `autoNotifyOnStart`
- `initialQuests`

Nested types:

- `QuestData`
- `QuestObjectiveData`
- `QuestRewardData`

## 24. Combat projectiles e damage helpers

### 24.1 `MagicProjectile`

File:

- `Assets/Scripts/Combat/MagicProjectile.cs`

Responsabilita':

- projectile magia
- init con owner/direction/damage/speed/lifetime/hitMask
- danno su `IDamageable`
- ignora owner
- ignora trigger non damageable per evitare vanish prematuro

### 24.2 `WeaponThrowProjectile`

File:

- `Assets/Scripts/Combat/WeaponThrowProjectile.cs`

Responsabilita':

- projectile del lancio arma
- puo' usare traiettoria balistica
- decide blade hit vs handle hit
- puo' rompere o droppare il pickup world dell'arma lanciata

### 24.3 `WeaponDamage`

File:

- `Assets/Scripts/Combat/WeaponDamage.cs`

Nota:

- esiste nel progetto, ma non e' stato un punto centrale del refactor recente
- se si rimettono mano hitbox melee, controllare anche questo file

## 25. File legacy / residui da conoscere

### 25.1 `StatBarManager`

- legacy
- obsoleto
- si disabilita in `Awake()`
- non usarlo come sistema principale barre

### 25.2 `SimpleDungeonGenerator`

- vecchio generator
- ancora presente nel progetto
- non e' il sistema corretto attuale
- il sistema attuale e' `CoreGenerator`

### 25.3 `DynamicBar`

- utility generica per barra con resize
- presente ma non e' il cuore attuale del player HUD

## 26. File e comportamenti che meritano attenzione se qualcosa si rompe

Ordine rapido di debug:

1. `MenuManager`
2. `InventoryUIManager`
3. `EquipmentManager`
4. `MagicInventoryManager`
5. `PlayerInventory`
6. `PlayerStats`
7. `PlayerUI`
8. `CoreGenerator`
9. `MinimapManager`
10. `TargetLockSystem`

## 27. Nota finale su stato reale del progetto

Il progetto adesso non e' piu' “solo un inventory refactor”.
I pezzi che si toccano tra loro davvero sono:

- input player
- menu managers
- inventory/loadout
- player stats
- player combat
- scene change
- dungeon generator
- minimap/compass/HUD

Quindi ogni modifica futura va ragionata guardando almeno queste dipendenze:

- `PlayerController`
- `PlayerCombat`
- `PlayerInventory`
- `PlayerStats`
- `MenuManager`
- manager UI specifico
- eventuale scena/hierarchy reference

## 28. Nomi reali che il codice auto-cerca

Questa sezione e' importante perche' diversi manager fanno auto-wire cercando nomi specifici in scena.
Se rinomini questi oggetti senza aggiornare il codice, qualcosa puo' rompersi in modo poco evidente.

### 28.1 Nomi scene trovati realmente in `HubScene` / `GameScene`

Nomi confermati presenti nelle scene:

- `HUD_Inventory`
- `HUD_Canvas`
- `TopLeftBars`
- `HealthBar`
- `HealthBar_Fill`
- `ManaBar`
- `ManaBar_Frame`
- `ManaBar_Fill`
- `StaminaBar`
- `StaminaBar_Frame`
- `StaminaBar_Fill`
- `MinimapContainer`
- `targetLock`
- `EquipmentBackground`
- `invBackground`
- `MagicBackground`
- `AttributesBackground`
- `JournalBackground`
- `SettingBackground`
- `DescWeapon`
- `WeaponCollumn`
- `ShieldCollumn`
- `ArmorCollumn`
- `GridInv`
- `QuestPanel`
- `Quest`
- `RewardContainer`
- `Rewards`
- `Objectives`
- `helmet`
- `chestplate`
- `boots`

Nota:

- in `GameScene` risultano anche `helmet`, `chestplate`, `boots`
- `leggings` non e' uscito nella grep rapida sulle scene, quindi va controllato a mano se e' salvato correttamente o ha un nome diverso

### 28.2 `MenuManager.ResolveReferences()` - nomi chiave

`MenuManager` cerca automaticamente:

- `HUD_Inventory`
- `HUD_Canvas`

e poi prova a trovare da li':

- `InventoryUIManager`
- `MagicInventoryManager`
- `EquipmentManager`
- `AttributesUIManager`
- `QuestJournalUI`

### 28.3 `InventoryUIManager.AutoWireArmorDetailReferences()`

`InventoryUIManager` cerca:

- `invBackground`
- dentro:
  - `DescWeapon`
  - `Image`
  - `Title`
  - `Desc_Custom`
  - `Desc`
  - `EquipBTN`

Per la colonna weapon cerca:

- `WeaponColumn`
- oppure `WeaponCollumn`

Per la colonna shield cerca:

- `ShieldColumn`
- oppure `ShieldCollumn`

Per la colonna armor cerca:

- `ArmorColumn`
- oppure `ArmorCollumn`

Per i valori stats cerca root con nome:

- `Damage`
- `Critical`
- `Weight`
- `Scaling`
- `Requirement`
- `Def Phy`
- `Def Mag`

### 28.4 `MagicInventoryManager.AutoWireMagicReferences()`

`MagicInventoryManager` cerca:

- `MagicBackground`
- `GridBackground/GridInv`
- fallback `GridInv`
- `DescMagic`
- dentro detail:
  - `Image`
  - `Title`
  - `Desc`
  - `Damage`
  - `Critical`
  - `Scaling`
  - `Requirement`

### 28.5 `EquipmentManager.ResolveArmorContainersFromHierarchy()`

`EquipmentManager` cerca i container armor con questi nomi esatti:

- `helmet`
- `chestplate`
- `leggings`
- `boots`

Ordine di ricerca:

1. riferimento Inspector gia' assegnato
2. child del proprio transform
3. child di `equipmentBackground`
4. child di `inventoryBackground`
5. ricerca globale in scena

### 28.6 `AttributesUIManager.AutoWireAttributesUIReferences()`

Cerca prima il tab key:

- `Skill`
- oppure `Attributes`

Fallback nomi:

- `SkillBackground`
- `Attributes`

Altri nomi usati:

- `Right/Attributes`
- `Center/Panel`
- `Left`
- `LevelTxt`
- `LevelValue`
- `XpValue`
- `HPValue`
- `ManaValue`
- `StaminaValue`
- `BasePhyDamageValue`
- `MagicDamageValue`
- `PhyDefValue`
- `MagicDefValue`
- `LoadValue`
- `Load`

Rows attribute attese:

- `Vigor`
- `Mind`
- `Endurance`
- `Strength`
- `Dexterity`
- `Intelligence`
- `Faith`
- `Evil`
- `Karma`

### 28.7 `QuestJournalUI.AutoWireQuestUIReferences()`

`QuestJournalUI` supporta tab key:

- `Quest`
- `Quests`
- `Journal`

Nomi/percorsi usati:

- `QuestBackground`
- `LeftSide/QuestPanel`
- `LeftSide/Quest`
- `QuestPanel`

Prefab editor fallback:

- `Assets/Prefabs/UI/Quest.prefab`

Nota:

- in scena risulta un oggetto chiamato `QuestUiManager`, ma la classe vera e' `QuestJournalUI`
- il nome GameObject e il nome della classe non coincidono sempre

### 28.8 `CompassSystem`

`CompassSystem` non fa grosse ricerche per nome, ma si aspetta:

- `Camera.main`
- `compassBarRect`
- cardinal icons:
  - `iconNorth`
  - `iconSouth`
  - `iconEast`
  - `iconWest`

### 28.9 `PlayerUI`

`PlayerUI` auto-risolve:

- `Camera.main`
- `PlayerStats.instance`
- `PlayerInventory`
- frame bars dal parent dei fill image
- testi:
  - `FlaskCount` o `FlaskCounter`
  - `KeyCount`

## 29. Scene hierarchy attesa / riferimenti pratici

Questa non e' una gerarchia completa, ma e' quella utile per non perdere i riferimenti.

### 29.1 HUD

Atteso:

- `HUD_Canvas`
  - `TopLeftBars`
    - `HealthBar`
      - `HealthBar_Frame`
        - `HealthBar_Fill`
    - `ManaBar`
      - `ManaBar_Frame`
        - `ManaBar_Fill`
    - `StaminaBar`
      - `StaminaBar_Frame`
        - `StaminaBar_Fill`
  - minimap UI
    - `MinimapContainer`
  - target lock UI
    - `targetLock`

### 29.2 Menu inventory

Atteso:

- `HUD_Inventory`
  - menu root
  - `EquipmentBackground`
  - `invBackground`
    - `GridBackground`
      - `GridInv`
    - `DescWeapon`
      - display comune
      - `WeaponCollumn`
      - `ShieldCollumn`
      - `ArmorCollumn`
  - `MagicBackground`
  - `AttributesBackground`
  - `JournalBackground`
  - `SettingBackground`

### 29.3 Armor slots in equipment

Attesi object names:

- `helmet`
- `chestplate`
- `leggings`
- `boots`

## 30. `PlayerInventory` - dettagli che nel documento mancavano

### 30.1 Loadout arrays reali

`PlayerInventory` usa:

- `rightLoadout = new WeaponItem[3]`
- `leftLoadout = new WeaponItem[3]`
- `magicLoadout = new MagicItemData[3]`
- `usableLoadout = new UsableItemData[3]`
- `armorLoadout = new ArmorItemData[4]`

Current indices:

- `currentRightIndex`
- `currentLeftIndex`
- `currentMagicIndex`
- `currentUsableIndex`

### 30.2 Instance IDs

Sistema importante:

- ogni copia di arma/usable/magic/armor puo' avere `instanceId`
- il sistema serve a distinguere copie identiche
- una stessa istanza non puo' stare in piu' slot

Array IDs paralleli:

- `rightInstanceIds`
- `leftInstanceIds`
- `magicInstanceIds`
- `usableInstanceIds`
- `armorInstanceIds`

Metodi importanti:

- `GetCurrentWeaponInstanceId(Hand hand)`
- `TryUnequipCurrentWeaponForThrow(...)`
- `TryRemoveWeaponInstanceFromInventory(...)`
- `HasWeaponInstanceInInventoryPublic(...)`
- `AddWeaponInstance(...)`
- `GetEquippedArmorInstanceId(ArmorItemData.ArmorSlot slot)`
- `IsInstanceEquipped(string instanceId)`
- `IsArmorInstanceEquipped(string instanceId)`

### 30.3 Compat legacy ancora presente

Campi legacy nascosti ma ancora usati per seed/loadout:

- `legacyRightHandWeapon`
- `legacyLeftHandWeapon`
- `legacyEquippedUsable`

Questi servono per:

- migrazione dal vecchio asset setup
- seed iniziale del loadout se gli array nuovi sono vuoti

### 30.4 Starting loadout

`PlayerInventory` ha:

- `startingLoadout`

Tipo entry:

- `weapon`
- `magic`
- `armor`
- `item`
- `usable`
- `quantity`

Regola:

- se in una entry e' settato piu' di un campo, il codice usa priorita':
  - `Weapon > Magic > Armor > Usable > Item`

### 30.5 Database assignment

`itemDatabase` resta importante.

Se manca:

- il gameplay puo' ancora partire
- ma il restore save diventa fragile o incompleto

## 31. Save / load runtime details da ricordare

### 31.1 `PlayerStats`

`PlayerStats` e':

- singleton:
  - `PlayerStats.instance`
- persistente:
  - `DontDestroyOnLoad(root.gameObject)`

Su `SceneManager.sceneLoaded` fa:

- `RecalculateDerivedStats`
- `RefreshArmorTotals`
- `UpdateAllUI`
- refresh UI frame successivo
- `ApplyLoadedQuestStateIfPossible()`
- `ApplyLoadedInventoryStateIfPossible()`

Nota gameplay importante:

- entrando in `GameScene`, il run wallet viene resettato:
  - `runGold = runSilver = runCopper = 0`

Questo e' coerente con il concetto di run-inventory / run-wallet.

### 31.2 `QuestManager`

`QuestManager`:

- singleton `Instance`
- opzionalmente persistente cross-scene
- se `persistAcrossScenes` e' attivo:
  - stacca il parent
  - fa `DontDestroyOnLoad(gameObject)`

### 31.3 `CoreGenerator` gotcha

C'e' una nota importante:

nel file esiste ancora questo log:

- `"nessun tema valido trovato, uso i pool legacy del CoreGenerator."`

Ma questo messaggio e' fuorviante.

Per il sistema attuale:

- il generator non va piu' considerato davvero “legacy fallback enabled”
- la configurazione corretta resta quella basata su `DungeonFloorThemeTable`

Quindi:

- se vedi quel log, non fidarti del testo
- controlla davvero la config dei theme asset

### 31.4 Oggetti persistenti reali da tenere d'occhio

Da non rompere nel cambio scena:

- `PlayerStats`
- `QuestManager`
- eventualmente camera/player root a seconda del bootstrap scena

Oggetti che invece devono riallinearsi alla scena e non essere considerati fonte persistente unica:

- `MenuManager`
- `PlayerUI`
- `MinimapManager`
- `CompassSystem`
- riferimenti UI in generale
