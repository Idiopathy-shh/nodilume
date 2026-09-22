# GRAPH.06.04 — piano editor relazioni

Data: 22 settembre 2026.
Base: main@fdd65e06ffb8f2f3837262d5b1e93fda888a6baa.
Branch: feat/graph-06-04-relation-editor.
Worktree: C:\Sviluppo\Nodilume-graph06-04.

## Obiettivo

Aggiungere l'editing esplicito delle Relation concettuali fra Idea della stessa
mappa personale: creazione, cambio estremi, inversione, direzione, tipo,
spiegazione ed eliminazione confermata. La patch resta indipendente da GRAPH.05.

## Contratti

- C# e SQLite restano autorevoli; ogni scrittura usa mapId e revisione attesa.
- Gli estremi devono esistere nella stessa mappa e devono essere Idea distinte.
- Il tipo contiene 1–80 caratteri stampabili; la spiegazione massimo 4000.
- Un no-op non incrementa la revisione, ma controlla comunque la revisione attesa.
- Update e delete conservano l'identità Relation; delete non rimuove Idea o Placement.
- Le scritture sono atomiche nella transazione già usata da IMapStore.ApplyAsync.
- Le letture interattive sono mirate; nessun LoadGraphAsync nel flusso WPF.

## UX

Il nodo selezionato può essere catturato come origine o destinazione anche dopo
navigazione semantica. Gli estremi possono essere scambiati. Le relazioni che
toccano l'Idea selezionata sono elencate fino a 64 risultati con stato partial
esplicito. Una relazione esistente può essere modificata o eliminata con due click.
Dopo ogni commit il catalogo e la proiezione 3D vengono aggiornati.

## Verifica

Test su SQLite reale per create/update/inversione/no-op/stale/cross-map/delete.
Smoke WPF/WebView2 su mappe temporanee per selezione dalla scena, rendering,
persistenza, isolamento, doppia conferma e riapertura. Build senza warning,
git diff --check e documentazione di validazione/handoff.

Fuori scope: ricerca UI, file dialog import/export, backup/recovery, cancellazione
di Idea/Placement, GRAPH.05 drag/pin/undo e migrazione SQLite v3.