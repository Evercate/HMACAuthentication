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
https://www.nuget.org/packages/Evercate.HMACAuthentication


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

    var ENVIRONMENT_HMAC_PREFIX_KEY = pm.variables.get("clientHmacPrefix");

    var CLIENT_KEY = null;
    var SECRET_KEY = null;
    var AUTH_TYPE = 'HMAC';

    var clientKeyVariableName = "clientKey";
    var clientSecretVariableName = "clientSecret";

    if(ENVIRONMENT_HMAC_PREFIX_KEY != null)
    {
        var prefixedClientKey = ENVIRONMENT_HMAC_PREFIX_KEY + clientKeyVariableName;
        var prefixedSecretKey = ENVIRONMENT_HMAC_PREFIX_KEY + clientSecretVariableName;

        console.log("The clientHmacPrefix '" + ENVIRONMENT_HMAC_PREFIX_KEY + "' was found, looking up '" + prefixedClientKey + "' and '" + prefixedSecretKey + "'");

        CLIENT_KEY = pm.variables.get(prefixedClientKey);
        SECRET_KEY = pm.variables.get(prefixedSecretKey);

        if(CLIENT_KEY == null || SECRET_KEY == null)
        {
            console.log("No variables found with the names of " + prefixedClientKey + "/" + prefixedSecretKey + ". We will look up keys with the default names");
        }
    }

    if(CLIENT_KEY == null || SECRET_KEY == null)
    {
        CLIENT_KEY = pm.variables.get(clientKeyVariableName);
        SECRET_KEY = pm.variables.get(clientSecretVariableName);

        console.log("We fetched the key/secret with " + clientKeyVariableName + "/" + clientSecretVariableName + " variable names. Key was: '" + CLIENT_KEY + "' and secret was: '" + SECRET_KEY + "'")
    }

    if(CLIENT_KEY == null || SECRET_KEY == null)
    {
        console.error("No key/secret pair was found. Make sure you have " + clientKeyVariableName + " and " + clientSecretVariableName + "as variables (environment or otherwise). Note you can also have a prefix to have multiple variables in the same environment");
        return;
    }
    else
    {
        console.log("Found key '" + CLIENT_KEY + "' and secret '" + SECRET_KEY + "'");
    }


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

    utfSignature = CryptoJS.enc.Utf8.parse(signature);
    hmacDigest = CryptoJS.enc.Base64.stringify(CryptoJS.HmacSHA256(utfSignature, SECRET_KEY));

    var authHeader = AUTH_TYPE + ' ' + CLIENT_KEY + ':' + hmacDigest;
    return authHeader;
}

pm.variables.set('hmacAuthHeader', getAuthHeader(request['method'], request['url'], request['data']));


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
