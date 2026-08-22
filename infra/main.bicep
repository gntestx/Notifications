@description('Globally unique name for the Azure App Service.')
param appName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@secure()
@minLength(24)
@description('Secret key required by the send endpoints.')
param adminKey string

@description('VAPID public key.')
param vapidPublicKey string

@secure()
@description('VAPID private key.')
param vapidPrivateKey string

@description('VAPID contact, normally mailto:you@example.com.')
param vapidSubject string

@description('Exact GitHub Pages origin, without a trailing slash.')
param clientOrigin string

var storageName = take(toLower(replace('${appName}${uniqueString(resourceGroup().id)}', '-', '')), 24)

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource pushSubscriptions 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: tableService
  name: 'PushSubscriptions'
}

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${appName}-plan'
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
}

resource app 'Microsoft.Web/sites@2024-04-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      alwaysOn: true
      http20Enabled: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'AdminKey'
          value: adminKey
        }
        {
          name: 'Push__Subject'
          value: vapidSubject
        }
        {
          name: 'Push__PublicKey'
          value: vapidPublicKey
        }
        {
          name: 'Push__PrivateKey'
          value: vapidPrivateKey
        }
        {
          name: 'AllowedOrigins__0'
          value: clientOrigin
        }
        {
          name: 'Storage__TableName'
          value: 'PushSubscriptions'
        }
        {
          name: 'Storage__TableConnectionString'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
      ]
    }
  }
}

output apiUrl string = 'https://${app.properties.defaultHostName}'
output webAppName string = app.name
