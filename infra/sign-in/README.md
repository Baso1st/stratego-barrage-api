# Players' sign-in

Players sign in with Google through a Microsoft Entra External ID tenant for each environment ([ARCHITECTURE.md](../../docs/ARCHITECTURE.md) section 3.8). Steps 1 to 4 are done by hand once, in order. Then [`external-id.ps1`](external-id.ps1) sets up the rest, and running it again changes nothing.

## What each environment has

| | Test | Prod |
| --- | --- | --- |
| External ID tenant | `Stratego Barrage Test` | `Stratego Barrage Prod` |
| Primary domain | `strategobarragetest.onmicrosoft.com` | `strategobarrageprod.onmicrosoft.com` |
| Tenant ID | `b503c68b-2067-4d2d-8f57-193c826458dd` | `009415ee-77ab-4994-a58a-e42a2c5bca5e` |
| Google Cloud project | `Stratego Barrage Test` (`stratego-barrage-test`) | `Stratego Barrage Prod` (`stratego-barrage-prod`) |
| Google OAuth client | `External ID Test` | `External ID Prod` |
| Google OAuth client ID | `693405543690-8l81fjr5q877nnblfoeqja9m0pqhkc1e.apps.googleusercontent.com` | `387690006234-e9llfikeidg39t7k7b388dud45imvhr6.apps.googleusercontent.com` |
| User flow | `SignUpSignIn-Test` | `SignUpSignIn-Prod` |
| API's app registration | `Stratego Barrage API Test` | `Stratego Barrage API Prod` |
| API's client ID (the audience) | `4f26330c-2ac8-424b-8d3b-402ca7be128a` | `ddfcf322-b4ad-44e6-9569-67d48ed49811` |
| Client's app registration | `Stratego Barrage Client Test` | `Stratego Barrage Client Prod` |
| Client's client ID | `5afc4ac1-aed5-4ef1-8f30-c4d2d1f39df5` | `f69cdb14-8d5d-4242-8ca8-fe3eb17f2ca7` |

None of these are secrets. The Google client secrets are kept only in the tenants.

## 1. Create the tenants

In the [Microsoft Entra admin center](https://entra.microsoft.com), signed in as an owner of the subscription, go to **Entra ID** > **Overview** > **Manage tenants** > **Create**. Select **External**, **Continue**, then **Use Azure Subscription**, and fill in:

| Field | Test | Prod |
| --- | --- | --- |
| Tenant Name | `Stratego Barrage Test` | `Stratego Barrage Prod` |
| Domain Name | `strategobarragetest` | `strategobarrageprod` |
| Country/Region | United States | United States |
| Subscription | Pay-As-You-Go | Pay-As-You-Go |
| Resource group | `rg-stratego-api-test` | `rg-stratego-api-prod` |

Then **Next: Review + Create** and **Create**. A tenant takes up to 30 minutes to create.

## 2. Create the Google OAuth clients

In the [Google Cloud console](https://console.cloud.google.com), for each environment:

1. **Create the project.** From the project list at the top left, select **New project**, fill in the table below, and select **Create**. Then select the new project in the project list.
2. **Set up the consent screen.** Go to **Google Auth Platform** > **Branding** > **Get started**, fill in each page, then agree to the Google API Services User Data Policy and select **Create**.
3. **Authorize Microsoft's sign-in domains.** On **Branding**, under **Authorized domains**, add `ciamlogin.com` and `microsoftonline.com`, and select **Save**.
4. **Create the client.** Go to **Clients** > **Create client**, fill in the table, add the environment's redirect URIs below, and select **Create**.
5. **Copy the client ID and secret.** Google shows the secret only once, and it goes into the tenant in step 3. A lost secret is replaced with **Add secret** on the client.
6. **Publish the app.** Go to **Audience** > **Publish app**. Google doesn't need to verify it, because External ID asks only for basic profile scopes.

| Field | Test | Prod |
| --- | --- | --- |
| Project name | `Stratego Barrage Test` | `Stratego Barrage Prod` |
| Location | No organization | No organization |
| App name | `Stratego Barrage Test` | `Stratego Barrage Prod` |
| User support email | The project owner's Gmail address | The project owner's Gmail address |
| Audience | External | External |
| Contact email | The project owner's Gmail address | The project owner's Gmail address |
| Client application type | Web application | Web application |
| Client name | `External ID Test` | `External ID Prod` |
| Authorized JavaScript origins | None | None |

Test's redirect URIs:

```text
https://login.microsoftonline.com
https://login.microsoftonline.com/te/b503c68b-2067-4d2d-8f57-193c826458dd/oauth2/authresp
https://login.microsoftonline.com/te/strategobarragetest.onmicrosoft.com/oauth2/authresp
https://b503c68b-2067-4d2d-8f57-193c826458dd.ciamlogin.com/b503c68b-2067-4d2d-8f57-193c826458dd/federation/oidc/accounts.google.com
https://b503c68b-2067-4d2d-8f57-193c826458dd.ciamlogin.com/strategobarragetest.onmicrosoft.com/federation/oidc/accounts.google.com
https://strategobarragetest.ciamlogin.com/b503c68b-2067-4d2d-8f57-193c826458dd/federation/oauth2
https://strategobarragetest.ciamlogin.com/strategobarragetest.onmicrosoft.com/federation/oauth2
```

Prod's redirect URIs:

```text
https://login.microsoftonline.com
https://login.microsoftonline.com/te/009415ee-77ab-4994-a58a-e42a2c5bca5e/oauth2/authresp
https://login.microsoftonline.com/te/strategobarrageprod.onmicrosoft.com/oauth2/authresp
https://009415ee-77ab-4994-a58a-e42a2c5bca5e.ciamlogin.com/009415ee-77ab-4994-a58a-e42a2c5bca5e/federation/oidc/accounts.google.com
https://009415ee-77ab-4994-a58a-e42a2c5bca5e.ciamlogin.com/strategobarrageprod.onmicrosoft.com/federation/oidc/accounts.google.com
https://strategobarrageprod.ciamlogin.com/009415ee-77ab-4994-a58a-e42a2c5bca5e/federation/oauth2
https://strategobarrageprod.ciamlogin.com/strategobarrageprod.onmicrosoft.com/federation/oauth2
```

## 3. Add Google to each tenant

In the Entra admin center, switch to the tenant (**Settings** > **Directories + subscriptions** > **Switch**). Go to **Entra ID** > **External Identities** > **All identity providers**, and on the **Built-in** tab select **Configure** next to **Google**:

| Field | Test | Prod |
| --- | --- | --- |
| Name | `Google` | `Google` |
| Client ID | Test's Google OAuth client ID | Prod's Google OAuth client ID |
| Client secret | Test's Google OAuth client secret | Prod's Google OAuth client secret |

## 4. Create the user flows

Still in the tenant, go to **Entra ID** > **External Identities** > **User flows** > **New user flow**:

| Field | Test | Prod |
| --- | --- | --- |
| Name | `SignUpSignIn-Test` | `SignUpSignIn-Prod` |
| Identity providers | Google | Google |
| User attributes | None | None |

The script removes every other sign-in option from the flow, so Google is the only one.

## 5. Run the script

Signed in with `az login` as an admin of both tenants:

```powershell
./infra/sign-in/external-id.ps1
```

It sets up each tenant:

| What | How it's set up |
| --- | --- |
| **The API's app registration** | Application ID URI `api://<its client ID>`, v2.0 access tokens, and one scope, `play`, that only an admin can consent to |
| **The client's app registration** | No secret, so it signs in with the authorization code flow and PKCE. Postman's callback, `https://oauth.pstmn.io/v1/callback`, is its redirect URI for a public client. It asks for Microsoft Graph's `openid`, `profile` and `offline_access`, and the API's `play` |
| **Admin consent** | Granted to the client for those scopes, for every user, because players can't consent themselves |
| **The user flow** | Google as its only sign-in option, and the client linked to it |

At the end it prints each environment's issuer, the API's audience, the scope, the client ID, and the URLs a client signs in with.

## Signing in by hand

To get a player's access token and call the API by hand, set up a request's **Authorization** in Postman:

| Field | Test | Prod |
| --- | --- | --- |
| Auth Type | OAuth 2.0 | OAuth 2.0 |
| Grant type | Authorization Code (With PKCE) | Authorization Code (With PKCE) |
| Callback URL | `https://oauth.pstmn.io/v1/callback`, with **Authorize using browser** ticked | `https://oauth.pstmn.io/v1/callback`, with **Authorize using browser** ticked |
| Auth URL | `https://strategobarragetest.ciamlogin.com/b503c68b-2067-4d2d-8f57-193c826458dd/oauth2/v2.0/authorize` | `https://strategobarrageprod.ciamlogin.com/009415ee-77ab-4994-a58a-e42a2c5bca5e/oauth2/v2.0/authorize` |
| Access Token URL | `https://strategobarragetest.ciamlogin.com/b503c68b-2067-4d2d-8f57-193c826458dd/oauth2/v2.0/token` | `https://strategobarrageprod.ciamlogin.com/009415ee-77ab-4994-a58a-e42a2c5bca5e/oauth2/v2.0/token` |
| Client ID | `5afc4ac1-aed5-4ef1-8f30-c4d2d1f39df5` | `f69cdb14-8d5d-4242-8ca8-fe3eb17f2ca7` |
| Client Secret | Empty | Empty |
| Code Challenge Method | SHA-256 | SHA-256 |
| Scope | `api://4f26330c-2ac8-424b-8d3b-402ca7be128a/play openid profile offline_access` | `api://ddfcf322-b4ad-44e6-9569-67d48ed49811/play openid profile offline_access` |
| Client Authentication | Send client credentials in body | Send client credentials in body |

Then select **Get New Access Token** and sign in with Google in the browser that opens.
