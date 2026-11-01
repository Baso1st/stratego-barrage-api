# Sets up players' sign-in in both External ID tenants: the API's and the client's app
# registrations, admin consent for the client, and the user flow the client signs in with.
# Run it signed in with `az login` as an admin of both tenants, after the steps done by hand
# in README.md. Running it again changes nothing.
#
#   ./infra/sign-in/external-id.ps1

$ErrorActionPreference = 'Stop'

$environments = @(
    @{
        Name      = 'Test'
        TenantId  = 'b503c68b-2067-4d2d-8f57-193c826458dd'
        Subdomain = 'strategobarragetest'
        UserFlow  = 'SignUpSignIn-Test'
    }
    @{
        Name      = 'Prod'
        TenantId  = '009415ee-77ab-4994-a58a-e42a2c5bca5e'
        Subdomain = 'strategobarrageprod'
        UserFlow  = 'SignUpSignIn-Prod'
    }
)

# The one scope in players' access tokens.
$scopeName = 'play'

# Redirect URIs for clients that sign in without a secret and aren't browser apps.
# Postman's callback lets us sign in and call the API by hand.
$publicClientRedirectUris = @('https://oauth.pstmn.io/v1/callback')

# Microsoft Graph, for the sign-in scopes every client asks for.
$graphAppId = '00000003-0000-0000-c000-000000000000'
$graphScopes = @('openid', 'profile', 'offline_access')

function Invoke-Graph([string] $Method, [string] $Path, $Body) {
    $request = @{
        Method  = $Method
        Uri     = "https://graph.microsoft.com/v1.0$Path"
        Headers = @{ Authorization = "Bearer $script:token" }
    }
    if ($null -ne $Body) {
        $request.ContentType = 'application/json'
        $request.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 10))
    }
    # A new object takes a few seconds to reach every replica, so retry while it can't be found.
    for ($attempt = 1; ; $attempt++) {
        try {
            return Invoke-RestMethod @request
        }
        catch {
            $status = 0
            if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
            if (($status -eq 400 -or $status -eq 404) -and $attempt -lt 6) {
                Start-Sleep -Seconds 5
                continue
            }
            Write-Host "$Method $Path failed: $($_.ErrorDetails.Message)"
            throw
        }
    }
}

function Get-Application([string] $DisplayName) {
    $found = @((Invoke-Graph GET "/applications?`$filter=displayName eq '$DisplayName'").value)
    if ($found.Count -gt 1) { throw "More than one app registration is named '$DisplayName'." }
    if ($found.Count -eq 1) { return $found[0] }
    Write-Host "  Creating the app registration '$DisplayName'"
    return Invoke-Graph POST '/applications' @{ displayName = $DisplayName; signInAudience = 'AzureADMyOrg' }
}

function Get-ServicePrincipal([string] $AppId) {
    $found = @((Invoke-Graph GET "/servicePrincipals?`$filter=appId eq '$AppId'").value)
    if ($found.Count -eq 1) { return $found[0] }
    return Invoke-Graph POST '/servicePrincipals' @{ appId = $AppId }
}

# Consents for every user of the tenant, since players can't consent themselves.
function Grant-AdminConsent($Client, $Resource, [string] $Scope) {
    $grants = @((Invoke-Graph GET "/oauth2PermissionGrants?`$filter=clientId eq '$($Client.id)' and resourceId eq '$($Resource.id)'").value)
    $grant = $grants | Where-Object { $_.consentType -eq 'AllPrincipals' } | Select-Object -First 1
    if ($grant) {
        Invoke-Graph PATCH "/oauth2PermissionGrants/$($grant.id)" @{ scope = $Scope } | Out-Null
    }
    else {
        Invoke-Graph POST '/oauth2PermissionGrants' @{
            clientId    = $Client.id
            consentType = 'AllPrincipals'
            resourceId  = $Resource.id
            scope       = $Scope
        } | Out-Null
    }
}

$results = @()
foreach ($environment in $environments) {
    Write-Host "$($environment.Name): $($environment.Subdomain).onmicrosoft.com"
    $script:token = az account get-access-token `
        --tenant $environment.TenantId `
        --resource-type ms-graph `
        --query accessToken `
        --output tsv
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # The API: v2.0 access tokens, with the scope players' tokens carry.
    $api = Get-Application "Stratego Barrage API $($environment.Name)"
    $scope = @($api.api.oauth2PermissionScopes) | Where-Object { $_.value -eq $scopeName } | Select-Object -First 1
    if ($scope) { $scopeId = $scope.id } else { $scopeId = [guid]::NewGuid().ToString() }
    Invoke-Graph PATCH "/applications/$($api.id)" @{
        identifierUris = @("api://$($api.appId)")
        api            = @{
            requestedAccessTokenVersion = 2
            oauth2PermissionScopes      = @(
                @{
                    id                      = $scopeId
                    value                   = $scopeName
                    type                    = 'Admin'
                    isEnabled               = $true
                    adminConsentDisplayName = 'Play Stratego Barrage'
                    adminConsentDescription = 'Lets the client play games through the API as the signed-in player.'
                }
            )
        }
    } | Out-Null
    $apiPrincipal = Get-ServicePrincipal $api.appId

    # The client: the authorization code flow with PKCE, and no secret.
    $client = Get-Application "Stratego Barrage Client $($environment.Name)"
    $graphPrincipal = Get-ServicePrincipal $graphAppId
    $graphAccess = foreach ($name in $graphScopes) {
        $id = ($graphPrincipal.oauth2PermissionScopes | Where-Object { $_.value -eq $name }).id
        if (-not $id) { throw "Microsoft Graph has no '$name' scope in this tenant." }
        @{ id = $id; type = 'Scope' }
    }
    Invoke-Graph PATCH "/applications/$($client.id)" @{
        publicClient           = @{ redirectUris = $publicClientRedirectUris }
        requiredResourceAccess = @(
            @{ resourceAppId = $graphAppId; resourceAccess = @($graphAccess) }
            @{ resourceAppId = $api.appId; resourceAccess = @(@{ id = $scopeId; type = 'Scope' }) }
        )
    } | Out-Null
    $clientPrincipal = Get-ServicePrincipal $client.appId
    Grant-AdminConsent $clientPrincipal $graphPrincipal ($graphScopes -join ' ')
    Grant-AdminConsent $clientPrincipal $apiPrincipal $scopeName

    # The user flow: Google as its only sign-in option, and the client linked to it.
    $flow = @((Invoke-Graph GET '/identity/authenticationEventsFlows').value) |
        Where-Object { $_.displayName -eq $environment.UserFlow }
    if (-not $flow) { throw "The user flow '$($environment.UserFlow)' doesn't exist. Create it as in README.md." }
    $flowPath = "/identity/authenticationEventsFlows/$($flow.id)"
    $providersPath = "$flowPath/microsoft.graph.externalUsersSelfServiceSignUpEventsFlow/onAuthenticationMethodLoadStart" +
        '/microsoft.graph.onAuthenticationMethodLoadStartExternalUsersSelfServiceSignUp/identityProviders'
    $providers = @((Invoke-Graph GET $providersPath).value)
    if (-not ($providers | Where-Object { $_.identityProviderType -eq 'Google' })) {
        throw "The user flow '$($environment.UserFlow)' doesn't offer Google. Add it as in README.md."
    }
    foreach ($provider in $providers | Where-Object { $_.identityProviderType -ne 'Google' }) {
        Write-Host "  Removing '$($provider.displayName)' from the user flow"
        Invoke-Graph DELETE "$providersPath/$($provider.id)/`$ref" | Out-Null
    }
    $appsPath = "$flowPath/conditions/applications/includeApplications"
    if (-not (@((Invoke-Graph GET $appsPath).value) | Where-Object { $_.appId -eq $client.appId })) {
        Write-Host '  Linking the client to the user flow'
        Invoke-Graph POST $appsPath @{
            '@odata.type' = '#microsoft.graph.authenticationConditionApplication'
            appId         = $client.appId
        } | Out-Null
    }

    $authority = "https://$($environment.Subdomain).ciamlogin.com/$($environment.TenantId)"
    $results += [pscustomobject]@{
        Environment  = $environment.Name
        Issuer       = "https://$($environment.TenantId).ciamlogin.com/$($environment.TenantId)/v2.0"
        Audience     = $api.appId
        Scope        = "api://$($api.appId)/$scopeName"
        ClientId     = $client.appId
        AuthorizeUrl = "$authority/oauth2/v2.0/authorize"
        TokenUrl     = "$authority/oauth2/v2.0/token"
    }
}

Write-Host ''
foreach ($result in $results) {
    Write-Host $result.Environment
    Write-Host "  Issuer          $($result.Issuer)"
    Write-Host "  API's audience  $($result.Audience)"
    Write-Host "  Scope           $($result.Scope)"
    Write-Host "  Client ID       $($result.ClientId)"
    Write-Host "  Authorize URL   $($result.AuthorizeUrl)"
    Write-Host "  Token URL       $($result.TokenUrl)"
}
