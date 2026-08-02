targetScope = 'resourceGroup'

param environmentName string = 'dev'
param projectName string = 'tfl-analytics'
param enableArrivalIngestion bool = true

var suffix = take(uniqueString(subscription().id, resourceGroup().id), 8)
var ingestionAppName = 'func-${projectName}-ingestion-${environmentName}-${suffix}'

module ingestionArrivalSettings 'modules/ingestion-arrival-settings.bicep' = {
  name: 'ingestion-arrival-settings'
  params: {
    existingAppSettings: list('${resourceId('Microsoft.Web/sites', ingestionAppName)}/config/appsettings', '2024-04-01').properties
    ingestionAppName: ingestionAppName
    enableArrivalIngestion: enableArrivalIngestion
  }
}

output ingestionFunctionAppName string = ingestionAppName
output arrivalIngestionEnabled bool = enableArrivalIngestion
