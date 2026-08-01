param processingAppName string
param signalRHostname string

@secure()
param existingAppSettings object

resource processingApp 'Microsoft.Web/sites@2024-04-01' existing = {
  name: processingAppName
}

resource processingAppSettings 'Microsoft.Web/sites/config@2024-04-01' = {
  parent: processingApp
  name: 'appsettings'
  properties: union(
    existingAppSettings,
    {
      SignalR__Endpoint: 'https://${signalRHostname}'
    }
  )
}
