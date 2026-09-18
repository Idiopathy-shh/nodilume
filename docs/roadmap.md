# Nodilume — roadmap

Stato 18 settembre 2026: GRAPH.00-01 completate. GRAPH.02 integrata con squash merge PR #2
(`5272f297bc58d4f1b69b17ad1eb2f236ec40c4db`). GRAPH.03 è implementata sul branch
`feat/graph-03-semantic-zoom` e ha PASS tecnico automatico + smoke WPF/WebView2 reale;
PR #3 è pubblicata come draft. Restano accettazione visiva dell'utente e integrazione.
GRAPH.04-08 non sono iniziate.

## Roadmap

| Fase | Consegna | Stato / criterio di uscita |
|---|---|---|
| GRAPH.00 | Repository dedicata, specifica, verifica ambiente e convenzioni | Completata |
| GRAPH.01 | Finestra Windows e scena 3D locale | Completata e accettata |
| GRAPH.02 | Identità, rappresentazioni, contenimento e SQLite | Integrata tramite PR #2 |
| GRAPH.03 | Zoom semantico continuo e collegamenti aggregati | Implementazione e PASS tecnico completati; richiede ancora accettazione visiva e merge |
| GRAPH.04 | Caricamento selettivo e prove di scala | Non iniziare prima della chiusura GRAPH.03; report 10k/100k/300k con limiti espliciti |
| GRAPH.05 | Spostamento, fissaggio, undo e ripristino | Non implementata |
| GRAPH.06 | Primo editor personale completo | Non implementata |
| GRAPH.07 | Automazioni AI | Non implementata |
| GRAPH.08 | Modalità libri | Non implementata |

## GRAPH.03 — checkpoint tecnico

Completati: contratti di proiezione contestuale, query limitate con antenati indipendenti
dalla pagina, frame locali, aggregazione Relation con conteggio/provenienza, soppressione
dei falsi self-link, direzioni opposte distinte, destinazioni multiple esplicite,
navigazione trasversale/ritorno, protocollo v2 requestId/map/revision/context,
cancellazione/risposte obsolete, transizioni continue, isteresi, riframing camera,
stati partial/leaf/empty/error e smoke Windows su fixture dedicata.

Il PASS tecnico non chiude la fase. Il criterio originario resta: entrata/uscita e
navigazione trasversale devono essere accettate visivamente dall'utente. La PR deve
restare draft fino a tale prova e non autorizza GRAPH.04.

Ogni fase richiede verifiche sui rischi concreti introdotti. Una fase non è conclusa
per il solo fatto che il codice compila.
