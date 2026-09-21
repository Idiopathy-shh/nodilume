# Nodilume — roadmap

Stato 21 settembre 2026: GRAPH.00-04 integrate, main
`0f6bdac9d988bb1a6a6c3ddb8b3f71d02bd872f5` (GRAPH.04 PR #5 merged).
GRAPH.05 PR #6 DRAFT: test .NET 4/4 e scale-smoke 300k PASS; smoke UI completo
Undo/Redo non ancora PASS e nuova DLL smoke bloccata da Smart App Control.
GRAPH.06.01 avviata in parallelo su worktree separata, direttamente da main;
la firma RSA di sviluppo non e' ancora disponibile. GRAPH.07-08 non iniziate.

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
| GRAPH.06.01 | Contratto JSON portabile del grafo | Codec + test sulla worktree separata, non UI/import file |
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
senza attendere il certificato RSA; GRAPH.06.01 e' isolata su
`C:\Sviluppo\Nodilume-graph06-01`, branch `feat/graph-06-01-portable-map`
da main (non da GRAPH.05). Vedere `docs/plans/graph-06-01-portable-map.md`
e `docs/validation/graph-06-01.md`. Le integrazioni fra 05 e 06 necessitano
di gate separati; la slice portabile NON e' un editor completo.

Ogni fase richiede verifiche sui rischi concreti introdotti. Una fase non è conclusa
per il solo fatto che il codice compila.
