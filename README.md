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

## Deploy to Azure

Complete the [setup](#getting-started) before deploying to Azure so that you have all the required parameters.

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Frogulati%2FVC-Interop-Test-site%2Fmain%2FARMTemplate%2Ftemplate.json)

You will be asked to enter the following parameters during deployment:

| Parameter | Description |
|-----------|-------------|
| **Web App Name** | Unique name for your Azure App Service (will be part of URL) |
| **Tenant Id** | Your Microsoft Entra ID tenant ID (GUID) |
| **Client Id** | Application (client) ID from your app registration |
| **Client Secret** | Client secret from your app registration |
| **Issuer Authority** | Your Verified ID Issuer DID (e.g., `did:web:yourdomain.com`) |
| **Verifier Authority** | Your Verified ID Verifier DID (e.g., `did:web:yourdomain.com`) |
| **Credential Manifest** | URL to your Verifiable Credential manifest |

After deployment:
1. Go to your App Service in Azure Portal
2. Copy the URL (e.g., `https://your-app-name.azurewebsites.net`)
3. Set up ngrok or a public callback URL so the VC Request Service can reach your app

## Table of Contents

- [Features](#features)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Deployment](#deployment)
- [Project Structure](#project-structure)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)

## Features

- **Credential Issuance** - Issue Verified ID credentials to a user's digital wallet via QR code
- **Credential Verification** - Verify presented credentials and display claims in a tabular format
- **FaceCheck Support** - Optional biometric liveness check during presentation using Azure AI Face API
- **Photo Claim Rendering** - Automatically decodes and displays base64url-encoded photo claims as images
- **FaceCheck Confidence Score** - Displays match confidence with color-coded indicators (green/amber/red)
- **Modern Dashboard UI** - Bootstrap 5 responsive design with gradient styling matching Entra branding
- **Structured Logging** - Enhanced logging throughout controllers for debugging and monitoring

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│            Entra Verified ID Test Site                          │
├─────────────────────────────────────────────────────────────────┤
│  ASP.NET Core 8.0 (Razor Pages)                                │
│  ├── IssuerController  - Issuance requests & callbacks         │
│  ├── VerifierController - Presentation requests & callbacks    │
│  └── Pages (Home, Issuer, Verifier)                            │
├─────────────────────────────────────────────────────────────────┤
│  Microsoft.Identity.Web (MSAL)                                 │
│  └── Client Credentials Flow                                   │
├─────────────────────────────────────────────────────────────────┤
│                         APIs                                   │
│  └── Verified ID Request Service API                           │
│      ├── createIssuanceRequest                                 │
│      └── createPresentationRequest                             │
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
    "CredentialManifest": "<your-credential-manifest-url>"
  }
}
```

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
│   └── VerifiedCredentialExpertRulesDefinition.json
├── Models/
│   └── AppSettingsModel.cs        # Configuration model
├── Pages/
│   ├── Shared/
│   │   └── _Layout.cshtml         # Main layout with Bootstrap 5 navbar
│   ├── Index.cshtml               # Home page with action cards
│   ├── Issuer.cshtml              # Credential issuance page
│   └── Verifier.cshtml            # Credential verification page
├── wwwroot/
│   ├── styles.css                 # Custom CSS (Entra-themed)
│   └── qrcode.min.js             # QR code generation library
├── IssuerController.cs            # Issuance API controller
├── VerifierController.cs          # Verification API controller
├── Program.cs                     # Application entry point
├── Startup.cs                     # Service configuration
├── appsettings.json               # Application configuration
├── issuance_request_config.json   # Issuance payload template
├── presentation_request_config.json               # Presentation payload
└── presentation_request_config_facecheck.json      # FaceCheck presentation payload
```

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
