# Weather 4 Agents
## Obiettivo del progetto
Questo progetto è stato realizzato con lo scopo di creare un middleware che consenta di fornire ad un agente le informazioni sul tempo metereologico. Le informazioni sono scaricate dal proprio provider preferito e saranno salvate in locale come file json. In questo modo l'agente cnsumerà meno token per ottenere le informazioni sul tempo

# Regole
- Se qualcosa non ti è chiaro chiedimelo
- Il progetto è realizzato seguendo i paradigmi della clean architecture
- Il progetto è realizzato seguendo le best practice dello sviluppo .NET Core (>= 10.0)
- Codice, e commenti in inglese
- Prima di procedere con aggiornamenti importanti prepara una pianificazione in todo.md

# Funzionalità progetto
- API Rest: ritorneranno le informazioni del meteo in base alla richiesta (singolo giorno / forecast N giorni)
- HybridCache per inserire in cache le informazioni meteo più aggiornate
- Salvataggio dei forecast in una directory del file system in file json (suddiviso in città, un file json per ogni giorno). Questo aspetto sarà gestito da un task automatico che girerà ogni X, dove X è un tempo variabile impostato

