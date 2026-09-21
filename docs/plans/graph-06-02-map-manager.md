# GRAPH.06.02 — gestione mappe personali
Data: 21/09/2026; OFFICE-PC; base main@21e7ea92e28329164763be60d0c172de3a12d7f9.
Branch feat/graph-06-02-map-manager; worktree C:\Sviluppo\Nodilume-graph06-02.
GRAPH.05 PR #6 e' DRAFT; nessuna sua modifica migrazione v3 o undo/redo nella slice.

## Scope
Nell'app WPF: elenco mappe, creazione di mappa vuota, scelta/apertura,
rinomina e passaggio da una mappa all'altra, con selezione persistente al
riavvio. Ogni mappa ha un proprio SQLite; la demo legacy rimane compatibile,
mai migrata/spostata/cancellata implicitamente. Scelta esplicita; nessuna
cancellazione, duplicazione, import file, editor Idea/Relation o cronologia.

## Contratti e integrita'
Catalogo limitato alla directory Maps configurata (test con dir temporanea).
Solo demo.sqlite preesistente e file <GUID>.sqlite: niente scansione di
documenti o credenziali. Entrate legate a MapId/real path letti dal DB.
Nuova mappa: identificatore casuale, nome validato e revisione iniziale 0,
transazione SQLite; non seminare con Idea demo. Rinomina: metadato map,
con controllo revisione/stale, senza reimpostare Idea/Placement/Relation.
Cambio: annullare richieste proiezione vecchie, svuotare cache per non
riciclare risultati, azzerare contesto, reinizializzare il viewer in modo
da scartare state e risposte della vecchia mappa. Non usare input come path.
Conservare selezione in file metadati atomico interno al catalogo.

## Gates
Test catalogo su fixture sintetiche: demo legacy, creazione, elenco,
isolamento DB, titoli Unicode/invalidi, rinomina/stale/no-op,
switch e selezione dopo riavvio, nessun overwrite DB esistente o
scansione file estranei, recupero quando la mappa selezionata manca.
Smoke WPF/WebView2 reale: lista, crea, apri, rinomina, switch,
ritorno alla mappa precedente e riapertura, nessun dato incrociato.
Build completa e regressioni viewer/Core/SQLite/GRAPH.03; non dichiarare
GRAPH.05 PASS per i test di questa slice. Nessuna modifica a Smart App Control.
