// A Function App (.NET 10, isolated worker model) on the shared plan. It reaches its storage,
// Application Insights and its triggers' services with its own identity: no keys or connection strings.

param name string
param location string
param tags object
param planId string
param identityId string
param identityClientId string
param storageAccountName string
param appInsightsConnectionString string

@description('Settings for the app\'s triggers and clients, by name.')
param connectionSettings object

var appSettings = concat(
  [
    { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
    { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
    { name: 'AZURE_CLIENT_ID', value: identityClientId }
    { name: 'AzureWebJobsStorage__accountName', value: storageAccountName }
    { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
    { name: 'AzureWebJobsStorage__clientId', value: identityClientId }
    { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
    { name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING', value: 'ClientId=${identityClientId};Authorization=AAD' }
  ],
  map(items(connectionSettings), setting => { name: setting.key, value: setting.value })
)

resource site 'Microsoft.Web/sites@2024-04-01' = {
  name: name
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    serverFarmId: planId
    httpsOnly: true
    keyVaultReferenceIdentity: identityId
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|10.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: appSettings
    }
  }
}

// Deployments sign in with Entra ID; publishing usernames and passwords are off.
resource scmBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: site
  name: 'scm'
  properties: {
    allow: false
  }
}

resource ftpBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: site
  name: 'ftp'
  properties: {
    allow: false
  }
}

output name string = site.name
