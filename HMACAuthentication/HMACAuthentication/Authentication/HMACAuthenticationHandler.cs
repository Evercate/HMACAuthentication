using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace HMACAuthentication.Authentication
{
    public sealed class HMACAuthenticationHandler : AuthenticationHandler<HMACAuthenticationOptions>
    {
        private const string DateHeader = "Date";
        private const string NonceHeader = "Nonce";
        private const string AuthorizationHeader = "Authorization";
        private readonly ISecretLookup lookup;
        private readonly IMemoryCache cache;

        public HMACAuthenticationHandler(IOptionsMonitor<HMACAuthenticationOptions> options,
                                         ILoggerFactory logger,
                                         UrlEncoder encoder,
                                         ISystemClock clock,
                                         ISecretLookup lookup,
                                         IMemoryCache cache)
            : base(options, logger, encoder, clock)
        {
            this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
            this.cache = cache;
        }

        protected async override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = SplitAuthenticationHeader();
            if (header == null)
                return AuthenticateResult.NoResult();

            // Verify that request data is within acceptable time
            if (!DateTimeOffset.TryParseExact(Request.Headers[DateHeader], "r", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out DateTimeOffset requestDate))
                return AuthenticateResult.Fail("Unable to parse Date header");

            if (requestDate > Clock.UtcNow.Add(Options.AllowedDateDrift) || requestDate < Clock.UtcNow.Subtract(Options.AllowedDateDrift))
                return AuthenticateResult.Fail("Date drifted more than allowed, adjust your time settings.");

            var nonce = Request.Headers[NonceHeader];

            if (!string.IsNullOrEmpty(nonce))
            {
                if (cache.TryGetValue(nonce, out string _))
                {
                    return AuthenticateResult.Fail("This message has already been processed");
                }

                //At two times the allowed drift the nonce cache will make sure we never have a repeat message within the allowed drift and outside the allowed drift the message will be invalid due to drift
                cache.Set<string>(nonce, nonce, TimeSpan.FromTicks(Options.AllowedDateDrift.Ticks * 2));
            }

            // Lookup and verify secret
            Logger.LogDebug("Looking up secret for {Id}", header.Value.id);
            var secret = await lookup.LookupAsync(header.Value.id);

            if (secret == null)
            {
                Logger.LogInformation("No secret found for {Id}", header.Value.id);
                return AuthenticateResult.Fail("Invalid id");
            }

            // Check signature
            string serverSignature = SignatureHelper.Calculate(secret, SignatureHelper.Generate(requestDate, await StreamToStringAsync(Request), Request.Method, Request.Path, Request.QueryString.Value, nonce.ToString())); ;
            Logger.LogDebug("Calculated server side signature {signature}", serverSignature);

            if (serverSignature.Equals(header.Value.signature))
            {
                return AuthenticateResult.Success(new AuthenticationTicket(
                    new GenericPrincipal(new GenericIdentity(header.Value.id), Options.GetRolesForId?.Invoke(header.Value.id) ?? null),
                    new AuthenticationProperties() { IsPersistent = false, AllowRefresh = false },
                    Options.Schema));
            }
            else
                return AuthenticateResult.Fail("Invalid signature");
        }

        private (string id, string signature)? SplitAuthenticationHeader()
        {
            var headerContent = Request.Headers[AuthorizationHeader].SingleOrDefault();
            if (headerContent == null)
                return null;

            var splitHeader = headerContent.Split(' ', ':');
            if (splitHeader.Length != 3)
                return null;

            return (splitHeader[1], splitHeader[2]);
        }

        private async Task<string> StreamToStringAsync(HttpRequest request)
        {
            string requestPayload;

            request.EnableBuffering();

            // Leave the body open so the next middleware can read it.
            using (var reader = new StreamReader(
                request.Body,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: -1,
                leaveOpen: true))
            {
                requestPayload = await reader.ReadToEndAsync();

                // Reset the request body stream position so the next middleware can read it
                request.Body.Position = 0;
            }
            
            return requestPayload;
            
            
        }
    }
}
