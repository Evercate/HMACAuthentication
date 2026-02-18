using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace HMACAuthentication.Authentication
{
    public static class SignatureHelper
    {
        public static string Generate(DateTimeOffset requestDate, string content, string method, string path, string? query, string nonce)
        {
            if (requestDate == default)
                throw new ArgumentException("Request date should be diffrent the default", nameof(requestDate));

            return (requestDate.ToString("r") + '\n' +
                   content + '\n' +
                   method + '\n' +
                   path + '\n' +
                   nonce + '\n' +
                   query?.TrimStart('?')).ToLower();
        }

        public static string Calculate(string secret, string signature)
        {
            if (secret == null)
                throw new ArgumentNullException(nameof(secret));

            if (signature == null)
                throw new ArgumentNullException(nameof(signature));

            using (HMAC hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signature)));
            }
        }

        public static void SetHmacHeaders(HttpRequestMessage request, string appId, string apiKey, string payload)
        {
            request.Headers.Date = DateTimeOffset.UtcNow;
            var nonce = Guid.NewGuid().ToString();
            request.Headers.Add("Nonce", nonce);
            string authenticationSignature = Calculate(apiKey, Generate(request.Headers.Date.Value, payload, request.Method.Method, request.RequestUri!.AbsolutePath, request.RequestUri.Query, nonce));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("HMAC", appId + ":" + authenticationSignature);
        }
    }
}
