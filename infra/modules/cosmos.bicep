// Cosmos DB for NoSQL: one database whose throughput the containers share. Keys are off: apps
// reach it with their identities, through Cosmos DB's own data-plane roles.

param name string
param location string
param tags object

@description('Serverless on test; on production, the free tier with 1,000 RU/s on the database.')
param serverless bool

param dataContributorPrincipalIds array

resource account 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: name
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    enableFreeTier: !serverless
    capabilities: serverless ? [{ name: 'EnableServerless' }] : []
    disableLocalAuth: true
    minimalTlsVersion: 'Tls12'
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-11-15' = {
  parent: account
  name: 'stratego'
  properties: {
    resource: {
      id: 'stratego'
    }
    options: serverless ? {} : { throughput: 1000 }
  }
}

var containers = [
  {
    name: 'games'
    partitionKey: '/gameId'
    // Nothing queries inside a game's state or an event's data.
    excludedPaths: ['/state/*', '/data/*']
  }
  { name: 'players', partitionKey: '/playerId', excludedPaths: [] }
  { name: 'logins', partitionKey: '/id', excludedPaths: [] }
  { name: 'leases', partitionKey: '/id', excludedPaths: [] }
]

resource sqlContainers 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = [
  for container in containers: {
    parent: database
    name: container.name
    properties: {
      resource: {
        id: container.name
        partitionKey: {
          paths: [container.partitionKey]
          kind: 'Hash'
          version: 2
        }
        indexingPolicy: {
          indexingMode: 'consistent'
          automatic: true
          includedPaths: [{ path: '/*' }]
          excludedPaths: concat(map(container.excludedPaths, path => { path: path }), [{ path: '/"_etag"/?' }])
        }
      }
    }
  }
]

// Cosmos DB Built-in Data Contributor
var dataContributor = '00000000-0000-0000-0000-000000000002'

resource dataContributors 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = [
  for principalId in dataContributorPrincipalIds: {
    parent: account
    name: guid(account.id, principalId, dataContributor)
    properties: {
      roleDefinitionId: '${account.id}/sqlRoleDefinitions/${dataContributor}'
      principalId: principalId
      scope: account.id
    }
  }
]

output accountId string = account.id
output endpoint string = account.properties.documentEndpoint
