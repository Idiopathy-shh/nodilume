# GRAPH.06.03 — handoff operativo editor Idee/Placement
Data: 21/09/2026; repository Idiopathy-shh/nodilume; OFFICE-PC.
Base main@06d92e744e84dbaa4cd8b704ec291096ad3208ab.
Branch feat/graph-06-03-idea-node-editor;
worktree C:\Sviluppo\Nodilume-graph06-03.

## Consegna
Crea Idea radice/figlia e rappresentazioni multiple in una mappa personale;
modifica titolo e contenuto condivisi di un'Idea e nota locale del nodo.
Pannello WPF integrato con selezione scena WebView2 e refresh del grafo,
cache invalidata, revisioni SQLite ottimistiche e verifica mapId.
Non usa letture globali del grafo per editing interattivo. Nessuna
migrazione SQLite v3, undo/redo o modifica al branch GRAPH.05.

## Test e limiti
Build completa PASS: viewer 9/9, .NET 6/6, 0 warning/errori.
WPF/WebView2 smoke GRAPH.03+GRAPH.06.02+GRAPH.06.03 PASS,
exit code 0, verificati nuovi contenuti, note e riapertura su DB sintetici.
Scale-smoke 300k PASS sulla visualizzazione (non editing 300k):
C:\Temp\nodilume-graph06-03-scale-20260921.json,
prima vista utile 4156.1699 ms; frame p95 16.7 ms,
selezione p95 14.0 ms. Vedere docs/validation/graph-06-03.md
e docs/plans/graph-06-03-idea-node-editor.md.
Accettazione manuale non dichiarata, nessun database personale letto.
Non confondere il PASS della 06.03 con smoke e accettazione della
GRAPH.05 PR #6, ancora aperta in Draft (blocco SAC).
GRAPH.06.04+: editor relazioni, ricerca, file import/export,
backup/recovery e integrazione successiva da pianificare.
