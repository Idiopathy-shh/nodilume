# GRAPH.06.07 — backup e recovery SQLite

Data: 22 settembre 2026.
Base: main@cdfa7a19d2d37a3b87f396454043f0859d3cc2d1.
Worktree: C:\Sviluppo\Nodilume-graph06-07.
Branch: feat/graph-06-07-backup-recovery.

## Obiettivo

Aggiungere un backup applicativo consistente dell'intero database SQLite e un
ripristino verificabile, sempre non distruttivo. Questa slice non riusa il codec
JSON GRAPH.06.01 e non sostituisce o modifica mai una mappa esistente.

## Contratto del pacchetto

Estensione: `.nodilume-backup`. Il pacchetto ZIP contiene esattamente:

- `manifest.json`: formato, data UTC, identita' sorgente, titolo, revisione,
  versione schema, dimensione e SHA-256 del database;
- `map.sqlite`: snapshot SQLite ottenuto con Online Backup API.

Il manifest e il database devono concordare. Sono rifiutati entry duplicate o
aggiuntive, path annidati, checksum/dimensioni discordanti, schema futuro,
database non integro e metadati differenti.## Creazione e retention

I backup sono scritti in `Maps\Backups`, prima su file temporanei nella stessa
directory e poi promossi atomicamente. Il database temporaneo e' uno snapshot
online consistente anche se il database sorgente usa WAL o riceve altre letture.

La retention e' esplicita: vengono conservati gli ultimi 10 pacchetti per MapId.
La pulizia riguarda soltanto file con nome gestito e manifest valido per quella
mappa; file estranei non vengono cancellati.

## Ripristino

Il ripristino legge il pacchetto in streaming con limiti, verifica SHA-256,
integrita' SQLite, schema e manifest prima della pubblicazione nel catalogo.
La copia ripristinata conserva titolo, revisione, idee, placement, relation e
ogni dato SQLite della versione supportata, ma riceve sempre un nuovo MapId e
un nuovo filename GUID. La sorgente e le mappe esistenti restano immutate.

L'identita' viene aggiornata in una transazione con foreign key differite e
`foreign_key_check`; il file diventa visibile al catalogo soltanto con move
atomico finale.

## UI e concorrenza

La toolbar espone `Crea backup` e `Ripristina backup`. Il primo salva nel
folder gestito; il secondo usa un dialog Windows in sola lettura. Backup,
restore, import/export, cambio mappa, ricerca ed editor si bloccano a vicenda.
Un restore riuscito seleziona e apre la nuova mappa.

## Gate

- test infrastruttura: snapshot, manifest/checksum, rekey, retention e rejection;
- smoke WPF/WebView2 reale: backup, restore, selezione, contenuto e riapertura;
- suite viewer e .NET complete, build Release e `git diff --check`;
- nessun accesso a mappe personali e nessuna modifica a Smart App Control.

Limiti: formato v1 legato allo schema SQLite supportato; nessuna cifratura del
backup, nessun cloud sync e nessuna garanzia di retention fuori dal folder gestito.