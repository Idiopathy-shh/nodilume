# Nodilume

Desktop Windows per esplorare mappe di idee come grafi 3D multiscala.

## Stato

GRAPH.00-04 integrate (GRAPH.04 PR #5; main@0f6bdac). GRAPH.05 PR #6 e'
una candidata DRAFT: viewer 13/13, test .NET 4/4 e scale-smoke 300k PASS
sul relativo branch, ma smoke UI Undo/Redo e accettazione manuale ancora mancanti;
Smart App Control blocca la DLL smoke non firmata su OFFICE-PC.
GRAPH.06.01 e' stata integrata tramite PR #7, squash c515f79: codec JSON
portabile del grafo, senza interfaccia editor completa o file backup.
GRAPH.06.02 e' integrata tramite PR #9 (squash a9df2bd): gestione
di mappe personali in database SQLite separati. GRAPH.06.03 e' integrata
tramite PR #11, squash 9478973: editor di Idee/nodi con gerarchie,
rappresentazioni multiple e note locali, indipendente da GRAPH.05.
GRAPH.06.04 e' candidata: editor Relation con create/update/inversione/delete;
viewer 9/9, .NET 7/7 e smoke WPF/WebView2 PASS su OFFICE-PC.
GRAPH.07-08 non iniziate. Vedere docs/coordination.md e docs/roadmap.md.

GRAPH.03 introduce zoom semantico contestuale, aggregazioni e navigazione trasversale.
GRAPH.04 rende raggiungibili i figli oltre la prima pagina, limita nodi/link/etichette,
evita caricamenti globali nel renderer e misura la scala fino a 300.000 Idea. Non è
ancora un editor completo: drag/pin, undo/redo e camera persistente sono candidate
GRAPH.05; gestione completa e ricerca UI appartengono alle fasi successive.
Il codec GRAPH.06.01 esporta/importa il grafo in memoria, senza file dialog o
integrazione con la shell WPF e senza dipendere dalla migrazione SQLite v3.
GRAPH.06.02 introduce la gestione mappe; GRAPH.06.03 rende possibile
aggiungere e modificare idee radice o figlie in tali mappe. GRAPH.06.04
aggiunge le Relation concettuali fra Idea con direzione, tipo e spiegazione.
Le operazioni Undo/Redo, drag/pin e camera persistente rimangono candidate
in GRAPH.05 e non sono comprese nel ramo indipendente GRAPH.06.04.

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

`build.ps1` esegue `npm ci`, nove test viewer/navigation, compilazione TypeScript,
bundle, sette gruppi .NET Core/SQLite/proiezione/codec/catalogo/editor Idea/Relation
su database temporanei reali e build .NET con restore bloccato. Compila inoltre
lo scale-smoke e il tool benchmark GRAPH.04.

Lo smoke GRAPH.03 usa una fixture separata dalla demo personale. Apre due finestre
WPF/WebView2 consecutive sullo stesso database temporaneo e verifica tre contesti
annidati, entrata/uscita, scelta fra rappresentazioni, destinazione trasversale e ritorno,
direzioni opposte, focus osservabile, resize e riapertura persistente. Salva
`artifacts/graph-03-smoke.png` e non sovrascrive database esistenti.

## Gestione delle mappe (GRAPH.06.02)

Il menu `Mappa` in alto elenca le mappe personali nella cartella locale.
Scrivi un nome nel campo e premi `Nuova mappa` per creare una mappa
vuota distinta; scegli una voce dal menu per aprirla o usa `Rinomina` per
cambiare il nome di quella attiva. La selezione viene ripristinata alla
riapertura. L'antica demo.sqlite resta disponibile: non viene spostata
o sovrascritta da nuovi file mappa GUID.sqlite. Nessuna cancellazione mappe
e nessun editor di contenuti ancora introdotti da questa patch.

## Editor di idee e nodi (GRAPH.06.03)

Nel pannello a destra scrivi titolo e contenuto, quindi scegli
`Nuova radice` per creare un'Idea indipendente. Seleziona un nodo
della scena per caricare il titolo e il contenuto condiviso della sua
Idea e l'annotazione propria di quel Placement. `Salva idea` aggiorna
il contenuto comune a tutte le sue rappresentazioni; `Salva annotazione
locale` modifica solo il nodo selezionato. `Nuova figlia` crea
una nuova Idea sotto il nodo selezionato usando i campi del pannello.
`Aggiungi rappresentazione qui` colloca la stessa Idea sotto il
contesto corrente, se non viola gli invarianti della gerarchia.
Le modifiche vengono scritte su SQLite; se rifiutate viene mostrato
un messaggio. La creazione di contenuti non include cancellazione di
Idea/Placement o Undo/Redo.

## Editor relazioni (GRAPH.06.04)

Seleziona un nodo e catturalo come origine; naviga e seleziona un'altra
Idea come destinazione. Imposta tipo, direzione e spiegazione, quindi crea
la Relation. L'elenco mostra le relazioni che toccano l'Idea selezionata;
una voce può essere invertita o modificata conservando la propria identità.
L'eliminazione richiede due click e rimuove soltanto la Relation. Oltre 64
risultati l'elenco dichiara che è parziale.

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
GRAPH.06.02: `docs/plans/graph-06-02-map-manager.md`,
`docs/validation/graph-06-02.md` e `docs/handoffs/GRAPH_06_02_RESULT.md`.
GRAPH.06.03: `docs/plans/graph-06-03-idea-node-editor.md`,
`docs/validation/graph-06-03.md` e `docs/handoffs/GRAPH_06_03_RESULT.md`.
GRAPH.06.04: `docs/plans/graph-06-04-relation-editor.md`,
`docs/validation/graph-06-04.md` e `docs/handoffs/GRAPH_06_04_RESULT.md`.
GRAPH.06.01: `docs/plans/graph-06-01-portable-map.md` e
`docs/validation/graph-06-01.md`; per verificare solo questa slice:
`dotnet restore tests/Nodilume.Tests --locked-mode`,
`dotnet build tests/Nodilume.Tests -c Release --no-restore`,
`dotnet run --project tests/Nodilume.Tests -c Release --no-build`.
La specifica completa è in `docs/specs/graph-3d-design.md`.
La roadmap è in `docs/roadmap.md`.

## Dati

Solo codice e fixture sintetiche appartengono a questa repository privata.
Libri, database personali, fotografie e credenziali restano fuori da git.
Nome scelto dall'utente; disponibilità legale del marchio non accertata.
