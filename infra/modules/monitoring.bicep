// Log Analytics and Application Insights. Apps send telemetry with their identities, not the
// connection string's key, so each identity is given Monitoring Metrics Publisher.

param environmentName string
param location string
param tags object

@description('Most telemetry the workspace takes in a day, in GB, as a decimal string; anything past it is dropped until the next day.')
param dailyCapGb string

param publisherPrincipalIds array

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-stratego-api-${environmentName}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: json(dailyCapGb)
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-stratego-api-${environmentName}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    IngestionMode: 'LogAnalytics'
    DisableLocalAuth: true
  }
}

var monitoringMetricsPublisher = '3913510d-42f4-4e42-8a64-420c390055eb'

resource publishers 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in publisherPrincipalIds: {
    name: guid(appInsights.id, principalId, monitoringMetricsPublisher)
    scope: appInsights
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringMetricsPublisher)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

output appInsightsId string = appInsights.id
output connectionString string = appInsights.properties.ConnectionString
