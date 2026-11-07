# Screenshot dimostrativi

Le catture PNG in questa cartella mostrano i controlli reali WinUI di KeepClip in una build dimostrativa isolata. I contenuti sono fittizi e in inglese; i nomi delle app di provenienza sono assegnati come esempi, senza acquisire dati dalle app nominate. Le icone di provenienza non disponibili usano il fallback reale dell'app. L'interfaccia resta in italiano, come la versione distribuita.

Per ottenere immagini adatte allo Store senza riprendere dati personali del desktop, la sola demo presenta il pannello su uno sfondo neutro. La finestra è impostata a 1600 × 900 pixel; la cattura delimitata esportata misura **1586 × 893 pixel**. Lo sfondo non è una funzione dell'app distribuita. Non sono state aggiunte funzioni, pulsanti o messaggi promozionali alle immagini. I pixel catturati sono stati soltanto ricodificati in PNG, senza ridimensionamento o ritocco.

- `01-history.png`: cronologia con testo, immagine e riferimento a un file.
- `02-search.png`: ricerca di una copia.
- `03-favorites.png`: filtro dei preferiti.

Le varianti `01-history-app-only.png`, `02-search-app-only.png` e `03-favorites-app-only.png` mostrano soltanto la finestra compatta dell'app, senza sfondo esterno, titoli o diciture aggiunte. Sono catture native separate, pensate per l'anteprima: non sostituiscono i tre PNG più grandi per lo Store, perché l'altezza della finestra compatta è inferiore al minimo Desktop richiesto.

La demo usa esclusivamente `artifacts/store-demo/app/demo-data`, senza aprire la cronologia personale, leggere/scrivere gli appunti di Windows, inizializzare monitor degli appunti, registrare scorciatoie o modificare l'avvio automatico. L'adattatore degli appunti è inerte. La compilazione dimostrativa non va distribuita come prodotto.

Le didascalie da copiare sono nelle schede Store. Ogni PNG deve essere controllato a grandezza naturale e mantenere almeno 1366 × 768 pixel per il caricamento Desktop, secondo i [requisiti Microsoft](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images). Non caricare GIF, database, log o la build dimostrativa.
