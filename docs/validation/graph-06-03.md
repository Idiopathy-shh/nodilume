# GRAPH.06.03 — editor idee e nodi: validazione OFFICE-PC

Data 21/09/2026. Repository Idiopathy-shh/nodilume.
Worktree C:\Sviluppo\Nodilume-graph06-03; branch feat/graph-06-03-idea-node-editor.
Base main@06d92e744e84dbaa4cd8b704ec291096ad3208ab (GRAPH.06.02 integrata).
PR #11 MERGED, squash 9478973e7a5be33c5487499e6e3ab560c342267e.
GRAPH.05 PR #6 resta DRAFT; non e' stata incorporata in questa slice.

## Funzionalita' realmente implementate
Editor WPF a destra della scena 3D: la selezione di un nodo visibile
invia un messaggio versionato al Desktop, che legge soltanto l'Idea e il
Placement appartenenti alla mappa corrente. Nuova radice, nuova Idea figlia
del nodo selezionato, modifica di titolo/contenuto condivisi, annotazione
locale e nuova rappresentazione della stessa Idea nel contesto corrente.
Il refresh dopo commit avviene tramite richiesta correlata nel renderer,
con verifica revisione/mapId e invalidazione cache della proiezione.
Ogni scrittura usa la revisione attesa ed e' atomica nella transazione
SQLite; gli ID Idea e Placement non vengono confusi. Coordinate iniziali
locali limitate, per evitare che tutti i nuovi nodi si sovrappongano.
Nessun caricamento globale delle idee sui percorsi interattivi; letture
puntuali per i riferimenti coinvolti.

## Evidenze riproducibili — esecuzione OFFICE-PC
`./scripts/build.ps1`: PASS, Node viewer 9/9, TypeScript e bundle PASS;
.NET 6/6 gruppi PASS (Domain, SQLite, Semantic, Portable JSON, Catalogo
multi-mappa e nuovo editor di Idea/Placement). Build Release test, Desktop,
Smoke, ScaleSmoke e benchmark PASS, 0 errori e 0 warning.
`dotnet run --project tests/Nodilume.Smoke -c Release --no-build`: PASS
exit code 0. GRAPH.03 e GRAPH.06.02 restano PASS; il nuovo smoke WPF/WebView2
crea un'Idea radice e figlia, sceglie nodi dalla scena, modifica contenuto
Idea, salva annotazione locale, aggiunge seconda rappresentazione, verifica
che la modifica condivisa appaia in entrambe, cambia mappa e riapre la
finestra. Controlla stato della scena e database SQLite temporanei reali.
I test del nucleo rifiutano revisioni stale incluse modifiche no-op,
parent/Idea di altre mappe, ciclo concettuale nella catena degli antenati,
titoli vuoti e annotazioni oltre il limite; confermano atomicita', contenuti
condivisi e annotazioni locali indipendenti.

`dotnet run --project tests/Nodilume.ScaleSmoke -c Release --no-build --
C:\Temp\nodilume-graph04-bench\graph04-300000.sqlite
C:\Temp\nodilume-graph06-03-scale-20260921.json`: PASS, exit code 0.
Prima vista utile 4156.1699 ms, frame p95 16.7 ms, final p95 16.7 ms,
selezione p95 14.0 ms; nessuna crescita monotona della memoria nel
campionamento. Sono misure di una esecuzione su OFFICE-PC, non SLA.
Lo scale-smoke verifica la regressione della visualizzazione sulla
fixture sintetica 300k, NON uno stress test di editing 300k.

## Gate separati e limiti
Non e' stato eseguito il playbook visivo manuale da parte dell'utente.
Non sono compresi: cancellazione Idea/Placement, riordino/reparent via UI,
editor Relation, ricerca UI, import/export file, backup/recovery, AI.
Le rappresentazioni si aggiungono nel contesto corrente; un'Idea non
puo' ricomparire lungo il proprio percorso di antenati. Limiti di input:
titolo 160 caratteri, contenuto 200000, annotazione 4000.
La cronologia e il salvataggio della camera di GRAPH.05 non sono inclusi;
lo smoke Undo/Redo GRAPH.05 resta indipendente e non validato.
Non sono stati letti o modificati database personali, chiavi o token,
ne' alterate le protezioni di Windows o utilizzato un altro PC.
