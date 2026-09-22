# Nodilume — roadmap

Stato 22 settembre 2026: GRAPH.00-04 integrate (GRAPH.04 PR #5);
GRAPH.06.01 PR #7 integrata con squash c515f79af7f326c6846e26acce4955dda6e6e8f6.
GRAPH.06.02 PR #9 integrata con squash a9df2bd15cfde495c120bdf2b754b8052fa5ef62:
gestione multi-mappa WPF/SQLite verificata su OFFICE-PC.
GRAPH.06.03 PR #11 integrata con squash
9478973e7a5be33c5487499e6e3ab560c342267e, indipendente da GRAPH.05:
editor di Idea/rappresentazioni, note locali e creazione gerarchica WPF;
viewer 9/9, .NET 6/6, smoke WPF/WebView2 e scale-smoke 300k PASS.
GRAPH.06.04 PR #13 integrata con squash
550ce57916d274b12606ce60f97f33a7d9f10d7a: editor Relation;
viewer 9/9, .NET 7/7 e smoke WPF/WebView2 PASS su OFFICE-PC.
GRAPH.05 PR #6 resta DRAFT: test .NET 4/4 e scale-smoke 300k PASS;
smoke UI Undo/Redo non ancora PASS e DLL smoke bloccata da Smart App Control.
La firma RSA di sviluppo non e' ancora disponibile. GRAPH.07-08 non iniziate.

## Roadmap

| Fase | Consegna | Stato / criterio di uscita |
|---|---|---|
| GRAPH.00 | Repository dedicata, specifica, verifica ambiente e convenzioni | Completata |
| GRAPH.01 | Finestra Windows e scena 3D locale | Completata e accettata |
| GRAPH.02 | Identità, rappresentazioni, contenimento e SQLite | Integrata tramite PR #2 |
| GRAPH.03 | Zoom semantico continuo e collegamenti aggregati | Integrata PR #3; PASS tecnico e smoke confermati |
| GRAPH.04 | Caricamento selettivo e prove di scala | PR #5 integrata; limiti del benchmark invariati |
| GRAPH.05 | Spostamento, fissaggio, undo e ripristino | PR #6 Draft; smoke UI e accettazione mancanti |
| GRAPH.06 | Primo editor personale completo | Avviato a slice; editor completo non implementato |
| GRAPH.06.01 | Contratto JSON portabile del grafo | PR #7 integrata; codec/test PASS, non UI/import file |
| GRAPH.06.02 | Gestione mappe personali | PR #9 integrata; elenco, creazione, selezione e rinomina WPF + SQLite; smoke PASS |
| GRAPH.06.03 | Editor idee e nodi | PR #11 integrata; crea radici/figli, contenuti condivisi, note locali, rappresentazioni multiple; smoke PASS |
| GRAPH.06.04 | Editor relazioni | PR #13 integrata; CRUD Relation revisionato, estremi dalla scena e doppia conferma delete; test/smoke PASS |
| GRAPH.06.05+ | Ricerca UI, import/export file, backup/recovery, integrazione | Da pianificare e implementare; fuori scope 06.04 |
| GRAPH.07 | Automazioni AI | Non implementata |
| GRAPH.08 | Modalità libri | Non implementata |

## GRAPH.03 — checkpoint tecnico

Completati: contratti di proiezione contestuale, query limitate con antenati indipendenti
dalla pagina, frame locali, aggregazione Relation con conteggio/provenienza, soppressione
dei falsi self-link, direzioni opposte distinte, destinazioni multiple esplicite,
navigazione trasversale/ritorno, protocollo v2 requestId/map/revision/context,
cancellazione/risposte obsolete, transizioni continue, isteresi, riframing camera,
stati partial/leaf/empty/error e smoke Windows su fixture dedicata.

Il merge è stato richiesto espressamente dall’utente. Non si dichiara retroattivamente
eseguito ogni passo del playbook visivo.

## GRAPH.04 — checkpoint tecnico

Completati: keyset paging figli, budget indipendenti nodi/link/etichette, stato
`partial` esplicito, cache LRU bounded/revision-aware, ricerca indicizzata, migrazione
SQLite v2, scale-smoke Windows e generatore/benchmark deterministico 10k/100k/300k.
Sul fixture 300k: prima vista utile 3,76 s, frame p95 16,8 ms dopo paging ripetuto,
selezione p95 15,2 ms e ricerca backend p95 0,881 ms. La memoria WPF+WebView2 non
mostra crescita monotona nei campioni registrati.

Le Relation oltre il budget `RelationLimit` restano dichiaratamente parziali; GRAPH.04
non introduce una UX separata per sfogliare tutte le pagine Relation. L'accettazione
visiva manuale resta distinta dal PASS tecnico. Dettagli in
`docs/validation/graph-04.md` e `docs/benchmarks/graph-04.md`.

GRAPH.05 resta DRAFT e non puo' ricevere PASS complessivo dalla sola compilazione,
dalla suite .NET o dallo scale-smoke. Lo smoke WPF/WebView2 e l'accettazione
manuale restano gate autonomi. L'utente autorizza GRAPH.06 in parallelo
senza attendere il certificato RSA: GRAPH.06.01 e' stata sviluppata sulla
worktree separata `C:\Sviluppo\Nodilume-graph06-01`, da main (non da GRAPH.05),
quindi integrata tramite PR #7. Vedere `docs/plans/graph-06-01-portable-map.md`
e `docs/validation/graph-06-01.md`. GRAPH.06.02 e' stata avviata separatamente
da main in `C:\Sviluppo\Nodilume-graph06-02` e integrata con PR #9:
elenco/creazione/rinomina/passaggio mappe persistenti (vedere
`docs/plans/graph-06-02-map-manager.md` e `docs/validation/graph-06-02.md`). Le integrazioni fra 05 e 06 necessitano
di gate separati. GRAPH.06.03 e' stata sviluppata in
`C:\Sviluppo\Nodilume-graph06-03` da main, senza incorporare GRAPH.05,
e integrata tramite PR #11: vedere `docs/plans/graph-06-03-idea-node-editor.md`
e `docs/validation/graph-06-03.md`. GRAPH.06.04 e' sviluppata separatamente
in `C:\Sviluppo\Nodilume-graph06-04`: editor Relation revisionato, accessi
SQLite mirati e smoke reale; vedere `docs/plans/graph-06-04-relation-editor.md`
e `docs/validation/graph-06-04.md`. Ricerca, file import/export,
backup/recovery e integrazione GRAPH.05 restano patch distinte.

Ogni fase richiede verifiche sui rischi concreti introdotti. Una fase non è conclusa
per il solo fatto che il codice compila.
