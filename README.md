# Nodilume

Desktop Windows per esplorare mappe di idee come grafi 3D multiscala.

## Stato

Avvio di GRAPH.00–GRAPH.01: ambiente e prima scena dimostrativa.
Non è ancora un editor: zoom semantico, SQLite e mappe personali sono nelle fasi successive.

## Architettura

C#/.NET 10 e WPF ospitano una vista TypeScript/Three.js attraverso WebView2.
La scena proviene da C# e le risorse della vista sono distribuite localmente.
Il futuro stato persistente appartiene al lato C#, non al renderer.

La specifica completa è in `docs/specs/graph-3d-design.md`.
La roadmap è in `docs/roadmap.md`.
Il piano della prima consegna è in `docs/plans/2026-09-18-graph-00-01.md`.

## Dati

Solo codice e fixture sintetiche appartengono a questa repository privata.
Libri, database personali, fotografie e credenziali restano fuori da git.
Nome scelto dall'utente; disponibilità legale del marchio non accertata.
