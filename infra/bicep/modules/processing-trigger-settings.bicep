param processingAppName string
param cosmosDatabaseName string
param cosmosRawEventsContainerName string
param cosmosLeasesContainerName string

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
      CosmosTriggerDatabaseName: cosmosDatabaseName
      CosmosTriggerRawEventsContainerName: cosmosRawEventsContainerName
      CosmosTriggerLeasesContainerName: cosmosLeasesContainerName
    }
  )
}
