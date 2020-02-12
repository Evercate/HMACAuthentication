# Usage
## Server
```csharp
services.AddMemoryCache();
services.AddAuthentication(options =>
{
    options.DefaultScheme = "HMAC";
}).AddHMACAuthentication();

services.AddAuthorization(options =>
{
    options.AddPolicy("AuthenticationRequired", policy =>
    {
        policy.RequireAuthenticatedUser();
    });
});

services.AddScoped<ISecretLookup, YOUR_IMPLEMENTATION>();

```
## Example ISecretLookup implementation
```csharp
public class HmacSecretLookup : ISecretLookup
{
    public Task<string> LookupAsync(string id)
    {
        if (id == "YOUR_APPLICATION_ID")
            return Task.FromResult("YOUR_SECRET_KEY");
        else
            return Task.FromResult<string>(null);
    }
}

````

## Client
```csharp
var request = new HttpRequestMessage(new HttpMethod("POST"), "YOUR_ENDPOINT");
SignatureHelper.SetHmacHeaders(request, "YOUR_APPLICATION_ID", "YOUR_SECRET_KEY", "PAYLOAD");
```

# Build as nuget package
1. Build project as normal (Release)
2. Bump the version in the nuspec file
3. Use CMD in project dir and run `.\nuget.exe pack -Prop Configuration=Release`
4. Run `.\nuget.exe push {package file} {apikey} -Source {nuget server url}`
