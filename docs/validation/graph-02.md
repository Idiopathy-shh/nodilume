# GRAPH.02 — verifica tecnica e playbook

Data: 18 settembre 2026.
Branch: `feat/graph-02-domain-sqlite`.
Base: `47119f7c5d470a3149a3a6dddbc1d503d2a36104`.

## Obiettivo verificato

GRAPH.02 introduce il modello C# persistente indipendente dal renderer:
`Map`, `Idea`, `Placement` e `Relation`. Il contenimento appartiene ai Placement;
le Relation collegano Idea. La persistenza usa un file SQLite per mappa e la finestra
Windows carica una proiezione limitata attraverso Application, senza accesso DB dal Viewer.

La demo contiene 25 Placement, 24 Idea e 3 Relation concettuali. I 24 rapporti
padre-figlio producono, insieme alle Relation, 27 collegamenti visuali. Una Idea
("Domande") ha due rappresentazioni con Placement e annotazioni di contesto distinti.

## Ambiente

- Windows 11 Pro, OFFICE-PC.
- .NET SDK 10.0.400.
- Node 24.19.0 e npm 11.17.0.
- WebView2 Runtime 153.0.4234.32.
- SQLite tramite Microsoft.Data.Sqlite 10.0.9.
- SQLitePCLRaw.lib.e_sqlite3 fissato a 2.1.13.
## Verifiche automatiche

| Verifica | Esito | Evidenza |
|---|---|---|
| Baseline GRAPH.01 prima delle modifiche | PASS | Viewer 4/4, build 0 errori/avvisi, smoke Windows PASS |
| Viewer test | PASS | 4/4 camera/focus |
| TypeScript + bundle | PASS | `npm run build` |
| Core/Application/Infrastructure | PASS | build Release 0 errori e 0 avvisi |
| Invarianti dominio | PASS | gruppo `Domain invariants` |
| Integrazione SQLite reale | PASS | gruppo `SQLite integration` |
| Restore riproducibile | PASS | lockfile e `--locked-mode` |
| Audit pacchetti .NET | PASS | nessun pacchetto vulnerabile riportato per Tests o Smoke |
| Smoke WPF/WebView2 | PASS | due aperture consecutive dello stesso DB temporaneo |
| Risorse Viewer locali | PASS | nessuna resource URL fuori da `nodilume.local` |
| Interazioni GRAPH.01 | PASS automatico | selezione, focus, home e resize |
| Diff whitespace | PASS | `git diff --check` senza errori |

Comandi finali principali:

```powershell
./scripts/build.ps1
dotnet run --project tests/Nodilume.Smoke -c Release --no-build
dotnet list tests/Nodilume.Tests/Nodilume.Tests.csproj package --vulnerable --include-transitive
dotnet list tests/Nodilume.Smoke/Nodilume.Smoke.csproj package --vulnerable --include-transitive
```
## Invarianti e casi limite coperti

- Idea e Placement non possono attraversare mappe.
- Un Placement ha zero o un padre; `null` rappresenta la radice senza sentinel fittizi.
- Self-parent, cicli di contenimento e parent mancanti sono respinti.
- La stessa Idea non può ricomparire nella medesima catena antenato-discendente.
- Lo spostamento di un sottoalbero confronta tutte le sue Idea con i nuovi antenati.
- Le Relation concettuali possono formare cicli.
- Rappresentazioni multiple condividono titolo/contenuto, ma mantengono coordinate,
  annotazione e pin locali.
- La rimozione di un Placement foglia conserva Idea, altre rappresentazioni e Relation.
- La rimozione di un Placement con figli è respinta.
- NaN e infinito sono respinti prima della mutazione.
- Una write SQLite che fallisce dopo una prima modifica viene completamente rollbackata.
- Una revisione obsoleta è respinta senza sovrascrivere lo stato confermato.
- Una versione schema futura è respinta senza reset o perdita dei dati.
- La paginazione keyset su dati stabili non salta e non duplica Placement.
- Le Relation non generano il prodotto cartesiano delle rappresentazioni nel renderer.
## Persistenza e riapertura

Il DB applicativo predefinito è fuori dalla repository:

`%LOCALAPPDATA%\Nodilume\Maps\demo.sqlite`

La fixture viene creata soltanto quando il database non contiene ancora una mappa.
La riapertura non rigenera la demo: lo smoke apre due finestre WPF/WebView2 consecutive
sullo stesso file temporaneo e confronta mapId, revisione, set degli ID, posizione,
parent, ideaId e annotazione. I test automatici e lo smoke usano directory temporanee
dedicate e non scrivono nel DB demo personale.

Ogni connessione abilita le foreign key. Lo schema usa FK composte con `map_id`,
indici per parent/idea/adiacenze e una tabella di versione. Le modifiche applicative
sono mirate e la revisione della mappa viene incrementata nello stesso commit SQLite.
## Limiti intenzionali di GRAPH.02

- La scena iniziale richiede al massimo 128 Placement; non carica l'intero DB come
  prerequisito dell'avvio. La selezione multiscala delle porzioni appartiene a GRAPH.03/04.
- La proiezione corrente sceglie in modo deterministico una rappresentazione visibile
  per ogni estremo di Relation; gestione completa delle destinazioni ambigue e
  aggregazioni multiscala appartengono a GRAPH.03.
- Non esiste ancora UI per creare/spostare/pinnare elementi. Le operazioni C# e la
  persistenza sono predisposte, mentre drag/pin interattivi e undo/redo sono GRAPH.05.
- Camera e ViewState non sono persistiti in questa fase.
- Nessun benchmark 10k/100k/300k viene dichiarato: è GRAPH.04.
- Non sono implementati EPUB, OCR, AI, cloud, collaborazione o VR.

## Prova manuale facoltativa

1. Eseguire `./scripts/build.ps1`.
2. Eseguire `./scripts/run.ps1`.
3. Verificare che la barra di stato indichi la mappa persistente SQLite.
4. Selezionare nodi, usare focus, panoramica, rotazione/pan/zoom e WASD/QE.
5. Chiudere Nodilume e riaprirlo con `./scripts/run.ps1`.
6. Verificare che la stessa scena ricompaia senza errori o rigenerazioni apparenti.

Il PASS tecnico è distinto dall'accettazione manuale dell'utente e dal merge della PR.
