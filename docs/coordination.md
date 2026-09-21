# Nodilume - coordinamento
Checkpoint: 21 settembre 2026.
Questa chat mantiene coordinamento e ha preso direttamente in carico GRAPH.05
su richiesta dell'utente, dopo i problemi della chat web implementatrice.

## Stato verificato
- GRAPH.00-03 integrate.
- GRAPH.04 integrata: PR #5, main 0f6bdac9d988bb1a6a6c3ddb8b3f71d02bd872f5.
- PR documentale #4 chiusa, contenuti precedenti incorporati in GRAPH.04.
- GRAPH.05 candidata su feat/graph-05-editing-viewstate, C:\Sviluppo\Nodilume-graph05.
- GRAPH.05 NON PASS complessivo: runtime bloccato da criterio Windows 0x800711C7.
- GRAPH.06-08 non iniziate.

## Evidenze GRAPH.05
Viewer 13/13 PASS; TypeScript/bundle e build .NET PASS senza errori/avvisi.
Suite .NET candidata 1/4 gruppi PASS; altri tre bloccati al caricamento DLL.
Smoke reale e scale-smoke candidati non eseguiti; accettazione manuale non eseguita.
Dettagli: docs/validation/graph-05.md e docs/handoffs/GRAPH_05_RESULT.md.

## Assegnazione
Un solo incarico GRAPH.05: riprendere la worktree esistente, non creare duplicati.
Le altre worktree restano separate e non vanno cambiate di branch.
Accesso: OFFICE-PC, GitHub CLI autenticata per Idiopathy-shh/nodilume.

## Regole
Registrare stato git, SHA e istruzioni applicabili prima di modificare.
Usare fixture temporanee e preservare database personali.
Nessun reset distruttivo, force-push o rebase autonomo.
Distinguere compilazione, test runtime, benchmark e accettazione visiva.
La candidata resta draft fino alla verifica dei gate mancanti; nessun merge da questo handoff.
GRAPH.04 mantiene limiti dichiarati: Relation parziali, misure specifiche del PC,
frame stabilizzati fuori transizioni e campioni memoria limitati.
