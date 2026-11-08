<a id="readme-top"></a>

<p align="center">
  <img src="src/KeepClip.App/Assets/AppLogo.png" width="80" alt="Logo KeepClip">
</p>

<h1 align="center">KeepClip</h1>

<p align="center"><strong>Copia una volta. Ritrova quando ti serve.</strong></p>
<p align="center">Testo, immagini e file in una cronologia degli appunti sempre a portata di tastiera.</p>
<p align="center">
  <a href="https://github.com/gdetra/KeepClip/actions/workflows/windows-ci.yml"><img src="https://github.com/gdetra/KeepClip/actions/workflows/windows-ci.yml/badge.svg?branch=main" alt="Build Windows"></a>
  &nbsp; Windows · x64 · ARM64 · x86
</p>
<p align="center">
  <a href="#scarica-una-build"><strong>Scarica KeepClip »</strong></a>
</p>
<p align="center">
  <a href="#demo">Guarda la demo</a> ·
  <a href="https://github.com/gdetra/KeepClip/issues/new">Segnala un problema</a> ·
  <a href="https://github.com/gdetra/KeepClip/issues">Proponi un miglioramento</a>
</p>

<details>
  <summary>Indice</summary>
  <ol>
    <li><a href="#il-progetto">Il progetto</a></li>
    <li><a href="#per-iniziare">Per iniziare</a></li>
    <li><a href="#come-si-usa">Come si usa</a></li>
    <li><a href="#privacy-e-dati-locali">Privacy e dati locali</a></li>
    <li><a href="#compatibilità-e-limiti">Compatibilità e limiti</a></li>
    <li><a href="#sviluppo">Sviluppo</a></li>
  </ol>
</details>

## Il progetto

KeepClip raccoglie le copie recenti in un pannello compatto per Windows. Cerca un contenuto, conserva quello che usi spesso e incollalo nella tua applicazione senza perdere il filo del lavoro.

![KeepClip con testo, immagini e file di esempio](docs/media/keepclip-overview.png)

*Interfaccia reale, contenuti inventati: la cattura proviene da un'istanza dimostrativa separata dalla cronologia e dagli appunti personali.*

### Funzionalità

- **Ritrovare le copie recenti:** testo, immagini, file e cartelle, con data e applicazione di provenienza.
- **Cercare al volo:** filtra il testo o i nomi dei file mentre scrivi.
- **Conservare i preferiti:** fissa le copie utili e ritrovale con il filtro dedicato.
- **Incollare dalla cronologia:** scegli una scheda con un clic oppure con frecce e Invio.
- **Personalizzare il pannello:** tema chiaro, scuro o di sistema, otto palette e superfici colorate opzionali.
- **Scegliere cosa attivare:** avvio all'accesso, suono di conferma e copia del testo alla selezione sono configurabili.

### Realizzato con

C# e .NET 10, WinUI 3 per l'interfaccia, SQLite per la persistenza locale, GitVersion per il versionamento e GitHub Actions per test e pacchetti. Le dipendenze sono dichiarate in [`Directory.Packages.props`](Directory.Packages.props).

<p align="right"><a href="#readme-top">Torna all'inizio ↑</a></p>

## Per iniziare

### Scarica una build

**[Apri le build Windows su GitHub Actions →](https://github.com/gdetra/KeepClip/actions/workflows/windows-ci.yml?query=branch%3Amain)**

Apri l'ultima esecuzione di `main` conclusa con successo, scorri fino ad **Artifacts** e scegli il download per il tuo dispositivo:

| Architettura | Per quale PC | Artifact |
| --- | --- | --- |
| [x64](https://github.com/gdetra/KeepClip/actions/workflows/windows-ci.yml?query=branch%3Amain) | La maggior parte dei PC Intel e AMD a 64 bit | `KeepClip-<versione>-windows-x64` |
| [ARM64](https://github.com/gdetra/KeepClip/actions/workflows/windows-ci.yml?query=branch%3Amain) | Windows su ARM, ad esempio Snapdragon | `KeepClip-<versione>-windows-arm64` |
| [x86](https://github.com/gdetra/KeepClip/actions/workflows/windows-ci.yml?query=branch%3Amain) | Sistemi Windows a 32 bit compatibili | `KeepClip-<versione>-windows-x86` |

Per scaricare gli artifact servono l'accesso a GitHub e i permessi sul repository. Ogni artifact contiene uno **ZIP portabile**, un **MSIX non firmato** e i checksum **`SHA256SUMS.txt`**. La conservazione richiesta è di 14 giorni, entro i limiti del repository; non vengono create automaticamente GitHub Releases.

### Avvio rapido

1. Estrai completamente lo **ZIP portabile interno** in una cartella stabile.
2. Avvia `KeepClip.exe`, mantenendo insieme tutti i file estratti. Il pacchetto include .NET e Windows App Runtime.
3. Copia un testo e apri la cronologia con **Win+V**, quando abilitato, oppure **Ctrl+Shift+V**.

L'app resta nell'area di notifica. Dal suo menu puoi aprire le impostazioni, mettere la cattura in pausa o uscire. Prima di avviare una nuova build, scegli **Esci** nella precedente istanza.

Il pacchetto MSIX richiede una firma con un certificato attendibile prima dell'installazione. Per provare l'app senza configurare la firma, usa lo ZIP portabile.

Se l'app non compare, verifica di aver estratto tutti i file dello ZIP in una cartella vuota.

## Come si usa

| Azione | Comando |
| --- | --- |
| Aprire o richiamare il pannello | **Win+V** quando abilitato; alternativa predefinita **Ctrl+Shift+V** |
| Cercare una copia | Scrivi nella casella di ricerca |
| Incollare una copia | Clic sulla scheda, oppure frecce e **Invio** |
| Chiudere senza incollare | **Esc** |
| Conservare una copia | Pulsante **Preferito** sulla scheda |
| Mostrare solo i preferiti | Filtro **Preferiti** in alto |
| Eliminare una copia | Pulsante **Elimina** sulla scheda |

Preferito ed Elimina agiscono sulla scheda senza incollarla. **Svuota…** conserva i preferiti, salvo scelta esplicita di includerli.

La **copia alla selezione** è opzionale: acquisisce il testo non modificabile esposto dalle applicazioni a Windows. Per campi di input, password e controlli non compatibili usa Ctrl+C. L'incolla verso finestre avviate come amministratore può richiedere Ctrl+V manuale.

### Demo

![Dalla cronologia completa alla ricerca del testo Ciao](docs/media/keepclip-search.gif)

*Sequenza di catture reali: cronologia completa e risultato della ricerca “Ciao”, con soli dati fittizi. [Preparazione dei media](docs/media/README.md).*

<p align="right"><a href="#readme-top">Torna all'inizio ↑</a></p>

## Privacy e dati locali

KeepClip conserva la cronologia **sul dispositivo**, senza account e senza inviare i contenuti degli appunti in rete.

- Nella versione portabile i dati sono nell'area locale dell'utente, normalmente `%LOCALAPPDATA%\KeepClip`; con MSIX vengono usati i dati locali del pacchetto. Il percorso effettivo è visibile nelle impostazioni.
- Il database SQLite e le immagini della cronologia sono separati dai file distribuiti dell'app.
- La cronologia **non è cifrata dall'app**. Metti in pausa la cattura quando lavori con contenuti che non vuoi conservare.
- Per file e cartelle vengono salvati i riferimenti agli originali. Eliminare una scheda non elimina i file; spostare o cancellare gli originali ne impedisce il successivo incolla.

### Aggiornare un'installazione precedente

La versione portabile usa normalmente `%LOCALAPPDATA%\KeepClip`. Le nuove build MSIX usano l'identità Store `15287gdetra.KeepClip`, il publisher `CN=58095AEE-5C8D-437E-90EB-9B5E2E56469C` e il nome publisher `gdetra`. Le precedenti build MSIX con identità `KeepClip.Personal` e publisher `CN=KeepClip` sono considerate un'app distinta da Windows.

Per conservare la cronologia, annota il percorso dati nelle impostazioni della versione precedente e chiudi entrambe le versioni. Conserva una copia di sicurezza dell'intera cartella dati, poi trasferiscine il contenuto nella cartella della nuova installazione prima di usarla. Se la nuova cartella contiene già una cronologia, conservala separatamente: non è previsto l'accorpamento di due database. Reimposta l'avvio automatico dalla nuova versione e disattiva la precedente voce nelle impostazioni di avvio di Windows. La migrazione non avviene automaticamente.

## Compatibilità e limiti

KeepClip è scritto in **C# / WinUI 3 per Windows**, con build x64, ARM64 e x86. macOS e Linux non sono supportati.

Il progetto dichiara Windows 10 dalla build 17763; i requisiti effettivi dipendono anche dalle versioni incluse di .NET e Windows App SDK. La compilazione automatica non certifica l'esecuzione su ogni versione di Windows o dispositivo ARM64. [Verifiche manuali e limiti noti](docs/manual-verification.md).

La cronologia conserva fino a **200 elementi e 250 MiB**. Le immagini sono normalizzate in PNG, fino a 20 MiB e 40 megapixel prima della decodifica. Sono supportati fino a 1000 riferimenti a file/cartelle per copia. OCR, formattazione HTML e file virtuali senza percorso non sono supportati. La destinazione deve accettare il tipo di contenuto incollato.

## Sviluppo

I materiali per la pubblicazione sul Microsoft Store sono in [docs/store/README.md](docs/store/README.md); l'[informativa privacy](docs/privacy.md) descrive il trattamento locale dei dati.

Servono lo SDK indicato in [`global.json`](global.json), il runtime .NET 8 per GitVersion e gli strumenti Windows/WinUI del Windows SDK. Apri [`KeepClip.slnx`](KeepClip.slnx) in un ambiente compatibile.

```powershell
dotnet tool restore
dotnet restore KeepClip.slnx --locked-mode
dotnet test tests/KeepClip.Tests --no-restore
dotnet test tests/KeepClip.Windows.Tests --no-restore
./scripts/Build-Portable.ps1 -Architecture x64
```

`-Architecture` accetta `x64`, `arm64` e `x86`. `Build-Package.ps1` genera MSIX; `Publish-App.ps1` produce la cartella con eseguibile e metadati di versione. Con `-SkipBuild` usa un publish generato da quest'ultimo script. Mantieni separati i publish per ZIP e MSIX, perché il packaging MSIX aggiunge il manifest di installazione. Le versioni NuGet sono centralizzate in [`Directory.Packages.props`](Directory.Packages.props).

Per lo Store usa `Build-Package.ps1 -StoreVersion 1.0.0.0` (o una versione successiva): il parametro applica al manifest MSIX una versione valida per lo Store, distinta dalla versione GitVersion dell'eseguibile. L'avvio manuale di Windows CI su `main` espone lo stesso parametro nel campo `store_version`. Vedi la [guida Store](docs/store/README.md) per comandi, progressione delle versioni e pubblicazione dell'informativa privacy.

La composizione dei servizi usa il container Microsoft con dipendenze nei costruttori, servizi condivisi e un nuovo ViewModel per ogni apertura delle impostazioni.

### Formattazione e qualità del codice

Le regole comuni sono in [`.editorconfig`](.editorconfig). Per applicarle:

```powershell
./scripts/Format-Code.ps1
./scripts/Format-Markup.ps1
```

Usa `-Check` su entrambi gli script per verificare lo stile senza modificare i file. Il formatter C# gestisce separatamente i progetti che condividono file sorgente. I membri hanno accessibilità esplicita e i campi privati seguono `_camelCase`; le firme lunghe usano parametri su più righe con rientro di quattro spazi. Il controllo sintattico richiede un solo tipo top-level per file e preserva token, commenti e direttive. `Test-CodeStyle.ps1` verifica questi casi in CI.

L'analyzer WinUI è una dipendenza privata del progetto App e viene eseguito nelle normali build. `SettingsCoordinator` serializza salvataggi e pausa dal tray, gestisce il rollback e restituisce risultati tipizzati con stato, motivi e avvisi. I ViewModel traducono questi risultati in stato della UI; finestre, dialoghi e picker restano nella presentazione.

### Versioni da Git

[`GitVersion.yml`](GitVersion.yml) calcola la versione da branch, tag e cronologia. Su `main` le build di sviluppo usano il suffisso `ci`; un tag come `v0.2.0` fissa la versione stabile. Controlla il risultato con `./scripts/Get-Version.ps1`. Per generare una build stabile in CI, applica il tag al commit corrente di `main` e avvia manualmente il workflow su `main`: il solo push del tag non avvia una build.

La CI calcola la versione una volta e la usa per tutte le architetture. Gli script di publish aggiornano le proprietà dell'eseguibile e salvano `KeepClip.version.json`; il packaging usa quei metadati per i nomi degli archivi e la versione numerica MSIX. I build diretti dall'IDE mantengono le versioni predefinite dello SDK.

### Quando parte la CI

Il workflow [Windows CI](.github/workflows/windows-ci.yml) esegue controlli di formattazione, build WinUI con analyzer e test sulle pull request verso `main`. I push a `main` che modificano codice o configurazione eseguono anche il packaging; i push limitati a Markdown o `docs/` sono esclusi. Push su altri branch e push di tag non avviano build automatiche. L'avvio manuale su `main` include il packaging; su altri branch esegue i controlli di qualità e i test.

<p align="right"><a href="#readme-top">Torna all'inizio ↑</a></p>

---

Struttura del README ispirata a [Best-README-Template](https://github.com/othneildrew/Best-README-Template), adattata a KeepClip.
