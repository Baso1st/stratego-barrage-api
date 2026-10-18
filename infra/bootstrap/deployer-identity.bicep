// The identity GitHub Actions signs in as, with OIDC, to deploy one environment.

@description('Name of the identity.')
param name string

param location string

@description('The only GitHub OIDC subject that can sign in as this identity: one GitHub environment of one repository.')
param subject string

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: name
  location: location
}

resource github 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: identity
  name: 'github'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: subject
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

output principalId string = identity.properties.principalId
output clientId string = identity.properties.clientId
