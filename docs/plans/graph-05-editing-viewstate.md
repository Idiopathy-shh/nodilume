# GRAPH.05 — piano esecutivo (21 settembre 2026)
Base: main@0f6bdac9d988bb1a6a6c3ddb8b3f71d02bd872f5 (PR #5 merged).
Branch: feat/graph-05-editing-viewstate; worktree: C:\Sviluppo\Nodilume-graph05.
Nessuna PR concorrente o branch GRAPH.05 rilevati al bootstrap.
Baseline: viewer 9/9, build .NET senza warning; esecuzione Nodilume.Tests bloccata
da criterio di controllo applicazioni Windows 0x800711C7. Non disabilitare protezioni.
## Contratti
C# e SQLite autorevoli su Placement e ViewState; viewer autorevole sull'anteprima,
camera e input. GRAPH.05 non cambia parent o Idea/Relation.
Drag: Shift + tasto sinistro su un nodo fratello del contesto attivo; senza Shift
restano rotazione/clic/doppio clic, tasto destro pan. Piano perpendicolare alla camera
passante per la posizione iniziale; offset iniziale conservato, conversione in
coordinate locali sottraendo il frame origin e il parent globale.
Nodo fissato: trascinamento manuale rifiutato visibilmente; sbloccare prima.
Esc/capture lost/cambio contesto annullano il gesto; niente zoom semantico durante drag.
Un rilascio valido = un solo comando, successo autorevole dopo commit; errori
ricaricano la proiezione; revisioni/identificatori transazione impediscono duplicati.
## Persistenza e cronologia
Migrazione v3 additiva: tabella view_state per mapId, tabella graph_edits per mapId
con colonne before/after, cursore redo edit_cursor per mappa e tabella edit_receipts.
Cronologia persistente bounded; massimo 100 operazioni, una sola transazione per
modifica/undo/redo, revisione map incrementata solo per grafi mutati, camera separata.
Concorrenza ottimistica sul revisione; no-op e comandi falliti non creano cronologia.
Undo/redo locale per singolo Placement, senza LoadGraphAsync.
Il cursore redo è invalidato da nuova modifica, anche quando si riapre il database.
Layout automatico non presente nei flussi correnti: nessun riordino inventato in GRAPH.05;
pin persistito è rispettato dall'unico drag manuale e da futuri layout.
## Vista
Stato per mappa con path Placement, camera/target locali, selezione, cursore paging.
Persistenza debounced e on close; no revisione grafo; validazione di finitezza,
mappa, antenati e parent; ripiego ad antenato esistente o overview; selezione
oltre prima pagina ripristinata tramite cursor id precedente o riserva proiezione.
## Verifiche / consegna
Test di regressione su fixture temporanee: coordinate locali, Idea multi-Placement,
gruppo, pin, undo/redo, redo invalidato, stale revision, comandi duplicate, rollback,
migrazione v2->v3 e ViewState invalido; test viewer delle trasformazioni input.
Smoke WPF/WebView2 con gesto reale e riapertura, se esecuzione consentita.
Ripetere scale-smoke 300k con fixture sintetica e registrare LIMITI o blocchi.
Aggiornare README, roadmap, coordination; validation e handoff GRAPH.05.
Pubblicare PR verso main, DRAFT se verifica obbligatoria mancante; non fare merge.

## Ripresa diretta della coordinatrice
Ripreso il lavoro non committato; baseline parziale inizialmente PASS, poi criterio
Windows 0x800711C7 ricomparso sulla DLL Application candidata. Stato finale:
viewer 13/13 e compilazione PASS, runtime NON VALIDATO. Non modificare protezioni.
Camera comprende up; salvataggio limitato a 500 ms e chiusura correlata.
Riordino automatico non introdotto, non esistendo un percorso automatico corrente.
