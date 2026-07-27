# Tirki

App di gestione delle finanze personali per Android, realizzata in .NET MAUI.

## Funzionalità

- Registro entrate/uscite con saldo e filtro per intervallo di date
- Categorie con suggerimento automatico in base alla descrizione del movimento
- Statistiche di spesa per categoria (grafico a torta)
- Andamento del risparmio mese per mese (grafico a linea)
- Preset per inserire al volo le spese ricorrenti
- Import dello storico da Excel
- Sincronizzazione su Google Drive tra più dispositivi
- Blocco dell'app con impronta digitale o PIN del telefono
- Tema chiaro/scuro

## Stack tecnico

- .NET MAUI (net10.0-android)
- SQLite (`sqlite-net-pcl`) per lo storage locale
- Google Drive API per la sincronizzazione
- LiveCharts2 per i grafici
- AndroidX.Biometric per il blocco con impronta/PIN

## Setup

Prima di compilare, crea questi due file a partire dai rispettivi `.template` (non sono su git perché contengono segreti):

- `Services/GoogleAuthService.Secrets.cs` — client secret OAuth di Google
- `Signing.local.props` — keystore per le build di release

Per lo sviluppo bastano gli strumenti standard .NET MAUI:

```
dotnet build -f net10.0-android
```

## Stato

Progetto personale a uso privato, non pubblicato su Play Store.
