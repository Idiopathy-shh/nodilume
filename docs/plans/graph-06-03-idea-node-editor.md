# GRAPH.06.03 — editor idee, rappresentazioni e annotazioni
21/09/2026, OFFICE-PC; branch feat/graph-06-03-idea-node-editor,
worktree C:\Sviluppo\Nodilume-graph06-03,
base main@06d92e744e84dbaa4cd8b704ec291096ad3208ab.

## Ambito
Editor WPF integrato con il grafo 3D: selezione di un Placement visibile
comunicata con messaggio versionato, vista/edit del titolo e contenuto
dell'Idea condivisa, annotazione locale del Placement, creazione di
Idea con Placement radice o figlio della selezione, nuova rappresentazione
della stessa Idea nel contesto corrente, senza crearne una copia.
Nessun operatore modifica un'altra mappa; scritture SQLite atomiche,
revisione ottimistica verificata, refresh della proiezione dopo commit.
Nessuna dipendenza da GRAPH.05, migrazione v3 o undo/redo.
Gestione coordinate iniziali bounded e deterministica; la progettazione
del layout drag/pin resta GRAPH.05.

## Test e sicurezza dati
Fixture temporanee; 2 mappe diverse, Idea condivisa in 2 Placement,
contenuti condivisi / annotazioni locali indipendenti, ciclo antenato
e riferimenti incrociati rifiutati, revisione stale e rollback,
contesto corrente e switch mappa; UI WPF/WebView2 e riapertura.
Non eseguire scansione full graph nei metodi interattivi: accesso mirato
a Idea/Placement, anche per una mappa 300k; regressioni build, viewer,
smoke, scale-smoke. Il click UI non deve essere considerato un PASS
senza una verifica reale del risultato persistito.
Limitare titolo/contenuto/annotazione in dimensione; nessuna cancellazione
automatica di Idea/Placement o relazioni in questo scope.
Vedere docs/validation/graph-06-03.md per risultati.
