# GRAPH.04 — benchmark 10k / 100k / 300k

Data: 18 settembre 2026.
Base: `main@1690c35fcde8f6e51bb17fda7e4f32da8c76d8a2`.
Checkpoint funzionale misurato: `35dc3ca`.
Seed sintetico: `20260918`.

## Obiettivo

Misurare caricamento selettivo, query SQLite, costruzione proiezione, serializzazione/bridge,
rendering WebView2, selezione, ricerca e memoria senza richiedere il caricamento globale
della mappa. Correttezza, prestazioni e accettazione visiva sono valutate separatamente.

Gli obiettivi proposti dal bootstrap sono: frame p95 <= 33 ms; selezione residente
p95 <= 100 ms; ricerca p95 <= 1 s; prima vista utile 300k <= 5 s; memoria senza crescita
monotona nei percorsi ripetuti.

## Ambiente

| Voce | Valore |
|---|---|
| CPU | AMD Ryzen 9 5950X, 16 core / 32 thread |
| RAM | 34.233.339.904 byte (31,88 GiB) |
| GPU | Radeon RX 460, ~4 GB, driver 31.0.21925.1001 |
| Display | 1920x1080; finestra scale-smoke 1280x820 |
| OS | Windows 11 Pro 10.0.26200 build 26200 |
| .NET | SDK 10.0.400; runtime benchmark 10.0.11 |
| Node | v24.19.0 |
| WebView2 | 153.0.4234.32 |
## Dataset

Il generatore `tools/Nodilume.Benchmarks` crea database SQLite deterministici separati
dalla demo. Mantiene quattro profili nello stesso dataset: hub con molti figli, catena
profonda 60 livelli, rete sparsa e Idea con Placement multipli. La densità delle Relation
resta 1:5 rispetto alle Idea; non viene ridotta al crescere del dataset.

| Idea | Placement | Relation | Profondità max | Grado max | DB |
|---:|---:|---:|---:|---:|---:|
| 10.000 | 10.005 | 2.000 | 60 | 506 | 10.821.632 B |
| 100.000 | 100.050 | 20.000 | 60 | 5.051 | 104.800.256 B |
| 300.000 | 300.150 | 60.000 | 60 | 5.151 | 311.635.968 B |

Budget applicativi misurati: Root 32, Child 128, Node 160, Link 256, Label 64,
Relation 256, Destination Placement 512, profondità antenati 64. Un budget esaurito
produce stato `partial` e motivi espliciti; non viene presentato come risultato completo.

## Metodo

Ogni misura backend viene eseguita in un nuovo processo. “Process-cold” significa processo
nuovo e inizializzazione SQLite nuova; non significa cache disco/OS fisicamente fredda.
Dopo un warm-up esplicito vengono raccolte 9 ripetizioni per query figli, pagina Relation,
ricerca indicizzata, proiezione non cached, cache-hit e serializzazione; si riportano
mediana e p95 e i campioni grezzi restano nei JSON versionati.

Lo scale-smoke usa un processo WPF nuovo e un profilo WebView2 temporaneo. Apre il DB
300k, attende la prima scena pronta, misura 210 frame stabilizzati, 9 selezioni e
8 cicli pagina avanti/indietro. La memoria somma il processo WPF e tutti i processi
WebView2 associati all'environment tramite `GetProcessInfos()`.
Il tempo bridge viene misurato dal timestamp C# immediatamente prima della
serializzazione/dispatch fino alla ricezione del messaggio accettato nel viewer.
Il tempo viewer-render va dalla ricezione alla fine della transizione e include
deliberatamente la transizione visiva di 650 ms. Il frame p95 usa solo frame nello
stato `ready`, fuori dalle transizioni.

La ricerca usa un intervallo di prefisso indicizzabile
(`title >= prefix AND title < prefix + U+FFFF`) sull'indice `ix_ideas_title`.
La paginazione figli usa keyset cursor su `id` e l'indice
`ix_placements_parent(map_id,parent_id,id)`.

## Risultati backend finali

| Dataset | process-cold backend | figli p95 | Relation p95 | ricerca p95 | proiezione p95 | cache-hit p95 | serializzazione p95 |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 10k | 330,0 ms | 1,217 ms | 1,559 ms | 1,065 ms | 220,5 ms | 2,126 ms | 6,433 ms |
| 100k | 392,1 ms | 1,278 ms | 1,717 ms | 0,901 ms | 271,5 ms | 2,234 ms | 3,321 ms |
| 300k | 389,9 ms | 1,458 ms | 5,565 ms | 0,881 ms | 274,5 ms | 2,130 ms | 2,694 ms |

Il working set del processo benchmark è rimasto nell'ordine di 77–81 MB nelle tre prove.
La ricerca resta tre ordini di grandezza sotto l'obiettivo di 1 secondo.
## Scale-smoke WPF/WebView2 300k

| Metrica | Risultato | Obiettivo | Esito |
|---|---:|---:|---|
| Prima vista utile end-to-end | 3.763,5 ms | <= 5.000 ms | PASS |
| Viewer start -> prima scena utile | 1.085,5 ms | informativa | MISURATA |
| Bridge iniziale | 45,0 ms | informativa | MISURATA |
| Viewer apply + transizione | 661,3 ms | informativa | MISURATA |
| Frame p95 stabilizzato iniziale | 16,7 ms | <= 33 ms | PASS |
| Frame p95 dopo paging ripetuto | 16,8 ms | <= 33 ms | PASS |
| Selezione p95, 9 campioni | 15,2 ms | <= 100 ms | PASS |
| Memoria, range 6 campioni | 10.813.440 B | no crescita monotona | PASS |

Campioni memoria: 604.712.960; 612.667.392; 613.310.464; 613.470.208;
615.526.400; 614.551.552 byte. La sequenza non è monotona e l'ultimo campione
scende rispetto al penultimo. Il JSON contiene i 210 campioni frame e i 9 campioni
di selezione.

Lo screenshot dopo i cicli di paging è in
`docs/validation/graph-04-300k.png`. È evidenza di esecuzione reale, non sostituisce
l'accettazione visiva manuale dell'utente.
## Collo di bottiglia trovato e corretto

La baseline 300k pre-fix mostrava un FAIL: query figli p95 2.271,8 ms e proiezione
p95 5.884,5 ms. `EXPLAIN QUERY PLAN` indicava che la clausola con `OR` usava
`ix_placements_parent` solo su `map_id` e costruiva un temp B-tree per
`ORDER BY`.

Prima:
`SEARCH placements USING COVERING INDEX ix_placements_parent (map_id=?)`
+ `USE TEMP B-TREE FOR ORDER BY`.

Dopo aver generato predicati separati per parent/cursor:
`SEARCH placements USING COVERING INDEX ix_placements_parent (map_id=? AND parent_id=?)`.

Sul medesimo DB 300k la query figli scende a 1,458 ms p95 e la proiezione a
274,5 ms p95. La cache non viene usata per nascondere il problema: è stata introdotta
solo dopo questa correzione.

## Cache

`SceneProjectionCache` è LRU, massimo 16 entry e 8 MiB stimati. La chiave comprende
mapId, revisione, contesto, cursor, selezione preservata e limiti. Cambio mappa/revisione
rimuove entry incompatibili; la finestra la svuota alla chiusura. Nel 300k una entry
misurata è ~573 KiB stimati; 9 hit su 9 dopo il warm-up portano la proiezione identica
da 246,7 ms mediana a 0,658 ms mediana.
## Limiti dichiarati

Il renderer non mostra 300.000 nodi insieme: la prova certifica esplorazione progressiva
di una mappa di quella dimensione con budget fissi. I figli oltre la prima pagina sono
raggiungibili con controlli pagina e keyset cursor; contesto, selezione e ritorno restano
coerenti.

Le Relation sono anch'esse limitate. Se `RelationLimit=256` viene esaurito la
proiezione dichiara `relation-page` e resta `partial`; non viene dichiarata
un'aggregazione globale esaustiva oltre quel budget. I test verificano che il paging
dei figli non perda o duplichi la provenienza Relation già inclusa e che una Relation
contribuisca al massimo una volta a ciascuna aggregazione. Un'eventuale UX per sfogliare
esplicitamente tutte le pagine Relation non è stata introdotta in GRAPH.04.

“Cold” non equivale a cache disco fredda. La prova di apertura WPF usa processo e profilo
WebView2 nuovi, ma Windows può avere pagine del file in cache. Le metriche valgono per
l'hardware e i runtime sopra indicati; non sono una promessa universale.

## Riproduzione

```powershell
dotnet run --project tools/Nodilume.Benchmarks -c Release -- generate --size 10000 --seed 20260918 --out C:\Temp\graph04-10000.sqlite
dotnet run --project tools/Nodilume.Benchmarks -c Release -- generate --size 100000 --seed 20260918 --out C:\Temp\graph04-100000.sqlite
dotnet run --project tools/Nodilume.Benchmarks -c Release -- generate --size 300000 --seed 20260918 --out C:\Temp\graph04-300000.sqlite
```
``powershell
dotnet run --project tools/Nodilume.Benchmarks -c Release --no-build -- measure --db C:\Temp\graph04-300000.sqlite --iterations 9 --out C:\Temp\measure-300000.json
dotnet run --project tests/Nodilume.ScaleSmoke -c Release --no-build -- C:\Temp\graph04-300000.sqlite C:\Temp\scale-300000.json
```

I database sintetici restano fuori da git. I raw result versionati sono:
`docs/benchmarks/graph-04-data/measure-10000-final.json`,
`measure-100000-final.json`, `measure-300000-final-backend.json`,
`scale-300000-final.json` e la baseline diagnostica
`measure-300000-before-query-fix.json`.
