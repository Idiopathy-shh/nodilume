# Nodilume — roadmap

Stato 18 settembre 2026: GRAPH.00-01 completate. GRAPH.02 integrata con squash merge PR #2 (5272f297bc58d4f1b69b17ad1eb2f236ec40c4db), dopo nuova verifica tecnica e autorizzazione esplicita dell'utente. GRAPH.03 pronta per assegnazione tramite docs/handoffs/BOOTSTRAP_NODILUME_GRAPH_03.md; implementazione e accettazione visiva ancora da eseguire. GRAPH.04-08 non implementate.

## 11. Roadmap

Questa è una roadmap di consegne, non ancora un piano esecutivo con tutte le modifiche al codice.

| Fase | Consegna | Criterio di uscita |
|---|---|---|
| GRAPH.00 | Repository dedicata, specifica, verifica ambiente e convenzioni | Build riproducibile prevista, ambiente Windows identificato, dati personali esclusi da git |
| GRAPH.01 | Finestra Windows e scena 3D locale | Rotazione, movimento e focus funzionanti offline |
| GRAPH.02 | Identità, rappresentazioni, contenimento e SQLite | Invarianti e persistenza verificate con fixture piccole |
| GRAPH.03 | Zoom semantico continuo e collegamenti aggregati | Entrata/uscita e navigazione trasversale accettate dall’utente |
| GRAPH.04 | Caricamento selettivo e prove di scala | Report 10k/100k/300k con risultati e limiti espliciti |
| GRAPH.05 | Spostamento, fissaggio, undo e ripristino | Modifiche locali persistenti e orientamento conservato; chiusura del prototipo |
| GRAPH.06 | Primo editor personale completo | Gestione mappe, nodi, relazioni, ricerca, import/export e recupero |
| GRAPH.07 | Automazioni AI | Comandi strutturati, provenienza, annullamento del lotto e protezione delle modifiche manuali |
| GRAPH.08 | Modalità libri | EPUB e materiali cartacei acquisiti; capitoli, riferimenti e copertura parziale esplicita |

Ogni fase richiede solo le verifiche che coprono rischi concreti introdotti. Una fase non è conclusa per il solo fatto che il codice compila. Il piano esecutivo iniziale coprirà GRAPH.00–GRAPH.01; i successivi dipenderanno dai risultati verificati, mantenendo i contratti sopra descritti.

