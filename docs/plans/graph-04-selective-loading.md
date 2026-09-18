# GRAPH.04 — caricamento selettivo e prove di scala

Data: 18 settembre 2026.
Repository: `Idiopathy-shh/nodilume`.
Branch: `feat/graph-04-selective-loading`.
Worktree: `C:\Sviluppo\Nodilume-graph04`.
Base effettiva: `1690c35fcde8f6e51bb17fda7e4f32da8c76d8a2`.

## Baseline verificata

- PR #3 GRAPH.03 integrata; PR #4 documentale aperta/draft e non incorporata.
- Nessun incarico GRAPH.04 concorrente; nessun `AGENTS.md` applicabile.
- Viewer 9/9 PASS; test .NET 3/3 gruppi PASS.
- Build Release: 0 errori, 0 avvisi; ~26,3 s nella sessione di bootstrap.
- Smoke WPF/WebView2 GRAPH.03: PASS; runtime WebView2 153.0.4234.32; ~16,5 s.
- Demo/database personali non usati per benchmark.

## Colli di bottiglia e rischi osservati

1. `ReadChildrenAsync` è limitata ma non espone cursore: i figli oltre la prima pagina risultano partial ma non raggiungibili.
2. `SceneService` ha un unico insieme di limiti; non separa budget di nodi, link ed etichette.
3. La proiezione Relation dipende da un sottoalbero limitato e da limiti globali: serve paginazione/correttezza di aggregazione senza perdite o duplicazioni.
4. Le destinazioni con Placement multipli possono richiedere molte letture di antenati; serve cache limitata, revision-aware e invalidabile solo dove misurata.
5. Il viewer crea DOM label per ogni nodo della proiezione e aggiorna tutte le label ogni frame: il budget etichette deve essere esplicito.
6. La specifica richiede ricerca indicizzata ma l'API applicativa corrente non espone una query di ricerca misurabile.

## Piano di implementazione

1. Introdurre contratti di pagina stabili (keyset cursor) per figli e letture pertinenti, preservando ordinamento deterministico e isolamento mappa.
2. Estendere richiesta/proiezione con stato di paginazione esplicito: cursore successivo, conteggio noto e comando per ottenere pagine successive senza perdere contesto/selezione/ritorno.
3. Separare budget per nodi, link ed etichette; il viewer renderizza etichette solo entro il budget dichiarato e segnala le omissioni.
4. Rendere le Relation corrette attraverso pagine: nessuna relazione deve essere duplicata o persa per effetto del caricamento progressivo; provenienza invariata.
5. Aggiungere cache applicativa LRU limitata per risultati costosi solo se i benchmark baseline ne mostrano il beneficio. Chiave minima: mapId, revision, contesto, cursore e limiti; invalidazione su revisione/cambio mappa; rilascio esplicito.
6. Esporre ricerca indicizzata su titolo con ordinamento stabile e limite/cursore, usando l'indice SQLite già presente o una modifica schema motivata da EXPLAIN/measure.
7. Aggiungere test mirati per paginazione, accessibilità oltre prima pagina, budget, partial/empty/error, cache/revisioni, cancellazione/risposte obsolete e aggregazioni.
8. Estendere smoke WPF/WebView2 con fixture di scala temporanea, senza toccare la demo.
9. Creare `tools/Nodilume.Benchmarks` per dataset e misure riproducibili 10k/100k/300k, con seed e metadati registrati.

## Dataset sintetici

Generatori deterministici separeranno Idea, Placement, Relation e oggetti visibili.
Profili da coprire: sparse, wide/hub, deep e multi-placement. Il dataset principale userà una combinazione documentata e manterrà la stessa difficoltà relativa fra 10k, 100k e 300k.
I database generati resteranno fuori da git; nel repository entreranno solo generatore, comandi, configurazioni e risultati testuali/CSV ragionevoli.

## Metodo benchmark

- Eseguire warm-up esplicito e più ripetizioni; riportare mediana e p95 quando applicabile.
- “Cold process” significa nuovo processo; non verrà chiamata “cache disco fredda” una semplice riapertura.
- Misurare separatamente apertura/prima vista utile, query SQLite, costruzione proiezione, serializzazione/bridge, scena viewer, selezione e ricerca.
- Per frame time raccogliere campioni nel viewer durante un percorso deterministico e calcolare p95.
- Per memoria osservare l'intero albero WPF/WebView2 prima/dopo percorsi ripetuti e dopo rilascio cache.
- Registrare CPU, GPU, RAM, risoluzione, .NET, Node e WebView2 della macchina di prova.

## Obiettivi e classificazione esiti

Obiettivi di lavoro: p95 frame <= 33 ms; selezione residente <= 100 ms; ricerca p95 <= 1 s; prima vista utile 300k <= 5 s; memoria senza crescita monotona nei percorsi ripetuti.
Una soglia mancata sarà riportata FAIL rispetto all'obiettivo; una misura non ottenuta sarà NON MISURATA. Correttezza, prestazioni e verifica visiva resteranno separate.

## Confini

- Nessun caricamento globale obbligatorio e nessuna simulazione fisica globale.
- Nessuna modifica alla gerarchia persistente per semplificare il rendering.
- Nessuna scrittura di coordinate persistenti durante zoom/navigazione.
- Nessun drag/pin, undo/redo o camera persistente (GRAPH.05).
- Nessun editor completo (GRAPH.06), AI, libri o cloud.
- Nessun merge: la consegna finale è una PR verificata verso `main`.

## Sequenza di commit prevista

1. Piano e contratti/paginazione SQLite.
2. Proiezione progressiva, budget e viewer.
3. Cache/ricerca solo dopo misura.
4. Generatore/benchmark e smoke scala.
5. Documentazione di validazione, benchmark e handoff finale.
