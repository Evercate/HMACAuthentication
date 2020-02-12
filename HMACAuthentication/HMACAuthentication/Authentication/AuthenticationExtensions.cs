namespace Microsoft.Extensions.DependencyInjection
{
    using HMACAuthentication.Authentication;
    using Microsoft.AspNetCore.Authentication;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public static class AuthenticationExtensions
    {
        public static AuthenticationBuilder AddHMACAuthentication(this AuthenticationBuilder builder)
        {
            return builder.AddHMACAuthentication((options) => { });
        }

        public static AuthenticationBuilder AddHMACAuthentication(this AuthenticationBuilder builder, Action<HMACAuthenticationOptions> options)
        {
            return builder.AddScheme<HMACAuthenticationOptions, HMACAuthenticationHandler>(HMACAuthenticationOptions.DefaultSchema, options);
        }
    }
}