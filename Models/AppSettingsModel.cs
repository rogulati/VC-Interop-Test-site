// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Web;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace AspNetCoreVerifiableCredentials
{
    /// <summary>
    /// Description of the configuration of an AzureAD confidential client application. This should
    /// match the application registration done in the Azure portal
    /// </summary>
    public class AppSettingsModel
    {
        /// <summary>
        /// instance of Azure AD, for example public Azure or a Sovereign cloud (Azure China, Germany, US government, etc ...)
        /// </summary>
        public string Instance { get; set; }
        /// <summary>
        /// URL of the client REST API endpoint.
        /// </summary>
        public string Endpoint { get; set; }
        /// <summary>
        /// Web Api scope. With client credentials flows, the scopes is ALWAYS of the shape "resource/.default"
        /// FUTURE THIS WILL CHANGE TO MS GRAPH SCOPE
        /// </summary>
        public string VCServiceScope { get; set; }

        /// <summary>
        /// Credential manifest URL for the WoodgroveTraining issuance flow.
        /// </summary>
        public string CredentialManifest { get; set; }

        /// <summary>
        /// Credential manifest URL for the VerifiedIdentity issuance flow. Set by the administrator via
        /// App Service configuration / appsettings.json (AppSettings__ManifestVerifiedIdentity). Like the
        /// other configurable flows, the VerifiedIdentity credential must be created in the tenant and its
        /// manifest URL supplied here for issuance to work.
        /// </summary>
        public string ManifestVerifiedIdentity { get; set; }

        /// <summary>
        /// Per-flow credential manifest URLs for the issuance test cases. These are set by the
        /// administrator via App Service configuration (environment variables) / appsettings.json.
        /// When a value is empty, the manifest defined inside the flow's request config file is used.
        /// </summary>
        public string ManifestEmployee { get; set; }
        public string ManifestIdTokenHint { get; set; }
        public string ManifestIdToken { get; set; }
        public string ManifestPresentation { get; set; }
        public string ManifestSelfIssued { get; set; }
        public string ManifestMultiple { get; set; }

        /// <summary>
        /// Per-flow credential type names for the issuance test cases. Set by the administrator via
        /// App Service configuration / appsettings.json so type names are not hard-coded in the
        /// request config template files. When empty, the type defined inside the flow's request
        /// config file is used.
        /// </summary>
        public string TypeEmployee { get; set; }
        public string TypeIdTokenHint { get; set; }
        public string TypeIdToken { get; set; }
        public string TypePresentation { get; set; }
        public string TypeSelfIssued { get; set; }
        public string TypeMultiple { get; set; }

        /// <summary>
        /// Claim name/value pairs for the Id token hint attestation flow. Set by the administrator via
        /// App Service configuration (e.g. AppSettings__IdTokenHintClaims__given_name = Megan) so the
        /// claim names and values are not hard-coded. When empty, the claims in the flow's request
        /// config file are used.
        /// </summary>
        public Dictionary<string, string> IdTokenHintClaims { get; set; }

        /// <summary>
        /// Claim name/value pairs for the idTokenHint attestation portion of the Multiple attestations
        /// flow. Set by the administrator via App Service configuration (e.g.
        /// AppSettings__MultipleClaims__given_name = Megan) so the claim names and values are not
        /// hard-coded. When empty, the claims in the Multiple flow's request config file are used.
        /// The credential's other attestations (self-issued, presentation, idToken) are collected in
        /// the wallet and require no input here.
        /// </summary>
        public Dictionary<string, string> MultipleClaims { get; set; }

        /// <summary>
        /// Value for the VerifiedIdentity verificationProvider claim. This is not supplied through the
        /// issuance form but configured via App Service configuration / appsettings.json.
        /// </summary>
        public string VerificationProvider { get; set; }

        public string IssuerAuthority { get; set; }

        public string VerifierAuthority { get; set; }

        public string CPAuthorityId { get; set; }
        /// <summary>
        /// The Tenant is:
        /// - either the tenant ID of the Azure AD tenant in which this application is registered (a guid)
        /// or a domain name associated with the tenant
        /// - or 'organizations' (for a multi-tenant application)
        /// </summary>
        public string TenantId { get; set; }
        /// <summary>
        /// Guid used by the application to uniquely identify itself to Azure AD
        /// </summary>
        public string ClientId { get; set; }
        /// <summary>
        /// URL of the authority
        /// </summary>
        public string Authority
        {
            get
            {
                return String.Format(CultureInfo.InvariantCulture, Instance, TenantId);
            }
        }

        /// <summary>
        /// Client secret (application password)
        /// </summary>
        /// <remarks>client credential applications can authenticate with AAD through two mechanisms: ClientSecret
        /// (which is a kind of application password: this property)
        /// or a certificate previously shared with AzureAD during the application registration 
        /// (and identified by the CertificateName property belows)
        /// <remarks> 
        public string ClientSecret { get; set; }
        /// <summary>
        /// Name of a certificate in the user certificate store
        /// </summary>
        /// <remarks>client credential applications can authenticate with AAD through two mechanisms: ClientSecret
        /// (which is a kind of application password: the property above)
        /// or a certificate previously shared with AzureAD during the application registration 
        /// (and identified by this CertificateName property)
        /// <remarks> 
        public string CertificateName { get; set; }
        /// <summary>
        /// Checks if the sample is configured for using ClientSecret or Certificate. This method is just for the sake of this sample.
        /// You won't need this verification in your production application since you will be authenticating in AAD using one mechanism only.
        /// </summary>
        /// <param name="config">Configuration from appsettings.json</param>
        /// <returns></returns>
        public bool AppUsesClientSecret(AppSettingsModel config)
        {
            string clientSecretPlaceholderValue = "[Enter here a client secret for your application]";
            string certificatePlaceholderValue = "[Or instead of client secret: Enter here the name of a certificate (from the user cert store) as registered with your application]";

            if (!String.IsNullOrWhiteSpace(config.ClientSecret) && config.ClientSecret != clientSecretPlaceholderValue)
            {
                return true;
            }

            else if (!String.IsNullOrWhiteSpace(config.CertificateName) && config.CertificateName != certificatePlaceholderValue)
            {
                return false;
            }

            else
                throw new Exception("You must choose between using client secret or certificate. Please update appsettings.json file.");
        }
        public X509Certificate2 ReadCertificate(string certificateName)
        {
            if (string.IsNullOrWhiteSpace(certificateName))
            {
                throw new ArgumentException("certificateName should not be empty. Please set the CertificateName setting in the appsettings.json", "certificateName");
            }
            CertificateDescription certificateDescription = CertificateDescription.FromStoreWithDistinguishedName(certificateName);
            DefaultCertificateLoader defaultCertificateLoader = new DefaultCertificateLoader();
            defaultCertificateLoader.LoadIfNeeded(certificateDescription);
            return certificateDescription.Certificate;
        }
    }

}



