# Nodilume — coordinamento delle chat

## Ruoli

La chat coordinatrice mantiene architettura, roadmap, dipendenze, criteri di accettazione e stato delle consegne. Le chat implementatrici eseguono incarichi circoscritti e consegnano PR con verifiche riproducibili. L'utente approva l'integrazione, coordinata da questa chat.

GitHub conserva lo stato ufficiale. I messaggi di una chat non sostituiscono commit, risultati dei test o documentazione aggiornata.

## Stato iniziale

- GRAPH.00: completata.
- GRAPH.01: accettata dall'utente in conversazione; chiusura tramite PR #1.
- GRAPH.02: implementazione tecnica completata sul branch `feat/graph-02-domain-sqlite`; PR/integrazione e accettazione manuale restano alla coordinatrice e all'utente.
- GRAPH.03–08: in attesa delle rispettive dipendenze.

L'accettazione di GRAPH.01 è complessiva: non viene presentata come verbale dettagliato di ogni gesto del playbook. Il caricamento locale è verificato; non è stata disconnessa la rete del PC.

## Assegnazione attiva

| Incarico | Chat | Branch proposto | Dipendenza | Consegna |
|---|---|---|---|---|
| GRAPH.02 — identità e SQLite | Chat implementatrice corrente | feat/graph-02-domain-sqlite | PR #1 merged | Implementazione/test/docs completati; apertura PR in consegna |

Non avviare in parallelo più implementazioni del medesimo incarico. La chat esecutrice registra base SHA, branch e percorsi prima delle modifiche. Un checkout condiviso non può essere cambiato di branch da due chat contemporaneamente.

## Flusso di consegna

1. La coordinatrice definisce perimetro e criteri e prepara il bootstrap.
2. L'utente apre una chat e allega il bootstrap.
3. La chat verifica repository, branch, stato locale e PR; prepara un piano esecutivo concreto e implementa.
4. La chat consegna una PR e un resoconto con SHA, test eseguiti, limiti e istruzioni runtime.
5. L'utente riporta qui il link PR o il resoconto. La coordinatrice verifica autonomamente lo stato effettivo.
6. Si esegue l'eventuale accettazione runtime e si integra solo con autorizzazione dell'utente.

Per ogni nuova fase, usare un bootstrap dedicato. Non estendere una patch a funzionalità di fasi successive senza riportare la proposta alla coordinatrice.
