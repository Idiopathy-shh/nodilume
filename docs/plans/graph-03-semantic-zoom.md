# GRAPH.03 — zoom semantico continuo e collegamenti aggregati

Data: 18 settembre 2026.
Branch: `feat/graph-03-semantic-zoom`.
Base effettiva: `5b867ba2d67f2614cb19c98a0a06fed614a382fd`.
Dipendenza: GRAPH.02 integrata tramite PR #2, squash `5272f297bc58d4f1b69b17ad1eb2f236ec40c4db`.

## Obiettivo

Sostituire la pagina piatta di GRAPH.02 con proiezioni contestuali autorevoli prodotte da C#.
La vista mantiene camera e animazioni, ma non decide gerarchia, identità o aggregazioni.
L'ingresso in un gruppo attenua il suo involucro e rivela i figli; l'uscita usa la stessa
macchina di transizione in senso inverso. La scena corrente resta visibile durante I/O.

GRAPH.03 non introduce layout globale, scritture di posizione, benchmark di scala,
drag/pin, undo/redo o persistenza della camera.

## Contratto di proiezione

Ogni risposta contiene versione, requestId, mapId, revisione e contesto richiesto.
Il contesto ha percorso completo di antenati, parent contestuale e origine del frame.
Le coordinate inviate al viewer sono transitorie e relative all'origine del contesto;
le coordinate locali persistenti dei Placement non vengono modificate.

La proiezione rende il percorso attivo, i fratelli necessari a conservare orientamento
e i figli immediati del contesto. Query e profondità hanno limiti espliciti. Una catena
antenati viene recuperata indipendentemente dalla pagina dei figli: una pagina incompleta
non può trasformare un figlio in una falsa radice.
## Query e stati

Aggiungere al port di persistenza letture mirate per radici, singolo Placement, antenati,
figli con indicazione `hasMore`, conteggi figli, Placement per Idea e relazioni che
toccano un insieme di Idea. Le query SQLite usano indici esistenti e limiti dichiarati.

Lo stato di una risposta distingue almeno:
- `ready`: dati coerenti entro i budget;
- `partial`: budget esaurito, con dati omessi dichiarati;
- `empty`: contesto valido ma privo di figli;
- `error`: risposta separata, senza sostituire la scena corrente con una rete vuota.

Un nodo foglia espone `hasChildren=false` e non può essere aperto.

## Relazioni

Ogni Relation concettuale contribuisce una sola volta. Per ogni estremo si individuano
le rappresentazioni pertinenti e il più profondo antenato visibile. Se entrambi gli
estremi hanno una destinazione visibile non ambigua, la Relation confluisce in un link
aggregato per coppia, tipo e direzione. Il link conserva conteggio e RelationId di origine.

Relazioni opposte restano distinte. Le Relation interne allo stesso gruppo chiuso vengono
conteggiate come interne e non producono self-link. Non viene prodotto alcun prodotto
cartesiano fra Placement.

Se un estremo ha più Placement pertinenti, la risposta contiene candidati con percorso:
il viewer mostra la scelta e non seleziona il primo ID. Se un'Idea non ha Placement,
la risposta dichiara che non esiste una destinazione navigabile.
## Navigazione e concorrenza

Il viewer genera requestId monotoni per ogni cambio di contesto. Desktop annulla la
richiesta precedente quando ne arriva una nuova; il viewer accetta solo la risposta
corrispondente all'ultima richiesta e scarta risposte obsolete o revisioni arretrate.

Doppio clic, pulsante Entra e soglia automatica invocano la stessa funzione. Il candidato
automatico è il nodo selezionato oppure quello puntato stabilmente, mai il nodo più vicino
alla camera. Entrata e uscita hanno soglie diverse e un dwell temporale per isteresi.

Durante un cambio di frame, camera, target e geometria esistente vengono riframati con
la differenza fra origini globali transitorie. Posizione e target restano finiti e
l'orientamento non cambia. La transizione parte sempre dallo stato visuale corrente,
quindi una richiesta inversa può interrompere una transizione senza salto.

La navigazione trasversale usa il Placement scelto, apre il suo parent come contesto,
focalizza la destinazione e salva un ritorno transitorio prevedibile nel viewer.
Panoramica e Livello superiore restano sempre accessibili.

## Viewer restart

Desktop conserva solo il contesto transitorio più recente e cancella il lavoro in corso
alla chiusura. Un nuovo messaggio `ready` ricostruisce la proiezione da SQLite; nessuno
stato grafico del viewer diventa fonte autorevole.
## Verifica

Test C# deterministici:
- antenati completi con pagina figli incompleta;
- foglia distinta da gruppo vuoto;
- aggregazione prima/dopo espansione;
- Relation interna senza self-link;
- Relation opposte/cicliche senza fusione errata;
- rappresentazioni multiple e scelta esplicita;
- Idea senza Placement;
- limiti/partial e revisione coerente.

Test TypeScript:
- riframing camera andata/ritorno finito e reversibile;
- isteresi entrata/uscita e dwell;
- inversione di una transizione da stato intermedio;
- filtro requestId/revisione obsoleti.

Smoke WPF/WebView2 su DB temporaneo dedicato:
almeno tre livelli, Entra/Esci, ritorno, destinazione trasversale, ambiguità,
resize e riapertura, con effetti DOM/camera osservabili.

Criteri finali: `./scripts/build.ps1`, smoke Release, `git diff --check`, working tree
coerente, documentazione/hand-off aggiornati e PR verso `main`. La PR resta draft finché
manca l'accettazione visiva dell'utente. Nessun merge e nessun lavoro GRAPH.04.
