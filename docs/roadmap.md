# Nodilume — roadmap

Stato 21 settembre 2026: GRAPH.00-04 integrate. GRAPH.04 PR #5 merged, main
`0f6bdac9d988bb1a6a6c3ddb8b3f71d02bd872f5`. GRAPH.05 candidata: compilazione
viewer 13/13 e suite .NET 4/4 PASS; scale-smoke 300k PASS, ma smoke UI
Undo/Redo incompleto e nuova DLL bloccata da SAC 0x800711C7.
GRAPH.06.01 avviata in parallelo da main su worktree separata; GRAPH.07-08 non iniziate.

## Roadmap

| Fase | Consegna | Stato / criterio di uscita |
|---|---|---|
| GRAPH.00 | Repository dedicata, specifica, verifica ambiente e convenzioni | Completata |
| GRAPH.01 | Finestra Windows e scena 3D locale | Completata e accettata |
| GRAPH.02 | Identità, rappresentazioni, contenimento e SQLite | Integrata tramite PR #2 |
| GRAPH.03 | Zoom semantico continuo e collegamenti aggregati | Integrata PR #3; PASS tecnico e smoke confermati |
| GRAPH.04 | Caricamento selettivo e prove di scala | Integrata tramite PR #5; limiti del report invariati |
| GRAPH.05 | Spostamento, fissaggio, undo e ripristino | Candidata; runtime bloccato, non PASS complessivo |
| GRAPH.06 | Primo editor personale completo | In corso a slice; editor completo NON implementato |
| GRAPH.06.01 | Codec JSON portabile del grafo | Implementato/testato sul branch indipendente; non UI/import file |
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

GRAPH.05 resta Draft: lo smoke UI e l'accettazione richiedono prove separate.
L'utente autorizza GRAPH.06.01 in parallelo, senza attendere la firma RSA:
worktree C:\Sviluppo\Nodilume-graph06-01 da main, non da GRAPH.05.
Il lavoro sul codec portabile non costituisce completamento di GRAPH.06.

Ogni fase richiede verifiche sui rischi concreti introdotti. Una fase non è conclusa
per il solo fatto che il codice compila.
