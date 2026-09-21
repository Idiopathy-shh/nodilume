# GRAPH.06.01 — contratto JSON portabile per mappe (slice indipendente)
Data: 21 settembre 2026. Base: main@0f6bdac9 (GRAPH.04); GRAPH.05 resta DRAFT.
Branch: feat/graph-06-01-portable-map; worktree: C:\Sviluppo\Nodilume-graph06-01.
Obiettivo: avanzare GRAPH.06 senza attendere la firma RSA e senza portare
modifiche GRAPH.05 nel branch. Questa slice NON equivale all'editor completo.

## Contratto
Il JSON v1 rappresenta il grafo logico: titolo della mappa, Idea condivise,
Placement per contesto (parent, XYZ, pin, annotazione) e Relation.
Identità Idea/Placement/Relation stabili nel documento; import in NUOVA mappa,
nuovo MapId e revisione 0; non è merge, overwrite o restore della mappa sorgente.
Formato distinto dalle versioni interne SQLite. Non contiene cronologia,
ricevute, view state, preferenze del viewer, file associati o credenziali.
Ordine deterministico degli elementi, versione verificata, dati validati prima
della scrittura del database. Import non deve toccare DB esistenti.
Usare solo fixture sintetiche. Nessun percorso a database personale per test.

## Gate
Test round-trip JSON + SQLite: Idea multi-Placement, relazioni direzionali,
contenimento, pin/annotazioni, UTF-8/Unicode e coordinate; seconda importazione
indipendente; JSON/versione/grafo malformati rifiutati prima della scrittura.
Build e suite .NET su OFFICE-PC; se Smart App Control blocca l'esecuzione,
registrare la distinzione build PASS/runtime BLOCCATO.
Limite intenzionale: primo codec in memoria su grafi piccoli; non dichiarare
import/export da 300k, gestione file atomica, backup o UI prima di verificarli.
Compatibilità con GRAPH.05 da integrare dopo lo smoke/merge di GRAPH.05.
## Disciplina del branch
La PR GRAPH.06.01 parte direttamente da main e non da GRAPH.05. Non cambiare
la worktree GRAPH.05, non effettuare cherry-pick della migrazione SQLite v3,
non introdurre shell UI o CLI che simulino l'editor completo.
La futura GRAPH.06 integrerà multi-map UI, CRUD, ricerca, import/export file,
backup/recovery e undo/redo nei contratti persistenti, con gate separati.
Il merge di questa slice è possibile soltanto se tutti i gate pertinenti
alla slice sono PASS; non certifica alcun gate ancora aperto di GRAPH.05.
