# Tags Sync tool
Tool om automatisch tags in te laden in Novilog (v1) databases via CSV file. Na het creeren of bijwerken van het ingesteld pad wordt de CSV file ingelezen en worden de tags toegevoegd of geupdate in de database. 

## Configuratie
Gebruik een `config.ini` bestand, zie het `example_config.ini` bestand voor een voorbeeld.

## Publiceren
Gebruik de .NET 10 SDK en gebruik dit commando om een standalone executable te maken:
```bash
dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true --self-contained true
```

## Maintainer
- Thomas Bruninx (IndigoCare)