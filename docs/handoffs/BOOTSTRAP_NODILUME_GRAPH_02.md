# Nodilume — bootstrap chat implementatrice GRAPH.02

Sei la chat implementatrice di **GRAPH.02 — identità, rappresentazioni, contenimento e SQLite**. Una chat separata coordina il progetto. Esegui questo incarico fino alla consegna di una PR verificata; non eseguire il merge e non iniziare GRAPH.03.

## 1. Repository e punto di partenza

- Repository privata: https://github.com/Idiopathy-shh/nodilume
- PC di riferimento: OFFICE-PC, Windows 11.
- Checkout esistente: `C:\Sviluppo\Nodilume`.
- Branch base: `main`, dopo lo squash merge della PR #1.
- PR precedente: https://github.com/Idiopathy-shh/nodilume/pull/1
- Branch proposto: `feat/graph-02-domain-sqlite`.
- Ambiente verificato: .NET SDK 10.0.400, Node 24.19.0, npm 11.17.0, WebView2 Runtime 153.0.4234.32.
- Usa GitHub e Remote Desktop Commander per verificare lo stato reale e lavorare su Windows. Se sono online più dispositivi, seleziona esplicitamente OFFICE-PC.

La PR #1 introduce WPF/WebView2, una fixture C# di 25 nodi e 27 collegamenti, selezione, rotazione/pan/zoom, WASD/QE, focus e panoramica. GRAPH.01 è stata accettata dall'utente. Non esiste ancora il modello persistente.

Prima di modificare:

```powershell
Set-Location C:\Sviluppo\Nodilume
git status --porcelain
git branch --show-current
git fetch origin
gh pr view 1 --json state,mergeCommit
git log -5 --oneline
```

Richiedi `state=MERGED`. Registra SHA base effettivo, branch e percorso. Leggi eventuali AGENTS.md applicabili. Se ci sono modifiche altrui, branch già impegnati o divergenze, preservali e segnala il conflitto. Non fare reset, force-push o rebase autonomi. Su checkout pulito e non impegnato, aggiorna `main` con `git pull --ff-only`, poi crea il branch. Se il branch esiste già, verifica se stai riprendendo questo lavoro prima di crearne un altro.

Non modificare repository OptionLab/QOE, impostazioni globali di Git, runtime o altre applicazioni aperte. Per Nodilume chiudi solo un'istanza identificata, se strettamente necessario per build/runtime, spiegando l'azione.

## 2. Letture obbligatorie

1. `docs/specs/graph-3d-design.md` — requisiti del progetto.
2. `docs/roadmap.md` e `docs/coordination.md` — fase e responsabilità.
3. `README.md`, `docs/validation/graph-01.md` — build e baseline verificata.
4. Codice attuale in `src/Nodilume.Desktop`, `src/Nodilume.Viewer`, `tests/Nodilume.Smoke` e script di build.

Le decisioni approvate restano valide. Prepara `docs/plans/graph-02-domain-sqlite.md` con file, contratti, sequenza e verifiche prima del codice. Risolvi autonomamente i dettagli implementativi ordinari; riporta alla coordinatrice soltanto conflitti o cambiamenti sostanziali di architettura.

## 3. Obiettivo e confini

Introdurre il modello C# indipendente dalla grafica e la persistenza SQLite, poi collegare la scena Windows a una mappa dimostrativa persistente. La chiusura e riapertura devono leggere gli stessi dati e identificatori, senza rigenerare o sovrascrivere una mappa esistente.

Responsabilità previste dalla specifica:

- `src/Nodilume.Core`: Map, Idea, Placement, Relation e invarianti; nessuna dipendenza WPF, WebView2, Three.js o SQLite.
- `src/Nodilume.Application`: operazioni validate, query e porte di persistenza.
- `src/Nodilume.Infrastructure`: SQLite, schema/migrazioni e transazioni.
- Desktop: composizione e caricamento della mappa tramite Application.
- Viewer: proiezione grafica; nessun accesso al database o autorità sui dati persistenti.
- `tests/Nodilume.Tests`: test comportamentali del dominio e integrazione su SQLite reale.

Un'idea ha un'identità stabile e contenuto condiviso. Le sue rappresentazioni (Placement) hanno posizione XYZ locale, annotazione, stato fissato e padre propri. Il contenimento appartiene al Placement: rappresentare di nuovo un'idea non duplica il suo sottoalbero. Relation collega idee, non coordinate né tutte le combinazioni dei loro Placement.

Persisti almeno Map, Idea, Placement e Relation con schema versionato e revisioni coerenti. Non introdurre implementazioni fittizie di undo/redo o camera persistente per riempire tipi previsti da fasi successive: queste capacità appartengono a GRAPH.05.

## 4. Invarianti e casi limite da coprire

- Nessun riferimento tra mappe diverse o verso entità inesistenti.
- Ogni Placement ha al massimo un padre nella medesima mappa; radice rappresentata senza parent fittizi ambigui.
- Rifiutare self-parent e cicli di contenimento, anche spostando un intero sottoalbero.
- Rifiutare la stessa Idea ripetuta lungo una catena antenato-discendente. Verificare anche le idee nei discendenti del sottoalbero spostato contro i nuovi antenati.
- Consentire cicli nelle relazioni concettuali: non confonderli con il contenimento.
- Più rappresentazioni della stessa idea condividono contenuto; annotazioni, coordinate e stato fissato restano locali.
- Rimuovere una rappresentazione foglia preserva idea, altre rappresentazioni e relazioni. Un'idea non collocata rimane recuperabile.
- La rimozione di un Placement con figli non provoca cancellazioni o promozioni silenziose. In questa fase è sufficiente rifiutare l'operazione non specificata; la UI distruttiva globale e undo restano fuori scope.
- Rifiutare coordinate NaN/infinite e preservare lo stato valido dopo un'operazione respinta.
- Fallimento della scrittura: nessun successo comunicato prima del commit e nessuna mutazione parziale. Proteggere da revisione obsoleta, senza sovrascritture silenziose.

## 5. Persistenza e integrazione Windows

- Un database per mappa, file dell'utente fuori dalla repository (sotto LocalApplicationData/Nodilume o percorso esplicito iniettato).
- Test e smoke usano directory temporanee dedicate; non scrivono nella mappa personale/demo dell'utente.
- Chiavi esterne attive su ogni connessione e vincoli coerenti con le invarianti; indici per padre, idea e adiacenze.
- Schema e migrazioni versionati e transazionali. Versione futura non supportata: errore esplicito, nessuna riscrittura o reset.
- Scritture mirate e transazioni atomiche; non usare il salvataggio dell'intera mappa a ogni modifica come soluzione definitiva.
- Query con limite/paginazione e ordinamento stabile. Non caricare indiscriminatamente tutti i dati come prerequisito dell'avvio; l'ottimizzazione e i benchmark estesi restano GRAPH.04.
- Apertura iniziale: crea la demo solo se assente; alla riapertura conserva ID, contenuti, posizioni e annotazioni. Gli errori I/O o di integrità non diventano una demo vuota apparentemente valida.
- Nel modello e nei test includi livelli annidati e la stessa Idea in due contesti. La UI può mostrare una proiezione limitata dichiarata: non deve appiattire o duplicare i dati persistenti per adattarli al renderer attuale.
- Mantieni selezione, focus e panoramica della baseline. La proiezione grafica distingue placementId da ideaId, così le rappresentazioni multiple non collidono nelle mappe del renderer.
- Evita prodotti cartesiani delle rappresentazioni delle relazioni. Le aggregazioni multiscala complete saranno GRAPH.03.

## 6. Verifiche richieste

Esegui prima la baseline `./scripts/build.ps1` e lo smoke. Poi aggiungi test significativi per ogni invariante sopra e per:

1. Round-trip su file SQLite chiuso e riaperto, con identità e valori invariati.
2. Due mappe indipendenti e tentativi di riferimenti incrociati respinti.
3. Contenuto comune e annotazioni locali per rappresentazioni multiple.
4. Spostamenti di sottoalberi validi e non validi; operazioni fallite senza modifiche residue.
5. Rollback della transazione e rifiuto delle revisioni obsolete.
6. Inizializzazione idempotente e versione schema non supportata senza perdita dati.
7. Query paginate che non saltano/duplicano elementi in dati stabili.
8. Avvio reale Windows, caricamento da database e successiva riapertura; preservazione delle interazioni GRAPH.01.

Usa SQLite reale nei test di integrazione. Aggiorna `scripts/build.ps1` affinché esegua anche i nuovi test e includi tutti i nuovi lockfile. Non indebolire lo smoke per farlo passare: aggiorna le aspettative solo se la fixture cambia intenzionalmente, mantenendo gli effetti osservabili verificati.

## 7. Fuori scope

Zoom semantico continuo e aggregazione multiscala; benchmark 10k/100k/300k; editor completo e gestione mappe dalla UI; drag/pin interattivo e undo/redo; salvataggio camera; EPUB, OCR, AI, cloud, collaborazione, VR. Predisporre contratti coerenti non significa implementare ora queste funzionalità.

## 8. Consegna alla coordinatrice

- Aggiorna README, roadmap e `docs/validation/graph-02.md` con comandi eseguiti, esiti, fixture e limiti.
- Documenta le scelte tecniche e le eventuali questioni rimaste aperte.
- Esegui revisione, build, test e verifica Windows. Mantieni distinti PASS tecnico e accettazione manuale dell'utente.
- Committa, pubblica il branch e apri PR verso `main`; draft se manca accettazione o un criterio.
- **Non effettuare merge e non iniziare la patch successiva.** La coordinatrice e l'utente gestiscono l'integrazione.
- Concludi con link PR, SHA base/finale, checkout/branch, riepilogo implementato, test ed esiti, limiti e istruzioni per l'eventuale prova manuale. Salva lo stesso riepilogo in `docs/handoffs/GRAPH_02_RESULT.md`.

Inizia ora dalle verifiche del repository e dalla baseline, poi procedi fino alla PR senza richiedere conferma per ogni attività ordinaria già compresa nell'incarico.
