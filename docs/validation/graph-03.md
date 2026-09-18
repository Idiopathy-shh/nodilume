# GRAPH.03 — verifica tecnica e playbook

Data: 18 settembre 2026.
Branch: `feat/graph-03-semantic-zoom`.
Base effettiva: `5b867ba2d67f2614cb19c98a0a06fed614a382fd`.
Base funzionale GRAPH.02: squash PR #2 `5272f297bc58d4f1b69b17ad1eb2f236ec40c4db`.
Piano: `44c94ff0079994b6a377ab165477378305d90af4`.
Proiezioni C#/SQLite: `db82aeb756eca4602581d6c33c7d89827671ecc5`.
Implementazione viewer/Desktop/smoke: `feaf2d151821f966b66afdd0d9069fdbedc2ec2f`.

## Obiettivo verificato

GRAPH.03 sostituisce la pagina piatta GRAPH.02 con proiezioni contestuali prodotte da C#.
Il contenimento resta dei Placement e le Relation restano fra Idea. Il viewer mantiene
camera, animazioni e stato grafico transitorio ma non decide identità, gerarchia,
aggregazioni o destinazioni.

L'ingresso attenua il contenitore del contesto e fa emergere i figli nel nuovo frame
locale. L'uscita usa la stessa interpolazione dal frame corrente verso quello del parent.
Una nuova richiesta durante una transizione parte dallo stato geometrico interpolato,
quindi non richiede un salto al frame finale precedente.

## Contratto C#–viewer

Protocollo versione 2:
- `requestId` correla ogni richiesta/risposta;
- `mapId`, revisione e contesto accompagnano la proiezione;
- Desktop annulla il CancellationToken della richiesta superata;
- il viewer ignora requestId non più attivi, mappe diverse e revisioni arretrate;
- un riavvio del viewer emette `ready` e Desktop ricostruisce il contesto più recente da SQLite;
- una richiesta fallita produce `projectionError` e non sostituisce la scena corrente con una rete vuota.

Stati dichiarati: `ready`, `partial`, `leaf`, `empty`, errore e caricamento.
Una foglia ha `hasChildren=false` e non viene presentata come gruppo apribile.

## Query e coordinate

Le letture SQLite aggiunte sono mirate e limitate: radici/figli, singolo Placement,
percorso antenati, conteggio figli, discendenti limitati, Placement per Idea e Relation
che toccano un insieme di Idea. Gli antenati vengono recuperati indipendentemente dalla
pagina dei figli; il raggiungimento del limite di profondità fallisce chiuso invece di
creare una falsa radice.

Le coordinate persistenti non vengono modificate durante lo zoom. C# compone un'origine
transitoria del frame; il viewer riframa camera, target e geometria con la differenza
fra origini. I test verificano finitezza e reversibilità dell'operazione.

## Relation aggregate e destinazioni

Ogni Relation contribuisce una sola volta alla proiezione pertinente. Quando i dettagli
sono chiusi, gli estremi vengono proiettati sul più profondo antenato visibile e le
Relation con stessa coppia/tipo/direzione vengono aggregate. Il link espone conteggio e
RelationId originali.

Sono verificati:
- nessun prodotto cartesiano fra Placement;
- Relation interne a un gruppo chiuso non diventano self-link;
- direzioni opposte restano distinte;
- conteggi/provenienza si ridistribuiscono senza perdita dopo l'espansione;
- una Idea con più Placement produce candidati separati con percorso leggibile;
- una Idea senza Placement resta nel modello ma viene dichiarata non navigabile.

## Navigazione

Doppio clic, pulsante Entra e zoom semantico usano la stessa richiesta contestuale.
Il candidato automatico è il nodo selezionato o il nodo puntato stabilmente. Entrata e
uscita hanno soglie diverse; dopo un ingresso la camera viene accompagnata verso il
nuovo contesto prima che l'uscita automatica venga riabilitata.

Sono disponibili breadcrumb, Livello superiore, Panoramica e Ritorna. Una navigazione
trasversale apre il parent della destinazione scelta, focalizza quel Placement e salva
un ritorno transitorio con contesto, selezione e camera globale.

## Verifiche automatiche finali

Comandi eseguiti:

```powershell
./scripts/build.ps1
dotnet run --project tests/Nodilume.Smoke -c Release --no-build
```

Esito finale:
- npm install riproducibile: PASS, 0 vulnerabilità riportate da npm;
- viewer/navigation: 8/8 PASS;
- TypeScript + bundle: PASS;
- restore .NET locked: PASS;
- build Core/Application/Infrastructure/Tests: 0 errori, 0 avvisi;
- Domain invariants: PASS;
- SQLite integration: PASS;
- Semantic projection: PASS;
- build Desktop/Smoke: 0 errori, 0 avvisi;
- smoke WPF/WebView2 GRAPH.03: PASS;
- runtime WebView2 osservato: 153.0.4234.32.

## Copertura deterministica

Test C#/SQLite:
- antenati completi anche con pagina figli parziale;
- partial dichiarato quando un budget viene esaurito;
- foglia esplicita e non espandibile;
- contesto inesistente: errore esplicito, nessun falso empty;
- limite antenati: fail-closed;
- aggregazione prima/dopo espansione;
- relazioni interne senza self-link;
- relazioni opposte distinte;
- provenienza senza duplicazione;
- rappresentazioni multiple con scelta;
- Idea senza Placement.

Test TypeScript:
- riframing camera andata/ritorno esattamente reversibile;
- coordinate finite;
- isteresi ingresso/uscita e dwell;
- inversione da valore intermedio della transizione;
- scarto di requestId, mapId o revisione obsoleti.

## Smoke WPF/WebView2

Lo smoke usa un database temporaneo e una fixture diversa dalla demo personale.
La gerarchia attraversata è:

`Radice → Gruppo A → Sottogruppo A1 → Foglia profonda`.

Verifica effetti osservabili nel DOM e nella camera:
1. apertura del contesto Radice;
2. ingresso in Gruppo A;
3. ingresso in Sottogruppo A1;
4. selezione della Foglia profonda;
5. due candidati distinti per Idea multipla;
6. due Relation opposte verso Foglia B1, senza fusione;
7. navigazione trasversale a Gruppo B / Foglia B1;
8. centraggio visivo della destinazione;
9. Ritorna a Sottogruppo A1 con selezione precedente;
10. Livello superiore;
11. Panoramica;
12. resize 1280→1000→1280;
13. screenshot `artifacts/graph-03-smoke.png`;
14. chiusura e seconda apertura dello stesso DB;
15. identità, revisione, Placement, Idea e Relation invariati.

## Playbook manuale per accettazione visiva

Questa prova deve essere svolta dall'utente prima del merge.

1. Eseguire `./scripts/build.ps1`, poi `./scripts/run.ps1`.
2. Selezionare un gruppo e avvicinarsi lentamente con la rotella: verificare che il
   contenitore si attenui e che i figli emergano senza un salto di camera.
3. Allontanarsi lentamente: verificare l'isteresi, cioè assenza di oscillazione vicino
   alla soglia e transizione inversa verso il parent.
4. Entrare con doppio clic e ripetere con il pulsante Entra: il comportamento deve essere
   semanticamente identico.
5. Durante un ingresso, invertire rapidamente direzione / scegliere Livello superiore:
   la transizione deve ripartire dallo stato visivo corrente, senza snap.
6. Fare zoom rapido attraversando la soglia: non devono partire espansioni casuali di
   gruppi non selezionati/non puntati.
7. Selezionare una foglia: Entra deve restare disabilitato; il focus è consentito.
8. Ruotare, fare pan, usare WASD/QE, cambiare focus e ridimensionare la finestra durante
   la navigazione: nessuna coordinata deve diventare NaN/infinita e l'orientamento deve
   restare comprensibile.
9. Seguire una Relation verso una destinazione fuori dal contesto corrente: verificare
   apertura del percorso necessario e focus della destinazione.
10. Usare Ritorna: contesto/camera precedenti devono essere prevedibili.
11. Su una Relation verso una Idea rappresentata più volte, scegliere prima una
    collocazione e poi l'altra; i percorsi devono rendere chiara la differenza.
12. Verificare che Panoramica e breadcrumb restino sempre disponibili.

Segnalare separatamente eventuali problemi estetici da errori di identità, contesto o
navigazione. Solo l'accettazione esplicita dell'utente chiude il criterio visivo GRAPH.03.

## Limiti intenzionali

GRAPH.03 non dichiara benchmark 10k/100k/300k e non introduce cache/ottimizzazione estesa:
questi sono GRAPH.04. Non introduce drag/pin, undo/redo o camera persistente: GRAPH.05.
Non modifica le posizioni persistenti durante lo zoom e non richiede caricamento globale.
La demo personale non viene sovrascritta dallo smoke.

PASS tecnico e accettazione visiva restano distinti. Nessun merge è autorizzato da
questo documento e GRAPH.04 non deve essere iniziata da questa chat.
