@description('Globalt unikt namn för Azure Web App.')
param appName string = 'notifications-${uniqueString(resourceGroup().id)}'

@description('Azure-region. Resursgruppens region används som standard.')
param location string = resourceGroup().location

@description('VAPID public key, Base64 URL-format.')
param vapidPublicKey string

@secure()
@description('VAPID private key. Skapas en gång och återanvänds.')
param vapidPrivateKey string

@secure()
@description('Hemlig nyckel som krävs för att skicka notiser.')
param adminKey string

@description('VAPID subject, exempelvis mailto:admin@example.com.')
param vapidSubject string

var storageName = take(replace('${appName}${uniqueString(resourceGroup().id)}', '-', ''), 24)
var planName = '${appName}-plan'

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      ftpsState: 'Disabled'
      http20Enabled: true
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'Storage__ConnectionString'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storage.listKeys().keys[0].value}'
        }
        {
          name: 'Push__VapidPublicKey'
          value: vapidPublicKey
        }
        {
          name: 'Push__VapidPrivateKey'
          value: vapidPrivateKey
        }
        {
          name: 'Push__Subject'
          value: vapidSubject
        }
        {
          name: 'Push__AdminKey'
          value: adminKey
        }
      ]
    }
  }
}

output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'

