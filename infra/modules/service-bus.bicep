// Service Bus, Basic tier, with its two queues. Keys are off: each app gets only the send or
// receive role it needs, on only the queues it uses.

param name string
param location string
param tags object

param apiPrincipalId string
param workerPrincipalId string
param botPrincipalId string

resource namespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    disableLocalAuth: true
    minimumTlsVersion: '1.2'
  }
}

resource botTurns 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = {
  parent: namespace
  name: 'bot-turns'
}

resource gameTimers 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = {
  parent: namespace
  name: 'game-timers'
}

var sender = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
var receiver = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'

// The API schedules leave checks.
resource apiSendsTimers 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(gameTimers.id, apiPrincipalId, sender)
  scope: gameTimers
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', sender)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// The worker queues bot turns, and schedules and handles timers.
resource workerSendsBotTurns 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(botTurns.id, workerPrincipalId, sender)
  scope: botTurns
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', sender)
    principalId: workerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource workerSendsTimers 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(gameTimers.id, workerPrincipalId, sender)
  scope: gameTimers
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', sender)
    principalId: workerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource workerReceivesTimers 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(gameTimers.id, workerPrincipalId, receiver)
  scope: gameTimers
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', receiver)
    principalId: workerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// The bot takes its turns.
resource botReceivesBotTurns 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(botTurns.id, botPrincipalId, receiver)
  scope: botTurns
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', receiver)
    principalId: botPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output fullyQualifiedNamespace string = '${namespace.name}.servicebus.windows.net'
