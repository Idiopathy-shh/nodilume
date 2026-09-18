# Nodilume — roadmap

Stato 18 settembre 2026: GRAPH.00-03 integrate. GRAPH.03 PR #3 squash su main
`1690c35fcde8f6e51bb17fda7e4f32da8c76d8a2`, con autorizzazione esplicita
dell’utente e verifiche automatiche/smoke rieseguiti dalla coordinatrice.
GRAPH.04: bootstrap preparato, non assegnata e implementazione non iniziata.
GRAPH.05-08 non iniziate.

## Roadmap

| Fase | Consegna | Stato / criterio di uscita |
|---|---|---|
| GRAPH.00 | Repository dedicata, specifica, verifica ambiente e convenzioni | Completata |
| GRAPH.01 | Finestra Windows e scena 3D locale | Completata e accettata |
| GRAPH.02 | Identità, rappresentazioni, contenimento e SQLite | Integrata tramite PR #2 |
| GRAPH.03 | Zoom semantico continuo e collegamenti aggregati | Integrata PR #3; PASS tecnico e smoke confermati |
| GRAPH.04 | Caricamento selettivo e prove di scala | Prossima fase; bootstrap preparato; report 10k/100k/300k con limiti espliciti |
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

Il merge è stato richiesto espressamente dall’utente. Non si dichiara retroattivamente
eseguito ogni passo del playbook visivo. Prestazioni di scala ancora non misurate.
Il prossimo incarico è definito in `docs/handoffs/BOOTSTRAP_NODILUME_GRAPH_04.md`.

Ogni fase richiede verifiche sui rischi concreti introdotti. Una fase non è conclusa
per il solo fatto che il codice compila.
