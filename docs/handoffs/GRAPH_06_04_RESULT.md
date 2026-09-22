# GRAPH.06.04 — handoff editor relazioni

Data: 22 settembre 2026.
Repository: Idiopathy-shh/nodilume.
Branch: feat/graph-06-04-relation-editor.
Worktree: C:\Sviluppo\Nodilume-graph06-04.
Base: main@fdd65e06ffb8f2f3837262d5b1e93fda888a6baa.
PR #13 MERGED con squash main@550ce57916d274b12606ce60f97f33a7d9f10d7a.

## Consegna

Editor WPF per creare, selezionare, invertire, aggiornare ed eliminare Relation
fra Idea della mappa attiva. Tipo, direzione e spiegazione sono persistenti.
Gli estremi vengono scelti dalla selezione reale della scena WebView2 e possono
trovarsi in contesti diversi. Delete richiede doppia conferma.

Lato applicazione/persistenza sono aggiunti accesso mirato per Relation,
UpdateRelationChange e RemoveRelationChange nella transazione revisionata.
Nessun caricamento globale nel flusso editor e nessuna dipendenza da GRAPH.05.

## Evidenze

Viewer 9/9 PASS; TypeScript/bundle PASS.
Suite .NET 7/7 PASS, inclusi test SQLite dell'editor Relation.
Build Tests/Desktop/Smoke/ScaleSmoke/Benchmarks PASS con 0 warning/errori.
Smoke WPF/WebView2 GRAPH.03 + 06.02 + 06.03 + 06.04 PASS, exit code 0.
Fixture e profili temporanei; nessuna mappa personale letta.
Accettazione manuale non dichiarata.

Dettagli: docs/validation/graph-06-04.md.
Piano: docs/plans/graph-06-04-relation-editor.md.

## Limiti e seguito

Elenco relazioni limitato a 64 per Idea con stato partial esplicito.
Ricerca UI, file import/export, backup/recovery e integrazione GRAPH.05 restano
patch separate. GRAPH.05 PR #6 rimane Draft con gate propri di firma/smoke.