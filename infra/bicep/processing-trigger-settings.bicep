targetScope = 'resourceGroup'

param environmentName string = 'dev'
param projectName string = 'tfl-analytics'
param cosmosDatabaseName string = 'tfl-analytics'
param cosmosRawEventsContainerName string = 'raw-events'
param cosmosLeasesContainerName string = 'leases'

var suffix = take(uniqueString(subscription().id, resourceGroup().id), 8)
var processingAppName = 'func-${projectName}-processing-${environmentName}-${suffix}'

module processingTriggerSettings 'modules/processing-trigger-settings.bicep' = {
  name: 'processing-trigger-settings'
  params: {
    cosmosDatabaseName: cosmosDatabaseName
    cosmosLeasesContainerName: cosmosLeasesContainerName
    cosmosRawEventsContainerName: cosmosRawEventsContainerName
    existingAppSettings: list('${resourceId('Microsoft.Web/sites', processingAppName)}/config/appsettings', '2024-04-01').properties
    processingAppName: processingAppName
  }
}

output processingFunctionAppName string = processingAppName
