# GRAPH.06.08 - validazione editor integrato

Data: 23 settembre 2026. Macchina: OFFICE-PC.
Base: main@3e64b0d7f7514b82fd41b2bdd242ef56f2accf83.
Stato: candidata; PR #21.

## Esiti osservati

| Verifica | Esito |
|---|---|
| Viewer Node tests | PASS 14/14 |
| TypeScript e bundle | PASS |
| Build completa iniziale | PASS, 0 warning/errori |
| Suite .NET dopo il port | PASS 11/11 gruppi |
| Suite .NET dopo fix restore v3 | BLOCCATA da SAC 0x800711C7 |
| Smoke WPF/WebView2 finale | PASS, exit 0; rerun finale 83.36 s |
| Scale-smoke 300k finale | BLOCCATO da SAC |
| git diff --check | PASS |
Lo smoke finale copre: drag reale, Esc, rollback SQLite, Undo/Redo, pin,
chiusura/riapertura, cambio mappa con vista persistita, editor Idea/Relation,
ricerca, import/export e backup/recovery. Il restore di un database v3 con
cronologia e view-state e' passato dopo il rekey delle quattro nuove tabelle.

## Evidenza critica

La prima integrazione ha trovato due incompatibilita' reali:
1. gli smoke delle slice 06.02-06.05 assumevano sempre il ritorno alla Radice,
   mentre il nuovo contratto deve ripristinare contesto e selezione per mappa;
2. il restore GRAPH.06.07 non reindicizzava le tabelle SQLite v3 e falliva con
   FOREIGN KEY constraint failed.

Entrambe sono corrette e coperte dallo smoke finale.

## Limiti

La suite .NET finale e lo scale-smoke ricompilato non sono stati eseguiti
perche' Smart App Control ha bloccato i relativi binari non firmati. La suite
11/11 era PASS prima dell'ultimo fix; il fix restore v3 e' pero' esercitato
dal successivo smoke WPF completo. Il precedente ramo GRAPH.05 aveva gia'
scale-smoke 300k PASS sul codice editing isolato, ma tale risultato non prova
da solo la combinazione attuale. Accettazione visuale manuale non eseguita.
Nessuna protezione Windows e' stata disattivata o aggirata.
