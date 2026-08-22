# Notifications

En .NET 10 Blazor Web App med Interactive WebAssembly, MudBlazor, mörkblått tema och stöd för att skicka och ta emot Web Push-notiser. Hela lösningen körs i Azure Web App. Prenumerationer sparas beständigt i Azure Table Storage.

## Arkitektur

- `Notifications` – ASP.NET Core-värd, push-API och Azure Table Storage.
- `Notifications.Client` – interaktivt WebAssembly-gränssnitt och JavaScript-interoperabilitet med Push API.
- `Notifications.Shared` – kontrakt som delas av klient och server.
- `Notifications.KeyGenerator` – skapar VAPID-nyckelpar och en säker sändningsnyckel.
- `infra/main.bicep` – App Service-plan, Web App, Storage Account och appinställningar.

Sändning kräver headern `X-Notifications-Key`. Nyckeln skrivs in i gränssnittet, hålls bara i minnet och sparas inte i webbläsaren.

## Kör lokalt

Du behöver .NET 10 SDK och, om prenumerationerna ska överleva omstarter, Azurite eller ett Azure Storage-konto.

1. Skapa nycklar:

   ```bash
   dotnet run --project tools/Notifications.KeyGenerator
   ```

2. Skapa `src/Notifications/appsettings.Local.json`:

   ```json
   {
     "Storage": {
       "ConnectionString": "UseDevelopmentStorage=true"
     },
     "Push": {
       "VapidPublicKey": "PUBLIC_KEY",
       "VapidPrivateKey": "PRIVATE_KEY",
       "Subject": "mailto:you@example.com",
       "AdminKey": "ADMIN_KEY"
     }
   }
   ```

3. Starta appen. `appsettings.Local.json` läses automatiskt men checkas inte in. Du kan även sätta motsvarande miljövariabler:

   ```bash
   export Storage__ConnectionString='UseDevelopmentStorage=true'
   export Push__VapidPublicKey='PUBLIC_KEY'
   export Push__VapidPrivateKey='PRIVATE_KEY'
   export Push__Subject='mailto:you@example.com'
   export Push__AdminKey='ADMIN_KEY'
   dotnet run --project src/Notifications
   ```

Utan `Storage__ConnectionString` används minneslagring lokalt. HTTPS krävs för Web Push, med undantag för browserns säkra `localhost`-kontext.

## Skapa Azure-resurser

Kopiera exempelparametrarna till en fil som inte checkas in och fyll i värdena från nyckelgeneratorn:

```bash
cp infra/main.bicepparam.example infra/main.bicepparam
az group create --name notifications-rg --location swedencentral
az deployment group create \
  --resource-group notifications-rg \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam
```

Bicep-mallen använder en Linux B1 App Service-plan och konfigurerar .NET 10, HTTPS, TLS 1.2 och en privat Storage-anslutning via appinställning.

## Automatisk driftsättning

I GitHub-repots environment `production` skapar du:

- Variable `AZURE_WEBAPP_NAME` – namnet som Bicep-kommandot returnerar.
- Secret `AZURE_WEBAPP_PUBLISH_PROFILE` – XML-filen från **Azure Portal → Web App → Download publish profile**.

Efter merge till `main` bygger och driftsätter `deploy-azure.yml` appen. `ci.yml` bygger även varje pull request.

## iPhone och iPad

På iOS/iPadOS måste webbappen först installeras via **Dela → Lägg till på hemskärmen**. Öppna sedan den installerade appen och tryck **Aktivera notiser**. Web Push fungerar inte från en vanlig Safari-flik på iPhone.

## Säkerhet och drift

- VAPID private key och sändningsnyckeln ska endast finnas i Azure App Settings/GitHub Secrets.
- Sändnings-endpointen har fast-tidsjämförelse av nyckeln och begränsas till fem anrop per minut och instans.
- Utgångna push-prenumerationer tas automatiskt bort när push-tjänsten svarar med `404` eller `410`.
- Byt `Push__AdminKey` i Azure om sändningsnyckeln har röjts.
