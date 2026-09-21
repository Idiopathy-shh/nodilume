# Nodilume — coordinamento operativo
Checkpoint: 21 settembre 2026. Repository privata: Idiopathy-shh/nodilume.
PC autorizzato per sviluppo e test: OFFICE-PC. GitHub CLI autenticata.

## Stato GitHub verificato
GRAPH.00-04 integrate; GRAPH.04 PR #5 merge 0f6bdac9d988bb1a6a6c3ddb8b3f71d02bd872f5.
GRAPH.06.01 PR #7 MERGED, squash c515f79af7f326c6846e26acce4955dda6e6e8f6.
GRAPH.06.02 PR #9 MERGED con squash
a9df2bd15cfde495c120bdf2b754b8052fa5ef62.
Sviluppata e verificata separatamente su OFFICE-PC, branch
feat/graph-06-02-map-manager, worktree C:\Sviluppo\Nodilume-graph06-02,
base main@21e7ea92e28329164763be60d0c172de3a12d7f9.
PR documentale #4 CLOSED (contenuti assorbiti in GRAPH.04).
GRAPH.05 PR #6 OPEN/DRAFT, branch feat/graph-05-editing-viewstate,
worktree C:\Sviluppo\Nodilume-graph05, head 946c2e019d32e157a483d2c68fdddc4e2f001e59.
GRAPH.05: viewer 13/13, .NET 4/4, scale-smoke 300k PASS; smoke reale
Undo/Redo NON PASS, successiva esecuzione bloccata dal criterio SAC 0x800711C7.
Accettazione manuale non conclusa. Non attribuire PASS/merge alla GRAPH.05.
La firma RSA di sviluppo deve ancora arrivare; non modificare protezioni Windows.

## GRAPH.06 in parallelo: GRAPH.06.01 e GRAPH.06.02 integrate
GRAPH.06 e' il primo editor personale completo (multi-map, nodi, relazioni,
ricerca, import/export, recupero). GRAPH.06.01 ha integrato SOLO il codec
JSON portabile di Idea/Placement/Relation: PR #7 MERGED in main, dalla
base GRAPH.04, NON GRAPH.05. Worktree storica della slice:
C:\Sviluppo\Nodilume-graph06-01; branch feat/graph-06-01-portable-map.
Nessuna migrazione SQLite v3 o UX editing copiata dalla candidata GRAPH.05.
Nuove mappe importate con MapId distinto e revisione 0; mai sovrascrivere
database esistenti. Non confondere codec in-memory con backup/restore SQLite.
GRAPH.06.02 aggiunge catalogo/gestione mappe WPF senza recuperare codice
GRAPH.05: demo legacy invariata, nuova mappa vuota in SQLite distinto,
rinomina e selezione persistente. Viewer Node 9/9, .NET 5/5,
smoke WPF GRAPH.03 + GRAPH.06.02 e scale-smoke 300k PASS.
Dettagli e gate in docs/plans/graph-06-01-portable-map.md,
docs/validation/graph-06-01.md, docs/plans/graph-06-02-map-manager.md
e docs/validation/graph-06-02.md.
## Governance e gate
GRAPH.06.01 e GRAPH.06.02 sono integrate in main senza chiudere
GRAPH.05, che resta Draft.
Non iniziare integrazioni che dipendono da GRAPH.05 finche' non e' validata.
Il PASS della 06.01 non certifica la gestione mappe: questa viene verificata
separatamente in GRAPH.06.02. Il PASS della 06.02 NON certifica editor
Idea/Relation, ricerca UI, import/export file, backup/recovery o GRAPH.05.
Pianificare gate futuri separati.
Usare fixture sintetiche e percorsi temporanei; non leggere o alterare mappe
personali, chiavi o token. Nessun reset distruttivo, rebase o force-push.
Registrare risultati di build, test runtime, scale, visione e limiti distinti.
Non disattivare Smart App Control per aggirare un errore di caricamento DLL.
Le altre worktree restano su branch propri e non vanno spostate.
Le Relation oltre RelationLimit restano partial, non globalmente esaustive.
