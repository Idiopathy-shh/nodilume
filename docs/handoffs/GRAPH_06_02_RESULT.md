# GRAPH.06.02 — handoff implementazione e integrazione
Data: 21 settembre 2026; repository Idiopathy-shh/nodilume.
PC: OFFICE-PC; worktree C:\Sviluppo\Nodilume-graph06-02;
branch feat/graph-06-02-map-manager, base main@21e7ea92e28329164763be60d0c172de3a12d7f9.

## Consegna
Gestione WPF della mappa attiva (elenco, creazione vuota, rinomina e switch),
catalogo locale delle mappe SQLite e persistenza della scelta attiva.
La demo storica resta utilizzabile. Cambi mappa azzerano stato del renderer
e richieste obsolete; nuovi file per mappe GUID.sqlite in cartella catalogo.
La ricerca e il contenuto grafico non sono un editor completo.

## Verifiche
./scripts/build.ps1 PASS: viewer 9/9, .NET 5/5, build desktop/smoke/scale/
benchmark, 0 warning, 0 errori.
tests/Nodilume.Smoke PASS: GRAPH.03 regressione e GRAPH.06.02 create/
rename/switch/reopen/isolamento in WPF/WebView2 reale.
tests/Nodilume.ScaleSmoke su fixture sintetica 300k PASS:
C:\Temp\nodilume-graph06-02-scale-20260921-final.json.
Riesecuzione finale: prima vista utile 3756.8415 ms; frame p95 16.8 ms,
selezione p95 15.3 ms.
Evidenze, comandi e limiti: docs/validation/graph-06-02.md.

## Coordinamento
GRAPH.05 PR #6 resta DRAFT separata (runtime Undo/Redo non validato,
DLL smoke bloccata da SAC); questa slice non contiene la migrazione SQLite v3
e non autorizza a dichiarare GRAPH.05 PASS.
GRAPH.06.03 dovra' introdurre l'editor dei contenuti e integrare in seguito
le politiche Undo/Redo di GRAPH.05 dopo accettazione della sua patch.
Non trasferire file/db personali nei test e non modificare Smart App Control.
