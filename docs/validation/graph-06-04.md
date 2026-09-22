# GRAPH.06.04 — validazione editor relazioni

Data: 22 settembre 2026. Macchina: OFFICE-PC.
Integrata: PR #13, squash main@550ce57916d274b12606ce60f97f33a7d9f10d7a.
Base candidata: main@fdd65e06ffb8f2f3837262d5b1e93fda888a6baa.
Fixture: database SQLite e profili WebView2 temporanei; nessuna mappa personale letta.

## Esiti

| Verifica | Esito |
|---|---|
| Viewer Node tests | PASS 9/9 |
| TypeScript e bundle | PASS |
| Build Nodilume.Tests Release | PASS, 0 warning/errori |
| Suite .NET | PASS 7/7 gruppi, incluso Relation editor |
| Build Desktop/Smoke/ScaleSmoke/Benchmarks | PASS, 0 warning/errori |
| Smoke WPF/WebView2 | PASS, exit code 0 |
| git diff --check | PASS |
| Accettazione manuale | Non dichiarata |

Comando build:

    ./scripts/build.ps1

Comando smoke:

    dotnet run --project tests/Nodilume.Smoke -c Release --no-build

## Copertura concreta

La suite Relation editor usa SQLite reale e verifica normalizzazione del tipo,
spiegazione Unicode, revisione singola, update/inversione senza cambio ID,
no-op revision-safe, stale revision, self-relation rifiutata, limiti dei campi,
estremi cross-map, update/delete cross-map e conservazione di Idea/Placement.

Lo smoke seleziona gli estremi dalla scena WebView2 in contesti diversi, crea una
relazione diretta, ne verifica rendering e persistenza, scambia gli estremi,
cambia tipo/spiegazione e la rende non diretta. Il primo click delete arma soltanto
la conferma; il secondo elimina la sola Relation. Verifica poi cambio mappa,
ritorno, riapertura e regressione GRAPH.03/06.02/06.03.

## Limiti

L'elenco WPF mostra al massimo 64 relazioni che toccano l'Idea selezionata e
dichiara quando esistono altri risultati. Non è ricerca globale. Non sono inclusi
import/export file, backup/recovery, cancellazione di nodi o funzioni GRAPH.05.
Lo scale-smoke 300k non è stato ripetuto: questa patch non cambia query di
proiezione, paging o renderer; i nuovi rischi sono coperti da test mirati e smoke.
Il PASS tecnico non sostituisce l'accettazione visiva manuale.