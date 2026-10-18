# One-time setup of the Azure subscription. Run it as an owner of the subscription, signed in
# with `az login` and with the subscription selected. Running it again changes nothing.
#
#   ./infra/bootstrap/bootstrap.ps1

$ErrorActionPreference = 'Stop'

# The deploying identities only have roles on their own resource groups,
# so they can't register resource providers. This does it for them.
$providers = @(
    'Microsoft.ManagedIdentity'
    'Microsoft.Web'
    'Microsoft.DocumentDB'
    'Microsoft.ServiceBus'
    'Microsoft.Storage'
    'Microsoft.KeyVault'
    'Microsoft.Insights'
    'Microsoft.OperationalInsights'
    'Microsoft.AlertsManagement'
)
foreach ($provider in $providers) {
    Write-Host "Registering $provider"
    az provider register --namespace $provider --wait
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host 'Deploying the resource groups and the GitHub Actions identities'
$json = az deployment sub create `
    --name stratego-api-bootstrap `
    --location centralus `
    --template-file "$PSScriptRoot/main.bicep" `
    --query properties.outputs `
    --output json
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$outputs = $json | Out-String | ConvertFrom-Json

Write-Host ''
Write-Host 'Add these to GitHub as variables (Settings > Secrets and variables > Actions > Variables).'
Write-Host 'Repository variables:'
Write-Host "  AZURE_TENANT_ID       $($outputs.tenantId.value)"
Write-Host "  AZURE_SUBSCRIPTION_ID $($outputs.subscriptionId.value)"
foreach ($deployer in $outputs.deployers.value) {
    Write-Host "Environment '$($deployer.githubEnvironment)' variable:"
    Write-Host "  AZURE_CLIENT_ID       $($deployer.clientId)"
}
