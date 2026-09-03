---
page_type: sample
languages:
- dotnet
- powershell
products:
- entra-verified-id
- azure-app-service
description: "A test site for Microsoft Entra Verified ID issuance and presentation with optional FaceCheck."
urlFragment: "entra-verifiedid-issuance-presentation-test"
---
# Entra Verified ID - Issuance and Presentation Test Site

A web application for testing Microsoft Entra Verified ID credential issuance and presentation flows, with optional FaceCheck (biometric liveness) support. Built with ASP.NET Core 8.0.

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)
![Azure](https://img.shields.io/badge/Azure-Entra%20Verified%20ID-0078D4?style=flat&logo=microsoft-azure)
![License](https://img.shields.io/badge/License-MIT-green.svg)

## Contents

- [How it works](#how-it-works)
- [Setup roadmap](#setup-roadmap) — **start here**
- [Features](#features)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Deployment](#deployment)
- [Project Structure](#project-structure)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)

## How it works

The site demonstrates three Microsoft Entra Verified ID flows you can run end to end:

- **Issue** a credential to a digital wallet via QR code — pick from 8 issuance flows in a dropdown.
- **Verify** a presented credential and view its claims.
- **FaceCheck** — optional biometric liveness check during verification.

## Setup roadmap

Do these in order. Finish steps 1–3 **before** deploying so you have every parameter ready.

| Step | What you do |
|------|-------------|
| **1. Register an app** | Create an Entra app registration and grant it the Verified ID permission → [details](#app-registration) |
| **2. Create your credentials** | Create the credential(s) you want to issue in your Verified ID tenant → [details](#2-create-your-credentials) |
| **3. Collect parameters** | Gather your IDs, secret, DIDs, and manifest URLs → [parameter list](#deployment-parameters) |
| **4. Deploy to Azure** | One-click deploy with the button below → [details](#deployment) |
| **5. Set callback URL** | Make your app publicly reachable so the request service can call back → [details](#5-set-a-public-callback-url) |

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Frogulati%2FVC-Interop-Test-site%2Fmain%2FARMTemplate%2Ftemplate.json)

### Deployment parameters

You will be asked to enter these during deployment:

| Parameter | Description |
|-----------|-------------|
| **Web App Name** | Unique name for your Azure App Service (will be part of URL) |
| **Tenant Id** | Your Microsoft Entra ID tenant ID (GUID) |
| **Client Id** | Application (client) ID from your app registration |
| **Client Secret** | Client secret from your app registration |
| **Issuer Authority** | Your Verified ID Issuer DID (e.g., `did:web:yourdomain.com`) |
| **Verifier Authority** | Your Verified ID Verifier DID (e.g., `did:web:yourdomain.com`) |
| **Manifest WoodgroveTraining** | Manifest URL for the **WoodgroveTraining** flow. Its rules must map `photo`, and its display definition must declare `photo` as `image/png;base64url` |
| **Manifest Verified Identity** | Manifest URL for the **VerifiedIdentity** flow (create the credential first, then paste its manifest URL here) |
| **CP Authority Id** | Your Verified ID CP Authority ID (GUID) — used for token details and `completeValidation` APIs |
| **Verification Provider** | Value for the VerifiedIdentity `verificationProvider` claim (e.g., `VIDTeamIDV`). Not entered in the issuance form — sourced from app configuration |
| **Manifest\* / Type\*** | Optional per-flow manifest URL and credential type for the six configurable issuance flows (Employee, IdTokenHint, IdToken, Presentation, SelfIssued, Multiple). Both `Manifest<Flow>` and `Type<Flow>` must be set for a flow to work |
| **Id Token Hint Given/Family Name** | Sample `given_name` / `family_name` claim values for the Id token hint attestation flow |

> **Filling the ARM/portal form:** Each parameter above is its own field in the **Deploy to Azure** form — just type the plain value into it (no `key=value` syntax). The template maps each field to the matching `AppSettings__<Name>` app setting automatically. The `key=value` form is only needed when using the Azure CLI or adding settings manually under App Service → **Environment variables**.

### 2. Create your credentials

Each issuance flow issues a credential you create in **your own** Verified ID tenant (Microsoft Entra admin center → **Verified ID** → **Credentials**). A credential is defined by a **rules definition** (what claims are collected and where they come from) and a **display definition** (how the card looks in the wallet). After you create one, copy its **manifest URL** — and, for the six configurable flows, its **type** — into the matching deployment parameter.

**Ready-made definitions** you can upload as a starting point, in [`CredentialFiles/`](CredentialFiles):

| Flow | Rules definition | Display definition |
|------|------------------|--------------------|
| WoodgroveTraining | [`WoodgroveTrainingRulesDefinition.json`](CredentialFiles/WoodgroveTrainingRulesDefinition.json) | [`WoodgroveTrainingDisplayDefinition.json`](CredentialFiles/WoodgroveTrainingDisplayDefinition.json) |
| VerifiedIdentity | [`VerifiedIdentityRulesDefinition.json`](CredentialFiles/VerifiedIdentityRulesDefinition.json) | [`VerifiedIdentityDisplayDefinition.json`](CredentialFiles/VerifiedIdentityDisplayDefinition.json) |
| IdTokenHint (sample) | [`VerifiedCredentialExpertRulesDefinition.json`](CredentialFiles/VerifiedCredentialExpertRulesDefinition.json) | [`VerifiedCredentialExpertDisplayDefinition.json`](CredentialFiles/VerifiedCredentialExpertDisplayDefinition.json) |

> Claim names in the rules definition must match what this app sends: **WoodgroveTraining** → `givenName`, `surname`, `email`, `displayName`, `photo`; **VerifiedIdentity** → the 10 IDV claims plus `verificationProvider`.

The WoodgroveTraining flow uses [`wwwroot/Ninja.png`](wwwroot/Ninja.png) as its fixed `photo` claim. The server reads the PNG for every issuance request and sends its bytes as unpadded base64url. Keep this file in the deployed application, and use `image/png;base64url` for the claim in the credential display definition. To replace the image, overwrite `wwwroot/Ninja.png` with another PNG and redeploy.

<details>
<summary><b>Where to find a flow's manifest URL and type</b></summary>

**Manifest URL** — open the credential in **Verified ID → Credentials** and copy its **Issue credential URL** (it ends in `/manifest`):

```
https://verifiedid.did.msidentity.com/v1.0/tenants/<tenantId>/verifiableCredentials/contracts/<contractId>/manifest
```

**Type** (configurable flows only) — the credential-specific value in the manifest's `type` array (e.g., `VerifiedEmployee`), **not** the generic `VerifiableCredential`. It must match exactly or issuance fails. Read it from the manifest URL in a browser, or from the credential name in the portal.

Example pairing for the Employee flow:

| App Service variable | Example value |
|----------------------|---------------|
| `AppSettings__ManifestEmployee` | `https://verifiedid.did.msidentity.com/v1.0/tenants/<tenantId>/verifiableCredentials/contracts/<contractId>/manifest` |
| `AppSettings__TypeEmployee` | `VerifiedEmployee` |

WoodgroveTraining and VerifiedIdentity set only the manifest (their type is fixed in the request config). Fill only the flows you want to test — leave the rest blank.

</details>

<details>
<summary><b>All 8 issuance flows and their how-to guides</b></summary>

Each row maps to a dropdown option on the Issuer page:

| Dropdown flow | `vctype` | Attestation type | Microsoft Learn how-to |
|---------------|----------|------------------|------------------------|
| WoodgroveTraining | `WoodgroveTraining` | `idTokenHint` | [Custom credential (ID token hint)](https://learn.microsoft.com/en-us/entra/verified-id/how-to-use-quickstart) |
| VerifiedIdentity | `VerifiedIdentity` | `idTokenHint` | [Custom credential (ID token hint)](https://learn.microsoft.com/en-us/entra/verified-id/how-to-use-quickstart) |
| Directory based - Employee | `Employee` | Managed / directory-based (`VerifiedEmployee`) | [Issue a VC for directory-based claims](https://learn.microsoft.com/en-us/entra/verified-id/how-to-use-quickstart-verifiedemployee) |
| Id token hint attestation | `IdTokenHint` | `idTokenHint` | [Custom credential (ID token hint)](https://learn.microsoft.com/en-us/entra/verified-id/how-to-use-quickstart) |
| Id token attestation | `IdToken` | `idToken` | [ID token attestation](https://learn.microsoft.com/en-us/entra/verified-id/how-to-use-quickstart-idtoken) |
| Presentation attestation | `Presentation` | `verifiablePresentation` | [Verifiable presentation attestation](https://learn.microsoft.com/en-us/entra/verified-id/how-to-use-quickstart-presentation) |
| Self issued attestation | `SelfIssued` | `selfIssued` | [Self-attested claims](https://learn.microsoft.com/en-us/entra/verified-id/how-to-use-quickstart-selfissued) |
| Multiple attestations | `Multiple` | combination (e.g. `idTokenHint` + `selfIssued`) | [Multiple attestations](https://learn.microsoft.com/en-us/entra/verified-id/how-to-use-quickstart-multiple) |

> **Employee** is a *managed* credential — its rules are fixed (claims come from the user's Microsoft Entra profile) and you only style the display. All other flows are *custom* credentials where you author both the rules and display JSON. The **Multiple** flow's `idTokenHint` claim names (`given_name`, `family_name`) must match what this app injects via `MultipleClaims`.

</details>

<details>
<summary><b>Verified ID setup and reference docs</b></summary>

- **Set up your tenant** — [Quick setup](https://learn.microsoft.com/en-us/entra/verified-id/verifiable-credentials-configure-tenant-quick) or [Advanced setup](https://learn.microsoft.com/en-us/entra/verified-id/verifiable-credentials-configure-tenant)
- **Customize credentials (author rules + display definitions)** — [Customize your verifiable credentials](https://learn.microsoft.com/en-us/entra/verified-id/credential-design)
- **Full JSON reference** — [Rules and display definition reference](https://learn.microsoft.com/en-us/entra/verified-id/rules-and-display-definitions-model)
- **All docs** — [Microsoft Entra Verified ID documentation](https://learn.microsoft.com/en-us/entra/verified-id/)

</details>

### 5. Set a public callback URL

After deployment:

1. Open your App Service in the Azure Portal.
2. Copy its URL (e.g., `https://your-app-name.azurewebsites.net`).
3. Use ngrok or the public App Service URL so the VC Request Service can reach your app's callback endpoint.

## Features

- **Credential Issuance** - Issue Verified ID credentials to a user's digital wallet via QR code
- **Multiple Credential Types** - Support for WoodgroveTraining, VerifiedIdentity, and six config-driven attestation flows (Directory based - Employee, Id token hint, Id token, Presentation, Self issued, Multiple attestations) via a dropdown selector. Each configurable flow sources its manifest, credential type, and (for Id token hint) claims from App Service configuration
- **Fixed Woodgrove Photo** - Include `wwwroot/Ninja.png` as an `image/png;base64url` photo claim in every WoodgroveTraining credential
- **VerifiedIdentity Claims Form** - Full claims entry form with 10 core claims covering 5 IDV providers (1Kosmos, IDEMIA, Au10tix, CLEAR, TrueCredential): name, gender, nationality, document details, dates, address, photo
- **Photo Capture** - Take a selfie via browser camera or upload a photo file; images are converted to JPEG and sent as `UrlEncode(Base64Encode(JPEG))`
- **Issuance Token Support** - Pass `?tokenId=xxx` to auto-populate token details and include the token in the issuance request
- **Issuance Token Validation** - For VerifiedIdentity requests with a token, calls `completeValidation` with the configured `CPAuthorityId` before creating the issuance request and shows **Details validated** in green above the QR code after validation succeeds
- **Token Details Display** - Fetches issuance token metadata from the Verified ID beta API and automatically displays every top-level response field, including `userId`; new fields appear without UI changes
- **Credential Verification** - Verify presented credentials and display claims in a tabular format
- **Revocation Toggle** - An **Allow Revoked** checkbox on the Verifier page (default off). When off, revoked credentials are rejected (`allowRevoked: false`); when on, revoked credentials are accepted (`allowRevoked: true`). Lets you test both the accepted and rejected revocation flows
- **FaceCheck Support** - Optional biometric liveness check during presentation using Azure AI Face API
- **Photo Claim Rendering** - Automatically decodes and displays base64url-encoded photo claims as images
- **FaceCheck Confidence Score** - Displays match confidence with color-coded indicators (green/amber/red)
- **Modern Dashboard UI** - Bootstrap 5 responsive design with gradient styling matching Entra branding
- **Self-Hosted UI Dependencies** - Bootstrap 5.3.2 and Bootstrap Icons 1.11.1 are served from `wwwroot/lib`, avoiding runtime dependencies on third-party CDNs
- **Structured Logging** - Enhanced logging throughout controllers for debugging and monitoring

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│            Entra Verified ID Test Site                          │
├─────────────────────────────────────────────────────────────────┤
│  ASP.NET Core 8.0 (Razor Pages)                                │
│  ├── IssuerController  - Issuance requests & callbacks         │
│  │   ├── GET/POST /api/issuer/issuance-request?vctype=...      │
│  │   ├── GET /api/issuer/token-details?tokenId=...             │
│  │   └── VCTypes: WoodgroveTraining, VerifiedIdentity,         │
│  │       Employee, IdTokenHint, IdToken, Presentation,        │
│  │       SelfIssued, Multiple (config-driven)                 │
│  ├── VerifierController - Presentation requests & callbacks    │
│  └── Pages (Home, Issuer, Verifier)                            │
│      └── /Issuer/{vctype?}?tokenId=xxx                         │
├─────────────────────────────────────────────────────────────────┤
│  Microsoft.Identity.Web (MSAL)                                 │
│  └── Client Credentials Flow                                   │
├─────────────────────────────────────────────────────────────────┤
│                         APIs                                   │
│  └── Verified ID Request Service API                           │
│      ├── completeValidation (VerifiedIdentity token flow)       │
│      ├── createIssuanceRequest (with optional token field)      │
│      ├── createPresentationRequest                             │
│      └── beta/issuanceToken/{tokenId} (token details)          │
└─────────────────────────────────────────────────────────────────┘
```

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Subscription](https://azure.microsoft.com/free/)
- [Microsoft Entra ID Tenant](https://learn.microsoft.com/en-us/entra/fundamentals/create-new-tenant)
- [Verified ID Service](https://learn.microsoft.com/en-us/entra/verified-id/verifiable-credentials-configure-tenant) configured in your tenant
- [ngrok](https://ngrok.com/) or similar tool for local development (callback URL must be publicly reachable)

## Getting Started

### App Registration

1. **Create App Registration**
   - Go to [Azure Portal](https://portal.azure.com) → Microsoft Entra ID → App registrations
   - Click **New registration**
   - Name: `VC Interop Test Site`
   - Supported account types: **Accounts in this organizational directory only**
   - Click **Register**

2. **Create Client Secret**
   - Go to **Certificates & secrets** → **New client secret**
   - Copy the secret value immediately

3. **Add API Permissions**
   - Go to **API permissions** → **Add a permission**
   - Search for `Verifiable Credential Request Service` (or use ID `3db474b9-6a0c-4840-96ac-1fceb342124f`)
   - Select **Application permissions** → check `VerifiableCredential.Create.All`
   - Click **Grant admin consent**

4. **Note Your Values**
   - **Application (client) ID**: Found on Overview page
   - **Directory (tenant) ID**: Found on Overview page
   - **Client Secret**: Created in step 2

### Configuration

Update `appsettings.json` with your values:

```json
{
  "AppSettings": {
    "Endpoint": "https://verifiedid.did.msidentity.com/v1.0/",
    "VCServiceScope": "3db474b9-6a0c-4840-96ac-1fceb342124f/.default",
    "Instance": "https://login.microsoftonline.com/{0}",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "IssuerAuthority": "did:web:yourdomain.com",
    "VerifierAuthority": "did:web:yourdomain.com",
    "CredentialManifest": "<your-credential-manifest-url>",
    "ManifestVerifiedIdentity": "<manifest-url-for-verifiedidentity>",
    "CPAuthorityId": "<your-cp-authority-id>",
    "VerificationProvider": "VIDTeamIDV",

    "ManifestEmployee": "<manifest-url-for-employee>",
    "ManifestIdTokenHint": "<manifest-url-for-idtokenhint>",
    "ManifestIdToken": "<manifest-url-for-idtoken>",
    "ManifestPresentation": "<manifest-url-for-presentation>",
    "ManifestSelfIssued": "<manifest-url-for-selfissued>",
    "ManifestMultiple": "<manifest-url-for-multiple>",
    "TypeEmployee": "<credential-type-for-employee>",
    "TypeIdTokenHint": "<credential-type-for-idtokenhint>",
    "TypeIdToken": "<credential-type-for-idtoken>",
    "TypePresentation": "<credential-type-for-presentation>",
    "TypeSelfIssued": "<credential-type-for-selfissued>",
    "TypeMultiple": "<credential-type-for-multiple>",
    "IdTokenHintClaims": {
      "given_name": "Megan",
      "family_name": "Bowen"
    },
    "MultipleClaims": {
      "given_name": "Megan",
      "family_name": "Bowen"
    }
  }
}
```

> **Per-flow configuration:** `WoodgroveTraining` and `VerifiedIdentity` work out of the box. The six configurable flows (Employee, IdTokenHint, IdToken, Presentation, SelfIssued, Multiple) are **opt-in** — a flow only works once **both** its `Manifest<Flow>` and `Type<Flow>` values are set. The `IdTokenHintClaims` dictionary supplies the claims injected into the Id token hint flow, and the `MultipleClaims` dictionary supplies the idTokenHint attestation claims for the Multiple attestations flow (its other attestations — self-issued, presentation, idToken — are collected in the wallet). None of these affect the WoodgroveTraining or VerifiedIdentity flows.

### Running Locally

```bash
# Clone the repository
git clone https://github.com/rogulati/VC-Interop-Test-site.git
cd VC-Interop-Test-site

# Restore dependencies
dotnet restore

# Build the application
dotnet build

# Run the application
dotnet run
```

The application will be available at:
- HTTP: `http://localhost:5000`

In a separate terminal, run ngrok:

```bash
ngrok http 5000
```

Use the ngrok HTTPS URL to access the site. The callback URL is dynamically set based on the host header.

## Deployment

### Option 1: Deploy using ARM Template (Recommended)

The easiest way to deploy is using the **Deploy to Azure** button at the top of this README. This creates an Azure App Service on the Free tier with all required configuration.

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Frogulati%2FVC-Interop-Test-site%2Fmain%2FARMTemplate%2Ftemplate.json)

After deployment:
1. Go to your App Service in Azure Portal
2. Copy the URL (e.g., `https://your-app-name.azurewebsites.net`)
3. Ensure the callback URL is reachable by the VC Request Service

The ARM template deploys the selected GitHub branch through App Service source control. That branch must contain `wwwroot/Ninja.png`; ASP.NET Core publishes the file with the other static assets. If the file is missing, WoodgroveTraining issuance returns `WoodgroveTraining photo not found`.

### Option 2: Deploy using Azure CLI

```bash
# Login to Azure
az login

# Create resource group
az group create --name rg-vc-test --location eastus

# Create App Service plan (Free tier)
az appservice plan create --name asp-vc-test --resource-group rg-vc-test --sku F1

# Create Web App
az webapp create --name vc-interop-test --resource-group rg-vc-test --plan asp-vc-test --runtime "DOTNET|8.0"

# Configure App Settings
az webapp config appsettings set --name vc-interop-test --resource-group rg-vc-test --settings \
  AppSettings__TenantId="your-tenant-id" \
  AppSettings__ClientId="your-client-id" \
  AppSettings__ClientSecret="your-client-secret" \
  AppSettings__IssuerAuthority="did:web:yourdomain.com" \
  AppSettings__VerifierAuthority="did:web:yourdomain.com" \
  AppSettings__CredentialManifest="your-manifest-url" \
  AppSettings__CPAuthorityId="your-cp-authority-id" \
  AppSettings__VerificationProvider="VIDTeamIDV" \
  AppSettings__ManifestEmployee="manifest-url-for-employee" \
  AppSettings__ManifestIdTokenHint="manifest-url-for-idtokenhint" \
  AppSettings__ManifestIdToken="manifest-url-for-idtoken" \
  AppSettings__ManifestPresentation="manifest-url-for-presentation" \
  AppSettings__ManifestSelfIssued="manifest-url-for-selfissued" \
  AppSettings__ManifestMultiple="manifest-url-for-multiple" \
  AppSettings__TypeEmployee="credential-type-for-employee" \
  AppSettings__TypeIdTokenHint="credential-type-for-idtokenhint" \
  AppSettings__TypeIdToken="credential-type-for-idtoken" \
  AppSettings__TypePresentation="credential-type-for-presentation" \
  AppSettings__TypeSelfIssued="credential-type-for-selfissued" \
  AppSettings__TypeMultiple="credential-type-for-multiple" \
  AppSettings__IdTokenHintClaims__given_name="Megan" \
  AppSettings__IdTokenHintClaims__family_name="Bowen" \
  AppSettings__MultipleClaims__given_name="Megan" \
  AppSettings__MultipleClaims__family_name="Bowen" \
  AppSettings__Endpoint="https://verifiedid.did.msidentity.com/v1.0/" \
  AppSettings__VCServiceScope="3db474b9-6a0c-4840-96ac-1fceb342124f/.default" \
  AppSettings__Instance="https://login.microsoftonline.com/{0}"

# Publish and deploy
dotnet publish -c Release -o ./publish
cd publish && zip -r ../publish.zip . && cd ..
az webapp deployment source config-zip --name vc-interop-test --resource-group rg-vc-test --src ./publish.zip
```

### Option 3: Deploy using Visual Studio

1. Right-click the project → **Publish**
2. Select **Azure** → **Azure App Service (Windows)**
3. Sign in and select/create your App Service
4. Configure settings and click **Publish**

### Existing App Service deployment

This repository's `.github/workflows/deploy.yml` workflow builds, publishes, and deploys the application to the configured Azure App Service whenever a commit is pushed to `main`. Keep deployment credentials in GitHub Actions secrets; do not commit publish profiles or application secrets.

## Project Structure

```
VC-Interop-Test-site/
├── ARMTemplate/
│   └── template.json              # ARM template for Azure deployment
├── AppCreationScripts/
│   ├── Configure.ps1              # PowerShell script for app registration
│   └── Cleanup.ps1                # Cleanup script
├── CredentialFiles/
│   ├── VerifiedCredentialExpertDisplayDefinition.json
│   ├── VerifiedCredentialExpertRulesDefinition.json
│   ├── VerifiedIdentityDisplayDefinition.json
│   ├── VerifiedIdentityRulesDefinition.json
│   ├── WoodgroveTrainingDisplayDefinition.json  # Declares photo as image/png;base64url
│   └── WoodgroveTrainingRulesDefinition.json    # Maps the photo idTokenHint claim
├── Models/
│   └── AppSettingsModel.cs        # Configuration model
├── Pages/
│   ├── Shared/
│   │   └── _Layout.cshtml         # Main layout with Bootstrap 5 navbar
│   ├── Index.cshtml               # Home page with action cards
│   ├── Issuer.cshtml              # Credential issuance page
│   └── Verifier.cshtml            # Credential verification page
├── wwwroot/
│   ├── lib/                      # Vendored Bootstrap 5.3.2 and Bootstrap Icons 1.11.1
│   ├── Ninja.png                 # Fixed WoodgroveTraining photo claim
│   ├── styles.css                 # Custom CSS (Entra-themed)
│   └── qrcode.min.js             # QR code generation library
├── IssuerController.cs            # Issuance API controller (WoodgroveTraining + VerifiedIdentity)
├── VerifierController.cs          # Verification API controller
├── Program.cs                     # Application entry point
├── Startup.cs                     # Service configuration
├── appsettings.json               # Application configuration
├── issuance_request_config.json   # WoodgroveTraining issuance payload template
├── issuance_request_config_verifiedidentity.json  # VerifiedIdentity issuance payload (10-claim core set + token)
├── presentation_request_config.json               # Presentation payload (allowRevoked: true)
├── presentation_request_config_enforcerevocation.json  # Presentation payload that rejects revoked credentials (allowRevoked: false)
└── presentation_request_config_facecheck.json      # FaceCheck presentation payload
```

## Issuance URL Patterns

| URL | Behavior |
|-----|----------|
| `/Issuer` | Default issuance page with WoodgroveTraining selected |
| `/Issuer/vid` | VerifiedIdentity claims form pre-selected |
| `/Issuer/vid?tokenId=xxx` | VerifiedIdentity with token details displayed; validation completed before issuance; green confirmation shown above the QR code |

## Verification Options

The Verifier page exposes two checkboxes that map to query parameters on `/api/verifier/presentation-request`:

| Query parameter | Default | Config file used | Behavior |
|-----------------|---------|------------------|----------|
| `allowRevoked=false` | ✓ (checkbox off) | `presentation_request_config_enforcerevocation.json` | Revoked credentials are **rejected** |
| `allowRevoked=true` | (checkbox on) | `presentation_request_config.json` | Revoked credentials are **accepted** |
| `faceCheck=true` | (checkbox on) | `presentation_request_config_facecheck.json` | Requires FaceCheck; takes priority if both are checked |

## Troubleshooting

### Common Issues

#### 1. "Insufficient privileges to complete the operation"
**Solution**: Grant admin consent for API permissions in Azure Portal:
- App registrations → Your app → API permissions → Grant admin consent

#### 2. "AADSTS7000215: Invalid client secret provided"
**Solution**: The client secret may have expired. Create a new one in Azure Portal:
- App registrations → Your app → Certificates & secrets → New client secret

#### 3. Callback URL not reachable
**Solution**: Ensure ngrok (or your public URL) is running and the callback URL is accessible from the internet. The VC Request Service needs to reach your callback endpoint.

#### 4. QR code not appearing
**Solution**: Check the browser console for errors. Ensure the API endpoints (`/api/issuer/issuance-request` and `/api/verifier/presentation-request`) are returning valid responses.

### Debugging Tips

1. **Enable detailed logging** - The app is configured with `Trace` level logging by default
2. **Check token claims** using [jwt.ms](https://jwt.ms) to decode access tokens
3. **Test API calls directly** using tools like Postman or curl with a valid token

## Security Considerations

1. **Never commit secrets** - Use User Secrets, Azure Key Vault, or environment variables
2. **Use HTTPS** in production
3. **Rotate client secrets** regularly
4. **Use certificates** instead of client secrets for production deployments
5. For more info, see [Integrate a daemon app with Key Vault and MSI](https://github.com/Azure-Samples/active-directory-dotnetcore-daemon-v2/tree/master/3-Using-KeyVault)

## More Information

- [Microsoft Entra Verified ID Documentation](https://learn.microsoft.com/en-us/entra/verified-id/)
- [Quickstart: Register an application with the Microsoft identity platform](https://docs.microsoft.com/azure/active-directory/develop/quickstart-register-app)
- [Acquiring a token for an application with client credential flows](https://aka.ms/msal-net-client-credentials)
- [FaceCheck with Verified ID](https://learn.microsoft.com/en-us/entra/verified-id/using-facecheck)

## License

This project is licensed under the MIT License.

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request
