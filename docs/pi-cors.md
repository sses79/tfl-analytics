# API CORS Configuration

The TfL Analytics API permits browser requests only from the Angular
development server and the deployed Azure Static Web App. It does not use a
wildcard origin.

## Why CORS Is Required

The browser treats the Angular dashboard and API as different origins:

```text
Local dashboard: http://localhost:4200
Local API:       http://localhost:8080

Azure dashboard: https://blue-bush-0491f9503.7.azurestaticapps.net
Azure API:       https://ca-tfl-api-dev-nhkpyupi.livelypebble-dde4d540.uksouth.azurecontainerapps.io
```

Without an API CORS policy, the browser blocks dashboard JavaScript from
reading API responses.

## ASP.NET Core Policy

`src/TflAnalytics.Api/Program.cs` reads an array from
`Cors:AllowedOrigins`:

```csharp
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .GetChildren()
    .Select(origin => origin.Value)
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Cast<string>()
    .ToArray();
```

The values are applied to the named `Dashboard` policy:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Dashboard", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});
```

The policy is added before endpoint mapping:

```csharp
app.UseCors("Dashboard");
app.MapControllers();
```

`WithOrigins` performs exact origin matching. Do not add a trailing slash to an
allowed origin.

## Local Development

`src/TflAnalytics.Api/appsettings.Development.json` permits both common Angular
development addresses:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:4200",
      "http://127.0.0.1:4200"
    ]
  }
}
```

Start the API in the `Development` environment so this file is loaded. Other
local ports are rejected unless they are added explicitly.

## Azure Configuration

The production origin is derived from the Static Web App Bicep output in
`infra/bicep/main.bicep`:

```bicep
dashboardOrigin: 'https://${compute.outputs.staticWebAppHostname}'
```

`infra/bicep/modules/api-hosting.bicep` passes it to the Container App:

```bicep
{
  name: 'Cors__AllowedOrigins__0'
  value: dashboardOrigin
}
```

ASP.NET Core maps the double underscores to this configuration path:

```text
Cors:AllowedOrigins:0
```

This avoids duplicating the generated Static Web App hostname in application
code. The Container App also runs with:

```text
ASPNETCORE_ENVIRONMENT=Production
```

## Smoke Tests

Test the local preflight request:

```bash
curl --fail --include --request OPTIONS \
  http://localhost:8080/api/tfl/line-status/victoria \
  --header "Origin: http://localhost:4200" \
  --header "Access-Control-Request-Method: GET"
```

Test the deployed preflight request:

```bash
source scripts/load-azure-outputs.sh

curl --fail --include --request OPTIONS \
  "https://$API_HOSTNAME/api/tfl/line-status/victoria" \
  --header "Origin: https://$STATIC_WEB_APP_HOSTNAME" \
  --header "Access-Control-Request-Method: GET"
```

The expected response is `204 No Content` with headers similar to:

```text
Access-Control-Allow-Methods: GET
Access-Control-Allow-Origin: https://blue-bush-0491f9503.7.azurestaticapps.net
```

Confirm that an unapproved origin is not granted access:

```bash
curl --include --request OPTIONS \
  "https://$API_HOSTNAME/api/tfl/line-status/victoria" \
  --header "Origin: https://example.com" \
  --header "Access-Control-Request-Method: GET"
```

The response must not contain an `Access-Control-Allow-Origin` header.

## Security Notes

- CORS is a browser access control, not authentication or authorization.
- Do not use `AllowAnyOrigin` for the deployed API.
- Do not combine `AllowAnyOrigin` with credentialed browser requests.
- Add each new dashboard hostname explicitly through Bicep configuration.
- Keep API keys server-side; CORS does not make a browser-exposed secret safe.
