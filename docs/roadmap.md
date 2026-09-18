# Nodilume — roadmap

Stato 18 settembre 2026: GRAPH.00-03 integrate. GRAPH.03 PR #3 squash su main
`1690c35fcde8f6e51bb17fda7e4f32da8c76d8a2`. GRAPH.04 è implementata sul branch
`feat/graph-04-selective-loading` e ha PASS tecnico su build, smoke GRAPH.03,
scale-smoke WPF/WebView2 e benchmark 10k/100k/300k. PR #5 è pubblicata come draft;
restano verifica della coordinatrice, accettazione visiva separata e integrazione.
GRAPH.05-08 non iniziate.

## Roadmap

| Fase | Consegna | Stato / criterio di uscita |
|---|---|---|
| GRAPH.00 | Repository dedicata, specifica, verifica ambiente e convenzioni | Completata |
| GRAPH.01 | Finestra Windows e scena 3D locale | Completata e accettata |
| GRAPH.02 | Identità, rappresentazioni, contenimento e SQLite | Integrata tramite PR #2 |
| GRAPH.03 | Zoom semantico continuo e collegamenti aggregati | Integrata PR #3; PASS tecnico e smoke confermati |
| GRAPH.04 | Caricamento selettivo e prove di scala | Implementata e PASS tecnico sul branch dedicato; PR/verifica coordinatrice e integrazione ancora pendenti |
| GRAPH.05 | Spostamento, fissaggio, undo e ripristino | Non implementata |
| GRAPH.06 | Primo editor personale completo | Non implementata |
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

GRAPH.05 non deve iniziare prima della verifica/infrastruttura di integrazione GRAPH.04.

Ogni fase richiede verifiche sui rischi concreti introdotti. Una fase non è conclusa
per il solo fatto che il codice compila.
