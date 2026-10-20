// Everything one environment runs on, applied to its resource group on every deployment:
//   az deployment group create -g rg-stratego-api-<env> -f infra/main.bicep -p infra/<env>.bicepparam

@allowed([
  'test'
  'prod'
])
param environmentName string

@description('App Service Plan size.')
param appServicePlanSku string

@description('Most telemetry the environment may send to Log Analytics in a day.')
param telemetryDailyCapGb string

@description('Where production alerts are emailed.')
param alertEmail string = ''

param location string = resourceGroup().location

var isProduction = environmentName == 'prod'
var tags = {
  app: 'stratego-api'
  environment: environmentName
}

// Identities: one per app, used for everything the app reaches in Azure.

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-stratego-api-app-${environmentName}'
  location: location
  tags: tags
}

resource workerIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-stratego-api-worker-${environmentName}'
  location: location
  tags: tags
}

resource botIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-stratego-api-bot-${environmentName}'
  location: location
  tags: tags
}

var allPrincipalIds = [
  apiIdentity.properties.principalId
  workerIdentity.properties.principalId
  botIdentity.properties.principalId
]

// Data and messaging

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    environmentName: environmentName
    location: location
    tags: tags
    dailyCapGb: telemetryDailyCapGb
    publisherPrincipalIds: allPrincipalIds
  }
}

module cosmos 'modules/cosmos.bicep' = {
  name: 'cosmos'
  params: {
    name: 'cosmos-stratego-api-${environmentName}'
    location: location
    tags: tags
    serverless: !isProduction
    dataContributorPrincipalIds: [
      apiIdentity.properties.principalId
      workerIdentity.properties.principalId
    ]
  }
}

module serviceBus 'modules/service-bus.bicep' = {
  name: 'service-bus'
  params: {
    name: 'sb-stratego-api-${environmentName}'
    location: location
    tags: tags
    apiPrincipalId: apiIdentity.properties.principalId
    workerPrincipalId: workerIdentity.properties.principalId
    botPrincipalId: botIdentity.properties.principalId
  }
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'key-vault'
  params: {
    name: 'kv-stratego-api-${environmentName}'
    location: location
    tags: tags
    readerPrincipalIds: allPrincipalIds
  }
}

module functionsStorage 'modules/functions-storage.bicep' = {
  name: 'functions-storage'
  params: {
    name: 'ststrategoapi${environmentName}'
    location: location
    tags: tags
    functionAppPrincipalIds: [
      workerIdentity.properties.principalId
      botIdentity.properties.principalId
    ]
  }
}

// Apps

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'asp-stratego-api-${environmentName}'
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: appServicePlanSku
  }
  properties: {
    reserved: true
  }
}

var apiSiteConfig = {
  linuxFxVersion: 'DOTNETCORE|10.0'
  alwaysOn: true
  webSocketsEnabled: true
  healthCheckPath: '/health'
  http20Enabled: true
  ftpsState: 'Disabled'
  minTlsVersion: '1.2'
  appSettings: [
    { name: 'AZURE_CLIENT_ID', value: apiIdentity.properties.clientId }
    { name: 'OTEL_SERVICE_NAME', value: 'app-stratego-api-${environmentName}' }
    { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: monitoring.outputs.connectionString }
    { name: 'ConnectionStrings__cosmos', value: cosmos.outputs.endpoint }
    { name: 'ConnectionStrings__servicebus', value: serviceBus.outputs.fullyQualifiedNamespace }
    { name: 'ConnectionStrings__keyvault', value: keyVault.outputs.uri }
  ]
}

resource api 'Microsoft.Web/sites@2024-04-01' = {
  name: 'app-stratego-api-${environmentName}'
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    keyVaultReferenceIdentity: apiIdentity.id
    siteConfig: apiSiteConfig
  }
}

// Production releases deploy here first, warm up, and are swapped in.
resource apiStaging 'Microsoft.Web/sites/slots@2024-04-01' = if (isProduction) {
  parent: api
  name: 'staging'
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    keyVaultReferenceIdentity: apiIdentity.id
    siteConfig: apiSiteConfig
  }
}

// Deployments sign in with Entra ID; publishing usernames and passwords are off.
resource apiScmBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: api
  name: 'scm'
  properties: {
    allow: false
  }
}

resource apiFtpBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: api
  name: 'ftp'
  properties: {
    allow: false
  }
}

module worker 'modules/function-app.bicep' = {
  name: 'worker'
  params: {
    name: 'func-stratego-api-worker-${environmentName}'
    location: location
    tags: tags
    planId: plan.id
    identityId: workerIdentity.id
    identityClientId: workerIdentity.properties.clientId
    storageAccountName: functionsStorage.outputs.name
    appInsightsConnectionString: monitoring.outputs.connectionString
    connectionSettings: {
      cosmos__accountEndpoint: cosmos.outputs.endpoint
      cosmos__credential: 'managedidentity'
      cosmos__clientId: workerIdentity.properties.clientId
      servicebus__fullyQualifiedNamespace: serviceBus.outputs.fullyQualifiedNamespace
      servicebus__credential: 'managedidentity'
      servicebus__clientId: workerIdentity.properties.clientId
      ConnectionStrings__keyvault: keyVault.outputs.uri
    }
  }
}

module bot 'modules/function-app.bicep' = {
  name: 'bot'
  params: {
    name: 'func-stratego-api-bot-${environmentName}'
    location: location
    tags: tags
    planId: plan.id
    identityId: botIdentity.id
    identityClientId: botIdentity.properties.clientId
    storageAccountName: functionsStorage.outputs.name
    appInsightsConnectionString: monitoring.outputs.connectionString
    connectionSettings: {
      servicebus__fullyQualifiedNamespace: serviceBus.outputs.fullyQualifiedNamespace
      servicebus__credential: 'managedidentity'
      servicebus__clientId: botIdentity.properties.clientId
      ConnectionStrings__keyvault: keyVault.outputs.uri
    }
  }
}

// Alerts: production only, where the service-level targets apply.
module alerts 'modules/alerts.bicep' = if (isProduction) {
  name: 'alerts'
  params: {
    environmentName: environmentName
    tags: tags
    alertEmail: alertEmail
    appInsightsId: monitoring.outputs.appInsightsId
    cosmosAccountId: cosmos.outputs.accountId
    planId: plan.id
  }
}

output apiName string = api.name
output apiHostName string = api.properties.defaultHostName
output workerName string = worker.outputs.name
output botName string = bot.outputs.name
