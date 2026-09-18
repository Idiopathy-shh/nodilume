# Nodilume — specifica e roadmap

Data: 18 settembre 2026. Versione: 0.2.

Stato: perimetro del prototipo e stack approvati in conversazione. Questo documento consolida tali decisioni e propone i dettagli tecnici necessari. GRAPH.00 completata; prima scena desktop GRAPH.01 implementata e verificata tecnicamente. Accettazione manuale della navigazione pendente; nessuna prestazione di scala è certificata. Nome approvato: Nodilume.

## 1. Obiettivo

Applicazione desktop Windows per creare, modificare ed esplorare più mappe indipendenti. Ogni mappa è una rete 3D multiscala: avvicinandosi a un nodo si scopre la sua rete interna; allontanandosi i dettagli si ricompongono. L’esperienza combina movimento libero, avvicinamento guidato e nodi espandibili.

Il primo uso è il brainstorming generale. Una futura modalità libri importerà EPUB e appunti/foto di testi cartacei, genererà mappe per capitoli e collegherà idee tramite AI. Il motore generale non incorpora categorie obbligatorie come libro o capitolo.

## 2. Decisioni approvate

- Windows come unico sistema iniziale.
- Repository GitHub nuova e separata dal software OptionLab/QOE.
- C#/.NET e WPF per applicazione e logica; TypeScript/Three.js in WebView2 per la vista; SQLite locale per i dati.
- Mappe indipendenti, con possibilità futura di specializzazioni nello stesso programma.
- Grafo 3D ruotabile e attraversabile, con zoom semantico continuo.
- Disposizione ibrida: organizzazione automatica iniziale, modifiche manuali e posizioni fissabili.
- Una sola identità per ogni idea, con rappresentazioni in più gruppi.
- Contenuto comune e annotazioni specifiche del contesto.
- Collegamenti trasversali anche tra livelli; contenimento privo di cicli.
- Conservazione delle posizioni e del punto di esplorazione.
- Obiettivo di crescita: centinaia di migliaia di nodi, senza mostrarli tutti contemporaneamente.
- AI futura attraverso le stesse operazioni dell’editor; azioni reversibili e rispetto delle modifiche manuali.

## 3. Perimetro del primo prototipo

Una vera finestra Windows con una mappa dimostrativa persistente, più livelli annidati, relazioni trasversali e almeno una stessa idea rappresentata in due contesti. Navigazione libera e guidata, zoom continuo, spostamento e fissaggio dei nodi, salvataggio e riapertura. Generatore deterministico di dati sintetici per le prove di scala.

Il primo prototipo non include l’importazione EPUB, OCR, chiamate AI, sincronizzazione cloud, collaborazione multiutente, VR o un catalogo bibliografico. La schermata completa per gestire più mappe appartiene al primo prodotto utilizzabile, successivo alla validazione della navigazione. Il modello dati distingue le mappe fin dall’inizio.

## 4. Struttura del programma

| Unità proposta | Responsabilità | Dipendenze ammesse |
|---|---|---|
| `src/Nodilume.Core` | Identità, contenimento, rappresentazioni, relazioni e invarianti | Nessuna dipendenza grafica o database |
| `src/Nodilume.Application` | Comandi, query, undo/redo, selezione delle porzioni visibili | Core e interfacce di persistenza |
| `src/Nodilume.Infrastructure` | SQLite, migrazioni, ricerca e salvataggio | Core/Application |
| `src/Nodilume.Desktop` | WPF, gestione finestra e host WebView2 | Application e composizione Infrastructure |
| `src/Nodilume.Viewer` | Camera, rendering, interazione e transizioni | Contratto messaggi; nessun accesso al database |
| `tests/Nodilume.Tests` | Invarianti, persistenza e contratti | Progetti pertinenti |
| `tools/Nodilume.Benchmarks` | Generazione dati e misurazioni riproducibili | Application/Infrastructure |

I progetti usano il nome approvato Nodilume. Le versioni precise di SDK e dipendenze saranno fissate dopo la verifica dell’ambiente Windows, usando versioni supportate e file di lock. Three.js è il renderer candidato; 3d-force-graph è un componente da valutare, non un vincolo del formato dati.

## 5. Modello delle mappe

### 5.1 Entità

- `Map`: identità della mappa, titolo, versione dello schema e revisione.
- `Idea`: identità stabile, titolo e contenuto condiviso, appartenente a una mappa.
- `Placement`: rappresentazione di un’idea, gruppo padre, posizione locale XYZ, stato fissato e annotazione di contesto.
- `Relation`: collegamento tra idee, tipo, direzione e spiegazione. Non dipende dalle coordinate.
- `ViewState`: percorso esplorato tramite identificatori di Placement, camera relativa al contesto e selezione.
- `CommandRecord`: operazione con informazioni sufficienti a ripristinare lo stato precedente.

Ogni idea nasce con una rappresentazione principale. “Mostra anche in…” crea un Placement aggiuntivo, non una copia del contenuto. “Duplica come idea indipendente” crea una nuova Idea.

### 5.2 Reti interne: proposta tecnica

Il contenimento appartiene alle rappresentazioni: ciascun Placement ha al massimo un padre, che è un altro Placement o la radice della mappa. La rete interna può così essere specifica del contesto. Rappresentare di nuovo un’idea non duplica automaticamente tutto il suo sottoalbero.

Il titolo e il contenuto dell’idea restano comuni; posizione, annotazione e organizzazione interna restano locali. Questa scelta va resa esplicita nell’editor. Un’eventuale duplicazione di una struttura sarà un comando distinto.

Non si può spostare un Placement dentro sé stesso o un suo discendente. Per evitare ricorsione semantica, non si può inserire nella stessa catena antenato-discendente un’altra rappresentazione della medesima Idea. Le relazioni tra idee possono formare cicli.

### 5.3 Rimozioni

- Rimuovere una rappresentazione lascia invariati contenuto e altre rappresentazioni.
- Rimuovere una rappresentazione con figli richiede un’azione esplicita sul sottoalbero; nessuna promozione o cancellazione silenziosa dei figli.
- Un’idea priva di rappresentazioni resta recuperabile nella raccolta delle idee non collocate.
- Eliminare l’idea ovunque mostra impatto su rappresentazioni e relazioni e richiede conferma; il comando è annullabile.
- Undo/redo ripristina anche identità, relazioni, posizioni e annotazioni, in una transazione.

## 6. Navigazione e zoom semantico

Il movimento della camera e il livello semantico sono separati. Avvicinarsi a una zona vuota non deve aprire un gruppo casuale. Il candidato all’espansione è il nodo selezionato o quello stabilmente puntato.

La transizione continua attenua l’involucro del gruppo e rivela la rete interna. In uscita applica la transizione inversa. Le soglie di entrata e uscita sono diverse (isteresi) per evitare oscillazioni. Un ritardo nel caricamento mantiene visibile il contenitore e indica il caricamento; non mostra una falsa rete vuota.

Il modello usa coordinate locali al gruppo per evitare che molti livelli impongano coordinate globali enormi o minuscole. La trasformazione della camera preserva la continuità durante il cambio di riferimento. Non promettiamo profondità infinita: la robustezza viene verificata anche su gerarchie profonde.

Sono sempre disponibili percorso del contesto, ritorno al livello superiore e panoramica. La camera ripristinata all’apertura viene validata: se il contesto non esiste più si torna al più vicino antenato disponibile.

### Collegamenti trasversali

Quando i dettagli sono chiusi, le relazioni sono aggregate tra gli antenati visibili e mostrano un conteggio. Aprendo un gruppo, il conteggio si distribuisce sugli estremi pertinenti. Le relazioni interne a un gruppo chiuso non diventano falsi collegamenti del gruppo a sé stesso.

Una relazione concettuale non viene moltiplicata automaticamente fra tutte le rappresentazioni dei suoi estremi. La vista usa le rappresentazioni pertinenti al contesto; se la destinazione è ambigua mostra le collocazioni disponibili. La navigazione segue il Placement scelto.

## 7. Disposizione ibrida

La disposizione automatica interessa i nodi fratelli della zona richiesta. Una volta stabilizzata viene salvata; non riparte globalmente a ogni apertura. I nodi aggiunti vengono collocati vicino al contesto di creazione, risolvendo le collisioni locali.

Un nodo fissato mantiene la sua posizione locale. Spostare il suo gruppo padre muove tutto il gruppo nello spazio preservando la geometria interna. “Riordina questa zona” rispetta i nodi fissati ed è annullabile. La vista può anticipare il trascinamento, ma la nuova posizione è acquisita solo dopo la conferma dell’operazione C#.

## 8. Comunicazione C#–vista

C# possiede lo stato persistente. La vista possiede camera, animazioni e stato grafico transitorio. I messaggi sono versionati e includono tipo, requestId, mapId e revisione dove necessario.

Famiglie iniziali: apertura di una mappa, richiesta di una porzione, risposta/differenza della scena, modifica di posizione, fissaggio, selezione, risultato del comando, errore e salvataggio del punto di vista. Le risposte obsolete vengono scartate e le richieste non più utili annullate.

Le modifiche attraversano comandi validati e restituiscono un risultato prima della conferma persistente nella UI. Non si inviano coordinate della camera a C# a ogni fotogramma. Il punto di vista viene salvato con frequenza limitata e alla chiusura. Contenuti e dati utente sono testo, mai codice eseguibile nel visualizzatore.

Le risorse grafiche sono distribuite localmente con il programma. Il grafo funziona senza accesso a Internet una volta installati i runtime necessari. La vista può essere ricostruita dallo stato persistente dopo un riavvio del renderer.

## 9. Persistenza e scala

Proposta: un database SQLite per mappa, con identificatori stabili, indici per padre e adiacenze, transazioni per i comandi e migrazioni versionate. I salvataggi aggiornano i dati modificati, senza riscrivere l’intera mappa. Backup coerenti e recupero si realizzano con procedure compatibili con SQLite, non copiando indiscriminatamente un database aperto.

Le query restituiscono porzioni limitate e paginate. La ricerca usa indici e funziona anche su dati non caricati nel renderer. Le aggregazioni frequenti hanno conteggi persistenti o cache invalidabili. Un nodo con un enorme numero di figli richiede gruppi visivi temporanei o caricamento per porzioni: questi non cambiano la gerarchia salvata.

La vista ha budget distinti per nodi, collegamenti ed etichette. Il dettaglio dipende da scala, selezione e interesse; gli elementi omessi sono segnalati. Una cache limitata conserva le zone vicine e libera quelle lontane. La simulazione fisica non coinvolge tutti i nodi del database.

Non è un requisito mostrare contemporaneamente 300.000 nodi. È un requisito esplorare e modificare porzioni di una mappa di quelle dimensioni con dati integri e con prestazioni misurate.

## 10. Strategia di verifica

### Correttezza

Verificare: cicli di contenimento rifiutati; relazioni cicliche ammesse; isolamento delle mappe; modifica del contenuto condiviso; annotazioni locali; rimozione di una sola rappresentazione; undo di spostamenti e cancellazioni; round-trip persistente; messaggi obsoleti; riconciliazione dopo errore di salvataggio; conservazione dei nodi fissati.

### Esperienza Windows

Eseguire nella finestra WPF/WebView2: entrata e uscita continua, inversione a metà transizione, zoom rapido, salto a un collegamento esterno, destinazioni con più rappresentazioni, pannelli ridimensionati, ripristino della camera e riavvio del visualizzatore. Registrare GPU, RAM, CPU, risoluzione, versione del runtime e impostazioni.

### Dati sintetici

Fixture deterministiche con seme registrato: 10.000, 100.000 e 300.000 idee. Per ogni dataset registrare numero di Placement, relazioni concettuali, collegamenti visuali aggregati, profondità e grado massimo. Provare reti sparse, hub molto connessi, gerarchie profonde, gruppi con molti figli e rappresentazioni multiple.

### Misure e obiettivi iniziali proposti

| Misura | Obiettivo di lavoro iniziale |
|---|---|
| Navigazione stabilizzata | p95 del tempo di fotogramma <= 33 ms sul PC di riferimento |
| Riscontro visivo di selezione | <= 100 ms quando i dati sono già disponibili |
| Ricerca indicizzata | p95 <= 1 s sui dataset di riferimento |
| Apertura | Prima panoramica utile entro 5 s per la mappa da 300.000 idee |
| Memoria | Misurare l’intero albero dei processi; nessuna crescita monotona ripetendo lo stesso percorso |

Sono obiettivi proposti da confrontare con il PC effettivo, non prestazioni approvate o ottenute. Registrare prove fredde e calde e le dimensioni visibili. Se un obiettivo fallisce, riportarlo senza ridurre silenziosamente il dataset. Verificare prima budget, etichette, aggregazione, query e comunicazione; poi rivalutare il renderer se necessario.

## 11. Roadmap

Questa è una roadmap di consegne, non ancora un piano esecutivo con tutte le modifiche al codice.

| Fase | Consegna | Criterio di uscita |
|---|---|---|
| GRAPH.00 | Repository dedicata, specifica, verifica ambiente e convenzioni | Build riproducibile prevista, ambiente Windows identificato, dati personali esclusi da git |
| GRAPH.01 | Finestra Windows e scena 3D locale | Rotazione, movimento e focus funzionanti offline |
| GRAPH.02 | Identità, rappresentazioni, contenimento e SQLite | Invarianti e persistenza verificate con fixture piccole |
| GRAPH.03 | Zoom semantico continuo e collegamenti aggregati | Entrata/uscita e navigazione trasversale accettate dall’utente |
| GRAPH.04 | Caricamento selettivo e prove di scala | Report 10k/100k/300k con risultati e limiti espliciti |
| GRAPH.05 | Spostamento, fissaggio, undo e ripristino | Modifiche locali persistenti e orientamento conservato; chiusura del prototipo |
| GRAPH.06 | Primo editor personale completo | Gestione mappe, nodi, relazioni, ricerca, import/export e recupero |
| GRAPH.07 | Automazioni AI | Comandi strutturati, provenienza, annullamento del lotto e protezione delle modifiche manuali |
| GRAPH.08 | Modalità libri | EPUB e materiali cartacei acquisiti; capitoli, riferimenti e copertura parziale esplicita |

Ogni fase richiede solo le verifiche che coprono rischi concreti introdotti. Una fase non è conclusa per il solo fatto che il codice compila. Il piano esecutivo iniziale coprirà GRAPH.00–GRAPH.01; i successivi dipenderanno dai risultati verificati, mantenendo i contratti sopra descritti.

## 12. Repository e separazione dei dati

Repository dedicata: `Idiopathy-shh/nodilume`, privata. Checkout Windows: `C:\Sviluppo\Nodilume`. Nessuna modifica alle repository esistenti.

Codice, documentazione, fixture sintetiche e risultati riproducibili possono stare in git. Database personali, libri, fotografie, chiavi e file locali dell’utente restano fuori. La visibilità iniziale consigliata è privata. Repository creata il 18 settembre 2026; prima implementazione su branch `feat/graph-01-desktop-scene`.

Struttura documentale prevista nella repository: `docs/specs/graph-3d-design.md`, `docs/roadmap.md`, `docs/plans/`, `docs/benchmarks/` e un README con avvio e limiti attuali. Questo file è la consegna consolidata precedente alla creazione del repository.

## 13. Fonti tecniche consultate

- WPF: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/
- WebView2: https://learn.microsoft.com/en-us/microsoft-edge/webview2/
- Microsoft.Data.Sqlite: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/
- Three.js: https://threejs.org/docs/
- 3d-force-graph: https://github.com/vasturiano/3d-force-graph
- Movimento: https://vasturiano.github.io/3d-force-graph/example/controls-fly/
- Focus: https://vasturiano.github.io/3d-force-graph/example/click-to-focus/
- Espansione: https://vasturiano.github.io/3d-force-graph/example/expandable-nodes/
- Alternativa .NET: https://github.com/helix-toolkit/helix-toolkit

Le librerie costituiscono basi tecniche. Le demo non certificano zoom semantico completo, capacità di scala o integrazione con questo progetto.
