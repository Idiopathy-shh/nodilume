# Nodilume — coordinamento delle chat

## Ruoli

La chat coordinatrice mantiene architettura, roadmap, dipendenze, criteri di accettazione
e stato delle consegne. Le chat implementatrici eseguono incarichi circoscritti e
consegnano PR con verifiche riproducibili. L'utente approva l'integrazione.

GitHub conserva lo stato ufficiale. I messaggi di una chat non sostituiscono commit,
risultati dei test o documentazione aggiornata.

## Stato

- GRAPH.00: completata.
- GRAPH.01: accettata dall'utente; chiusura tramite PR #1.
- GRAPH.02: integrata tramite PR #2, squash `5272f297bc58d4f1b69b17ad1eb2f236ec40c4db`.
- GRAPH.03: implementata sul branch `feat/graph-03-semantic-zoom`; PR #3 draft;
  PASS tecnico e smoke Windows completati. Accettazione visiva dell'utente e merge non
  ancora eseguiti.
- GRAPH.04-08: non iniziate.

## Assegnazione attiva

| Incarico | Chat | Branch | Dipendenza | Consegna |
|---|---|---|---|---|
| GRAPH.03 — zoom semantico e aggregazioni | Chat implementatrice corrente | `feat/graph-03-semantic-zoom` | PR #2 merged | PR draft, validation + handoff, nessun merge |

Non avviare GRAPH.04 e non creare un secondo incarico GRAPH.03 finché la coordinatrice
non ha verificato la PR e l'utente non ha completato l'accettazione visiva.

## Regole di integrazione

1. La chat implementatrice registra base, branch e working tree prima di modificare.
2. Piano esecutivo prima delle modifiche funzionali.
3. Commit funzionali separati da documentazione quando utile.
4. Build, test e smoke devono essere riproducibili e usare fixture temporanee dedicate.
5. PASS tecnico e accettazione visiva sono stati distinti.
6. La chat implementatrice pubblica la PR ma non effettua merge.
7. L'utente riporta la PR alla coordinatrice; la coordinatrice verifica autonomamente.
8. Solo dopo autorizzazione esplicita dell'utente si integra.
9. GRAPH.04 riceverà un bootstrap separato; nessun benchmark di scala viene anticipato in GRAPH.03.

Un checkout condiviso non può essere cambiato di branch da due chat contemporaneamente.
Nessuna chat deve resettare, force-pushare o fare rebase autonomo.
