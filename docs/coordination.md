# Nodilume - coordinamento delle chat
## Ruoli
Questa chat è la coordinatrice per continuità, architettura, roadmap, dipendenze, assegnazioni e verifica delle consegne.
Le chat implementatrici eseguono incarichi circoscritti e consegnano PR con verifiche riproducibili; non effettuano merge.
GitHub conserva lo stato ufficiale. La coordinatrice integra dopo autorizzazione dell'utente.
## Checkpoint del 18 settembre 2026
- GRAPH.00-01: completate; GRAPH.01 accettata, PR #1.
- GRAPH.02: integrata PR #2, squash 5272f297bc58d4f1b69b17ad1eb2f236ec40c4db.
- GRAPH.03: integrata PR #3 su main, squash 1690c35fcde8f6e51bb17fda7e4f32da8c76d8a2.
- Ultimo head verificato prima del merge: 69dd2b08db1555507dcd3fc92638e011a4ae38f6.
- Verifiche rieseguite dalla coordinatrice: viewer 9/9, C#/SQLite/proiezioni 3/3 gruppi, build 0 errori/avvisi, smoke WPF/WebView2 PASS.
- Merge espressamente richiesto dall'utente; non si dichiara retroattivamente eseguito ogni punto del playbook visivo manuale.
- GRAPH.04: implementata e PASS tecnico sul branch `feat/graph-04-selective-loading`;
  build, smoke GRAPH.03, scale-smoke 300k e benchmark 10k/100k/300k completati.
  PR #5 è pubblicata come draft; restano verifica coordinatrice, accettazione visiva
  separata e integrazione.
- GRAPH.05-08: non iniziate.
## Assegnazioni e accesso
GRAPH.04 è la sola consegna implementatrice attiva in questo checkpoint.
Worktree: `C:\Sviluppo\Nodilume-graph04`; branch: `feat/graph-04-selective-loading`;
base `1690c35fcde8f6e51bb17fda7e4f32da8c76d8a2`; checkpoint funzionale `35dc3ca`.
Non avviare GRAPH.05 prima della verifica e integrazione di GRAPH.04.
Repository Idiopathy-shh/nodilume; OFFICE-PC; checkout originale C:\Sviluppo\Nodilume.
GitHub CLI sul PC accede alla repository con la sessione già configurata.
Worktree documentale coordinatrice: `C:\Sviluppo\Nodilume-coordination`, branch
`docs/coordination-post-graph03`, PR #4 draft. Il suo checkpoint post-GRAPH.03 è stato
incorporato nel branch GRAPH.04; la coordinatrice dovrà riconciliare/chiudere la PR #4
prima dell'integrazione per evitare una PR documentale ormai sovrapposta.
Il checkout originale resta separato; non cambiarne branch mentre altre chat potrebbero usarlo.
## Regole di integrazione
1. Registrare base, branch, working tree e incarichi concorrenti prima delle modifiche.
2. Piano esecutivo prima del codice; worktree dedicata per ogni incarico.
3. Build, test e smoke riproducibili con fixture temporanee; preservare database personali.
4. PASS tecnico, accettazione visiva e risultati prestazionali restano distinti.
5. La implementatrice pubblica la PR; la coordinatrice verifica autonomamente prima dell'integrazione.
6. Integrare solo con autorizzazione dell'utente; nessun reset, force-push o rebase autonomo.
7. Non cambiare branch in un checkout condiviso usato da un'altra chat.
8. Le misure GRAPH.04 valgono per l'hardware/runtime documentato e non sono una promessa universale.
9. PASS tecnico, benchmark e accettazione visiva manuale restano distinti.
10. Le Relation oltre il budget restano esplicitamente `partial`; non dichiarare completezza globale non misurata.
