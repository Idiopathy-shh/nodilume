# GRAPH.02 — result handoff

Data: 18 settembre 2026.
Repository: `Idiopathy-shh/nodilume`.
Checkout: `C:\Sviluppo\Nodilume`.
Branch: `feat/graph-02-domain-sqlite`.
Base SHA: `47119f7c5d470a3149a3a6dddbc1d503d2a36104`.
PR: da registrare dopo l'apertura; non eseguire merge da questa chat.

## Risultato

GRAPH.02 è implementata tecnicamente. Il modello persistente distingue Idea e Placement,
assegna il contenimento al Placement, collega le Relation alle Idea e conserva identità
stabili. Core non dipende da WPF, WebView2, Three.js o SQLite. Application contiene
operazioni/porte; Infrastructure implementa SQLite; Desktop compone i servizi; Viewer
riceve soltanto una proiezione grafica.

La fixture persistente ha 25 Placement, 24 Idea, 3 Relation e 24 archi di contenimento.
"Domande" è rappresentata in due contesti con lo stesso ideaId e due placementId distinti.
## Persistenza

- Un file SQLite per mappa.
- Percorso demo applicativo: `%LOCALAPPDATA%\Nodilume\Maps\demo.sqlite`.
- Test e smoke: directory temporanee dedicate.
- Foreign key abilitate su ogni connessione.
- Schema v1 con indici per parent, idea e adiacenze.
- Migrazione iniziale transazionale e rifiuto esplicito di versioni future.
- Compare-and-swap su `maps.revision` nello stesso commit della modifica.
- Write mirate per create/update/move/remove/relation; nessun save globale a ogni comando.
- Query iniziale Placement limitata/paginata con ordinamento stabile.

La demo viene creata soltanto in assenza della mappa. I/O, integrità o schema non supportato
producono errore e non vengono trasformati in una demo vuota valida.
## Verifiche

PASS finali richiesti prima della pubblicazione:

- `./scripts/build.ps1`
  - viewer tests 4/4;
  - TypeScript/bundle PASS;
  - Core/Application/Infrastructure/Tests build Release: 0 errori, 0 avvisi;
  - Domain invariants: PASS;
  - SQLite integration: PASS;
  - Desktop/Smoke build Release: 0 errori, 0 avvisi;
  - restore `--locked-mode`: PASS.
- `dotnet run --project tests/Nodilume.Smoke -c Release --no-build`
  - due aperture WPF/WebView2 consecutive sul medesimo DB temporaneo;
  - 25 Placement / 27 link visuali;
  - mapId, revision, placementId, ideaId, posizione, parent e annotazione invariati;
  - risorse locali, selezione, focus/home e resize: PASS.
- `dotnet list ... package --vulnerable --include-transitive`
  - Nodilume.Tests: nessun pacchetto vulnerabile riportato;
  - Nodilume.Smoke: nessun pacchetto vulnerabile riportato.
- `git diff --check`: nessun errore.
## Casi coperti

Sono verificati isolamento mappe e riferimenti mancanti, self-parent, cicli di contenimento,
ripetizione della stessa Idea lungo una catena, collisione fra sottoalbero spostato e nuovi
antenati, relazioni concettuali cicliche, contenuto condiviso/annotazioni locali, rimozione
foglia, rifiuto della rimozione di parent, NaN/infinito, rollback atomico, revisione obsoleta,
inizializzazione idempotente, schema futuro senza perdita, paginazione senza skip/duplicati
e proiezione Relation senza prodotto cartesiano fra Placement.

## Limiti / follow-up

GRAPH.02 non implementa zoom semantico, aggregazioni multiscala, destinazioni visuali ambigue
complete, benchmark 10k/100k/300k, editor completo, drag/pin UI, undo/redo o camera persistente.
Questi punti restano nelle fasi GRAPH.03–05 secondo roadmap. La scena iniziale GRAPH.02 usa
una pagina fino a 128 Placement; il caricamento selettivo su larga scala è ancora GRAPH.04.

Il PASS tecnico non equivale all'accettazione manuale dell'utente. Nessun merge è autorizzato
da questo handoff e GRAPH.03 non deve essere iniziata da questa chat.
