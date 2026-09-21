# GRAPH.05 - validazione e blocco runtime
Data: 21 settembre 2026. Stato: CANDIDATA, NON PASS complessivo.
Base: 0f6bdac9d988bb1a6a6c3ddb8b3f71d02bd872f5 (GRAPH.04 PR #5 integrata).
Commit funzionale: 75655f7985326ee83d67aeed68351793598c8b61.

## Verifiche eseguite
| Verifica | Esito |
|---|---|
| Viewer Node tests | PASS 13/13, inclusi 4 nuovi test drag/correlazione |
| TypeScript e bundle | PASS |
| Build Nodilume.Tests Release | PASS, 0 errori/avvisi |
| Build Desktop/Smoke Release | PASS, 0 errori/avvisi |
| Build ScaleSmoke Release | PASS, 0 errori/avvisi |
| git diff --check | PASS |
| Esecuzione suite .NET sulla candidata | BLOCCATA da Windows, 1/4 gruppi PASS |
| Smoke GRAPH.03 + GRAPH.05 sulla candidata | NON ESEGUITO: dipendenza runtime bloccata |
| Scale-smoke 300k sulla candidata | NON ESEGUITO: dipendenza runtime bloccata |
| Accettazione manuale | NON ESEGUITA |

La baseline parziale ereditata ha inizialmente completato build e 3/3 gruppi .NET.
Dopo le modifiche, Windows ha bloccato il caricamento di Nodilume.Application.dll:
System.IO.FileLoadException, criterio di controllo applicazioni, HRESULT 0x800711C7.
SQLite integration, Semantic projection e Local edits and view state non hanno potuto
esercitare la candidata. Non si trasferisce il precedente PASS al nuovo codice.
Il criterio Windows non è stato modificato, disabilitato o aggirato.
La compilazione separata verifica solo correttezza statica, non sostituisce l'esecuzione.

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

## Test aggiunti ma ancora da eseguire in .NET
LocalEditTests copre identità e coordinate, rappresentazioni multiple, gruppo/figli,
pin, replay e revisione, no-op, undo/redo dopo riapertura, invalidazione redo,
isolamento mappa, coordinate invalide, limite cronologia, rollback tramite trigger,
migrazione v2, cache dopo move/undo, selezione oltre pagina e ViewState invalido.

EditingSmoke usa Input.dispatchMouseEvent di WebView2 per input reale del renderer,
non una chiamata diretta al comando C#. Copre drag, Esc, pulsanti undo/redo,
errore SQLite iniettato solo nel DB temporaneo, pin e riapertura della finestra.
Questi sono scenari implementati, NON risultati PASS.

ScaleSmoke crea una copia coerente del database sorgente tramite SQLite BackupDatabase.
Le nuove prove di modifica/undo/cache/ViewState operano soltanto sulla copia temporanea.
Verifica esattamente 300000 Idea prima delle prove; poi esegue le misure WPF esistenti.
Non si dichiara alcuna nuova prestazione prima dell'esecuzione.

## Ripresa
Una volta risolto il criterio Windows per i binari di sviluppo tramite la procedura
autorizzata del proprietario del PC, dalla worktree eseguire:

    ./scripts/build.ps1
    dotnet run --project tests/Nodilume.Smoke -c Release --no-build

Generare una fixture sintetica dedicata, senza usare mappe personali:

    dotnet run --project tools/Nodilume.Benchmarks -c Release -- generate --size 300000 --seed 20260918 --out C:\Temp\graph05-300000.sqlite
    dotnet run --project tests/Nodilume.ScaleSmoke -c Release --no-build -- C:\Temp\graph05-300000.sqlite C:\Temp\graph05-scale.json

Registrare SHA effettivo, risultati e limiti; correggere eventuali fallimenti reali prima del PASS.
I risultati GRAPH.04 non sono una certificazione della candidata GRAPH.05.

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
