// Key Vault, with Azure RBAC for access. Every app's identity can read its secrets.

param name string
param location string
param tags object
param readerPrincipalIds array

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
  }
}

var readerRole = '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User

resource readers 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in readerPrincipalIds: {
    name: guid(vault.id, principalId, readerRole)
    scope: vault
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', readerRole)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

output uri string = vault.properties.vaultUri
