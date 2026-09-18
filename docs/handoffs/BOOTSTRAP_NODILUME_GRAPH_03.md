# Nodilume — bootstrap chat implementatrice GRAPH.03

Esegui GRAPH.03 — zoom semantico continuo e collegamenti aggregati fino a una PR verificata. Questa chat implementa; la chat coordinatrice gestisce integrazione e roadmap. Non effettuare merge e non iniziare GRAPH.04.

## Repository e baseline
Repository privata https://github.com/Idiopathy-shh/nodilume. PC OFFICE-PC; checkout C:\Sviluppo\Nodilume. Non intervenire su OptionLab/QOE o altri runtime. Base main dopo PR #2, squash commit 5272f297bc58d4f1b69b17ad1eb2f236ec40c4db. Potranno seguire commit documentali: registra SHA effettivo.
Branch proposto feat/graph-03-semantic-zoom. Prima verifica stato pulito, branch, eventuali AGENTS.md applicabili, fetch origin, stato MERGED della PR #2 e assenza di incarichi concorrenti. Aggiorna main solo con fast-forward. Non fare reset, force-push o rebase autonomo; preserva modifiche altrui.
Leggi docs/specs/graph-3d-design.md, docs/roadmap.md, docs/coordination.md, docs/validation/graph-02.md, docs/handoffs/GRAPH_02_RESULT.md e codice pertinente. Prepara docs/plans/graph-03-semantic-zoom.md prima delle modifiche funzionali.

## Baseline da preservare
C# Core/Application/Infrastructure, WPF/WebView2, TypeScript/Three.js e SQLite. La demo persistente contiene 25 Placement, 24 Idea, 3 Relation e 24 archi di contenimento. Le due rappresentazioni di Domande condividono ideaId ma hanno placementId distinti.
GRAPH.02 proietta una prima pagina fino a 128 Placement; somma coordinate degli antenati disponibili e sceglie il primo Placement per le relazioni ambigue. Sono limiti da superare per la navigazione contestuale, non contratti definitivi.
Il contenimento appartiene ai Placement. Ogni rappresentazione ha figli propri: mostrare la stessa Idea altrove non replica il sottoalbero. Le relazioni collegano Idea; le coordinate locali e gli identificatori persistenti non devono cambiare durante la navigazione.

## Consegna
Realizza nella vera finestra Windows una navigazione continua attraverso almeno tre livelli, con involucro del gruppo che si attenua mentre emergono i figli, e transizione inversa in uscita. Conserva contesto e orientamento.
L'espansione riguarda il nodo selezionato o stabilmente puntato, non un gruppo casuale vicino alla camera. Usa isteresi; gestisci inversione a metà transizione, zoom rapido, focus e richieste concorrenti. Doppio clic/Entra e Esci devono usare la stessa transizione. Un nodo foglia non è un gruppo vuoto.
Mostra percorso di contesto, livello superiore e Panoramica. Mantieni rotazione, pan, tastiera, selezione, focus e resize. Trasformazioni locali/camera devono essere finite e continue; nessun layout globale o scrittura delle posizioni durante lo zoom.
C# mantiene autorità sul grafo e produce proiezioni coerenti con contesto e revisione. Camera e animazioni restano nel viewer. Definisci messaggi versionati con requestId, mapId, revisione e contesto; ignora risposte obsolete, annulla richieste superate e gestisci chiusura/riavvio del viewer.
Le query devono avere limiti espliciti e recuperare gli antenati necessari: una pagina incompleta non deve trasformare un figlio in falsa radice. Caricamento in corso, errore, gruppo vuoto e dati parziali devono essere distinguibili. Non introdurre caricamento globale obbligatorio.

## Relazioni e rappresentazioni multiple
Aggrega le relazioni tra gli antenati visibili dei gruppi chiusi; mostra conteggio e mantieni identità/provenienza delle Relation sottostanti. Espandendo, ridistribuisci gli estremi senza perdita o doppio conteggio. Distingui archi di contenimento e relazioni concettuali; rispetta tipo e direzione senza confondere relazioni opposte.
Una relazione interna a un gruppo chiuso non crea un falso self-link. Ogni relazione contribuisce una sola volta alla proiezione pertinente; nessun prodotto cartesiano tra Placement.
Quando più collocazioni sono candidate, presenta una scelta comprensibile con percorso e naviga al Placement scelto. Non risolvere silenziosamente l'ambiguità prendendo il primo ID. Idee senza Placement restano recuperabili nel modello: segnala l'assenza di destinazione navigabile.
Navigazione trasversale: apri il percorso necessario e accompagna la camera alla destinazione; conserva un ritorno prevedibile. Non duplicare contenuto o sottoalberi per semplificare il rendering.

## Sequenza suggerita
1. Contratti di contesto/proiezione, trasformazioni locali e test deterministici.
2. Aggregazione e risoluzione delle destinazioni, con fixture e test.
3. Transizioni nel viewer e integrazione WPF; richieste obsolete/errori.
4. Smoke reale, documentazione e consegna alla coordinatrice.
Puoi separare commit o sotto-patch coerenti; resta un solo incarico attivo. Non richiedere nuove approvazioni per dettagli ordinari già compresi.

## Verifiche e criteri di uscita
Esegui baseline ./scripts/build.ps1 e dotnet run --project tests/Nodilume.Smoke -c Release --no-build. Mantieni test dominio/SQLite e riapertura persistente.
Aggiungi test significativi su: trasformazioni camera andata/ritorno; isteresi e inversione; foglie; conteggi prima/dopo espansione; relazioni interne, opposte e cicliche; rappresentazioni multiple; scelta destinazione; pagina incompleta; risposta obsoleta e fallimento caricamento. Nessun falso PASS su dati mancanti.
Smoke WPF/WebView2 su DB temporaneo: almeno tre livelli, entrata/uscita, ritorno, navigazione trasversale, ambiguità, resize e riapertura; verifica effetti osservabili, non solo flag interni. La fixture di test può essere distinta dalla demo personale. Non sovrascrivere o resettare database esistenti.
Documenta playbook manuale per l'utente: zoom lento/rapido, inversione, cambio candidato, destinazione esterna e ritorno, selezione fra rappresentazioni. PASS tecnico e accettazione visiva restano distinti: la chiusura GRAPH.03 richiede prova dell'utente.
Aggiorna README, roadmap, coordinamento, docs/validation/graph-03.md e docs/handoffs/GRAPH_03_RESULT.md. Riporta comandi/esiti, limiti, SHA base/finale e link PR. Verifica git diff --check e working tree. Pubblica PR verso main, draft se manca accettazione; non mergiare.

## Fuori scope
Benchmark 10k/100k/300k e cache/ottimizzazione estesa (GRAPH.04); drag/pin, undo/redo e camera persistente (GRAPH.05); editor completo, AI, libri, cloud. Non dichiarare prestazioni di scala non misurate. Nessuna modifica sostanziale al modello approvato senza segnalarla alla coordinatrice.

