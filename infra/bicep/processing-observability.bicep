targetScope = 'resourceGroup'

@description('Azure region used by the processing Function App and monitoring resources.')
param location string = resourceGroup().location

param environmentName string = 'dev'
param projectName string = 'tfl-analytics'

@description('Daily Log Analytics ingestion cap in GB. Kept as a string because Bicep has no decimal number type.')
@allowed([
  '0.1'
  '0.5'
  '1'
])
param dailyQuotaGb string = '0.1'

var suffix = take(uniqueString(subscription().id, resourceGroup().id), 8)
var logAnalyticsName = 'log-${projectName}-${environmentName}-${suffix}'
var applicationInsightsName = 'appi-${projectName}-${environmentName}-${suffix}'
var processingAppName = 'func-${projectName}-processing-${environmentName}-${suffix}'
var tags = {
  environment: environmentName
  project: projectName
  managedBy: 'bicep'
  observability: 'application-insights-processing-only'
}

module observability 'modules/observability.bicep' = {
  name: 'processing-observability-resources'
  params: {
    location: location
    logAnalyticsName: logAnalyticsName
    applicationInsightsName: applicationInsightsName
    dailyQuotaGb: dailyQuotaGb
    tags: tags
  }
}

module processingConnection 'modules/processing-observability-connection.bicep' = {
  name: 'processing-observability-connection'
  params: {
    applicationInsightsName: applicationInsightsName
    existingAppSettings: list('${resourceId('Microsoft.Web/sites', processingAppName)}/config/appsettings', '2024-04-01').properties
    processingAppName: processingAppName
  }
  dependsOn: [
    observability
  ]
}

output applicationInsightsName string = observability.outputs.applicationInsightsName
output logAnalyticsWorkspaceName string = observability.outputs.logAnalyticsWorkspaceName
output processingFunctionAppName string = processingAppName
