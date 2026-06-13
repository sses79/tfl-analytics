targetScope = 'resourceGroup'

@description('Azure region used by regional resources.')
param location string = resourceGroup().location

@allowed([
  'dev'
  'test'
  'prod'
])
param environmentName string = 'dev'

param projectName string = 'tfl-analytics'

param deployApiContainer bool = false
param apiImageTag string = 'dev'

var suffix = take(uniqueString(subscription().id, resourceGroup().id), 8)
var storageAccountName = 'sttfl${suffix}'
var keyVaultName = 'kv-tfl-${suffix}'
var logAnalyticsName = 'log-${projectName}-${environmentName}-${suffix}'
var applicationInsightsName = 'appi-${projectName}-${environmentName}-${suffix}'
var commonTags = {
  environment: environmentName
  project: projectName
  managedBy: 'bicep'
  observability: 'datadog'
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    name: storageAccountName
    tags: commonTags
  }
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'key-vault'
  params: {
    location: location
    name: keyVaultName
    tenantId: subscription().tenantId
    tags: commonTags
  }
}

module observability 'modules/observability.bicep' = {
  name: 'observability'
  params: {
    location: location
    logAnalyticsName: logAnalyticsName
    applicationInsightsName: applicationInsightsName
    tags: commonTags
  }
}

module compute 'modules/compute.bicep' = {
  name: 'compute'
  params: {
    location: location
    staticWebAppLocation: 'westeurope'
    ingestionPlanName: 'fc-${projectName}-ingestion-${environmentName}-${suffix}'
    ingestionAppName: 'func-${projectName}-ingestion-${environmentName}-${suffix}'
    ingestionIdentityName: 'id-${projectName}-ingestion-${environmentName}-${suffix}'
    processingPlanName: 'fc-${projectName}-processing-${environmentName}-${suffix}'
    processingAppName: 'func-${projectName}-processing-${environmentName}-${suffix}'
    processingIdentityName: 'id-${projectName}-processing-${environmentName}-${suffix}'
    staticWebAppName: 'swa-${projectName}-${environmentName}-${suffix}'
    storageAccountName: storageAccountName
    ingestionDeploymentContainerName: 'function-ingestion-deployments'
    processingDeploymentContainerName: 'function-processing-deployments'
    applicationInsightsName: applicationInsightsName
    keyVaultName: keyVaultName
    tags: commonTags
  }
  dependsOn: [
    storage
    keyVault
    observability
  ]
}

module apiHosting 'modules/api-hosting.bicep' = {
  name: 'api-hosting'
  params: {
    location: location
    registryName: 'acrtfl${suffix}'
    environmentName: 'cae-${projectName}-${environmentName}-${suffix}'
    apiAppName: 'ca-tfl-api-${environmentName}-${suffix}'
    apiIdentityName: 'id-${projectName}-api-${environmentName}-${suffix}'
    applicationInsightsName: applicationInsightsName
    keyVaultName: keyVaultName
    deployApiContainer: deployApiContainer
    apiImageTag: apiImageTag
    tags: commonTags
  }
  dependsOn: [
    keyVault
    observability
  ]
}

module messaging 'modules/messaging.bicep' = {
  name: 'messaging'
  params: {
    location: location
    namespaceName: 'evhns-${projectName}-${environmentName}-${suffix}'
    eventHubName: 'tfl-events'
    tags: commonTags
  }
}

output storageAccountName string = storage.outputs.storageAccountName
output keyVaultName string = keyVault.outputs.keyVaultName
output eventHubsNamespaceName string = messaging.outputs.namespaceName
output eventHubName string = messaging.outputs.eventHubName
output applicationInsightsName string = observability.outputs.applicationInsightsName
output logAnalyticsWorkspaceName string = observability.outputs.logAnalyticsWorkspaceName
output containerRegistryName string = apiHosting.outputs.registryName
output containerRegistryLoginServer string = apiHosting.outputs.registryLoginServer
output containerAppsEnvironmentName string = apiHosting.outputs.containerAppsEnvironmentName
output apiAppName string = apiHosting.outputs.apiContainerAppName
output apiAppHostname string = apiHosting.outputs.apiContainerAppFqdn
output ingestionFunctionAppName string = compute.outputs.ingestionAppName
output processingFunctionAppName string = compute.outputs.processingAppName
output staticWebAppName string = compute.outputs.staticWebAppName
output staticWebAppHostname string = compute.outputs.staticWebAppHostname
