# GRAPH.04 — validazione

Data: 18 settembre 2026.
Base: `1690c35fcde8f6e51bb17fda7e4f32da8c76d8a2`.
Checkpoint funzionale: `35dc3ca`.

## Esito

**PASS tecnico GRAPH.04** per caricamento selettivo, paging figli, budget separati,
cache bounded/revision-aware, ricerca indicizzata e prove 10k/100k/300k sull'hardware
documentato. L'accettazione visiva manuale dell'utente resta distinta e non viene
dichiarata implicitamente da questo PASS.

## Verifiche finali

| Verifica | Esito |
|---|---|
| `scripts/build.ps1` | PASS, ~13,5 s |
| Viewer/navigation | 9/9 PASS |
| Core + SQLite + semantic projection | 3/3 gruppi PASS |
| Build .NET | 0 errori, 0 warning |
| Smoke GRAPH.03 WPF/WebView2 | PASS, ~14,7 s |
| Scale-smoke 300k WPF/WebView2 | PASS |
| `git diff --check` | PASS prima del commit funzionale |
| Demo/database personali | non usati né sovrascritti |

`build.ps1` ora compila anche `Nodilume.ScaleSmoke` e
`Nodilume.Benchmarks` con restore locked.
## Correttezza nuova coperta

- `ReadChildrenPageAsync`: keyset cursor stabile, nessun salto/duplicato su dati stabili.
- I figli oltre la prima pagina sono raggiungibili dalla UI con Avanti/Indietro.
- La selezione di una pagina precedente può essere preservata durante il cambio pagina.
- Il percorso antenati resta indipendente dalla pagina figli: nessuna falsa radice.
- Budget nodi, archi ed etichette sono indipendenti e producono motivi `partial` espliciti.
- Le etichette oltre budget non creano elementi DOM nascosti inutilmente.
- La provenienza Relation resta stabile attraverso il paging figli; nessuna Relation
  inclusa contribuisce due volte allo stesso insieme di link aggregati.
- Rappresentazioni multiple e destinazioni ambigue di GRAPH.03 restano esplicite.
- Foglia, empty, partial ed error restano distinti.
- La cancellazione Desktop e il filtro viewer per risposte obsolete restano invariati.
- La cache distingue mapId, revisione, contesto, cursor, selezione e limiti.
- LRU e budget memoria cache sono verificati; cambio revisione invalida le entry.
- La ricerca per prefisso usa l'indice anche su Idea non presenti nel renderer.

La migrazione SQLite passa da schema v1 a v2 e aggiunge
`ix_relations_map_id(map_id,id)`; l'inizializzazione resta idempotente e gli schemi
futuri continuano a fallire chiuso.
## Smoke funzionale

Lo smoke storico GRAPH.03 è stato mantenuto e ampliato con 140 figli aggiuntivi nel
contesto profondo. La prova entra in tre contesti, seleziona una foglia, avanza alla
pagina successiva, verifica che la selezione resti visibile, torna alla prima pagina
e continua con destinazioni multiple, relazione trasversale, ritorno, uscita,
panoramica, resize e riapertura persistente.

Questo evita un falso PASS in cui il paging funziona in SQLite ma non attraverso
Desktop/WebView2.

## Scale-smoke 300k

Fixture: 300.000 Idea, 300.150 Placement, 60.000 Relation, profondità 60,
grado massimo 5.151, seed 20260918.

Risultati finali: prima vista utile 3.763,5 ms; bridge iniziale 45,0 ms;
viewer apply+transizione 661,3 ms; frame p95 16,7 ms iniziale e 16,8 ms dopo paging;
selezione p95 15,2 ms su 9 campioni. Sei campioni memoria dell'intero albero
WPF/WebView2 hanno range 10.813.440 byte e non mostrano crescita monotona.

La schermata reale catturata dopo i cicli è
`docs/validation/graph-04-300k.png`.
## Benchmark e regressione prestazionale

Il primo benchmark 300k ha trovato un problema reale nella query figli:
p95 2.271,8 ms e proiezione p95 5.884,5 ms. Il query plan mostrava scansione
dell'indice solo su `map_id` e temp B-tree per l'ordinamento.

Dopo la rimozione dell'`OR` parametrico dal predicato e la generazione di query
specifiche parent/root + cursor, il medesimo dataset misura figli p95 1,458 ms
e proiezione p95 274,5 ms. La cache è stata aggiunta solo dopo questa correzione.

La cache-hit 300k misura p95 2,130 ms contro 274,5 ms non cached. La ricerca
indicizzata misura p95 0,881 ms, ampiamente sotto l'obiettivo di 1 secondo.

Dettagli, raw data, metodo e limiti: `docs/benchmarks/graph-04.md`.

## Risultati parziali e limite Relation

Una proiezione che supera `RelationLimit=256` dichiara `relation-page` e stato
`partial`. GRAPH.04 non presenta come esaustivo un conteggio Relation oltre quel
budget. Il paging progressivo aggiunto alla UI riguarda i figli; non è stata introdotta
una UX separata per sfogliare tutte le pagine Relation.

Questa è una limitazione dichiarata, non un errore nascosto. Le Relation effettivamente
incluse mantengono identità/provenienza e non vengono duplicate fra pagine figli/cache.
## Accettazione visiva

La verifica automatica dimostra finestra WPF reale, canvas WebGL, paging, selezione,
frame time, resize e screenshot. Non può sostituire il giudizio visivo umano su densità,
leggibilità e qualità dell'interazione. Lo screenshot 300k viene quindi conservato
come evidenza tecnica; l'eventuale approvazione estetica/percettiva dell'utente resta
un checkpoint separato.

## Fuori scope confermato

GRAPH.04 non introduce drag/pin interattivo, undo/redo, camera persistente,
gestione completa delle mappe, editor completo, AI, libri, cloud o collaborazione.
Non scrive coordinate persistenti durante la navigazione e non cambia la gerarchia
persistente per semplificare il rendering.
