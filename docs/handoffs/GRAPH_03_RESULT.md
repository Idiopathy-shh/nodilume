# GRAPH.03 — result handoff

Data: 18 settembre 2026.
Repository: `Idiopathy-shh/nodilume`.
Checkout: `C:\Sviluppo\Nodilume`.
Branch: `feat/graph-03-semantic-zoom`.
Base SHA: `5b867ba2d67f2614cb19c98a0a06fed614a382fd`.
Implementation SHA: `feaf2d151821f966b66afdd0d9069fdbedc2ec2f`.
PR: https://github.com/Idiopathy-shh/nodilume/pull/3 (draft).
Non eseguire merge da questa chat. Non iniziare GRAPH.04.

## Risultato

GRAPH.03 è implementata tecnicamente. C# produce proiezioni contestuali coerenti con
mappa/revisione e mantiene autorità su identità, contenimento, Relation, aggregazioni e
destinazioni. Il viewer mantiene camera e animazioni transitorie.

Sono implementati:
- zoom semantico su nodo selezionato o puntato stabilmente;
- isteresi ingresso/uscita;
- transizioni invertibili da stato intermedio;
- frame locali senza scrittura delle coordinate persistenti;
- breadcrumb, Livello superiore, Panoramica e Ritorna;
- navigazione trasversale con apertura del percorso e focus destinazione;
- candidati espliciti quando una Idea ha più Placement;
- Idea senza Placement dichiarata non navigabile;
- Relation aggregate con conteggio e provenance;
- direzioni opposte distinte;
- nessun falso self-link per Relation interne;
- protocollo v2 con requestId/mapId/revision/context;
- cancellazione richieste superate e scarto risposte obsolete;
- ricostruzione del contesto dopo restart del viewer;
- stati loading/ready/partial/leaf/empty/error.

## Persistenza e query

Il database e lo schema persistente GRAPH.02 non vengono riscritti per lo zoom.
Sono state aggiunte query limitate per Placement, figli, antenati, child-count,
discendenti, Placement per Idea e Relation che toccano Idea. Gli antenati sono
recuperati indipendentemente dalla pagina dei figli; limite di profondità e contesto
mancante falliscono esplicitamente.

Non viene introdotto caricamento globale obbligatorio. I budget GRAPH.03 sono limitati e
un superamento viene dichiarato `partial`.

## Verifiche finali

```powershell
./scripts/build.ps1
dotnet run --project tests/Nodilume.Smoke -c Release --no-build
```

PASS:
- viewer tests 8/8;
- TypeScript/bundle;
- restore locked;
- .NET build 0 errori / 0 avvisi;
- Domain invariants;
- SQLite integration;
- Semantic projection;
- smoke WPF/WebView2 GRAPH.03;
- npm audit del restore eseguito da `npm ci`: 0 vulnerabilità riportate.

Lo smoke attraversa `Radice → Gruppo A → Sottogruppo A1`, seleziona una foglia,
verifica due collocazioni ambigue, Relation opposte, navigazione esterna a Gruppo B,
focus visibile, Ritorna, Livello superiore, Panoramica, resize e riapertura persistente.
Usa DB temporaneo e non modifica la demo personale.

Evidenza visuale automatica:
`artifacts/graph-03-smoke.png`.

## Accettazione ancora richiesta

Il PASS tecnico non equivale alla chiusura GRAPH.03. Prima del merge l'utente deve
eseguire il playbook in `docs/validation/graph-03.md` e confermare visivamente:
zoom lento/rapido, isteresi, inversione a metà transizione, doppio clic/Entra,
uscita, foglia, navigazione esterna/Ritorna e scelta fra rappresentazioni.

Fino a quella conferma la PR deve restare draft.

## Limiti

Non sono stati eseguiti né dichiarati benchmark 10k/100k/300k. Cache/ottimizzazione
estesa restano GRAPH.04. Drag/pin, undo/redo e camera persistente restano GRAPH.05.
Nessuna funzionalità editor completo, AI, libri o cloud è stata anticipata.

La coordinatrice deve verificare autonomamente PR, commit e test prima di proporre
l'integrazione. Nessun merge è autorizzato da questo handoff.
