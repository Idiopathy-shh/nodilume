# GRAPH.06.01 — validazione contratto JSON portabile
Data: 21 settembre 2026; PC: OFFICE-PC; branch feat/graph-06-01-portable-map.
Base: main@0f6bdac9 (GRAPH.04). GRAPH.05 non e' stata mergiata.
Questa e' una slice preliminare di GRAPH.06, non un editor completo.

## Funzionalita' implementate
PortableMapJson.Export(MapGraph): JSON v1 con titolo mappa, Idea, Placement e
Relation in ordine deterministico. PortableMapJson.Import(json, newMapId,
destinationSchemaVersion): ricostruisce il grafo in una nuova identita' mappa,
revisione 0, controllando versione, campi obbligatori, GUID e invarianti
dominio. Il caller crea un nuovo DB SQLite con CreateMapAsync: su DB
preesistente il metodo rifiuta la creazione; nessun overwrite previsto.
Gli ID Idea/Placement/Relation restano stabili all'interno del documento;
MapId differente isola le mappe importate.
Non vengono esportate cronologia, ricevute, view state o stato transitorio.
JSON v1 non e' legato al numero di schema SQLite.

## Evidenze (esecuzione reale su OFFICE-PC)
Comandi: dotnet restore tests/Nodilume.Tests --locked-mode;
dotnet build tests/Nodilume.Tests -c Release --no-restore -v:q;
dotnet run --project tests/Nodilume.Tests -c Release --no-build.
Restore PASS, build PASS (0 warning, 0 errori), 4/4 gruppi test PASS,
exit code 0. Portable map JSON PASS. Viewer baseline di main: npm ci
con lockfile PASS, test Node 9/9 PASS, TypeScript/bundle PASS.
Build WPF/Smoke Release PASS (0 warning e 0 errori); smoke GRAPH.03
WPF/WebView2 PASS su OFFICE-PC, exit code 0, runtime 153.0.4234.48.
L'assenza iniziale del bundle sulla worktree nuova e' stata risolta tramite
npm ci e npm run build, senza alterare il codice. Nessuna modifica ai
criteri Windows. Questo smoke NON esercita Undo/Redo di GRAPH.05.
Test su demo sintetica: export deterministico, identita' nuova e revisione 0,
Idea condivisa in due Placement, gerarchia, XYZ, pin/annotazione, Unicode,
Relation, round-trip su nuovo SQLite, import indipendenti, rifiuto database
preesistente, JSON/versione/campi/ID/relazioni/parent invalidi.
## Limiti e gate futuri
Codec solo in memoria, massimo 64 Mi caratteri JSON: NON e' stata validata
la portabilita' di mappe da 300k. Non esiste ancora dialogo file, import/export
con scrittura atomica, backup coerente SQLite, recovery, UI multi-map o CRUD
interattivo. Questi aspetti richiedono slice e validazioni dedicate.
Il contratto non importa lo stato grafico o la cronologia introdotti
da GRAPH.05. Non dichiarare GRAPH.05 PASS sulla base di questi test.
