# Nodilume - bootstrap GRAPH.04
Data: 18 settembre 2026. Stato: incarico preparato, non ancora assegnato a una chat implementatrice.

## Mandato e baseline
Implementare caricamento selettivo e prove di scala; consegnare una PR verificata verso main, senza merge.
Repository: Idiopathy-shh/nodilume. Dispositivo: OFFICE-PC.
Baseline funzionale: main@1690c35fcde8f6e51bb17fda7e4f32da8c76d8a2, GRAPH.03 PR #3 integrata.
La chat corrente coordina architettura, scope, verifica e integrazione. Una sola chat implementatrice GRAPH.04.
Usare una worktree dedicata e branch feat/graph-04-selective-loading; non cambiare branch al checkout condiviso.
Prima di modificare: verificare AGENTS.md applicabili, stato git, fetch, main, PR aperte e assenza di incarichi concorrenti.
Leggere README.md, docs/coordination.md, docs/roadmap.md, docs/specs/graph-3d-design.md, docs/validation/graph-03.md e codice pertinente.
Registrare SHA effettivo e piano in docs/plans/graph-04-selective-loading.md prima delle modifiche funzionali.

## Scope
Conservare autorità C# su identità, contenimento, revisioni, aggregazioni e destinazioni; viewer autorevole solo per camera/rendering transitori.
Misurare prima i colli di bottiglia; introdurre query limitate/paginate, budget separati per nodi/archi/etichette e gestione esplicita dei risultati parziali.
Garantire accesso progressivo ai dati esclusi dal budget: non rendere irraggiungibili i figli oltre la prima pagina.
Gestire gruppi con molti figli, hub, gerarchie profonde e rappresentazioni multiple, senza cambiare la gerarchia persistente.
Aggiungere cache limitata e invalidabile solo dove giustificata dalle misure; definire chiavi mapId/revisione/contesto/parametri e politica di rilascio.
Conservare cancellazione, scarto risposte obsolete, ritorno, selezione e correttezza delle Relation aggregate, anche attraverso pagine e cache.
Nessun caricamento globale obbligatorio, nessuna simulazione fisica globale e nessuna riscrittura delle coordinate durante la navigazione.
La ricerca indicizzata è un obiettivo della specifica: dichiararne esplicitamente copertura e risultati, senza anticipare l'intero editor GRAPH.06.

## Prove riproducibili
Generatore deterministico di 10k, 100k e 300k Idea; registrare seed, Placement, Relation, profondità, grado massimo e dimensione DB.
Coprire reti sparse, hub, gerarchie profonde, molti figli e rappresentazioni multiple; documentare combinazioni effettivamente provate.
Misurare prove fredde/calde, apertura utile, query/proiezione/trasferimento/rendering, p95 frame time, selezione, ricerca e memoria dell'intero albero WPF/WebView2.
Definire numero di ripetizioni, warm-up, significato di freddo/caldo, percorso di navigazione e metodo di misura; includere dati grezzi e comandi.
Registrare CPU/GPU/RAM, risoluzione, runtime e budget visibili. Ripetere lo stesso percorso per verificare rilascio memoria e cache.
Obiettivi proposti, non già certificati: p95 frame <=33 ms, selezione residente <=100 ms, ricerca p95 <=1 s, prima vista utile 300k <=5 s.
Non ridurre silenziosamente dataset o densità per dichiarare PASS. Una misura mancante è NON MISURATA, una soglia mancata è FAIL rispetto all'obiettivo.
Separare correttezza, prestazioni e accettazione visiva. Non confondere tempi del solo backend con fluidità del viewer.

## Consegna e limiti
Baseline: scripts/build.ps1 e dotnet run --project tests/Nodilume.Smoke -c Release --no-build.
Aggiungere solo test sui rischi nuovi: paginazione completa, aggregazioni senza duplicati, cache/revisioni, budget e risposte superate.
Smoke Windows reale su fixture temporanee, preservando demo e database personali; ripetere navigazione GRAPH.03 con dati di scala.
Consegnare docs/benchmarks/graph-04.md, docs/validation/graph-04.md e docs/handoffs/GRAPH_04_RESULT.md, aggiornando README/roadmap/coordinamento.
Riportare SHA base/finale, PR, esiti, limiti e istruzioni di riproduzione. Working tree pulito e git diff --check.
Nessun reset, force-push o rebase autonomo. Nessuna modifica a QOE o ad altre repository.
Fuori scope: drag/pin, undo/redo e camera persistente (GRAPH.05), editor completo, AI, libri e cloud.
Pubblicare PR; la coordinatrice verifica autonomamente e gestisce l'integrazione secondo autorizzazione dell'utente.
