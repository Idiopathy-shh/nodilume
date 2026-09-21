# GRAPH.05 - consegna candidata
Data: 21 settembre 2026.
Repository: Idiopathy-shh/nodilume.
Branch: feat/graph-05-editing-viewstate.
Worktree: C:\Sviluppo\Nodilume-graph05.
Base: 0f6bdac9d988bb1a6a6c3ddb8b3f71d02bd872f5.
Commit funzionale: 75655f7985326ee83d67aeed68351793598c8b61.

## Stato
Implementazione candidata committata. PR da mantenere DRAFT.
NON PASS complessivo: runtime .NET bloccato dal criterio Windows 0x800711C7
sul caricamento di Nodilume.Application.dll. Nessun merge autorizzato da questa consegna.
GRAPH.06 non iniziata.

## Lavoro recuperato e completato
Riprese le modifiche non committate lasciate dalla chat precedente: piano, contratti,
migrazione v3, persistenza delle modifiche e resolver vista.
Completati collegamento viewer/host, drag con anteprima e rollback, pin,
undo/redo persistente, salvataggio camera e selezione, chiusura correlata al salvataggio.
Corretti replay che arretravano revisione/stato cronologia e doppia chiusura.
Aggiunti test C#/SQLite, test viewer e percorsi smoke reali con riapertura.

## Evidenze disponibili
13/13 test viewer PASS; TypeScript/bundle PASS.
Build Tests/Desktop/Smoke/ScaleSmoke senza errori/avvisi.
La suite .NET sulla candidata riporta 1/4 gruppi PASS: gli altri sono BLOCCATI
prima dell'esercizio del codice da FileLoadException 0x800711C7.
Smoke e scale-smoke candidati NON ESEGUITI per tale dipendenza bloccata.
Nessuna misura prestazionale nuova e nessuna accettazione manuale dichiarata.
Vedere docs/validation/graph-05.md per comandi e dettagli.

## Contratti
Drag: Maiusc+sinistro, solo Placement del contesto corrente, piano camera.
Pin: sblocco esplicito prima del drag; nessun layout automatico aggiunto.
Undo/redo persistente per mappa, 100 operazioni; ricevute recenti 1000.
Camera salvata ogni 500 ms se cambiata e alla chiusura, separata dalla revisione.
Nessun reparent, creazione/cancellazione Idea/Relation, AI o funzionalità GRAPH.06.

## Prossimo passo
Risolvere il criterio di esecuzione Windows tramite gestione autorizzata del PC.
Non aggirarlo cambiando percorsi/binari o disattivando protezioni.
Poi eseguire build completa, suite, smoke GRAPH.03+05, scale-smoke 300k,
correggere eventuali errori e completare il playbook manuale.
Non assegnare GRAPH.06 e non dichiarare GRAPH.05 chiusa prima di queste evidenze.
GitHub è autorevole per SHA finale, URL PR e stato.
