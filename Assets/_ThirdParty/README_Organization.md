# Organizzazione degli asset di terze parti

Questa cartella e divisa prima per stato d'uso e poi per tipo di contenuto.

## Cartelle principali

- `_Used`: asset referenziati dal progetto, incluse tutte le dipendenze serializzate e native rilevate dall'AssetDatabase di Unity (prefab, mesh, materiali, texture, shader e script).
- `_Unused`: asset dei pacchetti importati che al momento non fanno parte della catena di dipendenze del gioco. Non sono stati eliminati.

Prima di eliminare `_Unused`, va comunque eseguita una verifica finale in Unity per eventuali caricamenti dinamici aggiunti successivamente.

## Categorie

- `Characters`: personaggi e animali.
- `Environment`: dungeon, natura, cielo, acqua, superfici e ambientazioni medievali.
- `Props`: oggetti di scena come contenitori e attrezzatura da campeggio.
- `VFX`: effetti visivi, fuoco, magia e buff.
- `Weapons`: armi e collezioni da armeria.

## Nomi delle collezioni

Il nome descrive prima il contenuto e mantiene alla fine il pacchetto o l'autore originale, per esempio:

- `Low_Poly_Dungeon_BrokenVector`
- `Magic_Effects_HovlStudio`
- `Free_Camping_Props_GanzSe`

Gli asset vanno spostati dall'Editor Unity, non da Esplora file, cosi i file `.meta` e i GUID restano corretti. Quando un asset di `_Unused` viene usato, devono essere trasferite in `_Used` anche tutte le sue dipendenze.
