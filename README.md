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

# Build and publish NuGet package
Run the build script from the `HMACAuthentication` solution directory:
```powershell
.\BuildAndPublish.ps1
```

The script will:
1. Auto-increment the patch version (or let you specify a version manually)
2. Build and pack the project
3. Push to GitHub Packages

**Prerequisites:** Create a `NugetKey.txt` file in the solution directory containing a GitHub Personal Access Token (classic) with `write:packages` scope.

# Install package from GitHub Packages
```
dotnet nuget add source "https://nuget.pkg.github.com/Evercate/index.json" --name "evercate_github" --username YOUR_GITHUB_USERNAME --password YOUR_GITHUB_PAT
dotnet add package Evercate.HMACAuthentication
```


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

    console.log("clientKey: "+ CLIENT_KEY);

    var requestPath = getPath(requestUrl);
    var queryString = getQueryString(requestUrl);
    if (!requestBody || Object.keys(requestBody).length === 0) {
        requestBody = '';
    }

    var date = new Date();
    pm.variables.set('date', date.toUTCString());
    var nonce = uuidv4();
    pm.variables.set('nonce', nonce);
    var signature = generate(date, requestBody, httpMethod.toLowerCase(), requestPath, queryString, nonce);

    utfSignature = require('crypto-js').enc.Utf8.parse(signature);
    hmacDigest = require('crypto-js').enc.Base64.stringify(require('crypto-js').HmacSHA256(utfSignature, SECRET_KEY));

    var authHeader = AUTH_TYPE + ' ' + CLIENT_KEY + ':' + hmacDigest;
    return authHeader;
}

// Set up request body based on the body mode
var requestBody = '';
if (pm.request.body) {
    switch (pm.request.body.mode) {
        case 'raw':
            requestBody = pm.request.body.raw;
            if (pm.request.headers.get("Content-Type") === "application/json") {
                requestBody = JSON.stringify(JSON.parse(requestBody));
            }
            break;
        case 'urlencoded':
            requestBody = pm.request.body.urlencoded.toString();
            break;
        case 'formdata':
            requestBody = pm.request.body.formdata.toString();
            break;
        default:
            requestBody = '';
    }
}

pm.variables.set('hmacAuthHeader', getAuthHeader(pm.request.method, pm.request.url.toString(), requestBody));
````

#### Multiple keys in one environment
The code above will check in the environment *clientHmacPrefix* and if it's found it will try to use that prefix on the clientKey/clientSecret so it will instead look for myprefix.clientKey/myprefix.clientSecret if the *clientHmacPrefix* is set to "myprefix.". Note that the period is not automatically placed, if you wish a period in your prefix you have to set it in your prefix.

To not have manually update the prefix when working with multiple services you can automate this by setting a pre-request script either on a subfolder (under the collection that holds the main pre-request script) or directly on each request.

**Note:** For each place you put this code you need to change the top line to your desired prefix.
```js
//Only change this top line to change prefix
var clientHmacPrefix = 'myprefix.';

pm.variables.set('changedClientHmacPrefix', false);
var currentHmacPrefix = pm.environment.get('clientHmacPrefix');

if(clientHmacPrefix != currentHmacPrefix)
{
    pm.environment.set('clientHmacPrefix', clientHmacPrefix);
    pm.variables.set('changedClientHmacPrefix', true);

    if(currentHmacPrefix == null)
    {
        console.log("No clientHmacPrefix environment variable found, setting new clientHmacPrefix to: '"+clientHmacPrefix+"'");
    }
    else
    {
        console.log("Current clientHmacPrefix environment variable is '" + currentHmacPrefix + "' we are changing it to: '"+clientHmacPrefix+"'");
    }

    console.log("We had the wrong clientHmacPrefix, we set it correctly now but you have to rerun this query!");
}
````

Since pre-request scripts runs outermost folder first the first run on a new service will be with the old clientHmacPrefix. Throwing an error in the pre-request script that changes the clientHmacPrefix stopped the value from being persisted.
Instead we have stored a variable named changedClientHmacPrefix which is true when we detected change to remind us to rerun the request.
Place this on the same collection/folder as the main pre-request script but on the test tab (it will be run after the request)

```js
var changed = pm.variables.get('changedClientHmacPrefix');
pm.variables.set('changedClientHmacPrefix', false);

if(changed === true)
{
    throw new Error("Rerun this query. See console for why.");
}
````
