# GRAPH.06.05 — validazione ricerca UI

Data: 22 settembre 2026. Macchina: OFFICE-PC.
Base: main@2f3eb05d654ea3846400c514d7e5cbca4acc0179.
Fixture: database SQLite e profili WebView2 temporanei; nessuna mappa personale letta.

## Esiti

| Verifica | Esito |
|---|---|
| Viewer Node tests | PASS 10/10 |
| TypeScript e bundle | PASS |
| Build Nodilume.Tests Release | PASS, 0 warning/errori |
| Suite .NET | PASS 8/8 gruppi, incluso Map search |
| Build Desktop/Smoke/ScaleSmoke/Benchmarks | PASS, 0 warning/errori |
| Smoke WPF/WebView2 | PASS, exit code 0 |
| Benchmark backend 300k | PASS |
| Scale-smoke WPF/WebView2 300k | PASS |
| git diff --check | PASS |
| Accettazione manuale | Non dichiarata |

Comandi principali:

    ./scripts/build.ps1
    dotnet run --project tests/Nodilume.Smoke -c Release --no-build
## Copertura concreta

La suite Map search usa SQLite reale e verifica ordine/paginazione keyset,
normalizzazione, case sensitivity, percorsi completi e troncati, due Placement
della stessa Idea, limite globale delle rappresentazioni e input/limiti rifiutati.

Lo smoke cerca dalla sidebar, verifica che una Idea condivisa produca due scelte
esplicite con percorso e ID breve, mostra l'anteprima e apre il Placement esatto
nel contesto genitore. Copre input vuoto senza crash, sincronizzazione editor,
ricerca della radice, reset al cambio mappa e assenza di risultati cross-map.
Riesegue inoltre le regressioni GRAPH.03/06.02/06.03/06.04.

## Scala 300k

Fixture deterministica: 300.000 Idea, 300.150 Placement, 60.000 Relation,
311.635.968 byte. Ricerca indicizzata backend p95 0,9661 ms su 9 iterazioni.
Prima vista utile WPF 4.213,3 ms; frame p95 finale 16,8 ms; selezione p95
14,8 ms. I campioni memoria non mostrano crescita monotona.

## Limiti

La ricerca è per prefisso ordinale case-sensitive, non fuzzy o full-text.
Ogni pagina contiene al massimo 20 Idea, 64 Placement complessivi e percorsi
fino a 128 antenati; i tagli sono dichiarati ma le rappresentazioni non hanno
una paginazione separata. Un comando di apertura viene ignorato se il viewer
sta già elaborando un'altra richiesta. Il PASS tecnico non sostituisce
l'accettazione visiva manuale e non certifica GRAPH.05, import/export file
o backup/recovery.
