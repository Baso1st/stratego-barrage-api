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

var keyVaultSecretsUser = '4633458b-17de-408a-b874-0445c86b69e6'

resource readers 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in readerPrincipalIds: {
    name: guid(vault.id, principalId, keyVaultSecretsUser)
    scope: vault
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUser)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

output uri string = vault.properties.vaultUri
