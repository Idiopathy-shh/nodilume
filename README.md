# Nodilume

Desktop Windows per esplorare mappe di idee come grafi 3D multiscala.

## Stato

GRAPH.00 completata. GRAPH.01 implementata e verificata tecnicamente su Windows;
accettazione manuale della navigazione ancora da eseguire.
Non è ancora un editor: zoom semantico, SQLite e mappe personali sono nelle fasi successive.

## Architettura

C#/.NET 10 e WPF ospitano una vista TypeScript/Three.js attraverso WebView2.
La scena proviene da C# e le risorse della vista sono distribuite localmente.
Il futuro stato persistente appartiene al lato C#, non al renderer.

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

`build.ps1` esegue npm ci, quattro test della camera, compilazione TypeScript,
bundle e build .NET con restore bloccato. Lo smoke apre una finestra propria,
controlla scena, selezione, movimento di focus/panoramica e ridimensionamento,
salva un'immagine in `artifacts` e chiude soltanto quella finestra.

## Comandi della vista

- Trascina col tasto sinistro per ruotare; col destro per spostare.
- Rotella per avvicinarti o allontanarti.
- Clic sul nodo o sulla sua etichetta per selezionarlo.
- Doppio clic, oppure «Avvicinati al nodo», per il focus animato.
- Clic sullo sfondo della scena, poi WASD per attraversare la rete, Q/E per scendere/salire, Maiusc per accelerare.
- «Panoramica» ripristina il punto di vista iniziale.

Questa versione mostra una fixture di 25 nodi e 27 relazioni: non salva modifiche,
non entra ancora in sottografi e non certifica prestazioni su grandi mappe.
Le prove effettive e il playbook manuale sono in `docs/validation/graph-01.md`.

La specifica completa è in `docs/specs/graph-3d-design.md`.
La roadmap è in `docs/roadmap.md`.
Il piano della prima consegna è in `docs/plans/2026-09-18-graph-00-01.md`.

## Dati

Solo codice e fixture sintetiche appartengono a questa repository privata.
Libri, database personali, fotografie e credenziali restano fuori da git.
Nome scelto dall'utente; disponibilità legale del marchio non accertata.
