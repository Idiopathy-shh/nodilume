# Nodilume — coordinamento operativo
Checkpoint: 21 settembre 2026. Repository privata: Idiopathy-shh/nodilume.
PC autorizzato per sviluppo e test: OFFICE-PC. GitHub CLI autenticata.

## Stato GitHub verificato
GRAPH.00-04 integrate, main@0f6bdac9d988bb1a6a6c3ddb8b3f71d02bd872f5.
GRAPH.04 PR #5 MERGED; PR documentale #4 CLOSED (contenuti assorbiti).
GRAPH.05 PR #6 OPEN/DRAFT, branch feat/graph-05-editing-viewstate,
worktree C:\Sviluppo\Nodilume-graph05, head 946c2e019d32e157a483d2c68fdddc4e2f001e59.
GRAPH.05: viewer 13/13, .NET 4/4, scale-smoke 300k PASS; smoke reale
Undo/Redo NON PASS, successiva esecuzione bloccata dal criterio SAC 0x800711C7.
Accettazione manuale non conclusa. Non attribuire PASS/merge alla GRAPH.05.
La firma RSA di sviluppo deve ancora arrivare; non modificare protezioni Windows.

## GRAPH.06 senza attesa firma: incarico parallelo autorizzato
GRAPH.06 e' il primo editor personale completo (multi-map, nodi, relazioni,
ricerca, import/export, recupero). GRAPH.06.01 ne sviluppa soltanto il
contratto JSON portabile di Idea/Placement/Relation e il relativo codec:
worktree C:\Sviluppo\Nodilume-graph06-01; branch feat/graph-06-01-portable-map.
Base autonoma main@0f6bdac, NON GRAPH.05; nessuna migrazione SQLite v3
o UX editing copiata dalla candidata GRAPH.05.
Nuove mappe importate con MapId distinto e revisione 0; mai sovrascrivere
database esistenti. Non confondere codec in-memory con backup/restore SQLite.
Dettagli e gate in docs/plans/graph-06-01-portable-map.md e
docs/validation/graph-06-01.md.
## Governance e gate
La GRAPH.06.01 puo' progredire in branch/PR autonomi mentre GRAPH.05 resta Draft.
Non iniziare integrazioni che dipendono da GRAPH.05 finche' non e' validata.
Una eventuale PR di GRAPH.06.01 verso main e' indipendente; la chiusura
della slice NON certifica UI multi-map, import/export file, backup o
l'editor GRAPH.06 completo. I gate futuri verranno pianificati separatamente.
Usare fixture sintetiche e percorsi temporanei; non leggere o alterare mappe
personali, chiavi o token. Nessun reset distruttivo, rebase o force-push.
Registrare risultati di build, test runtime, scale, visione e limiti distinti.
Non disattivare Smart App Control per aggirare un errore di caricamento DLL.
Le altre worktree restano su branch propri e non vanno spostate.
Le Relation oltre RelationLimit restano partial, non globalmente esaustive.
