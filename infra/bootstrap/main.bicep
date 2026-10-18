// One-time setup of the subscription, deployed by bootstrap.ps1 in this folder.
// Creates each environment's resource group, and the identity GitHub Actions deploys that environment as.

targetScope = 'subscription'

@description('Azure region for the resource groups and identities.')
param location string = 'centralus'

@description('The GitHub repository whose workflows deploy the environments, as GitHub names it in OIDC subjects: owner@owner-id/name@repository-id. The IDs never change, so a recreated repository with the same name can\'t sign in.')
param repository string = 'Baso1st@29257536/stratego-barrage-api@1423901951'

var environments = [
  { name: 'test', githubEnvironment: 'test' }
  { name: 'prod', githubEnvironment: 'production' }
]

// Kept apart from the environments' groups, so a deployment can never delete the identity running it.
resource identitiesGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-stratego-api-identities'
  location: location
}

resource environmentGroups 'Microsoft.Resources/resourceGroups@2024-03-01' = [
  for env in environments: {
    name: 'rg-stratego-api-${env.name}'
    location: location
  }
]

module deployers 'deployer-identity.bicep' = [
  for env in environments: {
    name: 'deployer-identity-${env.name}'
    scope: identitiesGroup
    params: {
      name: 'id-stratego-api-github-${env.name}'
      location: location
      subject: 'repo:${repository}:environment:${env.githubEnvironment}'
    }
  }
]

module deployerRoles 'deployer-roles.bicep' = [
  for (env, i) in environments: {
    name: 'deployer-roles-${env.name}'
    scope: environmentGroups[i]
    params: {
      principalId: deployers[i].outputs.principalId
    }
  }
]

output tenantId string = tenant().tenantId
output subscriptionId string = subscription().subscriptionId
output deployers array = [
  for (env, i) in environments: {
    githubEnvironment: env.githubEnvironment
    clientId: deployers[i].outputs.clientId
  }
]
