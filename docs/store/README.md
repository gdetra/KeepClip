# Pubblicazione di KeepClip sul Microsoft Store

Materiali preparati per l'app MSIX registrata in Partner Center. Non costituiscono una submission già inviata o approvata.

## Identità configurata

| Campo | Valore |
| --- | --- |
| Package/Identity/Name | `15287gdetra.KeepClip` |
| Package/Identity/Publisher | `CN=58095AEE-5C8D-437E-90EB-9B5E2E56469C` |
| Package/Properties/PublisherDisplayName | `gdetra` |

I tre valori sono pubblici e sono salvati in `packaging/AppxManifest.xml`. Le precedenti installazioni `KeepClip.Personal` restano app distinte; vedere la migrazione nel README principale.

## Materiali

- `listing-it-IT.md`: testi da copiare nella scheda italiana. L'interfaccia attuale è italiana.
- `listing-en-US.md`: descrizione inglese, con indicazione esplicita che l'interfaccia è italiana.
- `../privacy.md`: informativa basata sul codice di questa versione.
- `screenshots/`: catture dimostrative; vedere il file README nella cartella per provenienza e limiti.

Per la privacy serve un URL pubblico e raggiungibile. Dopo la pubblicazione del file sul ramo `main` di un repository pubblico, un possibile URL è `https://github.com/gdetra/KeepClip/blob/main/docs/privacy.md`. **Non è dato per già pubblicato o accessibile**: verificarlo senza autenticazione prima di inserirlo in Partner Center. Se il repository è privato, pubblicare l'informativa su un sito pubblico o GitHub Pages. Non è stato eseguito alcun push o deploy.

Il contatto indicato è il repository pubblico del progetto. Se desideri offrire un canale privato per le richieste privacy, aggiungi un recapito controllato da te prima della pubblicazione; non sono stati inventati indirizzi email o dati del titolare.

## Prima di generare il pacchetto da inviare

`Build-Package.ps1 -StoreVersion 1.0.0.0` genera la prima versione del pacchetto per lo Store. Il parametro accetta quattro componenti numerici, con primo componente maggiore di zero, quarto componente zero e valori al massimo 65535. Versioni come `0.2.0.55040`, `1.0.0.1` o suffissi prerelease sono rifiutati prima del publish. Senza questo parametro il packaging ordinario continua a usare `AssemblySemFileVer` di GitVersion.

La versione del pacchetto Store è distinta dalla versione dell'eseguibile calcolata da GitVersion. Non vengono modificati tag o versioni SemVer del progetto. Il nome del file include la versione Store per distinguerlo dai pacchetti ordinari. Le versioni successive devono crescere, per esempio `1.0.1.0`, `1.1.0.0`, `2.0.0.0`: prima di ogni invio verificare in Partner Center l'ultima versione già distribuita. Non riutilizzare `1.0.0.0` per aggiornare utenti che hanno già quella versione.

Da PowerShell, nella radice del repository:

```powershell
./scripts/Build-Package.ps1 -Architecture x64 -StoreVersion 1.0.0.0 -PublishDirectory ./artifacts/store-publish-x64 -OutputDirectory ./artifacts/store-packages
```

Il risultato è `artifacts/store-packages/KeepClip-1.0.0.0-store-windows-x64.msix`. Usare lo stesso valore `-StoreVersion` per le altre architetture, mantenendo cartelle di publish distinte. Questa opzione non installa l'app e non firma il pacchetto.

In GitHub Actions, dopo aver integrato queste modifiche su `main`, aprire **Actions → Windows CI → Run workflow**, selezionare `main` e compilare il nuovo campo `store_version` con `1.0.0.0`. Il valore viene validato e applicato a tutte le architetture. Lasciandolo vuoto si ottengono gli MSIX ordinari; gli ZIP restano portabili e conservano il versionamento GitVersion. Le build automatiche su push non impostano una versione Store.

Le build locali effettuate prima del commit includono le modifiche non committate, mentre i metadati GitVersion identificano il commit di partenza. Per una build finale riproducibile usare il commit revisionato e integrato su `main`.

Usare publish separati per ZIP e MSIX e compilare dal codice corrente del branch, senza riutilizzare eseguibili di altri branch presenti in `artifacts/`. Sono disponibili le architetture x64, ARM64 e x86; caricare solo quelle effettivamente verificate. Il codice attuale dichiara Windows 10 build 17763 come minimo: verificare che i runtime inclusi lo supportino prima di dichiararlo nella scheda.

Per la distribuzione tramite Microsoft Store non occorre acquistare un certificato di firma: Microsoft firma il pacchetto dopo la certificazione. I pacchetti distribuiti direttamente da GitHub richiedono invece una firma attendibile per l'installazione ordinaria. Non sono state installate chiavi o certificati sul computer.

## Compilare la submission

1. Aprire KeepClip in Partner Center e scegliere **Start submission**.
2. Inserire i testi di `listing-it-IT.md` nella scheda italiana e caricare gli screenshot PNG.
3. Inserire l'URL privacy soltanto dopo aver pubblicato e verificato il documento.
4. Scegliere categoria **Produttività**, disponibilità, mercati e prezzo. Nessuna scelta commerciale è stata effettuata al posto del publisher.
5. Compilare il questionario di classificazione in base alle funzioni effettive dell'app. I contenuti copiati dagli utenti non sono contenuti editoriali forniti dall'app.
6. Dichiarare accuratamente l'accesso a testo, immagini e file: conservazione locale non significa assenza di accesso a dati personali.
7. Caricare i pacchetti verificati, eseguire Windows App Certification Kit e completare le verifiche manuali di installazione, avvio e aggiornamento.
8. Controllare i campi e il contratto sviluppatore aggiornato prima dell'invio alla certificazione.

## Pubblicare l'informativa privacy

La procedura più breve, se `gdetra/KeepClip` è pubblico, è pubblicare `docs/privacy.md` su `main`. Non serve attivare GitHub Pages per leggere un file Markdown su GitHub. Il file locale non è automaticamente disponibile online.

1. Aprire il repository GitHub e verificare che sia **Public**. Non cambiare la visibilità di un repository privato solo per pubblicare questo documento: in quel caso usare un sito pubblico o un repository separato dedicato all'informativa.
2. Dopo aver revisionato il testo, committare e inviare il branch `chore/signature`, quindi aprire una pull request verso `main`. Integrare la pull request quando le modifiche sono pronte. Se si vuole pubblicare solo l'informativa prima delle modifiche al packaging, è sufficiente includere il solo file `docs/privacy.md` in una pull request dedicata.
3. Aprire `https://github.com/gdetra/KeepClip/blob/main/docs/privacy.md` in una finestra privata del browser, senza autenticazione. Deve comparire il testo completo, con nome KeepClip, publisher, data e contatti; una pagina di login o un errore 404 non è adatta.
4. In Partner Center aprire la submission di KeepClip, individuare il campo **Privacy policy URL** nelle proprietà dell'app e incollare l'URL verificato. Salvare la sezione. Se il portale presenta il campo nella scheda Store, utilizzare lo stesso URL.
5. Mantenere stabile il percorso e aggiornare il documento e la data quando cambia il trattamento dei dati. Non usare il link al branch di lavoro come URL definitivo.

Non è stato eseguito alcun commit, push, merge o invio a Partner Center per pubblicare il documento. Per un canale di contatto privato, aggiungere prima un recapito reale controllato dal publisher; al momento il testo rimanda al repository e avverte che le segnalazioni GitHub sono pubbliche.

## Note suggerite per i certificatori

KeepClip è un'app desktop WinUI 3 per la cronologia locale degli appunti. Non richiede credenziali, account nell'app o acquisti. Il permesso `runFullTrust` serve per l'integrazione desktop: lettura degli appunti, icona nell'area di notifica, scorciatoie globali, identificazione dell'app di origine e incolla nella finestra scelta. La copia alla selezione, opzionale, usa le API di accessibilità Windows per leggere il testo selezionato non modificabile. L'avvio all'accesso è configurabile.

Per verificare: avviare KeepClip; copiare un breve testo in Blocco note; aprire il pannello con Ctrl+Shift+V; cercarlo, aggiungerlo ai preferiti e incollarlo in una finestra di Blocco note non elevata. Verificare anche immagini e file locali, pausa della cattura, eliminazione e svuotamento dei preferiti. Chiudere l'app dal menu dell'icona nell'area di notifica. Il database e le immagini restano locali e non vengono inviati allo sviluppatore.

## Fonti

- [Requisiti MSIX e versioni](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements)
- [Screenshot e immagini Store](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images)
- [Opzioni di firma](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)
