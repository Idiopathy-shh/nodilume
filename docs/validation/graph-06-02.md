# GRAPH.06.02 — gestione delle mappe personali, validazione OFFICE-PC

Data: 21/09/2026. Base main@21e7ea92e28329164763be60d0c172de3a12d7f9;
branch feat/graph-06-02-map-manager; worktree C:\Sviluppo\Nodilume-graph06-02.
PR #9 MERGED, squash a9df2bd15cfde495c120bdf2b754b8052fa5ef62.
GRAPH.05 PR #6 e' DRAFT e NON fa parte di questa patch.

## Scope e contratti verificati
- Nella finestra WPF: menu mappe, campo nome, Nuova mappa e Rinomina.
  Cambio mappa con ricaricamento WebView2, azzeramento contesto/selezione/camera,
  annullamento proiezioni pendenti e invalidazione della cache.
- Un nuovo file SQLite con nome GUID per nuova mappa; identita' MapId distinta,
  revisione 0 e grafo vuoto. Non crea automaticamente idee della demo.
  La demo legacy demo.sqlite resta disponibile e non viene spostata.
- Catalogo limitato alla cartella mappe: solo DB legacy configurato e file
  GUID.sqlite. Letture catalogo SQLite read-only; non crea/cerca file esterni.
- Rinomina del metadato titolo con revisione ottimistica, senza toccare
  Idea, Placement e Relation delle altre mappe. Nessuna cancellazione.
- Selezione in active-map.txt con sostituzione atomica nella cartella catalogo;
  fallback alla demo o a mappa esistente in caso di selezione invalida.
  Se esistono mappe personali, l'assenza della demo non la rigenera.

## Evidenze riproducibili su OFFICE-PC
`./scripts/build.ps1`: PASS. npm ci, viewer Node 9/9 PASS, TypeScript/bundle PASS.
.NET 5/5 gruppi PASS: Domain, SQLite, Semantic, Portable JSON, Multi-map catalog.
Compilazione Release Tests/Desktop/Smoke/ScaleSmoke/Benchmarks: PASS,
zero errori e zero warning.
`dotnet run --project tests/Nodilume.Smoke -c Release --no-build`: PASS,
exit 0: regressione GRAPH.03 e GRAPH.06.02 reale WPF/WebView2. Il test
gestione mappe crea mappa vuota, la rinomina, alterna con la fixture legacy,
controlla isolamento dei due SQLite e riapre una nuova finestra che conserva
la selezione e visualizza la mappa vuota.
Suite catalogo su DB temporanei sintetici: nomi validi/invalidi, isolamento,
demo legacy, nuovo grafo vuoto, selezione persistente, no-op/stale rename,
file estraneo ignorato, fallimento selezione mappa inesistente e fallback.
`dotnet run --project tests/Nodilume.ScaleSmoke -c Release --no-build --
C:\Temp\nodilume-graph04-bench\graph04-300000.sqlite
C:\Temp\nodilume-graph06-02-scale-20260921-final.json`: PASS, exit 0.
Riesecuzione finale OFFICE-PC su fixture sintetica 300k: prima vista utile
3756.8415 ms; frame p95 e final p95 16.8 ms, selezione p95 15.3 ms,
nessuna crescita monotona nei campioni memoria registrati.
Le misure sono specifiche di questa esecuzione sul PC, non SLA;
non misurano 300k mappe catalogate ne' import/export di mappe da 300k.

## Limiti / non implementato
L'editor di contenuti Idea/Placement/Relation, la ricerca UI, dialogo
import/export file, duplicazione, cancellazione, backup, ripristino,
sincronizzazione cloud e visualizzazione multi-map simultanea sono fuori scope.
La finestra puo' creare e aprire mappe vuote; il popolamento tramite editor
e' previsto in GRAPH.06.03. Non esiste test visivo manuale dell'utente;
lo smoke automatico esercita la UI reale. GRAPH.05 ha un blocco separato
sulla propria DLL smoke/Undo-Redo: nessun risultato di GRAPH.06.02 la
certifica. Nessuna protezione Windows modificata e nessun DB personale letto.
