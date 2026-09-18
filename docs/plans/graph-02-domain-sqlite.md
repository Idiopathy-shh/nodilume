# GRAPH.02 — piano esecutivo dominio e SQLite

Data: 18 settembre 2026.
Base: `47119f7c5d470a3149a3a6dddbc1d503d2a36104`.
Branch: `feat/graph-02-domain-sqlite`.

## Perimetro

GRAPH.02 introduce identità persistenti, contenimento basato sui Placement, relazioni tra Idea,
operazioni validate e persistenza SQLite. Non implementa zoom semantico, camera persistente,
drag interattivo, undo/redo, benchmark di scala o gestione completa delle mappe.

## Progetti e responsabilità

- `Nodilume.Core`: ID tipizzati, Map/Idea/Placement/Relation e aggregate `MapGraph` con invarianti.
- `Nodilume.Application`: porte di persistenza, operazioni validate, demo deterministica e proiezione scena.
- `Nodilume.Infrastructure`: database SQLite per mappa, schema/versione, transazioni, optimistic revision.
- `Nodilume.Desktop`: composizione Infrastructure/Application e caricamento della demo persistente.
- `Nodilume.Viewer`: renderer puro; usa placementId come identità grafica e ideaId come identità concettuale.
- `Nodilume.Tests`: test dominio e integrazione contro file SQLite reali in directory temporanee.

## Contratti principali

Il contenimento è un albero di Placement per mappa: parent nullo = radice. Un Placement riferisce una Idea
della stessa mappa e possiede coordinate locali finite, annotazione e pin. Le relazioni riferiscono Idea,
non Placement. `MapGraph` rifiuta riferimenti mancanti, self-parent, cicli e la stessa Idea lungo una
catena antenato-discendente, compreso lo spostamento di sottoalberi.

Le scritture SQLite modificano solo le righe interessate e incrementano `maps.revision` con compare-and-swap.
Il controllo della revisione e la modifica vivono nella stessa transazione; errori successivi causano rollback.
La creazione iniziale della fixture è l'unica operazione che inserisce l'intero grafo in una transazione.

Lo schema usa chiavi esterne composte con `map_id` per impedire riferimenti incrociati, indici per parent,
idea e adiacenze, `PRAGMA foreign_keys=ON` su ogni connessione e una tabella di versione. Una versione
futura non supportata produce errore esplicito senza modificare il file.

## Sequenza

1. Aggiungere Core e test delle invarianti.
2. Aggiungere Application e contratti di persistenza.
3. Aggiungere Infrastructure SQLite, migrazione v1 e test di round-trip/concorrenza/rollback/paginazione.
4. Sostituire `DemoScene` con inizializzazione idempotente e proiezione dal database.
5. Adattare Viewer e smoke mantenendo selezione, focus, home e resize.
6. Aggiornare build, lockfile, README, roadmap, validation e handoff.
7. Eseguire build completa, test, smoke Windows, riapertura con stesso DB, review diff e PR.

## Fixture persistente

La demo mantiene 25 Placement e 27 collegamenti visivi complessivi: 24 archi di contenimento e 3 relazioni.
Include livelli annidati e una Idea rappresentata in due contesti. Le coordinate salvate sono locali al parent;
la proiezione GRAPH.02 calcola coordinate assolute solo per il renderer corrente. Una Relation produce al
massimo un collegamento grafico scegliendo una rappresentazione visibile deterministica per estremo, senza
prodotto cartesiano.

## Verifiche di uscita

Testare round-trip, isolamento di mappe, contenuto condiviso/annotazioni locali, move validi e invalidi,
rimozione foglia, coordinate non finite, rollback, revisioni obsolete, init idempotente, schema futuro e
paginazione stabile. Lo smoke deve usare DB e profilo temporanei. La prova di riapertura deve dimostrare
stesso mapId, placementId, ideaId, posizione e annotazione dopo chiusura e nuova istanza del repository.