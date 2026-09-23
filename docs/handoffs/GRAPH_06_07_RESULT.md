# GRAPH.06.07 — risultato

Data: 23 settembre 2026.
Branch: feat/graph-06-07-backup-recovery.
Base: main@cdfa7a19d2d37a3b87f396454043f0859d3cc2d1.
PR: #19 OPEN.

## Consegnato

- Snapshot SQLite online consistente, distinto dall'export JSON.
- Pacchetto `.nodilume-backup` con manifest, dimensione e SHA-256.
- Limite 8 GiB, struttura ZIP chiusa e verifica `integrity_check`.
- Retention gestita: ultimi 10 backup validi per mappa.
- Restore sempre non distruttivo con nuovo MapId e filename GUID.
- Titolo, revisione, dati del grafo e tabelle SQLite extra preservati.
- Pulsanti WPF `Crea backup` e `Ripristina backup`.
- Blocco reciproco con import/export, gestione mappe ed editor.

## Verifica

Viewer 10/10; suite .NET 10/10; build completa senza warning/errori.
Smoke WPF/WebView2 PASS, incluse regressioni GRAPH.03 e GRAPH.06.02–06.07.
Fixture SQLite oltre 64 MiB e tabella extra: PASS.
git diff --check PASS.

## Pro e contro

Pro: il backup e' streaming, consistente e conserva il database completo; il
restore non sovrascrive nulla e diventa visibile al catalogo soltanto dopo tutte
le verifiche. Retention e fallimenti sono espliciti.

Contro: SHA-256 non e' una firma; pacchetti manipolati intenzionalmente non sono
autenticati. Il formato v1 supporta al massimo 8 GiB, non e' cifrato e il rekey
deve evolvere insieme alle future migrazioni SQLite. Nessun cloud sync.

## Prossima slice suggerita

GRAPH.06.08: chiusura/integrazione dell'editor personale e decisione esplicita
sul rapporto con la candidata GRAPH.05. Non incorporare automaticamente
GRAPH.05 finche' i suoi gate Undo/Redo reali e la firma non sono risolti.
Un'eventuale firma/cifratura dei backup va trattata come hardening separato.