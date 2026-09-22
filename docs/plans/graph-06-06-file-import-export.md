# GRAPH.06.06 — import/export file portabile

Data: 22 settembre 2026. Base: main@5624163e47c439bdbde757c75cd36a22b89e0330.
Branch: feat/graph-06-06-file-import-export. Macchina autorizzata: OFFICE-PC.

## Obiettivo

Collegare il codec JSON GRAPH.06.01 alla shell WPF e al catalogo multi-mappa,
senza trasformarlo impropriamente in backup SQLite e senza consentire
sovrascritture della mappa attiva durante un import.

## Contratto export

- Dialogo SaveFileDialog limitato a file .json / .nodilume.json.
- Snapshot logico della mappa scelta, non copia del database SQLite.
- UTF-8 senza BOM e ordinamento deterministico ereditato dal codec.
- Limite file 64 MiB.
- Scrittura in un file temporaneo nella cartella destinazione, flush su disco
  e sostituzione finale; cleanup del temporaneo su errore o cancellazione.
- Destinazioni non JSON rifiutate prima di qualsiasi scrittura.
## Contratto import

- Dialogo OpenFileDialog per file .json / .nodilume.json.
- Lettura UTF-8 bounded con file bloccato contro scritture concorrenti.
- Formato/versione/invarianti verificati dal codec prima di creare SQLite.
- Ogni import genera un nuovo MapId, revisione 0 e database GUID.sqlite.
- Il nome e le identità Idea/Placement/Relation del documento sono preservati.
- Riserva esclusiva del filename e cancellazione di DB/WAL/SHM su errore.
- Import riuscito selezionato e riaperto nel viewer; sorgente mai modificata.

## Concorrenza UI

Azioni mappa/file, editor Idea, editor Relation e ricerca si bloccano
reciprocamente. Questo impedisce che un edit concorra con lo snapshot o
che un import cambi mappa mentre è in corso una scrittura revisionata.

## Limiti e scelta critica

Il codec GRAPH.06.01 è in-memory e destinato a mappe piccole/medie.
Il tetto 64 MiB riduce rischio di allocazioni incontrollate ma non consente
di dichiarare portabili tutte le fixture da 300.000 Idea. Backup/recovery
SQLite e un eventuale codec streaming restano patch separate.

## Gate

- Viewer Node/build TypeScript e suite .NET completa.
- Test file reali per atomicità, identità fresca, doppio import,
  UTF-8/UTF-16, malformed/empty/oversize, cancellazione ed estensioni.
- Smoke WPF/WebView2 sui pulsanti reali tramite dialogo file iniettato.
- git diff --check e review prima della PR.
