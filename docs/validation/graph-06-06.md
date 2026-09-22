# GRAPH.06.06 — validazione import/export file

Data: 22 settembre 2026. Macchina: OFFICE-PC.
Base candidata: main@5624163e47c439bdbde757c75cd36a22b89e0330.
Fixture: file, database SQLite e profili WebView2 temporanei.

## Esiti

| Verifica | Esito |
|---|---|
| Viewer Node tests | PASS 10/10 |
| TypeScript e bundle | PASS |
| Build Nodilume.Tests Release | PASS, 0 warning/errori |
| Suite .NET | PASS 9/9 gruppi, incluso Portable map files |
| Build Desktop/Smoke/ScaleSmoke/Benchmarks | PASS, 0 warning/errori |
| Smoke WPF/WebView2 | PASS, exit code 0 |
| git diff --check | PASS |
| Scale-smoke 300k runtime | Non ripetuto; renderer/query invariati |
| Accettazione manuale dialoghi nativi | Non dichiarata |

Comandi:

    ./scripts/build.ps1
    dotnet run --project tests/Nodilume.Smoke -c Release --no-build
## Copertura .NET

I test esportano su file reale, verificano UTF-8 senza BOM e round-trip
byte-logico nel codec. Due import dello stesso documento producono MapId e
database distinti a revisione 0, senza modificare la sorgente.

Sono rifiutati senza lasciare database: JSON malformato, UTF-8 invalido,
UTF-16, file vuoto, file oltre 64 MiB, estensione non JSON e MapId sorgente
inesistente. Export cancellato conserva la destinazione. Nessun .tmp,
.sqlite-wal o .sqlite-shm resta dopo le operazioni.

## Copertura WPF/WebView2

Lo smoke usa i pulsanti Importa/Esporta reali e sostituisce soltanto il
dialogo nativo con un selettore deterministico. Esporta la mappa personale
popolata dalle slice precedenti, la importa con identità fresca, confronta
tutto il grafo, verifica che la sorgente sia invariata e apre la copia nel
viewer. Un file corrotto mantiene catalogo/selezione e mostra l'errore;
la selezione importata sopravvive alla riapertura.

## Limiti

Il documento è uno snapshot logico, non contiene cronologia, preferenze,
selezione, camera o file SQLite. Il codec in-memory e il limite 64 MiB non
coprono necessariamente mappe da 300.000 Idea. Non è backup/recovery.
Il dialogo Windows reale non è stato pilotato automaticamente: sono testati
la sua configurazione compilata e l'intero flusso successivo tramite
l'interfaccia iniettata. Il PASS tecnico non sostituisce l'accettazione manuale.
