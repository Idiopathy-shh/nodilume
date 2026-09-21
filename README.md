# Nodilume

Desktop Windows per esplorare mappe di idee come grafi 3D multiscala.

## Stato

GRAPH.00-04 integrate. GRAPH.04: PR #5, main `0f6bdac9d988bb1a6a6c3ddb8b3f71d02bd872f5`.
GRAPH.05 è una candidata sul branch `feat/graph-05-editing-viewstate`: drag locale,
pin, undo/redo persistente e ripristino della vista. Non è ancora PASS complessivo.
Viewer 13/13 e compilazione PASS; test .NET bloccati dal criterio Windows 0x800711C7.
Smoke reale e scale-smoke della candidata non eseguiti.
Dettagli in `docs/validation/graph-05.md`; GRAPH.06-08 non iniziate.

## Architettura

C#/.NET 10 e WPF ospitano una vista TypeScript/Three.js attraverso WebView2.
`Nodilume.Core` contiene identità e invarianti senza dipendenze grafiche o database;
`Nodilume.Application` espone operazioni, contratti di proiezione e porte di persistenza;
`Nodilume.Infrastructure` implementa un database SQLite per mappa.

C# è autorevole per grafo, contesto, revisione, aggregazioni e destinazioni. Il viewer
possiede camera, animazioni e stato grafico transitorio. I messaggi GRAPH.03 sono
versionati e correlati tramite `requestId`, `mapId`, revisione e contesto; richieste
superate vengono annullate lato Desktop e risposte obsolete vengono ignorate lato viewer.
Le risorse del renderer sono distribuite localmente.

## Avvio da sorgente

Prerequisiti: Windows 11, .NET SDK 10.0.400 (o patch compatibile), Node 24 e npm,
Microsoft Edge WebView2 Runtime. Il primo restore richiede Internet.
Le dipendenze sono fissate nei lockfile; la vista usa solo risorse locali.

Da PowerShell, nella radice della repository:

```powershell
./scripts/build.ps1
dotnet run --project tests/Nodilume.Smoke -c Release --no-build
./scripts/run.ps1
```

`build.ps1` esegue `npm ci`, 13 test viewer/navigation/editing, compilazione TypeScript,
bundle, test Core/SQLite/proiezione semantica su database temporanei reali e build .NET
con restore bloccato. Compila inoltre lo scale-smoke e il tool benchmark GRAPH.04.

Lo smoke GRAPH.03 usa una fixture separata dalla demo personale. Apre due finestre
WPF/WebView2 consecutive sullo stesso database temporaneo e verifica tre contesti
annidati, entrata/uscita, scelta fra rappresentazioni, destinazione trasversale e ritorno,
direzioni opposte, focus osservabile, resize e riapertura persistente. Salva
`artifacts/graph-03-smoke.png` e non sovrascrive database esistenti.

## Comandi della vista

- Trascina col tasto sinistro per ruotare; col destro per spostare.
- Rotella per avvicinarti o allontanarti.
- Clic sul nodo o sulla sua etichetta per selezionarlo.
- Doppio clic o «Entra» apre un nodo che possiede figli; una foglia viene soltanto messa a fuoco.
- Avvicinarsi a un nodo selezionato o puntato stabilmente può aprirlo automaticamente; entrata e uscita usano soglie diverse.
- «Livello superiore» oppure Esc esce dal contesto; il breadcrumb permette di scegliere un antenato.
- «Pagina» / «Altri» sfogliano progressivamente i figli quando il contesto supera il budget.
- «Panoramica» torna alla proiezione principale.
- «Ritorna» ripristina il contesto/camera precedente dopo una navigazione trasversale.
- Nei collegamenti con più rappresentazioni viene mostrato il percorso di ogni Placement: la vista non sceglie silenziosamente il primo ID.
- Clic sullo sfondo, poi WASD per attraversare la rete, Q/E per scendere/salire e Maiusc per accelerare.

Durante il caricamento la scena corrente resta visibile. Stati ready, partial, leaf,
empty ed errore sono espliciti. Un nodo foglia espone `hasChildren=false` e non viene
trattato come un gruppo apribile.

La demo persistente contiene 25 Placement, 24 Idea e 3 Relation concettuali; una Idea
è rappresentata in due contesti con Placement distinti. Il database predefinito è
`%LOCALAPPDATA%\Nodilume\Maps\demo.sqlite` e viene inizializzato solo se assente.
GRAPH.04 non richiede il caricamento globale della mappa e non scrive posizioni durante
zoom o paging. Se un budget viene esaurito la proiezione resta esplicitamente `partial`.

Le prove GRAPH.02 sono in `docs/validation/graph-02.md`.
Le prove e il playbook GRAPH.03 sono in `docs/validation/graph-03.md`.
La validazione GRAPH.04 è in `docs/validation/graph-04.md` e i benchmark in
`docs/benchmarks/graph-04.md`.
La specifica completa è in `docs/specs/graph-3d-design.md`.
La roadmap è in `docs/roadmap.md`.

## Dati

Solo codice e fixture sintetiche appartengono a questa repository privata.
Libri, database personali, fotografie e credenziali restano fuori da git.
Nome scelto dall'utente; disponibilità legale del marchio non accertata.

## Modifiche locali GRAPH.05 (candidate)

Maiusc+trascina sposta un nodo del contesto. Fissa/Sblocca protegge il Placement;
sbloccare prima del drag. Annulla/Ripeti (Ctrl+Z/Ctrl+Y) conserva fino a 100
operazioni per mappa anche dopo riapertura. Il punto di vista viene salvato
separatamente; la nuova UX richiede ancora esecuzione smoke e accettazione.
