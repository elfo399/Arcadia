  # Arcadia - Handoff Dettagliato

  Data ultimo audit completo: 2026-06-23
  Workspace: `d:\Unity\Arcadia`
  Documento pensato per riprendere il lavoro su un altro PC senza dover riaprire questa chat.

  Versione Unity verificata: `2022.3.62f3`
  Baseline Git verificata: `main` / `origin/main` a `18b3da5`

  > Nota di validita': le sezioni 38-45 contengono l'audit del 2026-06-23 e prevalgono sulle note storiche precedenti quando descrivono sistemi cambiati dopo marzo 2026.

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
  - chest treasure con loot table ScriptableObject e apertura via interazione

  ## 2.1 Baseline Git verificata

  Stato verificato il 2026-06-23:

  - branch: `main`
  - `HEAD`: `18b3da5` - `align repo`
  - tracking: `origin/main`
  - commit locali avanti rispetto a origin: nessuna
  - sono presenti modifiche non committate alla lista quest e a questo handoff; vedi sezione 44

  Il vecchio riferimento `143563f` resta utile solo come punto storico del sistema chest. Dal precedente aggiornamento dell'handoff (`e81bcc5`) al commit corrente sono cambiati in modo sostanziale UI, player stats, salvataggi, quest, scene e tool editor.

  ## 2.2 Treasure chest system

  Script nuovi / toccati:

  - `Assets/Scripts/Items/TreasureChestLootTable.cs`
  - `Assets/Scripts/Editor/TreasureChestLootTableDrawer.cs`
  - `Assets/Scripts/Rooms/TreasureChest.cs`
  - `Assets/Scripts/Player/PlayerInventory.cs`

  Comportamento corretto attuale:

  - la chest e' un `IInteractable`
  - il player la apre con `Triangolo` tramite `PlayerInteraction`
  - la chest estrae sempre e solo **1 reward**
  - la quantita' e' sempre **1**
  - il reward viene aggiunto direttamente al `PlayerInventory`
  - per ora non c'e' spawn di `objectOnTheGround`

  ### 2.2.1 Loot table chest

  `TreasureChestLootTable`:

  - contiene una lista di entry possibili
  - ogni entry ha:
    - `dropChance`
    - un solo asset reward assegnato

  Tipi reward supportati:

  - `ItemData`
  - `UsableItemData`
  - `MagicItemData`
  - `ArmorItemData`
  - `WeaponItem`

  Nota importante:

  - `dropChance` e' usato come **peso relativo di selezione**
  - non e' un multi-roll
  - la chest sceglie un solo elemento dalla lista

  Il drawer custom dell'inspector mostra soltanto:

  - `Drop %`
  - `Reward`

  Il tipo reward viene dedotto automaticamente dal tipo dell'asset assegnato.

  ### 2.2.2 Setup chest in prefab

  Campi principali di `TreasureChest`:

  - `lootTable`
  - `prompt`
  - `consumeOnlyOnce`
  - `animator`
  - `openStateName`
  - `closedStateName`
  - `enableAnimatorOnlyWhenOpened`
  - `rewardDelaySeconds`
  - `closedVisual`
  - `openedVisual`

  Setup minimo per funzionare:

  - collider presente sulla chest oppure su un figlio rilevabile da `PlayerInteraction`
  - layer della chest incluso in `PlayerInteraction.interactLayer`
  - `lootTable` assegnata

  ### 2.2.3 Animator chest - nota critica

  Per la chest animata il setup corretto del controller e':

  - stato default `Closed`
  - stato `Open`
  - la clip `Open` **non** deve essere in loop
  - la chest una volta aperta deve restare nello stato `Open`

  Nota pratica importante:

  - lo script prova a forzare prima `Open` come stato diretto
  - il trigger `Open` e' fallback
  - se l'animazione non parte, la prima cosa da controllare e' che l'`Animator` assegnato nel prefab sia quello corretto e che il controller contenga davvero gli stati `Closed` / `Open`

  ### 2.2.4 PlayerInventory - helper loot

  In `PlayerInventory` sono stati aggiunti helper runtime per reward:

  - `AddWeaponLoot(...)`
  - `AddArmorLoot(...)`
  - `AddMagicLoot(...)`
  - `AddUsableLoot(...)`
  - `AddGenericItemLoot(...)`

  Regola attuale:

  - armi e armature entrano come entry separate
  - item / magic / usable vengono stackati se gia' presenti

  ## 2.3 Note UI pad dopo ultima commit

  In `MenuManager` e `EquipmentManager` e' stato toccato il routing input del controller.

  Nota importante:

  - era presente un problema di doppia lettura del D-Pad
  - e' stato anche rimosso un forcing hardcoded verso `Armor`
  - se la navigazione equipment dovesse ancora risultare poco affidabile, il prossimo step corretto non e' aggiungere altre scorciatoie, ma fare una mappa di navigazione esplicita slot-per-slot

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
    - lista quest e scroll
    - fasi quest
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

  - `Assets/Scripts/UI/DynamicBar.cs`
  - `Assets/Scripts/UI/PlayerStatDynamicBar.cs`
  - `Assets/Scripts/UI/ProgressBarUI.cs`
    - componenti visuali per le diverse barre correnti
    - i valori player sono coordinati da `PlayerUI` / `PlayerStats`

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

  - `StatBarManager` e' stato rimosso
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

  ### Dependencies

  - `menuManager`
  - `inventoryUIManager`
  - `magicInventoryManager`
  - `playerInventory`
  - `playerStats`

  ### Quest UI

  - `useQuestManager`
  - `questListContainer`
  - `questItemPrefab`
  - `questListScrollRect`
  - `questListViewport`
  - `questListLayout`
  - `questListContentSizeFitter`
  - `questListMouseWheelPixels`
  - `startingQuests`

  I vecchi filtri Active/Completed non sono piu' presenti: i tre metodi filtro pubblici inoltrano tutti alla lista completa.

  ### Quest Detail UI

  - `questDetailTypeText`
  - `questDetailRecommendedText`
  - `questDetailImage`
  - `questDetailTitleText`
  - `questDetailLocationText`
  - `questDetailLoreTitleText`
  - `questDetailLoreDescriptionText`
  - `questDetailLoreAuthorText`
  - `questDetailPanelRoot`
  - `showQuestDetailOnlyOnSelection`
  - `questObjectivesContainer`
  - `questObjectivePrefab`
  - `questRewardsContainer`
  - `questRewardPrefab`
  - `questClaimRewardButton`
  - `questRewardInventoryCapacity`
  - `questRewardMagicCapacity`

  ### Quest Phase UI

  - `questPhaseText`
  - `questPreviousPhaseButton`
  - `questNextPhaseButton`
  - `questPadFocusBorderColor`
  - `questPadFocusBorderThickness`

  Regole correnti:

  - ogni obiettivo ha `phase >= 1`
  - il runtime normalizza l'ordine e limita ogni fase a massimo 5 obiettivi
  - progrediscono solo gli obiettivi della fase corrente
  - la UI mostra solo gli obiettivi della fase visualizzata
  - la UI puo' tornare alle fasi completate, ma non avanzare oltre la fase runtime corrente
  - il formato del campo dedicato e' `FASE 1/2`
  - se `questPhaseText` non e' collegato, la fase viene accodata a `questDetailRecommendedText`

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
  - `StatBarManager` non esiste piu'; usare `PlayerUI` e i componenti barra correnti

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
  - non reintrodurre `StatBarManager`
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

  ### 18.6 Blocco input camera durante il menu

  `CameraInputBlocker` e' stato rimosso. La responsabilita' corrente e' in `MenuManager`:

  - usa `cameraInputProviders` come lista primaria
  - usa `cameraInputFallbacks` quando serve
  - disabilita l'input camera all'apertura del menu
  - lo riabilita alla chiusura

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

  - salva/carica un JSON separato per personaggio in:
    - `Application.persistentDataPath/gamedata_<characterId>.json`
  - conserva il personaggio selezionato in `PlayerPrefs`, chiave `SelectedCharacterId`
  - mantiene il fallback legacy `Application.persistentDataPath/gamedata.json`
  - migra il vecchio wallet oro/argento/rame verso `bankCoins`

  Metodi:

  - `SaveData(GameData data)`
  - `LoadData()`
  - `LoadData(string characterId, bool allowLegacyFallback)`
  - `SelectCharacter(string characterId)`
  - `EnsureCharacterData(string characterId, string characterName)`
  - `GetSaveFilePath()`
  - `GetSaveFilePath(string characterId)`

  ### 22.2 `GameData`

  File:

  - `Assets/Scripts/System/GameData.cs`

  Struttura del salvataggio:

  - personaggio:
    - `selectedCharacterId`
    - `characterName`
    - `selectedCharacterStartApplied`

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
  - monete correnti:
    - `usesUnifiedCoins`
    - `bankCoins`
    - `runCoins`
  - i campi legacy `bankGold`, `bankSilver`, `bankCopper` sono solo dati di migrazione non serializzati dal nuovo modello
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

  - `PlayerStats` resetta il run wallet alla morte prima di tornare in `HubScene`
  - la banca resta persistente
  - `PlayerStats` applica quest e inventory caricati in `Start()` / `OnSceneLoaded()`, non durante il primo `Awake()`

  ## 23. Quest system runtime

  ### 23.1 `QuestManager`

  File:

  - `Assets/Scripts/System/QuestManager.cs`
  - `Assets/Scripts/System/QuestManager.JournalRuntime.cs`
  - `Assets/Scripts/System/QuestManager.ObjectiveEvents.cs`
  - `Assets/Scripts/System/QuestDefinition.cs`
  - `Assets/Scripts/System/QuestEvents.cs`

  Responsabilita':

  - mantiene la lista runtime delle quest
  - crea le quest iniziali da asset `QuestDefinition`
  - seed / merge compatibile da `QuestEntryData` della UI
  - notifica cambi alla UI
  - puo' persistere cross-scene
  - indicizza gli obiettivi per tipo evento
  - aggiorna quantita' e completamento solo nella fase attiva
  - valida capacita' inventory prima del claim reward
  - applica reward item, weapon, usable, magic, armor ed experience

  Campi principali:

  - `persistAcrossScenes`
  - `autoNotifyOnStart`
  - `initialQuestDefinitions`

  Nested types:

  - `QuestData`
  - `QuestObjectiveData`
  - `QuestRewardData`

  Eventi obiettivo supportati:

  - `KillEnemy`
  - `CollectItem`
  - `Interact`
  - `EnterRoom`
  - `ClearRoom`
  - `OpenChest`
  - `ReachFloor`

  Gli emitter reali sono collegati a `EnemyHealth`, `PlayerInventory`, `CoinPickup`, `KeyPickup`, `PlayerInteraction`, `Room`, `TreasureChest` e `FloorPortal`.

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

  ## 25. File legacy rimossi e componenti ancora attivi

  Dal refactor di giugno 2026 sono stati rimossi:

  - `StatBarManager.cs`
  - `SimpleDungeonGenerator.cs`
  - `RandomProp.cs`
  - `CameraInputBlocker.cs`

  Non cercare di ripristinarli come dipendenze. I sostituti correnti sono:

  - dungeon: `CoreGenerator`
  - blocco camera menu: `MenuManager` agisce direttamente sui provider configurati
  - barre HUD: `PlayerUI` coordina i valori; `DynamicBar` e' ancora un componente attivo nelle scene
  - prop placement: tool editor `PrefabScatterToolWindow`

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

  ## 32. File di supporto e classi minori

  Questi file non sono il centro del refactor, ma fanno parte del progetto e possono servire per debugging o extension.

  ### 32.1 `PlayerAnimationEvents`

  File:

  - `Assets/Scripts/Player/PlayerAnimationEvents.cs`

  Responsabilita':

  - ponte tra animation events e gameplay combat
  - abilita/disabilita hitbox mano destra/sinistra
  - chiama `combat.EndAttack()`
  - prepara il danno del colpo per `WeaponDamage`

  Campi:

  - `rightHandDamage`
  - `leftHandDamage`

  ### 32.2 `WeaponDamage`

  File:

  - `Assets/Scripts/Combat/WeaponDamage.cs`

  Responsabilita':

  - hitbox melee attiva durante le finestre degli animation events
  - legge il danno preparato oppure refresh dall'arma equipaggiata
  - applica danno a `IDamageable`
  - evita multi-hit sullo stesso target nello stesso swing con `hitTargets`

  Campi chiave:

  - `hand`
  - `fallbackDamage`
  - `damage`
  - `logDamage`
  - `damageCollider`
  - `currentWeapon`
  - `lastHitWasCritical`
  - `lastAttackType`

  ### 32.3 Prop placement

  `RandomProp` e' stato rimosso. Il placement assistito corrente e' editor-only tramite `PrefabScatterToolWindow`; lo spawn nemici runtime resta responsabilita' di `EnemySpawner` / `Room`.

  ### 32.4 `EnemySetup`

  File:

  - `Assets/Scripts/Enemy/EnemySetup.cs`

  Responsabilita':

  - crea/risolve `HeadPoint`
  - crea `LockOnPoint`
  - costruisce la health bar world-space enemy se manca

  Campi:

  - `defaultHeightOffset`
  - `healthBarScale`
  - `customHealthBarPrefab`

  ### 32.5 `EnemyHealthBar`

  File:

  - `Assets/Scripts/Enemy/EnemyHealthBar.cs`

  Nota:

  - wrapper/bar script usato da `EnemyHealth` e `EnemySetup`
  - sistema separato dalla HUD del player

  ### 32.6 `InventoryItem`

  File:

  - `Assets/Scripts/UI/InventoryItem.cs`

  Nota:

  - e' il contenitore runtime usato da `PlayerInventory` / `InventoryUIManager`
  - puo' rappresentare:
    - weapon
    - magic
    - armor
    - usable
    - item
  - conserva anche `instanceId` quando necessario

  ### 32.7 `InventorySlot`

  File:

  - `Assets/Scripts/UI/InventorySlot.cs`

  Responsabilita':

  - singolo slot UI riusato in:
    - inventory
    - equipment
    - magic
    - HUD front slots
  - setup sprite / quantity
  - focus visual
  - pointer / submit / drag callbacks tramite `IInventorySlotHandler`

  ### 32.8 `IInventorySlotHandler`

  File:

  - `Assets/Scripts/UI/IInventorySlotHandler.cs`

  Interfaccia usata da:

  - `InventoryUIManager`
  - `MagicInventoryManager`
  - `EquipmentManager`

  ### 32.9 `MenuTabEntry`

  File:

  - `Assets/Scripts/UI/MenuTabEntry.cs`

  Usato da:

  - `MenuManager`

  Ruolo:

  - definisce:
    - `key`
    - `label`
    - `background`

  ### 32.10 Quest row UI helpers

  File:

  - `Assets/Scripts/UI/QuestItemUI.cs`
  - `Assets/Scripts/UI/QuestObjectiveItemUI.cs`
  - `Assets/Scripts/UI/QuestRewardItemUI.cs`

  Ruolo:

  - componenti UI per row di:
    - quest list
    - objective list
    - reward list

  ### 32.11 `WeaponAnimationProfile`

  File:

  - `Assets/Scripts/Items/WeaponAnimationProfile.cs`

  Contiene:

  - anim name per:
    - right hand light/heavy
    - left hand light/heavy
    - air attack opzionali

  ### 32.12 `IDamageable`

  File:

  - `Assets/Scripts/Interface/IDamageable.cs`

  Usato da:

  - `EnemyHealth`
  - `PlayerStats`
  - projectile e melee hitbox

  ### 32.13 `IInteractable`

  File:

  - `Assets/Scripts/Interface/IInteractable.cs`

  Usato da:

  - `InteractableDoor`
  - `WeaponWorldPickup`
  - altri interactable futuri

  ## 33. Editor scripts presenti

  Questa parte e' utile se riapri il progetto e ti chiedi perche' certi inspector appaiono custom.

  ### 33.1 `WeaponItemEditor`

  File:

  - `Assets/Scripts/Editor/WeaponItemEditor.cs`

  Responsabilita':

  - inspector custom per `WeaponItem`
  - mostra campi condizionali in base a `WeaponCategory`
  - shield:
    - block/parry
  - wand:
    - projectile/mana/cooldown
  - bow:
    - projectile/ammo/cooldown

  ### 33.2 `QuestRewardDrawer`

  File:

  - `Assets/Scripts/Editor/QuestRewardDrawer.cs`

  Responsabilita':

  - property drawer per:
    - `QuestManager.QuestRewardData`
    - `QuestRewardEntryData`
  - mostra un singolo campo asset dinamico in base a `QuestRewardType`

  ### 33.3 `TreasureChestLootTableDrawer`

  File:

  - `Assets/Scripts/Editor/TreasureChestLootTableDrawer.cs`

  Responsabilita':

  - property drawer per `TreasureChestLootTable.LootEntry`
  - supporta reward asset multipli:
    - weapon
    - armor
    - magic
    - usable
    - item

  Nota:

  - la classe runtime `TreasureChestLootTable` esiste ed e' usata dal sistema chest

  ### 33.4 `QuestObjectiveDrawer`

  File:

  - `Assets/Scripts/Editor/QuestObjectiveDrawer.cs`

  Responsabilita':

  - drawer per `QuestManager.QuestObjectiveData` e `QuestObjectiveEntryData`
  - espone fase, tipo evento, target, quantita' e stato in modo compatto

  ### 33.5 Tool UI e level design

  File:

  - `Assets/Scripts/Editor/AttributeProgressBarPrefabReplacer.cs`
  - `Assets/Scripts/Editor/PrefabScatterToolWindow.cs`
  - `Assets/Scripts/Editor/ProBuilderFloorDeformerWindow.cs`

  Menu:

  - `Tools/Arcadia/UI/Replace Attribute Bars With Prefab`
  - `Tools/Arcadia/Prefab Scatter Tool`
  - `Tools/Arcadia/ProBuilder Floor Deformer`

  Questi script sono editor-only e non devono essere referenziati dal runtime.

  ## 34. File legacy / residui / da verificare

  ### 34.1 Generator legacy

  `SimpleDungeonGenerator` e' stato eliminato. Il solo generator runtime attuale e' `CoreGenerator`.

  ### 34.2 `DynamicBar`

  File:

  - `Assets/Scripts/UI/DynamicBar.cs`

  Ruolo:

  - utility generic bar
  - puo' ridimensionare una frame in base a `maxValue`

  Nota:

  - presente e usato nelle scene per le barre HUD
  - i valori e i binding restano coordinati da `PlayerUI` / `PlayerStats`

  ### 34.3 `SceneLoader`

  Anche se usato, e' estremamente semplice e generico.
  Se il flow futuro sara' hub -> dungeon -> multi-floor -> boss -> ritorno hub, il punto centrale resta `CoreGenerator` + `FloorPortal`, non `SceneLoader`.

  ### 34.4 `TreasureChestLootTableDrawer`

  Non e' residuo: il relativo runtime esiste in `Assets/Scripts/Items/TreasureChestLootTable.cs` ed e' consumato da `TreasureChest`.

  ## 35. Problemi noti / gotcha pratici

  Questa sezione serve per evitare trappole quando riprendi.

  ### 35.1 Nomi UI scritti in modo inconsistente

  Esempi:

  - `WeaponCollumn`
  - `ShieldCollumn`
  - `ArmorCollumn`

  Sono typo storici ma ormai fanno parte del setup reale.
  Il codice ha fallback sia `Column` sia `Collumn` in alcuni punti, ma non ovunque conviene rinominare a mano.

  ### 35.2 `leggings` da verificare in scena

  Nella grep scene rapida risultavano:

  - `helmet`
  - `chestplate`
  - `boots`

  ma non e' emerso chiaramente `leggings`.

  Quindi:

  - controllare `HubScene` e `GameScene`
  - verificare che l'oggetto `leggings` sia:
    - salvato
    - nominato esattamente cosi'
    - assegnato o risolvibile

  ### 35.3 `QuestUiManager` come nome GameObject

  Il GameObject in scena puo' ancora chiamarsi `QuestUiManager`, ma la classe vera e' `QuestJournalUI`.
  Non confondere:

  - nome oggetto in hierarchy
  - nome classe C#

  ### 35.4 `CoreGenerator` log fuorviante sui legacy pools

  Gia' segnalato:

  - se compare il log "uso i pool legacy", non assumerlo vero
  - controllare la config tema, non il fallback

  ### 35.5 `StatBarManager` e' stato rimosso

  Le barre reali sono gestite da `PlayerUI`, con componenti `DynamicBar`, `PlayerStatDynamicBar` e `ProgressBarUI` a seconda del pannello.

  ## 36. Checklist finale prima di cambiare PC / prima build

  ### 36.1 Prima di spostarti su un altro PC

  1. assicurati che:
    - scene siano salvate
    - prefab siano salvati
    - asset ScriptableObject recenti siano salvati
  2. porta con te:
    - l'intero progetto
    - questo file `handoff.md`
  3. se usi git:
    - commit o stash pulito

  ### 36.2 Quando apri il progetto sull'altro PC

  1. aspetta il reimport completo Unity
  2. apri `HubScene`
  3. apri `GameScene`
  4. controlla console per missing refs
  5. controlla:
    - `CoreGenerator`
    - `MenuManager`
    - `InventoryUIManager`
    - `EquipmentManager`
    - `PlayerUI`
    - `MinimapManager`

  ### 36.3 Smoke test minimo

  1. apri menu
  2. equip weapon
  3. equip shield
  4. equip armor
  5. equip magic
  6. muoviti hub
  7. entra dungeon
  8. verifica spawn in start
  9. verifica camera lock
  10. verifica minimap
  11. verifica compass
  12. fatti colpire con e senza armor

  ## 37. Stato del documento

  Questo file ora copre:

  - architettura generale
  - versione Unity, pacchetti e scene di build
  - manager UI
  - player systems
  - personaggi selezionabili e salvataggi separati
  - combat
  - inventory/loadout/save
  - dungeon generator
  - room flow
  - enemy system
  - items/database/pickup
  - quest/journal, fasi ed event bus
  - meteo e pagina mappa
  - limiter e display FPS
  - auto-wire names
  - scene hierarchy aspettata
  - editor scripts
  - file legacy/residui
  - gotcha pratici
  - modifiche locali non committate e risultato compilazione

  Se in futuro si aggiungono sistemi grossi nuovi, aggiornare questo file nelle sezioni:

  - architettura
  - setup Unity
  - gotcha
  - smoke tests

  ## 38. Audit completo del progetto - 2026-06-23

  ### 38.1 Perimetro verificato

  Sono stati controllati:

  - stato Git e cronologia dal precedente handoff
  - tutti i 91 script C# sotto `Assets/Scripts`
  - le due scene incluse nei Build Settings
  - componenti custom collegati nelle scene
  - prefab e asset ScriptableObject rilevanti
  - `Packages/manifest.json`
  - compilazione Unity batch

  Dimensioni indicative del progetto:

  - 4.675 file sotto `Assets`
  - 91 script C# totali, di cui 7 editor-only
  - 299 prefab
  - 96 asset `.asset`
  - 7 scene totali, ma solo 2 scene applicative in build

  ### 38.2 Versione e pacchetti chiave

  - Unity `2022.3.62f3`
  - URP `14.0.12`
  - Input System `1.14.2`
  - Cinemachine `2.10.5`
  - AI Navigation `1.1.7`
  - ProBuilder `5.2.4`
  - TextMeshPro `3.0.9`
  - Post Processing `3.4.0`

  ### 38.3 Scene di build

  Ordine corrente:

  1. `Assets/Scenes/HubScene.unity`
  2. `Assets/Scenes/GameScene.unity`

  Le altre cinque scene sono demo di asset importati e non fanno parte del flusso applicativo.

  ## 39. Architettura runtime corrente

  ### 39.1 HubScene

  Sistemi principali collegati:

  - player completo con controller, combat, interaction, inventory, stats, visuals e target lock
  - `MenuManager` e manager UI separati
  - `QuestManager` persistente
  - `QuestJournalUI`
  - HUD, minimap e compass
  - `SceneLoader` sul portale verso il dungeon

  ### 39.2 GameScene

  Contiene gli stessi sistemi player/UI e in piu':

  - `CoreGenerator`
  - `WeatherManager`
  - `MapPageManager`
  - contenuto dungeon generato e minimappa associata

  ### 39.3 Persistenza e duplicati di scena

  - `PlayerStats` mantiene persistente il root del player
  - `QuestManager` puo' essere `DontDestroyOnLoad`
  - i duplicati incontrati dopo il cambio scena vengono eliminati dai singleton
  - modificare solo la copia di `GameScene` di un manager persistente puo' non avere effetto quando il gioco parte da `HubScene`
  - per test attendibili verificare sia avvio diretto di `GameScene` sia transizione `HubScene -> GameScene`

  ## 40. Personaggi selezionabili e salvataggi

  ### 40.1 Asset personaggio

  Sistema basato su:

  - `PlayerCharacterData`
  - `PlayerCharacterDatabase`
  - `PlayerCharacterSelection`
  - `PlayerCharacterBootstrapper`
  - `PlayerCharacterSelectionButton`

  Database runtime:

  - `Assets/Resources/PlayerCharacterDatabase.asset`
  - caricato tramite `Resources.Load("PlayerCharacterDatabase")`
  - personaggio default: `Warrior`

  Personaggi configurati:

  - `warrior` / Guerriero
  - `mage` / Maga
  - `assassin` / Robert
  - `archer` / Arciere

  Ogni asset definisce attributi, risorse base, flask, alignment, loadout e backpack iniziale. `previewPrefab` e `playerPrefab` risultano attualmente non assegnati nei quattro asset.

  ### 40.2 Regola di bootstrap

  Il pacchetto iniziale del personaggio viene applicato una sola volta e poi il salvataggio diventa autorevole tramite `selectedCharacterStartApplied`.

  `PlayerStats` puo' usare anche `inspectorStartingCharacter` come override. Questo rende importante non lasciare flag di test attivi in produzione.

  ### 40.3 Save per personaggio

  Il file corrente e':

  - `gamedata_<characterId>.json`

  `gamedata.json` e' solo fallback legacy. Il sistema conserva e ripristina:

  - identita' personaggio
  - livello, esperienza e attributi
  - monete banca/run
  - quest incluse fase, progresso e reward claimato
  - inventory e tutti i loadout con instance ID

  ## 41. Menu, mappa e meteo

  ### 41.1 Menu libro

  `MenuManager` ora gestisce:

  - apertura e chiusura animate
  - animazioni pre/post contenuto
  - flip pagina destro/sinistro con frame sprite per tab
  - accelerazione quando si saltano piu' pagine
  - scelta automatica mouse/tastiera o pad
  - routing della navigazione ai manager della tab attiva
  - blocco dei provider camera configurati mentre il menu e' aperto

  Tab configurate:

  - Hub: Equipment, Inventory, Magic, Attributes, Journal, Setting
  - Game: Maps, Equipment, Inventory, Magic, Attributes, Journal, Setting

  La tab iniziale e' Equipment in Hub e Maps in Game.

  ### 41.2 MapPageManager

  La pagina Maps mostra:

  - piano e tema del dungeon
  - minimappa
  - nome e ritratto personaggio
  - meteo corrente
  - timer run
  - monete run
  - progress bar HP, mana e XP

  Si aggiorna dagli eventi di `CoreGenerator` e `MinimapManager`, oltre a leggere `PlayerStats` e `WeatherManager`.

  ### 41.3 WeatherManager

  In `GameScene` il ciclo configurato e':

  - Alba: 45 s
  - Giorno: 90 s
  - Tramonto: 60 s
  - Notte: 75 s

  Il meteo viene rilanciato ogni 30 s e al cambio fase. Pesi correnti:

  - Clear: 60
  - Rain: 25
  - Storm: 15

  Il manager pilota Animator, directional light e ambient light, con transizione luce progressiva.

  ### 41.4 FPS

  - `FrameRateLimiter` disattiva VSync e imposta `Application.targetFrameRate = 120` prima del caricamento scene
  - `FpsDisplay` crea un overlay persistente a runtime e aggiorna il valore ogni 0,25 s

  ## 42. Quest system corrente

  ### 42.1 Flusso dati

  - authoring: `QuestDefinition` ScriptableObject
  - runtime autorevole: `QuestManager`
  - eventi gameplay: `QuestEvents`
  - presentazione: `QuestJournalUI`
  - persistenza: `SavedQuestData` dentro `GameData`

  L'asset configurato e' `Assets/ScriptableObjects/Quests/InitialQuest/InitialQuest.asset`.

  ### 42.2 Fasi

  - gli obiettivi devono essere ordinati e raggruppati per fase nell'asset
  - `NormalizeObjectivePhases` rende le fasi contigue
  - ogni fase contiene al massimo 5 obiettivi
  - al sesto obiettivo il runtime crea automaticamente una fase successiva
  - eventi e completamento manuale agiscono solo sulla fase corrente
  - completata una fase, il primo obiettivo incompleto della fase seguente diventa attivo
  - la quest e' completata solo quando tutti gli obiettivi sono completati

  L'asset `InitialQuest` contiene attualmente 5 obiettivi placeholder: 3 dichiarati in fase 1 e 2 in fase 2. Tutti hanno `eventType = None`, quindi non progrediscono automaticamente.

  ### 42.3 Event bus

  Target matching:

  - per `targetObject`, risolve ID da `EnemyData`, `RoomData` o asset item
  - in alternativa usa `targetId`
  - puo' fare match anche su `targetTag`
  - rimuove il suffisso `(Clone)` prima del confronto
  - se ID e tag non sono configurati, qualsiasi evento dello stesso tipo e' valido

  ### 42.4 Reward

  Tipi supportati:

  - Item
  - Weapon
  - Usable
  - Magic
  - Armor
  - Experience

  Prima del claim il manager verifica dipendenze, asset reward e capacita' di inventory/magic inventory. Item, usable e magic possono stackare; weapon e armor occupano entry separate.

  ### 42.5 UI Journal

  - la lista crea una row dal prefab `Quest.prefab` per ogni quest runtime
  - il dettaglio mostra immagine, lore, obiettivi della fase visualizzata e reward
  - il focus pad e la selezione quest sono mantenuti dal manager
  - le fasi precedenti possono essere consultate
  - la fase futura non e' consultabile prima di essere sbloccata

  Regola critica: ogni `QuestDefinition.questId` deve essere univoco. Inserire lo stesso asset molte volte produce row indistinguibili e tutte le operazioni per ID colpiscono la prima quest corrispondente.

  ## 43. Audit configurazione scene e debito attivo

  ### 43.1 Flag e dati di test

  Prima di una build reale verificare:

  - `forceStartDataIgnoreSave = true` su `PlayerStats` in entrambe le scene
  - `GameScene.PlayerStats.vigor = 1000`
  - `MapPageManager.showFullMapForTesting = true`
  - `MapPageManager.defaultPlayerName` contiene un placeholder di test
  - `QuestJournalUI.startingQuests` contiene dati placeholder nelle due scene
  - `InitialQuest` usa obiettivi duplicati con `eventType = None`

  ### 43.2 Divergenze Quest UI tra scene

  `GameScene`:

  - ha viewport e riferimenti scroll collegati nella working tree locale
  - `questPhaseText`, `questPreviousPhaseButton` e `questNextPhaseButton` sono ancora null
  - la label fase usa quindi il fallback dentro `questDetailRecommendedText`

  `HubScene`:

  - non ha il nuovo viewport della lista collegato
  - conserva nel YAML campi serializzati rimossi dal codice, per esempio vecchi auto-wire e detail scroll
  - va salvata da Unity dopo una verifica manuale solo quando si decide di allinearne davvero la UI

  ### 43.3 Duplicati quest usati per il test scroll

  Nella modifica locale di `GameScene`:

  - `initialQuestDefinitions` contiene 20 volte lo stesso asset `InitialQuest`
  - sono presenti 3 istanze scene del prefab Quest contro 1 nella baseline

  Questo setup e' utile solo per stressare lo scroll. Non e' una configurazione dati valida perche' tutte le quest hanno `questId = initial_quest`.

  ### 43.4 Integrita' asset animazioni libro

  Unity segnala 14 `.anim.meta` sotto:

  - `Assets/Animations/UI/BookPause/FlipLeftPage`
  - `Assets/Animations/UI/BookPause/FlipRightPage`

  Il parser YAML non estrae direttamente il GUID e usa il fallback testuale. La compilazione continua, ma prima di rinominare, rigenerare o spostare quelle animazioni verificare i `.meta` e le reference del controller.

  ### 43.5 Debito strutturale

  - non esistono assembly definition custom
  - non esistono test automatici di progetto
  - diversi manager UI hanno ancora fallback `FindObjectOfType` / ricerca per nome
  - alcuni manager creano componenti o preview UI a runtime; non assumere che tutta la UI sia scene-authored
  - sono presenti numerosi log diagnostici nel runtime
  - la validazione funzionale resta manuale per menu, scene change, quest, save e combat

  ## 44. Modifiche locali non committate al momento dell'audit

  La branch non ha commit locali da pushare. Le modifiche funzionali in working tree sono:

  - `Assets/Prefabs/UI/Quest.prefab`
    - row alta 32 px
    - `Button` permanente sul root
    - `SelectionOverlay` full-row con `Outline`
    - graphic decorative senza raycast
  - `Assets/Scripts/UI/QuestItemUI.cs`
    - espone la graphic di selezione
    - selezione dorata trasparente sull'intera row
  - `Assets/Scripts/UI/QuestJournalUI.cs`
    - riferimenti scroll espliciti
    - rotellina mouse con clamp
    - calcolo altezza contenuto dalle row attive
    - auto-scroll della quest focalizzata col pad
    - niente aggiunta runtime di `Button` / `Outline` alle row quest
  - `Assets/Scenes/GameScene.unity`
    - `QuestListViewport` con `RectMask2D`
    - `ScrollRect`, layout e content collegati
    - dati duplicati per stress test della lista
  - `handoff.md`
    - aggiornamento documentazione corrente

  `Assets/Scenes/HubScene.unity` appare modificata nello status, ma `git diff` non mostra differenze di contenuto; e' verosimilmente una differenza di working-tree/line ending.

  `git diff --check` segnala inoltre whitespace nelle nuove righe YAML di `Quest.prefab` e `GameScene.unity`. Sono righe serializzate da Unity, ma vanno considerate se la pipeline applica controlli whitespace rigidi.

  Prima del commit della lista quest:

  1. sostituire le 20 quest duplicate con definizioni aventi ID univoci oppure ridurre il test a una sola quest
  2. rimuovere le 2 istanze Quest aggiuntive dalla scena se non sono template intenzionali
  3. decidere se creare e collegare davvero testo/pulsanti fase oppure mantenere il fallback sulla label recommended
  4. fare test con avvio diretto Game e transizione da Hub
  5. verificare mouse, pad, selezione, scroll fino all'ultima row e claim reward

  ## 45. Verifica eseguita durante l'audit

  Comando equivalente eseguito:

  - Unity `2022.3.62f3` in batch mode, `-nographics -quit`

  Risultato:

  - compilazione script completata
  - nessun errore `CSxxxx`
  - nessun warning `CSxxxx`
  - uscita Unity con codice 0
  - 14 warning non bloccanti sui GUID dei `.anim.meta` del page flip

  Non sono stati eseguiti Play Mode test automatici perche' il progetto non contiene test assembly. Lo smoke test manuale resta obbligatorio.
