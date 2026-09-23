# GRAPH.06.08 - chiusura editor e integrazione

Data: 23 settembre 2026.
Base: main@3e64b0d7f7514b82fd41b2bdd242ef56f2accf83.
Branch: feat/graph-06-08-editor-closure.
Worktree: C:\Sviluppo\Nodilume-graph06-08.

## Decisione

La PR #6 GRAPH.05 non viene mergiata: e' DRAFT, conflittuale con main e il
suo smoke Undo/Redo non aveva raggiunto PASS. GRAPH.06.08 porta i soli
contratti funzionali su main corrente, integra le slice 06.02-06.07 e li
rivalida end-to-end. Dopo l'integrazione, PR #6 e' stata chiusa come
superseded, senza trasformarla retroattivamente in PASS.

## Scope

- Maiusc + drag di Placement sul piano della camera, con Esc/capture-lost.
- Pin/sblocco persistente e rifiuto esplicito del drag su nodo fissato.
- Undo/Redo transazionale, persistente per mappa e bounded a 100 operazioni.
- Ricevute idempotenti, revisione ottimistica e rollback degli errori.
- View-state per mappa: contesto, selezione, pagina, camera, target e up.
- Migrazione SQLite v3 additiva; nessun reset di database personali.
- Integrazione con gestione mappe, editor, ricerca, file e backup/recovery.
- Restore v3 con rekey di graph_edits, edit_cursor, edit_receipts e view_state.
- Smoke aggiornati affinche' il cambio mappa ripristini la vista persistita.

## Esclusioni

Nessun layout automatico, reparenting, merge di PR #6, cifratura/firma backup,
cloud sync o modifica a Smart App Control. La firma RSA resta un tema release.

## Gate

- Viewer Node/TypeScript e build completa senza warning/errori.
- Suite .NET, se ammessa dalla policy Windows.
- Smoke WPF/WebView2 completo: editing, riapertura e regressioni 03/06.02-06.07.
- Scale-smoke 300k per editing/cache/view-state, se ammesso dalla policy.
- git diff --check e worktree pulita prima della pubblicazione.
- Accettazione manuale distinta dal PASS automatico.

## Rischi

Il port e' ampio e tocca input, persistenza e lifecycle della finestra. Il
vantaggio e' eliminare la divergenza fra GRAPH.05 e l'editor attuale; il costo
e' una migrazione v3 irreversibile verso il basso e maggior complessita' nel
restore. I backup v1/v2 restano supportati; schemi futuri continuano a essere
rifiutati invece di essere reinterpretati.
