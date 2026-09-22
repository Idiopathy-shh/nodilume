# GRAPH.06.05 — risultato

Data: 22 settembre 2026.
Branch: feat/graph-06-05-search-ui.
Base: main@2f3eb05d654ea3846400c514d7e5cbca4acc0179.
PR: da aprire.

## Consegnato

- Servizio applicativo di ricerca bounded basato sull'indice GRAPH.04.
- Sidebar WPF con prefisso, risultati, anteprima, paginazione e apertura.
- Scelte Placement esplicite con breadcrumb e ID breve.
- Navigazione viewer protocollo v2 vincolata a mappa e revisione.
- Reset/invalidation al cambio mappa e feedback partial/error.
- Test unitari/integrativi, viewer e smoke WPF/WebView2.

## Verifica

Viewer 10/10; suite .NET 8/8; build completa senza warning/errori.
Smoke WPF/WebView2 PASS. Fixture 300k e scale-smoke PASS:
ricerca backend p95 0,9661 ms, prima vista utile 4,213 s,
frame p95 finale 16,8 ms e selezione p95 14,8 ms.

## Decisioni e limiti

La ricerca conserva la semantica ordinale case-sensitive dell'indice esistente.
Carica 20 Idea per pagina, al massimo 64 Placement e 128 antenati.
Non seleziona automaticamente una rappresentazione; un risultato senza Placement
non è apribile. I limiti sono dichiarati come parziali.

Non sono compresi fuzzy/full-text, filtri, paginazione separata dei Placement,
import/export file, backup/recovery, cancellazione di nodi o GRAPH.05.
Accettazione visiva manuale non dichiarata.

## Prossima slice suggerita

GRAPH.06.06: import/export file usando il codec già integrato in GRAPH.06.01,
con file dialog, import sempre in nuova mappa, scrittura atomica e smoke
su round-trip/errore senza sovrascrivere database esistenti.
