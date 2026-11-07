# Informativa privacy di KeepClip

Ultimo aggiornamento: 7 novembre 2026.

KeepClip è un gestore della cronologia degli appunti per Windows, pubblicato da **gdetra**. Questa informativa riguarda l'app KeepClip e le funzionalità descritte qui, non i servizi Microsoft o GitHub.

## Dati a cui accede l'app

Quando è in esecuzione e la cattura non è in pausa, KeepClip legge gli appunti di Windows e conserva i contenuti supportati: testo, immagini e riferimenti a file e cartelle. Memorizza anche data e ora della copia, tipo di contenuto, stato di preferito e, quando disponibile, nome e icona dell'applicazione di provenienza. Per file e cartelle conserva i percorsi agli originali, senza creare una copia dei file nella cronologia.

Se abiliti la copia alla selezione, KeepClip può leggere il testo selezionato non modificabile esposto dalle applicazioni tramite le API di accessibilità di Windows. Questa funzione esclude i controlli riconosciuti come campi password o campi modificabili. La normale cattura degli appunti può comunque conservare password e altri dati sensibili che copi manualmente: KeepClip non offre un filtro generale per riconoscerli.

KeepClip salva inoltre le preferenze dell'app e un log diagnostico locale con eventi tecnici, orari, identificativi di processo e informazioni sugli errori. Il log non è progettato per registrare testo o immagini degli appunti; le tracce degli errori di avvio possono contenere percorsi tecnici.

## Finalità e conservazione

I dati servono a mostrare la cronologia, cercare e riutilizzare le copie, conservare i preferiti, applicare le impostazioni e diagnosticare problemi dell'app.

La cronologia è conservata localmente, normalmente in `%LOCALAPPDATA%\KeepClip` per la versione portabile e nell'area dati locale del pacchetto per la versione MSIX. Il percorso effettivo è visibile nelle impostazioni. I dati restano disponibili tra gli avvii dell'app.

La cronologia è soggetta a limiti di 200 elementi e 250 MiB di contenuti contabilizzati dall'app; questi valori non sono una garanzia sulla dimensione complessiva della cartella dati. Al raggiungimento dei limiti vengono rimosse le copie non preferite più vecchie. I preferiti possono impedire l'acquisizione di nuovi elementi se occupano tutta la capacità disponibile. Non è previsto un termine fisso di scadenza per ogni copia.

## Trasmissione e servizi esterni

KeepClip non richiede un account nell'app e non implementa sincronizzazione cloud, pubblicità o invio automatico della cronologia e dei log allo sviluppatore. Il codice di questa versione non include servizi di analisi dell'utilizzo o telemetria remota.

Quando riutilizzi una copia, il contenuto viene scritto negli appunti di Windows e può essere incollato nell'applicazione scelta. Le altre applicazioni, le funzioni di sincronizzazione degli appunti di Windows, i backup e gli strumenti di sicurezza del dispositivo possono trattare quei dati secondo le proprie impostazioni e informative.

Microsoft Store può trattare dati relativi a download, aggiornamenti, acquisti o diagnostica secondo l'[informativa Microsoft](https://privacy.microsoft.com/privacystatement). L'uso di GitHub per assistenza è soggetto all'[informativa GitHub](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement).

## Sicurezza e controllo dei dati

La cronologia, le immagini e le impostazioni **non sono cifrate da KeepClip**. La loro protezione dipende dall'account Windows, dalle autorizzazioni del dispositivo e dalle eventuali misure di cifratura o backup configurate sul sistema.

Puoi mettere in pausa la cattura dal menu dell'icona nell'area di notifica, disabilitare la copia alla selezione nelle impostazioni, eliminare singole copie oppure usare **Svuota…**. Lo svuotamento conserva i preferiti se non scegli esplicitamente di includerli. Eliminare un riferimento a un file dalla cronologia non elimina il file originale. La cancellazione nell'app non equivale alla cancellazione sicura dei supporti e non rimuove eventuali backup o copie in altre applicazioni.

Il log diagnostico può essere esportato su tua iniziativa. Controllalo prima di condividerlo e non allegare database, immagini della cronologia o altri dati sensibili alle segnalazioni pubbliche.

## Contatti e aggiornamenti

Per domande sul funzionamento dell'app e sulla privacy puoi contattare gdetra attraverso il [repository ufficiale KeepClip](https://github.com/gdetra/KeepClip). Le [segnalazioni GitHub](https://github.com/gdetra/KeepClip/issues) sono pubbliche: non inserirvi dati personali o contenuti degli appunti.

Questa informativa potrà essere aggiornata se cambia il trattamento dei dati. La data in apertura identifica la versione del testo.
