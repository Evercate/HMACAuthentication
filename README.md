# Usage
## Server
```csharp
public void ConfigureServices(IServiceCollection services)
{
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
    
    ...
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    app.UseAuthentication();
    app.UseAuthorization();
    
    ...
}

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

# Use package from Nuget
https://www.nuget.org/packages/CaptainAndrey.HMACAuthentication/


-----------------------------------

# Manual use from Postman
1. Create a collection in Postman
2. Click edit on your collection
3. On tab Pre-request Scripts add the script below
4. Save
5. Go to Headers tab for the call
6. Add the following
    * Authorization: {{hmacAuthHeader}}
    * Nonce: {{nonce}}
    * Date: {{date}}
7. Open your Environment and add the following variables
    * clientKey: *your hmac key*
    * clientSecret: *your hmac secret*

*Future improvements*: Handle more different key/secret pairs for different services (see if we can mix in local variables in postman)

The script to add on step 3
```js
function generate(requestDate, content, method, path, query, nonce) {
    return (requestDate.toUTCString() + '\n' +
        content + '\n' +
        method + '\n' +
        path + '\n' +
        nonce + '\n' +
        query).toLowerCase();
}

function uuidv4() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
        var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

function getPath(url) {
    var pathRegex = /.+?\:\/\/.+?(\/.+?)(?:#|\?|$)/;
    var result = url.match(pathRegex);
    return result && result.length > 1 ? result[1] : '';
}

function getQueryString(url) {
    var arrSplit = url.split('?');
    return arrSplit.length > 1 ? url.substring(url.indexOf('?') + 1) : '';
}

function getAuthHeader(httpMethod, requestUrl, requestBody) {
    var CLIENT_KEY = pm.variables.get("clientKey");
    var SECRET_KEY = pm.variables.get("clientSecret");
    var AUTH_TYPE = 'HMAC';

    var requestPath = getPath(requestUrl);
    var queryString = getQueryString(requestUrl);
    if (httpMethod == 'GET' || !requestBody) {
        requestBody = '';
    }

    var date = new Date();
    pm.variables.set('date', date.toUTCString());
    var nonce = uuidv4();
    pm.variables.set('nonce', nonce);
    var signature = generate(date, requestBody, httpMethod.toLowerCase(), requestPath, queryString, nonce);

    utfSignature = CryptoJS.enc.Utf8.parse(signature);
    hmacDigest = CryptoJS.enc.Base64.stringify(CryptoJS.HmacSHA256(utfSignature, SECRET_KEY));

    var authHeader = AUTH_TYPE + ' ' + CLIENT_KEY + ':' + hmacDigest;
    return authHeader;
}

pm.variables.set('hmacAuthHeader', getAuthHeader(request['method'], request['url'], request['data']));


````
