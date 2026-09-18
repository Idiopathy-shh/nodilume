# Nodilume

Desktop Windows per esplorare mappe di idee come grafi 3D multiscala.

## Stato

GRAPH.00 completata. GRAPH.01 implementata, verificata su Windows e accettata dall'utente.
GRAPH.02 introduce il modello persistente Idea/Placement/Relation e SQLite ed è verificata
tecnicamente; l'integrazione della PR e l'eventuale prova manuale restano separate.
Non è ancora un editor completo: zoom semantico, modifica interattiva, undo e prove di scala
appartengono alle fasi successive.

## Architettura

C#/.NET 10 e WPF ospitano una vista TypeScript/Three.js attraverso WebView2.
`Nodilume.Core` contiene identità e invarianti senza dipendenze grafiche o database;
`Nodilume.Application` espone operazioni e porte di persistenza; `Nodilume.Infrastructure`
implementa un database SQLite per mappa. La scena è una proiezione C# e la vista non accede
al database. Le risorse del renderer sono distribuite localmente.

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
bundle, test comportamentali Core/SQLite su database temporanei reali e build .NET con restore
bloccato. Lo smoke apre due finestre WPF/WebView2 consecutive sullo stesso database temporaneo,
verifica riapertura persistente, scena, selezione, focus/panoramica e ridimensionamento,
salva un'immagine in `artifacts` e chiude soltanto le finestre create dal test.

## Comandi della vista

- Trascina col tasto sinistro per ruotare; col destro per spostare.
- Rotella per avvicinarti o allontanarti.
- Clic sul nodo o sulla sua etichetta per selezionarlo.
- Doppio clic, oppure «Avvicinati al nodo», per il focus animato.
- Clic sullo sfondo della scena, poi WASD per attraversare la rete, Q/E per scendere/salire, Maiusc per accelerare.
- «Panoramica» ripristina il punto di vista iniziale.

La demo persistente contiene 25 Placement, 24 Idea e 3 Relation concettuali; il renderer
mostra anche i 24 archi di contenimento, per 27 collegamenti visuali. Una Idea è rappresentata
in due contesti con identità di Placement distinte. Il database predefinito è
`%LOCALAPPDATA%\Nodilume\Maps\demo.sqlite` e viene inizializzato solo se assente.
La vista GRAPH.02 è intenzionalmente limitata; zoom semantico e aggregazioni complete sono GRAPH.03.
Le prove GRAPH.02 sono in `docs/validation/graph-02.md`.

La specifica completa è in `docs/specs/graph-3d-design.md`.
La roadmap è in `docs/roadmap.md`.
Il piano della prima consegna è in `docs/plans/2026-09-18-graph-00-01.md`.

## Dati

Solo codice e fixture sintetiche appartengono a questa repository privata.
Libri, database personali, fotografie e credenziali restano fuori da git.
Nome scelto dall'utente; disponibilità legale del marchio non accertata.
