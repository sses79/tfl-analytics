targetScope = 'resourceGroup'

param environmentName string = 'dev'
param projectName string = 'tfl-analytics'
param waterlooCityLineId string = 'waterloo-city'

var suffix = take(uniqueString(subscription().id, resourceGroup().id), 8)
var ingestionAppName = 'func-${projectName}-ingestion-${environmentName}-${suffix}'

module ingestionLineSettings 'modules/ingestion-line-settings.bicep' = {
  name: 'ingestion-line-settings'
  params: {
    existingAppSettings: list('${resourceId('Microsoft.Web/sites', ingestionAppName)}/config/appsettings', '2024-04-01').properties
    ingestionAppName: ingestionAppName
    waterlooCityLineId: waterlooCityLineId
  }
}

output ingestionFunctionAppName string = ingestionAppName
