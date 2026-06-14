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
param sqlLocation string = 'centralus'

var suffix = take(uniqueString(subscription().id, resourceGroup().id), 8)
var storageAccountName = 'sttfl${suffix}'
var keyVaultName = 'kv-tfl-${suffix}'
var logAnalyticsName = 'log-${projectName}-${environmentName}-${suffix}'
var applicationInsightsName = 'appi-${projectName}-${environmentName}-${suffix}'
var cosmosAccountName = 'cosmos-${projectName}-${environmentName}-${suffix}'
var cosmosDatabaseName = 'tfl-analytics'
var sqlServerName = 'sql-${projectName}-${environmentName}-${suffix}'
var sqlDatabaseName = 'tfl-analytics'
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
    eventHubsNamespaceName: messaging.outputs.namespaceName
    eventHubName: messaging.outputs.eventHubName
    cosmosAccountName: cosmosAccountName
    cosmosDatabaseName: cosmosDatabaseName
    cosmosLiveEventsContainerName: 'live-events'
    cosmosLineStatusContainerName: 'line-status'
    sqlServerFqdn: '${sqlServerName}${environment().suffixes.sqlServerHostname}'
    sqlDatabaseName: sqlDatabaseName
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
    dashboardOrigin: 'https://${compute.outputs.staticWebAppHostname}'
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

module cosmos 'modules/cosmos.bicep' = {
  name: 'cosmos'
  params: {
    location: location
    accountName: cosmosAccountName
    databaseName: cosmosDatabaseName
    apiPrincipalId: apiHosting.outputs.apiPrincipalId
    processingPrincipalId: compute.outputs.processingDeploymentIdentityPrincipalId
    tags: commonTags
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    location: sqlLocation
    serverName: sqlServerName
    databaseName: sqlDatabaseName
    administratorLogin: compute.outputs.processingIdentityName
    administratorObjectId: compute.outputs.processingDeploymentIdentityPrincipalId
    tenantId: subscription().tenantId
    tags: commonTags
  }
}

module realtime 'modules/realtime.bicep' = {
  name: 'realtime'
  params: {
    location: location
    name: 'sigr-${projectName}-${environmentName}-${suffix}'
    dashboardOrigin: 'https://${compute.outputs.staticWebAppHostname}'
    apiPrincipalId: apiHosting.outputs.apiPrincipalId
    processingPrincipalId: compute.outputs.processingDeploymentIdentityPrincipalId
    tags: commonTags
  }
}

module workloadRbac 'modules/workload-rbac.bicep' = {
  name: 'workload-rbac'
  params: {
    eventHubsNamespaceName: messaging.outputs.namespaceName
    eventHubName: messaging.outputs.eventHubName
    keyVaultName: keyVault.outputs.keyVaultName
    apiPrincipalId: apiHosting.outputs.apiPrincipalId
    ingestionPrincipalId: compute.outputs.ingestionDeploymentIdentityPrincipalId
    processingPrincipalId: compute.outputs.processingDeploymentIdentityPrincipalId
  }
}

module diagnostics 'modules/diagnostics.bicep' = {
  name: 'diagnostics'
  params: {
    logAnalyticsWorkspaceId: observability.outputs.logAnalyticsWorkspaceId
    keyVaultName: keyVault.outputs.keyVaultName
    eventHubsNamespaceName: messaging.outputs.namespaceName
    cosmosAccountName: cosmos.outputs.accountName
    signalRName: realtime.outputs.name
    sqlServerName: sql.outputs.serverName
    sqlDatabaseName: sql.outputs.databaseName
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
output apiIdentityName string = apiHosting.outputs.apiIdentityName
output ingestionFunctionAppName string = compute.outputs.ingestionAppName
output ingestionIdentityName string = compute.outputs.ingestionIdentityName
output processingFunctionAppName string = compute.outputs.processingAppName
output processingIdentityName string = compute.outputs.processingIdentityName
output staticWebAppName string = compute.outputs.staticWebAppName
output staticWebAppHostname string = compute.outputs.staticWebAppHostname
output cosmosAccountName string = cosmos.outputs.accountName
output cosmosEndpoint string = cosmos.outputs.accountEndpoint
output cosmosDatabaseName string = cosmos.outputs.databaseName
output cosmosLiveEventsContainerName string = cosmos.outputs.liveEventsContainerName
output cosmosLineStatusContainerName string = cosmos.outputs.lineStatusContainerName
output sqlServerName string = sql.outputs.serverName
output sqlServerFqdn string = sql.outputs.serverFqdn
output sqlDatabaseName string = sql.outputs.databaseName
output signalRName string = realtime.outputs.name
output signalRHostname string = realtime.outputs.hostname
