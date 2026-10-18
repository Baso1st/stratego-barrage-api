// What the deploying identity may do in its environment's resource group: create resources,
// and give the apps' own identities the data roles they need, but no other role.

@description('Principal ID of the deploying identity.')
param principalId string

var contributor = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
var rbacAdministrator = 'f58310d9-a9f6-439a-9e8d-f62e7b41a168'

var assignableRoles = [
  '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39' // Azure Service Bus Data Sender
  '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0' // Azure Service Bus Data Receiver
  'b7e6dc6d-f1e8-4753-8033-0f276bb0955b' // Storage Blob Data Owner
  '974c5e8b-45b9-4653-ba55-5f855dd0fb88' // Storage Queue Data Contributor
  '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3' // Storage Table Data Contributor
  '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
  '3913510d-42f4-4e42-8a64-420c390055eb' // Monitoring Metrics Publisher
]
var roles = join(assignableRoles, ', ')

resource contributorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, principalId, contributor)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributor)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

resource rbacAdministratorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, principalId, rbacAdministrator)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', rbacAdministrator)
    principalId: principalId
    principalType: 'ServicePrincipal'
    description: 'Can assign and remove only the data roles the apps need.'
    conditionVersion: '2.0'
    condition: '((!(ActionMatches{\'Microsoft.Authorization/roleAssignments/write\'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${roles}})) AND ((!(ActionMatches{\'Microsoft.Authorization/roleAssignments/delete\'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${roles}}))'
  }
}
