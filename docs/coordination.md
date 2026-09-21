# Nodilume — coordinamento operativo
Checkpoint: 21 settembre 2026. Repository privata: Idiopathy-shh/nodilume.
PC autorizzato per sviluppo e test: OFFICE-PC. GitHub CLI autenticata.

## Stato GitHub verificato
GRAPH.00-04 integrate; GRAPH.04 PR #5 merge 0f6bdac9d988bb1a6a6c3ddb8b3f71d02bd872f5.
GRAPH.06.01 PR #7 MERGED, squash c515f79af7f326c6846e26acce4955dda6e6e8f6.
PR documentale #4 CLOSED (contenuti assorbiti in GRAPH.04).
GRAPH.05 PR #6 OPEN/DRAFT, branch feat/graph-05-editing-viewstate,
worktree C:\Sviluppo\Nodilume-graph05, head 946c2e019d32e157a483d2c68fdddc4e2f001e59.
GRAPH.05: viewer 13/13, .NET 4/4, scale-smoke 300k PASS; smoke reale
Undo/Redo NON PASS, successiva esecuzione bloccata dal criterio SAC 0x800711C7.
Accettazione manuale non conclusa. Non attribuire PASS/merge alla GRAPH.05.
La firma RSA di sviluppo deve ancora arrivare; non modificare protezioni Windows.

## GRAPH.06 in parallelo: GRAPH.06.01 gia' integrata
GRAPH.06 e' il primo editor personale completo (multi-map, nodi, relazioni,
ricerca, import/export, recupero). GRAPH.06.01 ha integrato SOLO il codec
JSON portabile di Idea/Placement/Relation: PR #7 MERGED in main, dalla
base GRAPH.04, NON GRAPH.05. Worktree storica della slice:
C:\Sviluppo\Nodilume-graph06-01; branch feat/graph-06-01-portable-map.
Nessuna migrazione SQLite v3 o UX editing copiata dalla candidata GRAPH.05.
Nuove mappe importate con MapId distinto e revisione 0; mai sovrascrivere
database esistenti. Non confondere codec in-memory con backup/restore SQLite.
Dettagli e gate in docs/plans/graph-06-01-portable-map.md e
docs/validation/graph-06-01.md.
## Governance e gate
GRAPH.06.01 e' integrata in main senza chiudere GRAPH.05, che resta Draft.
Non iniziare integrazioni che dipendono da GRAPH.05 finche' non e' validata.
Il PASS della slice indipendente NON certifica UI multi-map, import/export
file, backup o l'editor GRAPH.06 completo. Pianificare gate futuri separati.
Usare fixture sintetiche e percorsi temporanei; non leggere o alterare mappe
personali, chiavi o token. Nessun reset distruttivo, rebase o force-push.
Registrare risultati di build, test runtime, scale, visione e limiti distinti.
Non disattivare Smart App Control per aggirare un errore di caricamento DLL.
Le altre worktree restano su branch propri e non vanno spostate.
Le Relation oltre RelationLimit restano partial, non globalmente esaustive.
