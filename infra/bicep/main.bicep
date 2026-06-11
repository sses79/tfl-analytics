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

var suffix = take(uniqueString(subscription().id, resourceGroup().id), 8)
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
    name: 'sttfl${suffix}'
    tags: commonTags
  }
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'key-vault'
  params: {
    location: location
    name: 'kv-tfl-${suffix}'
    tenantId: subscription().tenantId
    tags: commonTags
  }
}

module observability 'modules/observability.bicep' = {
  name: 'observability'
  params: {
    location: location
    logAnalyticsName: 'log-${projectName}-${environmentName}-${suffix}'
    applicationInsightsName: 'appi-${projectName}-${environmentName}-${suffix}'
    tags: commonTags
  }
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
