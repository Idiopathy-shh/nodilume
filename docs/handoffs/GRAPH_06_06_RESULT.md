# GRAPH.06.06 — risultato

Data: 22 settembre 2026.
Branch: feat/graph-06-06-file-import-export.
Base: main@5624163e47c439bdbde757c75cd36a22b89e0330.
PR: #17 OPEN.

## Consegnato

- Pulsanti WPF Importa file / Esporta file con dialoghi Windows.
- Export JSON UTF-8 atomico tramite file temporaneo e flush.
- Import validato sempre in nuova mappa GUID a revisione 0.
- Cleanup di database e sidecar se la creazione fallisce.
- Limite 64 MiB e rifiuto dei file non JSON/non UTF-8.
- Blocco reciproco fra trasferimento, editing, relazioni e ricerca.
- Test infrastruttura e smoke reale WPF/WebView2.

## Verifica

Viewer 10/10; suite .NET 9/9; build completa senza warning/errori.
Smoke WPF/WebView2 PASS, incluse regressioni GRAPH.03 e GRAPH.06.02–06.06.
git diff --check PASS.
## Pro e contro

Pro: nessuna sovrascrittura della mappa corrente, documento deterministico,
copie importate indipendenti e fallimenti senza catalog entry parziali.

Contro: è uno snapshot logico in memoria, non un backup SQLite. Non include
stato UI/storia e può rifiutare mappe grandi oltre 64 MiB. Il dialogo nativo
richiede ancora accettazione manuale visiva, pur essendo il flusso testato
tramite la stessa interfaccia.

## Prossima slice suggerita

GRAPH.06.07: backup/recovery SQLite controllato, con copia consistente,
manifest/checksum, retention esplicita e ripristino sempre non distruttivo.
Un codec JSON streaming può essere valutato separatamente se serve
portabilità per mappe superiori a 64 MiB.
