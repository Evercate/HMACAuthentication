using Microsoft.AspNetCore.Authentication;
using System;

namespace HMACAuthentication.Authentication
{
    public class HMACAuthenticationOptions : AuthenticationSchemeOptions
    {
        public const string DefaultSchema = "HMAC";

        public string Schema => DefaultSchema;

        public TimeSpan AllowedDateDrift { get; set; } = TimeSpan.FromMinutes(5);

        public Func<string, string[]>? GetRolesForId { get; set; }
    }
}
