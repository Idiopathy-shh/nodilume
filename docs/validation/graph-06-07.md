# GRAPH.06.07 — validazione backup/recovery SQLite

Data: 23 settembre 2026. Macchina: OFFICE-PC.
Base candidata: main@cdfa7a19d2d37a3b87f396454043f0859d3cc2d1.
Integrazione: PR #19, squash 073aaebbc575061548ef539987da8fd922f4b96d.
Fixture: database, backup, profili WebView2 e cartelle temporanei.

## Esiti

| Verifica | Esito |
|---|---|
| Viewer Node tests | PASS 10/10 |
| TypeScript e bundle | PASS |
| Build Nodilume.Tests Release | PASS, 0 warning/errori |
| Suite .NET | PASS 10/10 gruppi |
| Build Desktop/Smoke/ScaleSmoke/Benchmarks | PASS, 0 warning/errori |
| Smoke WPF/WebView2 | PASS |
| git diff --check | PASS |

Il runtime scale-smoke 300k non e' stato ripetuto: renderer, query e protocolli
viewer non cambiano. La copertura specifica usa invece un database SQLite oltre
64 MiB, superiore al limite del codec JSON, e verifica il ripristino di una
tabella extra non nota al modello del grafo.

## Copertura infrastruttura

- snapshot consistente tramite SQLite Online Backup API;
- pacchetto con sole entry `manifest.json` e `map.sqlite`;
- dimensione e SHA-256 verificati prima di aprire il database;
- `integrity_check`, schema supportato e metadati manifest/database concordanti;
- restore ripetuto con MapId e filename GUID sempre nuovi;
- titolo, revisione, grafo e tabella SQLite extra preservati;
- sorgente invariata e nessuna entry catalogo parziale;- rifiuto di checksum errato, manifest alterato, SQLite corrotto, entry extra,
  estensione errata e operazioni cancellate;
- retention esplicita a tre copie nel test; il default applicativo e' 10;
- file estraneo con nome compatibile ma manifest non valido non cancellato;
- nessun file temporaneo residuo dopo successo o rifiuto.

## Copertura smoke

Il flusso usa i pulsanti WPF reali e sostituisce soltanto il dialog nativo con
un adapter deterministico. Verifica creazione nel folder gestito, restore in
nuova mappa, revisione/contenuto preservati, selezione/apertura viewer, rifiuto
di un archivio malformato, annullamento e selezione persistente alla riapertura.
Sono rieseguite le regressioni GRAPH.03 e GRAPH.06.02–06.06.

## Limiti e lettura critica

Il limite del pacchetto v1 e' 8 GiB e lo schema SQLite deve essere supportato
dalla build corrente. La modifica del MapId conosce le tabelle schema v2:
future migrazioni devono aggiornare esplicitamente il rekey prima del restore.

SHA-256 rileva corruzione ma non autentica il mittente: un attaccante che puo'
riscrivere pacchetto e manifest puo' ricalcolare il checksum. Non ci sono firma,
cifratura, cloud sync o password. Il backup va quindi protetto con i permessi e
la cifratura del sistema operativo.

La retention vale solo in `Maps\Backups`; copie spostate altrove non vengono
gestite. Un errore di cancellazione non invalida il backup nuovo ed e' mostrato
come warning. Il dialog Windows reale non ha accettazione visiva manuale in
questa esecuzione, pur usando lo stesso contratto verificato dallo smoke.