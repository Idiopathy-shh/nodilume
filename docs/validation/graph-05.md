# GRAPH.05 - validazione su OFFICE-PC
Data: 21 settembre 2026. Stato: CANDIDATA, NON PASS complessivo (smoke reale incompleto).
Base: 0f6bdac9d988bb1a6a6c3ddb8b3f71d02bd872f5 (GRAPH.04 PR #5 integrata).
Commit funzionale: 75655f7985326ee83d67aeed68351793598c8b61.
Riesecuzione: OFFICE-PC, branch feat/graph-05-editing-viewstate, PR #6 Draft.

## Verifiche eseguite
| Verifica | Esito |
|---|---|
| Viewer Node tests | PASS 13/13, inclusi 4 nuovi test drag/correlazione |
| TypeScript e bundle | PASS |
| Build Nodilume.Tests Release | PASS, 0 errori/avvisi |
| Build Desktop/Smoke Release | PASS, 0 errori/avvisi |
| Build ScaleSmoke Release | PASS, 0 errori/avvisi |
| git diff --check | PASS (rieseguito nella worktree) |
| Suite .NET sulla candidata | PASS 4/4: Domain, SQLite, Semantic, Local edits and view state |
| Smoke GRAPH.03 + GRAPH.05 | FAIL: attesa UI Undo/Redo; nuova build del test bloccata da Smart App Control |
| Scale-smoke 300k sulla candidata | PASS: edit/undo/cache/ViewState e WPF/WebView2 su clone del DB sintetico |
| Accettazione manuale | NON ESEGUITA |

Riesecuzione OFFICE-PC: viewer 13/13, TypeScript/bundle e build .NET tutti PASS;
la suite Nodilume.Tests ha restituito 4/4 gruppi PASS ed exit code 0. I risultati
precedenti 1/4 erano un blocco runtime, non un errore dei gruppi di test.
Il primo smoke reale ha raggiunto EditingSmoke.cs:72 (attesa Undo/Redo); il secondo,
con diagnostica a timeout, ha raggiunto l'attesa di Redo con stato ready, editPending=false,
undo abilitato e redo disabilitato. Le cause funzionali/timing NON sono ancora dimostrate.
Il test ora attende che i pulsanti siano abilitati e genera diagnostica dopo 12s:
modifiche compilate PASS ma non ancora validate runtime, perché Smart App Control
ha bloccato il caricamento della nuova Nodilume.Smoke.dll (HRESULT 0x800711C7).
Il criterio Windows non è stato modificato, disabilitato o aggirato.
Non trasferire il PASS della suite .NET o dello scale-smoke allo smoke UI mancante.

## Funzionalità candidate
Maiusc + trascinamento con tasto sinistro muove un Placement del contesto corrente
su un piano perpendicolare alla camera. Si conserva l'offset iniziale e si persiste
il delta nelle coordinate locali. Il parent non cambia.
Esc, perdita cattura, blur e cambio contesto annullano l'anteprima.
La camera e lo zoom semantico sono sospesi durante il gesto/commit.
Un gesto equivale a un comando. Solo il commit SQLite conferma il salvataggio.
Un errore ricarica la proiezione autorevole e mantiene un messaggio visibile.

Fissa/Sblocca opera sul Placement. Un nodo fissato deve essere sbloccato prima del drag.
Non esiste ancora un layout automatico: nessun comando di riordino è stato aggiunto.
Le altre rappresentazioni della stessa Idea e le coordinate locali dei figli restano invariate.

Undo/redo: comandi e revisione in transazione, cronologia persistente per mappa
limitata a 100 operazioni. Una nuova modifica elimina il ramo redo.
No-op e fallimenti non creano voci. Ricevute limitate a 1000 richieste per mappa;
un replay recente restituisce revisione e disponibilità undo/redo correnti.
Oltre la finestra di ricevute, la revisione attesa impedisce di ripetere una vecchia modifica.

SQLite v3: graph_edits, edit_cursor, edit_receipts e view_state.
Migrazione additiva da v2, nessun reset o modifica a database personali.
ViewState contiene percorso, camera/target/up, selezione e cursore pagina; non incrementa
la revisione del grafo. Salvataggio a cambiamento, al massimo ogni 500 ms, e alla chiusura.
Contesti mancanti ripiegano sull'antenato valido; camera invalida viene ignorata.
Una chiusura con salvataggio fallito chiede esplicitamente se conservare l'ultima vista salvata.

## Test runtime sulla candidata
LocalEditTests PASS nella suite .NET 4/4: identità e coordinate, rappresentazioni multiple,
gruppo/figli, pin, replay e revisione, no-op, undo/redo dopo riapertura, redo invalidato,
isolamento mappa, coordinate invalide, cronologia, rollback tramite trigger,
migrazione v2, cache dopo move/undo, selezione oltre pagina e ViewState invalido.

EditingSmoke usa Input.dispatchMouseEvent di WebView2 per input reale del renderer.
La sequenza completa drag/Esc/undo/redo/fallimento SQLite/pin/riapertura NON e' PASS:
il test originale ha fallito nelle attese UI Undo/Redo, poi SAC ha bloccato
l'assembly ricompilato del test. Non attribuire il fallimento a SQLite senza prove.

ScaleSmoke PASS su copia SQLite BackupDatabase della fixture sintetica GRAPH.04 da
300000 Idea (C:\Temp\nodilume-graph04-bench\graph04-300000.sqlite).
Ha eseguito editing/undo/cache/ViewState e lo smoke WPF/WebView2 con exit code 0.
Risultati: C:\Temp\nodilume-graph05-scale-20260921.json;
openingToFirstUsefulMs=4004.4717, frameP95Ms=16.7, finalFrameP95Ms=16.8,
selectionP95Ms=13.0. Sono risultati di questa esecuzione su OFFICE-PC, non SLA.

## Ripresa
Continuare ESCLUSIVAMENTE su OFFICE-PC, C:\Sviluppo\Nodilume-graph05, PR #6 Draft.
Non cambiare criterio di sicurezza, percorso dei binari o protezioni per eludere SAC.
Risolto il caricamento delle DLL di sviluppo in modo autorizzato, eseguire:

    ./scripts/build.ps1
    dotnet run --project tests/Nodilume.Smoke -c Release --no-build

Lo scale-smoke e' gia' PASS sulla candidata: ripeterlo solo dopo modifiche funzionali.
Diagnosticare il mancato invio/accettazione dei comandi Undo/Redo nello smoke reale
senza confondere pulsanti disabilitati, transizioni UI e mancato commit SQLite.
Completare i passi del playbook e registrare SHA, esiti, limiti e accettazione manuale;
nessun merge e nessun GRAPH.06 prima dello smoke completo PASS.

## Playbook utente
1. Aprire solo una fixture di test; selezionare un nodo del contesto.
2. Maiusc+drag: verificare assenza di salto iniziale e posizione salvata al rilascio.
3. Ripetere e premere Esc prima del rilascio: nessuna modifica persistente.
4. Fissare il nodo: drag rifiutato; sbloccare e riprovare.
5. Annulla/Ripeti o Ctrl+Z/Ctrl+Y: verificare ripristino esatto.
6. Spostare un gruppo: verificare geometria interna e altre rappresentazioni invariate.
7. Navigare a una pagina successiva, selezionare, cambiare camera e chiudere.
8. Riaprire: controllare contesto, selezione, camera e pin.
9. Distinguere problemi visivi dai test di correttezza e annotare entrambi.
