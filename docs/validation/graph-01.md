# GRAPH.00–GRAPH.01 — verifica e playbook

Data: 18 settembre 2026.

## Ambiente verificato

- Windows 11 Pro 10.0.26200, OFFICE-PC.
- AMD Ryzen 9 5950X, Radeon RX 460, circa 32 GiB di RAM.
- .NET SDK 10.0.400, Node 24.19.0, npm 11.17.0.
- WebView2 Runtime 153.0.4234.32.
- Scena: 25 nodi e 27 relazioni, un solo livello visibile.
- Screenshot acquisito: superficie WebView2 1264 x 752 pixel nella finestra iniziale.

## Verifiche eseguite

| Verifica | Esito | Portata |
|---|---|---|
| Repository privata separata | PASS | Idiopathy-shh/nodilume, checkout C:\Sviluppo\Nodilume |
| Test camera prima dell'implementazione | 4 fallimenti attesi | Funzione di focus non implementata |
| Test camera implementata | 4/4 PASS | Distanza, direzione, caso coincidente, input invalidi |
| Build viewer + desktop | PASS | Zero errori e zero avvisi .NET; TypeScript controllato |
| Ripristino dipendenze bloccate | PASS | npm ci e dotnet restore --locked-mode |
| Avvio reale WPF/WebView2 | PASS | Scena ricevuta da C# e disegnata |
| Risorse della pagina | PASS | Tutte le risorse osservate provengono da nodilume.local |
| Selezione | PASS | Il titolo selezionato arriva al pannello |
| Focus e panoramica | PASS automatico | Il nodo proiettato raggiunge il centro e poi se ne allontana |
| Ridimensionamento | PASS | Canvas si adatta al restringimento e al ripristino della finestra |
| Aspetto iniziale | Ispezionato | Screenshot della vista reale, testi e rete leggibili |
| Sensazione di navigazione con mouse/tastiera | DA ACCETTARE | Richiede prova dell'utente |
| Prestazioni 10k/100k/300k | NON ESEGUITE | Previste da GRAPH.04 |

Il controllo delle risorse prova il caricamento locale della pagina; non è una misura
del traffico dell'intero sistema e non ha disattivato la connessione del PC.
La qualità percettiva di rotazione, pan e attraversamento non è certificata dallo smoke.

## Correzioni emerse durante verifica/revisione

- Dichiarazione del modulo CSS richiesta dal controllo TypeScript.
- Chiusura durante avvio: intercettare le eccezioni anche quando WebView2 è stato disposto;
  aggiornare la barra di stato soltanto se la finestra è ancora aperta.
- Smoke rafforzato per osservare lo spostamento del nodo proiettato e il resize;
  timeout applicato anche alle operazioni asincrone WebView2.
- Messaggi con versione non numerica vengono ignorati senza eccezioni.

## Prova manuale dell'utente

1. Avviare `./scripts/run.ps1` dalla radice della repository.
2. Ruotare in entrambe le direzioni e verificare la profondità dei nodi.
3. Spostare la scena con il tasto destro; usare la rotella in entrambi i sensi.
4. Fare doppio clic su «Conoscenza» e poi su un nodo periferico.
5. Premere Panoramica: la rete deve tornare interamente leggibile.
6. Fare clic sullo sfondo; provare WASD, Q/E e Maiusc. Cambiare finestra:
   al ritorno il movimento non deve continuare da solo.
7. Ridimensionare e chiudere la finestra; riaprire e verificare l'avvio.

Registrare accettazione o problemi in questo documento. GRAPH.01 non è chiusa
dal solo esito della compilazione. Nessuna modifica alla mappa è persistente in questa fase.

## Prossima consegna

GRAPH.02: identità Idea/Placement, contenimento e invarianti, isolamento mappe,
SQLite e round-trip dei dati. Lo zoom multiscala continuo appartiene a GRAPH.03.
