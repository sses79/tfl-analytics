param applicationInsightsName string
param processingAppName string

@secure()
param existingAppSettings object

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: applicationInsightsName
}

resource processingApp 'Microsoft.Web/sites@2024-04-01' existing = {
  name: processingAppName
}

resource processingAppSettings 'Microsoft.Web/sites/config@2024-04-01' = {
  parent: processingApp
  name: 'appsettings'
  properties: union(
    existingAppSettings,
    {
      APPLICATIONINSIGHTS_CONNECTION_STRING: applicationInsights.properties.ConnectionString
    }
  )
}
