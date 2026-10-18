// The Function Apps' own storage. Shared keys are off: the apps reach it with their identities.

param name string
param location string
param tags object
param functionAppPrincipalIds array

resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    allowSharedKeyAccess: false
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

var roles = [
  'b7e6dc6d-f1e8-4753-8033-0f276bb0955b' // Storage Blob Data Owner
  '974c5e8b-45b9-4653-ba55-5f855dd0fb88' // Storage Queue Data Contributor
  '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3' // Storage Table Data Contributor
]

var grants = flatten(map(functionAppPrincipalIds, principalId => map(roles, role => {
  principalId: principalId
  role: role
})))

resource access 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for grant in grants: {
    name: guid(account.id, grant.principalId, grant.role)
    scope: account
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', grant.role)
      principalId: grant.principalId
      principalType: 'ServicePrincipal'
    }
  }
]

output name string = account.name
