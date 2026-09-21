# GRAPH.05 - consegna candidata
Data: 21 settembre 2026.
Repository: Idiopathy-shh/nodilume.
Branch: feat/graph-05-editing-viewstate.
Worktree: C:\Sviluppo\Nodilume-graph05.
Base: 0f6bdac9d988bb1a6a6c3ddb8b3f71d02bd872f5.
Commit funzionale: 75655f7985326ee83d67aeed68351793598c8b61.

## Stato
Implementazione candidata committata. PR #6 da mantenere DRAFT: NON PASS complessivo.
Su OFFICE-PC la suite .NET 4/4 e lo scale-smoke 300k sono PASS; lo smoke UI
GRAPH.03+05 non e' ancora PASS (attese Undo/Redo, poi blocco SAC della DLL ricompilata).
Non effettuare merge; GRAPH.06 non iniziata.

## Lavoro recuperato e completato
Riprese le modifiche non committate lasciate dalla chat precedente: piano, contratti,
migrazione v3, persistenza delle modifiche e resolver vista.
Completati collegamento viewer/host, drag con anteprima e rollback, pin,
undo/redo persistente, salvataggio camera e selezione, chiusura correlata al salvataggio.
Corretti replay che arretravano revisione/stato cronologia e doppia chiusura.
Aggiunti test C#/SQLite, test viewer e percorsi smoke reali con riapertura.

## Evidenze disponibili (riesecuzione OFFICE-PC, 21/09/2026)
13/13 test viewer PASS; TypeScript/bundle PASS e build .NET senza errori/avvisi.
Suite .NET sulla candidata: 4/4 gruppi PASS, exit code 0.
Scale-smoke 300k: PASS su copia SQLite sintetica, exit code 0;
prima vista utile 4004.4717 ms, frame p95 16.7 ms, selezione p95 13 ms.
JSON misure: C:\Temp\nodilume-graph05-scale-20260921.json.
Smoke reale: sequenza arrivata fino a Undo/Redo ma FAIL su attesa UI.
Diagnostica e attese dei pulsanti aggiunte e compilate; nuova esecuzione BLOCCATA
da Smart App Control (Nodilume.Smoke.dll, 0x800711C7), non PASS.
Nessuna accettazione manuale dichiarata. Dettagli in docs/validation/graph-05.md.

## Contratti
Drag: Maiusc+sinistro, solo Placement del contesto corrente, piano camera.
Pin: sblocco esplicito prima del drag; nessun layout automatico aggiunto.
Undo/redo persistente per mappa, 100 operazioni; ricevute recenti 1000.
Camera salvata ogni 500 ms se cambiata e alla chiusura, separata dalla revisione.
Nessun reparent, creazione/cancellazione Idea/Relation, AI o funzionalità GRAPH.06.

## Prossimo passo
Continuare ESCLUSIVAMENTE su OFFICE-PC e sulla worktree esistente.
Consentire l'esecuzione delle DLL di sviluppo con una soluzione autorizzata,
senza cambiare percorsi/binari per eludere SAC o disattivare le protezioni.
Rieseguire lo smoke completo con le attese dei pulsanti, indagare eventuali
mancati comandi Undo/Redo, correggere errori e completare accettazione manuale.
Ripetere scale-smoke se cambia codice funzionale; altrimenti il PASS e' registrato.
Non assegnare GRAPH.06 e non mergiare GRAPH.05 fino ai gate PASS.
GitHub resta autorevole per SHA finale, URL PR e stato.
