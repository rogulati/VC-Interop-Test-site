using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCoreVerifiableCredentials
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class IssuerController : ControllerBase
    {
        const string ISSUANCEPAYLOAD = "issuance_request_config.json";
        const string ISSUANCEPAYLOAD_VERIFIEDIDENTITY = "issuance_request_config_verifiedidentity.json";
        const string ISSUANCEPAYLOAD_EMPLOYEE = "issuance_request_config_employee.json";
        const string ISSUANCEPAYLOAD_IDTOKENHINT = "issuance_request_config_idtokenhint.json";
        const string ISSUANCEPAYLOAD_IDTOKEN = "issuance_request_config_idtoken.json";
        const string ISSUANCEPAYLOAD_PRESENTATION = "issuance_request_config_presentation.json";
        const string ISSUANCEPAYLOAD_SELFISSUED = "issuance_request_config_selfissued.json";
        const string ISSUANCEPAYLOAD_MULTIPLE = "issuance_request_config_multiple.json";

        // Supported issuance flows. WoodgroveTraining and VerifiedIdentity keep their existing behavior;
        // the remaining flows are the admin-configurable test cases whose manifest URL is supplied
        // through App Service configuration (see GetManifestForVcType).
        private static readonly string[] AllowedVcTypes =
        {
            "WoodgroveTraining", "VerifiedIdentity",
            "Employee", "IdTokenHint", "IdToken", "Presentation", "SelfIssued", "Multiple"
        };

        // Maps a vctype to the request config template file that ships with the app.
        private static string GetPayloadFileForVcType(string vctype) => vctype switch
        {
            "VerifiedIdentity" => ISSUANCEPAYLOAD_VERIFIEDIDENTITY,
            "Employee" => ISSUANCEPAYLOAD_EMPLOYEE,
            "IdTokenHint" => ISSUANCEPAYLOAD_IDTOKENHINT,
            "IdToken" => ISSUANCEPAYLOAD_IDTOKEN,
            "Presentation" => ISSUANCEPAYLOAD_PRESENTATION,
            "SelfIssued" => ISSUANCEPAYLOAD_SELFISSUED,
            "Multiple" => ISSUANCEPAYLOAD_MULTIPLE,
            _ => ISSUANCEPAYLOAD // WoodgroveTraining (default)
        };
        // Returns the admin-configured manifest URL for the given flow, or null/empty when no override
        // is configured, in which case the manifest defined in the flow's own request config file is used.
        private string GetManifestForVcType(string vctype) => vctype switch
        {
            "WoodgroveTraining" => AppSettings.CredentialManifest,
            "VerifiedIdentity" => AppSettings.ManifestVerifiedIdentity,
            "Employee" => AppSettings.ManifestEmployee,
            "IdTokenHint" => AppSettings.ManifestIdTokenHint,
            "IdToken" => AppSettings.ManifestIdToken,
            "Presentation" => AppSettings.ManifestPresentation,
            "SelfIssued" => AppSettings.ManifestSelfIssued,
            "Multiple" => AppSettings.ManifestMultiple,
            _ => null
        };

        // Returns the admin-configured credential type for the given flow, or null/empty when the flow
        // should keep the type defined in its own request config file (WoodgroveTraining, VerifiedIdentity).
        private string GetTypeForVcType(string vctype) => vctype switch
        {
            "Employee" => AppSettings.TypeEmployee,
            "IdTokenHint" => AppSettings.TypeIdTokenHint,
            "IdToken" => AppSettings.TypeIdToken,
            "Presentation" => AppSettings.TypePresentation,
            "SelfIssued" => AppSettings.TypeSelfIssued,
            "Multiple" => AppSettings.TypeMultiple,
            _ => null
        };
        protected readonly AppSettingsModel AppSettings;
        protected IMemoryCache _cache;
        protected readonly ILogger<IssuerController> _log;
        private IHttpClientFactory _httpClientFactory;
        private string _apiKey;
        public IssuerController(IOptions<AppSettingsModel> appSettings, IMemoryCache memoryCache, ILogger<IssuerController> log, IHttpClientFactory httpClientFactory)
        {
            this.AppSettings = appSettings.Value;
            _cache = memoryCache;
            _log = log;
            _httpClientFactory = httpClientFactory;
            _apiKey = System.Environment.GetEnvironmentVariable("API-KEY");
        }

        /// <summary>
        /// This method is called from the UI to initiate the issuance of the verifiable credential
        /// </summary>
        /// <returns>JSON object with the address to the presentation request and optionally a QR code and a state value which can be used to check on the response status</returns>
        [HttpGet("/api/issuer/issuance-request")]
        [HttpPost("/api/issuer/issuance-request")]
        public async Task<ActionResult> IssuanceRequest([FromQuery] string vctype = "WoodgroveTraining")
        {
            try
            {
                //they payload template is loaded from disk and modified in the code below to make it easier to get started
                //and having all config in a central location appsettings.json. 
                //if you want to manually change the payload in the json file make sure you comment out the code below which will modify it automatically
                //
                string jsonString = null;
                string newpin = null;

                // Select the correct payload file based on the requested VCType
                if (!AllowedVcTypes.Contains(vctype))
                {
                    return BadRequest(new { error = "400", error_description = "Invalid vctype parameter" });
                }
                string payloadFile = GetPayloadFileForVcType(vctype);
                string payloadpath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), payloadFile);
                _log.LogInformation("IssuanceRequest started. Loading payload from: {PayloadPath}", payloadpath);
                if (!System.IO.File.Exists(payloadpath))
                {
                    _log.LogError("Issuance payload file not found: {PayloadPath}", payloadpath);
                    return BadRequest(new { error = "400", error_description = payloadFile + " not found" });
                }
                jsonString = System.IO.File.ReadAllText(payloadpath);
                if (string.IsNullOrEmpty(jsonString))
                {
                    _log.LogError("Issuance payload file is empty: {PayloadPath}", payloadpath);
                    return BadRequest(new { error = "400", error_description = payloadFile + " error reading file" });
                }
                _log.LogDebug("Issuance payload loaded successfully, length: {Length} bytes", jsonString.Length);

                //check if pin is required, if found make sure we set a new random pin
                //pincode is only used when the payload contains claim value pairs which results in an IDTokenhint
                JObject payload = JObject.Parse(jsonString);
                if (payload["pin"] != null)
                {
                    if (IsMobile())
                    {
                        _log.LogInformation("PIN element found in payload, but on mobile - removing PIN for deep linking");
                        //consider providing the PIN through other means to your user instead of removing it.
                        payload["pin"].Parent.Remove();

                    }
                    else
                    {
                        _log.LogInformation("PIN element found in payload, generating random PIN");
                        var length = (int)payload["pin"]["length"];
                        var pinMaxValue = (int)Math.Pow(10, length) - 1;
                        var randomNumber = RandomNumberGenerator.GetInt32(1, pinMaxValue);
                        newpin = string.Format("{0:D" + length.ToString() + "}", randomNumber);
                        payload["pin"]["value"] = newpin;
                    }

                }
                string state = Guid.NewGuid().ToString();

                //modify payload with new state, the state is used to be able to update the UI when callbacks are received from the VC Service
                if (payload["callback"]["state"] != null)
                {
                    payload["callback"]["state"] = state;
                }

                //get the IssuerDID from the appsettings
                if (payload["authority"] != null)
                {
                    payload["authority"] = AppSettings.IssuerAuthority;
                }

                //modify the callback method to make it easier to debug 
                //with tools like ngrok since the URI changes all the time
                //this way you don't need to modify the callback URL in the payload every time
                //ngrok changes the URI

                if (payload["callback"]["url"] != null)
                {
                    //localhost hostname can't work for callbacks so we won't overwrite it.
                    //this happens for example when testing with sign-in to an IDP and https://localhost is used as redirect URI
                    //in that case the callback should be configured in the payload directly instead of being modified in the code here
                    string host = GetRequestHostName();
                    if (!host.Contains("//localhost"))
                    {
                        payload["callback"]["url"] = String.Format("{0}/api/issuer/issuanceCallback", host);
                    }
                }

                // set our api-key in the request so we can check it in the callbacks we receive
                if (payload["callback"]["headers"]["api-key"] != null) 
                {
                    payload["callback"]["headers"]["api-key"] = this._apiKey;
                }

                //get the manifest from the appsettings, this is the URL to the credential created in the azure portal. 
                //the display and rules file to create the credential can be dound in the credentialfiles directory
                //make sure the credentialtype in the issuance payload matches with the rules file
                //for this sample it should be VerifiedCredentialExpert
                if (payload["manifest"] != null)
                {
                    // Each flow takes its manifest from App Service configuration when one is set;
                    // otherwise the manifest defined in the flow's own request config file is used.
                    string manifestOverride = GetManifestForVcType(vctype);
                    if (!string.IsNullOrWhiteSpace(manifestOverride))
                    {
                        payload["manifest"] = manifestOverride;
                    }
                }

                // Override the credential type from App Service configuration when provided, so type
                // names are not hard-coded in the request config templates. WoodgroveTraining and
                // VerifiedIdentity keep the type from their own config files (helper returns null).
                string typeOverride = GetTypeForVcType(vctype);
                if (!string.IsNullOrWhiteSpace(typeOverride))
                {
                    payload["type"] = typeOverride;
                }

                // For the Id token hint flow, take the claim name/value pairs from App Service
                // configuration when supplied, so claim names are not hard-coded in the template.
                if (vctype == "IdTokenHint" && AppSettings.IdTokenHintClaims != null && AppSettings.IdTokenHintClaims.Count > 0)
                {
                    var idTokenHintClaims = new JObject();
                    foreach (var claim in AppSettings.IdTokenHintClaims)
                    {
                        idTokenHintClaims[claim.Key] = claim.Value;
                    }
                    payload["claims"] = idTokenHintClaims;
                }

                // For the Multiple attestations flow, supply the idTokenHint attestation claims from
                // App Service configuration when present. The credential's other attestations
                // (self-issued, presentation, idToken) are collected in the wallet and need no input
                // here. When MultipleClaims is empty, the claims defined in the request config file
                // (issuance_request_config_multiple.json) are used as a fallback.
                if (vctype == "Multiple" && AppSettings.MultipleClaims != null && AppSettings.MultipleClaims.Count > 0)
                {
                    var multipleClaims = new JObject();
                    foreach (var claim in AppSettings.MultipleClaims)
                    {
                        multipleClaims[claim.Key] = claim.Value;
                    }
                    payload["claims"] = multipleClaims;
                }

                //here you could change the payload manifest and change the firstname and lastname
                string issuanceTokenId = null;
                if (vctype == "WoodgroveTraining")
                {
                    payload["claims"]["givenName"] = "Megan";
                    payload["claims"]["surname"] = "Bowen";
                    payload["claims"]["email"] = "megan@vcinteropdemo.com";
                    payload["claims"]["displayName"] = "Megan Bowen";
                }
                else if (vctype == "VerifiedIdentity")
                {
                    // Read claims from POST body
                    JObject userClaims = null;
                    if (HttpContext.Request.Method == "POST")
                    {
                        using var reader = new StreamReader(HttpContext.Request.Body);
                        var body = await reader.ReadToEndAsync();
                        if (!string.IsNullOrEmpty(body))
                        {
                            userClaims = JObject.Parse(body);
                        }
                    }

                    payload["claims"]["firstName"] = userClaims?["firstName"]?.ToString() ?? "";
                    payload["claims"]["lastName"] = userClaims?["lastName"]?.ToString() ?? "";
                    payload["claims"]["gender"] = userClaims?["gender"]?.ToString() ?? "";
                    payload["claims"]["address"] = userClaims?["address"]?.ToString() ?? "";
                    payload["claims"]["nationality"] = userClaims?["nationality"]?.ToString() ?? "";
                    payload["claims"]["documentNumber"] = userClaims?["documentNumber"]?.ToString() ?? "";
                    payload["claims"]["documentCode"] = userClaims?["documentCode"]?.ToString() ?? "";
                    payload["claims"]["dateOfBirth"] = userClaims?["dateOfBirth"]?.ToString() ?? "";
                    payload["claims"]["dateOfExpiry"] = userClaims?["dateOfExpiry"]?.ToString() ?? "";
                    payload["claims"]["photo"] = userClaims?["photo"]?.ToString() ?? "";

                    // verificationProvider is not supplied through the form; it is sourced from
                    // App Service configuration / appsettings.json (placeholder value: VIDTeamIDV)
                    payload["claims"]["verificationProvider"] = AppSettings.VerificationProvider;

                    // Pass issuanceTokenId at root level if provided
                    issuanceTokenId = userClaims?["token"]?.ToString();
                    if (!string.IsNullOrEmpty(issuanceTokenId))
                    {
                        payload["token"] = issuanceTokenId;
                    }
                }
                
                jsonString = JsonConvert.SerializeObject(payload);

                //CALL REST API WITH PAYLOAD
                HttpStatusCode statusCode = HttpStatusCode.OK;
                string response = null;
                bool validationPassed = false;

                try
                {
                    //The VC Request API is an authenticated API. We need to clientid and secret (or certificate) to create an access token which 
                    //needs to be send as bearer to the VC Request API
                    _log.LogInformation("Acquiring access token for VC Request API...");
                    var accessToken = await GetAccessToken();
                    if (accessToken.Item1 == String.Empty)
                    {
                        _log.LogError("Failed to acquire access token. Error: {Error}, Description: {Description}", accessToken.error, accessToken.error_description);
                        return BadRequest(new { error = accessToken.error, error_description = accessToken.error_description });
                    }
                    _log.LogInformation("Access token acquired successfully");

                    var client = _httpClientFactory.CreateClient();
                    var defaultRequestHeaders = client.DefaultRequestHeaders;
                    defaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.token);

                    if (vctype == "VerifiedIdentity" && !string.IsNullOrEmpty(issuanceTokenId))
                    {
                        if (string.IsNullOrWhiteSpace(AppSettings.IssuerAuthority))
                        {
                            return BadRequest(new { error = "400", error_description = "IssuerAuthority not configured" });
                        }

                        string validationUrl = AppSettings.Endpoint
                            + "verifiableCredentials/authorities/"
                            + Uri.EscapeDataString(AppSettings.IssuerAuthority)
                            + "/issuanceToken/"
                            + Uri.EscapeDataString(issuanceTokenId)
                            + "/completeValidation";
                        string validationPayload = JsonConvert.SerializeObject(new { validationPassed = true });

                        _log.LogInformation("Completing validation for VerifiedIdentity issuance token");
                        HttpResponseMessage validationResult = await client.PostAsync(
                            validationUrl,
                            new StringContent(validationPayload, Encoding.UTF8, "application/json"));
                        string validationResponse = await validationResult.Content.ReadAsStringAsync();

                        if (!validationResult.IsSuccessStatusCode)
                        {
                            _log.LogError(
                                "Issuance token validation failed. Status: {StatusCode}, Response: {Response}",
                                validationResult.StatusCode,
                                validationResponse);
                            return BadRequest(new
                            {
                                error = ((int)validationResult.StatusCode).ToString(),
                                error_description = "Issuance token validation failed: " + validationResponse
                            });
                        }

                        JObject validationResultPayload = JObject.Parse(validationResponse);
                        if (validationResultPayload["validationPassed"]?.Value<bool>() != true)
                        {
                            _log.LogError("Issuance token validation did not return validationPassed=true");
                            return BadRequest(new
                            {
                                error = "400",
                                error_description = "Issuance token validation was not accepted"
                            });
                        }

                        validationPassed = true;
                        _log.LogInformation("Issuance token validation completed successfully");
                    }

                    HttpResponseMessage res = await client.PostAsync(AppSettings.Endpoint + "verifiableCredentials/createIssuanceRequest", new StringContent(jsonString, Encoding.UTF8, "application/json"));
                    response = await res.Content.ReadAsStringAsync();
                    statusCode = res.StatusCode;

                    _log.LogDebug("VC Request API responded with status: {StatusCode}", statusCode);

                    if (statusCode == HttpStatusCode.Created)
                    {
                        _log.LogInformation("Issuance request created successfully. State: {State}", state);
                        JObject requestConfig = JObject.Parse(response);
                        if (newpin != null) { requestConfig["pin"] = newpin; }
                        if (validationPassed) { requestConfig["validationPassed"] = true; }
                        requestConfig.Add(new JProperty("id", state));
                        jsonString = JsonConvert.SerializeObject(requestConfig);

                        //We use in memory cache to keep state about the request. The UI will check the state when calling the presentationResponse method

                        var cacheData = new
                        {
                            status = "notscanned",
                            message = "Request ready, please scan with Authenticator",
                            expiry = requestConfig["expiry"].ToString()
                        };
                        _cache.Set(state, JsonConvert.SerializeObject(cacheData));

                        return new ContentResult { ContentType = "application/json", Content = jsonString };
                    }
                    else
                    {
                        _log.LogError("VC Request API call failed. Status: {StatusCode}, Response: {Response}", statusCode, response);
                        return BadRequest(new { error = "400", error_description = "Something went wrong calling the API: " + response });
                    }

                }
                catch (Exception ex)
                {
                    return BadRequest(new { error = "400", error_description = "Something went wrong calling the API: " + ex.Message });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "400", error_description = ex.Message });
            }
        }

        /// <summary>
        /// This method is called by the VC Request API when the user scans a QR code and accepts the issued Verifiable Credential
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult> IssuanceCallback()
        {
            try
            {
                string content = await new System.IO.StreamReader(this.Request.Body).ReadToEndAsync();
                _log.LogInformation("IssuanceCallback received. Content length: {Length}", content.Length);
                _log.LogDebug("IssuanceCallback body: {Content}", content);
                this.Request.Headers.TryGetValue("api-key", out var apiKey);
                if (this._apiKey != apiKey)
                {
                    _log.LogWarning("IssuanceCallback rejected: api-key wrong or missing");
                    return new ContentResult() { StatusCode = (int)HttpStatusCode.Unauthorized, Content = "api-key wrong or missing" };
                }
                JObject issuanceResponse = JObject.Parse(content);
                var state = issuanceResponse["state"].ToString();
                var requestStatus = issuanceResponse["requestStatus"].ToString();
                _log.LogInformation("IssuanceCallback - State: {State}, Status: {RequestStatus}", state, requestStatus);

                //there are 2 different callbacks. 1 if the QR code is scanned (or deeplink has been followed)
                //Scanning the QR code makes Authenticator download the specific request from the server
                //the request will be deleted from the server immediately.
                //That's why it is so important to capture this callback and relay this to the UI so the UI can hide
                //the QR code to prevent the user from scanning it twice (resulting in an error since the request is already deleted)
                if (requestStatus == "request_retrieved")
                {
                    _log.LogInformation("QR code scanned for issuance. State: {State}", state);
                    var cacheData = new
                    {
                        status = "request_retrieved",
                        message = "QR Code is scanned. Waiting for issuance...",
                    };
                    _cache.Set(state, JsonConvert.SerializeObject(cacheData));
                }

                //
                //This callback is called when issuance is completed.
                //
                if (requestStatus == "issuance_successful")
                {
                    _log.LogInformation("Credential issued successfully. State: {State}", state);
                    var cacheData = new
                    {
                        status = "issuance_successful",
                        message = "Credential successfully issued",
                    };
                    _cache.Set(state, JsonConvert.SerializeObject(cacheData));
                }
                //
                //We capture if something goes wrong during issuance. See documentation with the different error codes
                //
                if (requestStatus == "issuance_error")
                {
                    _log.LogError("Issuance error for state: {State}. Code: {ErrorCode}, Message: {ErrorMessage}",
                        state, issuanceResponse["error"]["code"], issuanceResponse["error"]["message"]);
                    var cacheData = new
                    {
                        status = "issuance_error",
                        payload = issuanceResponse["error"]["code"].ToString(),
                        //at the moment there isn't a specific error for incorrect entry of a pincode.
                        //So assume this error happens when the users entered the incorrect pincode and ask to try again.
                        message = issuanceResponse["error"]["message"].ToString()

                    };
                    _cache.Set(state, JsonConvert.SerializeObject(cacheData));
                }

                return new OkResult();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "400", error_description = ex.Message });
            }
        }

        //
        //this function is called from the UI polling for a response from the AAD VC Service.
        //when a callback is recieved at the issuanceCallback service the session will be updated
        //this method will respond with the status so the UI can reflect if the QR code was scanned and with the result of the issuance process
        //
        [HttpGet("/api/issuer/issuance-response")]
        public ActionResult IssuanceResponse()
        {
            try
            {
                //the id is the state value initially created when the issuanc request was requested from the request API
                //the in-memory database uses this as key to get and store the state of the process so the UI can be updated
                string state = this.Request.Query["id"];
                if (string.IsNullOrEmpty(state))
                {
                    return BadRequest(new { error = "400", error_description = "Missing argument 'id'" });
                }
                JObject value = null;
                if (_cache.TryGetValue(state, out string buf))
                {
                    value = JObject.Parse(buf);

                    Debug.WriteLine("check if there was a response yet: " + value);
                    return new ContentResult { ContentType = "application/json", Content = JsonConvert.SerializeObject(value) };
                }

                return new OkResult();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "400", error_description = ex.Message });
            }
        }

        //some helper functions
        protected async Task<(string token, string error, string error_description)> GetAccessToken()
        {

            // You can run this sample using ClientSecret or Certificate. The code will differ only when instantiating the IConfidentialClientApplication
            bool isUsingClientSecret = AppSettings.AppUsesClientSecret(AppSettings);

            // Since we are using application permissions this will be a confidential client application
            IConfidentialClientApplication app;
            if (isUsingClientSecret)
            {
                app = ConfidentialClientApplicationBuilder.Create(AppSettings.ClientId)
                    .WithClientSecret(AppSettings.ClientSecret)
                    .WithAuthority(new Uri(AppSettings.Authority))
                    .Build();
            }
            else
            {
                X509Certificate2 certificate = AppSettings.ReadCertificate(AppSettings.CertificateName);
                app = ConfidentialClientApplicationBuilder.Create(AppSettings.ClientId)
                    .WithCertificate(certificate)
                    .WithAuthority(new Uri(AppSettings.Authority))
                    .Build();
            }

            //configure in memory cache for the access tokens. The tokens are typically valid for 60 seconds,
            //so no need to create new ones for every web request
            app.AddDistributedTokenCache(services =>
            {
                services.AddDistributedMemoryCache();
                services.AddLogging(configure => configure.AddConsole())
                .Configure<LoggerFilterOptions>(options => options.MinLevel = Microsoft.Extensions.Logging.LogLevel.Debug);
            });

            // With client credentials flows the scopes is ALWAYS of the shape "resource/.default", as the 
            // application permissions need to be set statically (in the portal or by PowerShell), and then granted by
            // a tenant administrator. 
            string[] scopes = new string[] { AppSettings.VCServiceScope };

            AuthenticationResult result = null;
            try
            {
                result = await app.AcquireTokenForClient(scopes)
                    .ExecuteAsync();
            }
            catch (MsalServiceException ex) when (ex.Message.Contains("AADSTS70011"))
            {
                // Invalid scope. The scope has to be of the form "https://resourceurl/.default"
                // Mitigation: change the scope to be as expected
                return (string.Empty, "500", "Scope provided is not supported");
                //return BadRequest(new { error = "500", error_description = "Scope provided is not supported" });
            }
            catch (MsalServiceException ex)
            {
                // general error getting an access token
                return (String.Empty, "500", "Something went wrong getting an access token for the client API:" + ex.Message);
                //return BadRequest(new { error = "500", error_description = "Something went wrong getting an access token for the client API:" + ex.Message });
            }

            _log.LogDebug("Access token acquired. Expires on: {ExpiresOn}", result.ExpiresOn);
            return (result.AccessToken, String.Empty, String.Empty);
        }
        protected string GetRequestHostName()
        {
            string scheme = "https";// : this.Request.Scheme;
            string originalHost = this.Request.Headers["x-original-host"];
            string hostname = "";
            if (!string.IsNullOrEmpty(originalHost))
                hostname = string.Format("{0}://{1}", scheme, originalHost);
            else hostname = string.Format("{0}://{1}", scheme, this.Request.Host);
            return hostname;
        }

        protected bool IsMobile()
        {
            string userAgent = this.Request.Headers["User-Agent"];

            if (userAgent.Contains("Android") || userAgent.Contains("iPhone"))
                return true;
            else
                return false;
        }

        /// <summary>
        /// IDV issuance entry point. Returns 200 OK for the certification probe
        /// (tokenId=CERTIFICATIONTEST), and for a real issuanceTokenId redirects to the
        /// VerifiedIdentity issuance page where token details are auto-populated and the
        /// token is included in the issuance request.
        /// </summary>
        [HttpGet("/Issuer/vid")]
        public ActionResult Vid([FromQuery] string tokenId)
        {
            // Certification test probe used by the IDV certification harness.
            if (string.Equals(tokenId, "CERTIFICATIONTEST", StringComparison.OrdinalIgnoreCase))
            {
                return Ok();
            }

            // A real issuance tokenId: hand off to the Issuer page for the VerifiedIdentity flow.
            if (!string.IsNullOrWhiteSpace(tokenId))
            {
                return Redirect($"/Issuer/VerifiedIdentity?tokenId={Uri.EscapeDataString(tokenId)}");
            }

            return BadRequest(new { error = "400", error_description = "Invalid or missing tokenId" });
        }

        /// <summary>
        /// Fetches issuance token details from the Verified ID service
        /// </summary>
        [HttpGet("/api/issuer/token-details")]
        public async Task<ActionResult> GetTokenDetails([FromQuery] string tokenId)
        {
            try
            {
                if (string.IsNullOrEmpty(tokenId))
                {
                    return BadRequest(new { error = "400", error_description = "Missing tokenId parameter" });
                }

                if (string.IsNullOrEmpty(AppSettings.CPAuthorityId))
                {
                    return BadRequest(new { error = "400", error_description = "CPAuthorityId not configured" });
                }

                var accessToken = await GetAccessToken();
                if (accessToken.Item1 == String.Empty)
                {
                    return BadRequest(new { error = accessToken.error, error_description = accessToken.error_description });
                }

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.token);

                string url = $"https://verifiedid.did.msidentity.com/beta/verifiableCredentials/authorities/{AppSettings.CPAuthorityId}/issuanceToken/{tokenId}";
                HttpResponseMessage res = await client.GetAsync(url);
                string response = await res.Content.ReadAsStringAsync();

                if (res.StatusCode == HttpStatusCode.OK)
                {
                    return new ContentResult { ContentType = "application/json", Content = response };
                }
                else
                {
                    return BadRequest(new { error = ((int)res.StatusCode).ToString(), error_description = response });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "400", error_description = ex.Message });
            }
        }
    }
}
