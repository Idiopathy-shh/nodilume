# GRAPH.06.08 - risultato

Data: 23 settembre 2026.
Branch: feat/graph-06-08-editor-closure.
Base: main@3e64b0d7f7514b82fd41b2bdd242ef56f2accf83.
PR: #21.

## Consegnato

- Drag/pin con input reale e rollback autorevole.
- Undo/Redo persistente, idempotente e limitato per mappa.
- View-state persistito e ripristinato tra mappe e riaperture.
- SQLite v3 additivo per cronologia, ricevute, cursore e vista.
- Compatibilita' restore v3 con nuova identita' MapId.
- Regressioni integrate con tutte le slice GRAPH.06 gia' in main.

## Verifica

Viewer 14/14; build completa 0 warning/errori; suite .NET 11/11 prima
dell'ultimo fix; smoke WPF/WebView2 finale PASS. Rerun finale .NET e scala
300k bloccati da Smart App Control 0x800711C7, senza modificare la policy.
## Pro e contro

Pro: chiude il gap funzionale dell'editor senza mergiare una PR obsoleta e
conflittuale; le operazioni restano atomiche e isolate per mappa. Il test
end-to-end ha scoperto e corretto anche una regressione backup v3.

Contro: il gate scala combinato non ha evidenza finale a causa di SAC; la
migrazione v3 aumenta la superficie da mantenere in ogni futura operazione di
backup/migrazione. Non esiste layout automatico e l'accettazione manuale resta
separata.

## Chiusura GRAPH.05

Dopo l'eventuale merge di GRAPH.06.08, chiudere PR #6 come superseded con un
commento che rimandi alla nuova PR e allo squash. Non mergiare PR #6 e non
attribuirle retroattivamente il PASS ottenuto dalla nuova base.
