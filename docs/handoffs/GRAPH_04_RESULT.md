# GRAPH.04 — risultato e handoff

Data: 18 settembre 2026.
Repository: `Idiopathy-shh/nodilume`.
Branch: `feat/graph-04-selective-loading`.
Worktree: `C:\Sviluppo\Nodilume-graph04`.
Base: `1690c35fcde8f6e51bb17fda7e4f32da8c76d8a2`.
Checkpoint funzionale: `35dc3ca`.

Stato: **PASS tecnico**, pronto per verifica della chat coordinatrice.
Nessun merge eseguito. GRAPH.05 non è stata iniziata.

## Cosa è stato implementato

- Keyset paging tipizzato per figli e pagine Relation; ricerca per prefisso indicizzata.
- UI pagina avanti/indietro per figli oltre il budget, con contesto e selezione preservati.
- Budget indipendenti per nodi, link ed etichette; stato `partial` esplicito.
- Il viewer crea label DOM solo entro `LabelBudget`.
- Migrazione SQLite v2 con indice Relation per pagina ordinata.
- Query figli riscritta per sfruttare `(map_id,parent_id,id)` senza temp B-tree.
- Cache proiezioni LRU bounded e revision-aware, svuotata alla chiusura.
- Strumentazione di bridge, render, frame p95 e selezione.
- Generatore/benchmark deterministico 10k/100k/300k.
- Scale-smoke WPF/WebView2 separato su database sintetico esterno.
- Build script esteso a compile-check di benchmark e scale-smoke.
## Correttezza verificata

I test coprono paging senza skip/duplicati, cursori, ricerca indicizzata, migrazione,
budget, selezione preservata, cache con cursor/revisione, LRU, provenienza Relation
attraverso pagine figli e assenza di doppio contributo alle aggregazioni.

Lo smoke GRAPH.03 continua a passare ed è stato ampliato con un contesto da oltre
128 figli: attraversa realmente la seconda pagina WebView2 e ritorna alla prima prima
di continuare le prove di navigazione trasversale e riapertura persistente.

Le Relation oltre `RelationLimit=256` restano dichiaratamente parziali tramite
`relation-page`; non viene dichiarata completezza globale oltre il budget. Non esiste
in GRAPH.04 una UX separata per sfogliare tutte le pagine Relation.

## Evidenze finali

- `scripts/build.ps1`: PASS, ~13,5 s.
- Viewer: 9/9 PASS.
- Core/SQLite/proiezione: 3/3 gruppi PASS.
- Build: 0 errori, 0 warning.
- Smoke GRAPH.03 WPF/WebView2: PASS, ~14,7 s.
- Scale-smoke 300k: PASS.
- Prima vista utile 300k: 3,763 s.
- Frame p95: 16,8 ms dopo paging ripetuto.
- Selezione p95: 15,2 ms su 9 campioni.
- Ricerca 300k p95: 0,881 ms.
- Memoria WPF+WebView2: nessuna crescita monotona nei 6 campioni.
Il report completo è `docs/benchmarks/graph-04.md`.
La validazione è `docs/validation/graph-04.md`.
I raw data sono sotto `docs/benchmarks/graph-04-data/`.
Screenshot tecnico: `docs/validation/graph-04-300k.png`.

## Bottleneck confutato con misura

Prima della fix query, sul 300k: pagina figli p95 2.271,8 ms e proiezione p95
5.884,5 ms. `EXPLAIN QUERY PLAN` mostrava indice usato solo su `map_id`
e temp B-tree. Dopo il predicato specializzato: figli p95 1,458 ms e proiezione
p95 274,5 ms. La cache è stata aggiunta solo successivamente; cache-hit p95 2,130 ms.

## Metodo/limiti da preservare

“Process-cold” nel report non significa disco freddo. Windows può mantenere il file
nel page cache. I risultati sono specifici dell'hardware documentato.

Non ridurre i dataset o i budget silenziosamente in verifiche future. Se una soglia
viene mancata deve risultare FAIL; una metrica assente è NON MISURATA.
Accettazione visiva manuale e PASS tecnico restano separati.

## Confini per la fase successiva

Non sono stati implementati drag/pin, undo/redo o camera persistente: appartengono
a GRAPH.05. Non sono stati implementati editor completo, AI, libri o cloud.
## Ripresa coordinatrice

1. Verificare branch, diff, raw benchmark e `git diff --check`.
2. Rieseguire almeno `./scripts/build.ps1` e lo smoke GRAPH.03.
3. Per scala: generare o riusare un fixture sintetico 300k e lanciare
   `tests/Nodilume.ScaleSmoke`; non usare database personali.
4. Valutare separatamente lo screenshot/accettazione visiva.
5. Integrare solo dopo autorizzazione dell'utente.
6. Dopo integrazione aggiornare il checkpoint main e soltanto allora preparare GRAPH.05.

La PR viene pubblicata dalla chat implementatrice dopo la creazione di questo handoff;
GitHub resta la fonte autorevole per numero PR e head finale.
