# GRAPH.06.05 — ricerca UI e navigazione

Data: 22 settembre 2026. Base: main@2f3eb05d654ea3846400c514d7e5cbca4acc0179.
Branch: feat/graph-06-05-search-ui. Macchina autorizzata: OFFICE-PC.

## Obiettivo

Rendere raggiungibile dalla sidebar la ricerca indicizzata introdotta in GRAPH.04,
senza caricare globalmente la mappa e senza scegliere silenziosamente una
rappresentazione quando una Idea possiede più Placement.

## Contratto

- Ricerca per prefisso del titolo nella sola mappa attiva.
- Semantica ordinale e case-sensitive, coerente con l'indice SQLite esistente.
- 20 Idea per pagina con cursore keyset e comando «Altri risultati».
- Massimo 64 Placement risolti per pagina e 128 antenati per percorso.
- Stato parziale esplicito se rappresentazioni o antenati eccedono i limiti.
- Una riga distinta per ogni Placement, con breadcrumb e ID breve.
- Anteprima del contenuto condiviso; «Apri» usa il Placement scelto.
- Idea senza Placement visibile ma non apribile.
## Navigazione

Il Desktop invia un comando protocollo v2 con mapId, revisione,
placementId e contesto genitore. Il viewer accetta il comando soltanto
per mappa/revisione attive e quando non esiste una richiesta pendente.
Apre il contesto genitore e mette a fuoco il Placement esatto; la
navigazione precedente entra nello stack «Ritorna».

## Isolamento e concorrenza

Cambio mappa o nuova generazione invalida la ricerca in corso e azzera
input, risultati, cursore e anteprima. Le query usano un nuovo store
sullo stesso database della mappa selezionata. Nessuna scrittura e
nessun accesso a mappe personali durante i test: solo fixture temporanee.

## Fuori scope

Ricerca fuzzy/full-text/globale, filtri avanzati, modifica dell'indice,
paginazione delle rappresentazioni oltre il limite dichiarato,
import/export file, backup/recovery, cancellazione nodi e GRAPH.05.

## Gate

- Viewer Node e build TypeScript.
- Suite .NET con SQLite reale.
- Smoke WPF/WebView2 per input invalido, rappresentazioni multiple,
  apertura esatta, radice e isolamento fra mappe.
- Benchmark e scale-smoke WPF/WebView2 su fixture sintetica 300k.
- git diff --check e review del diff prima della PR.
