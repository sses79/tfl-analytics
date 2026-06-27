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
param enableSql bool = false

@description('Deploy Log Analytics workspace and Application Insights. Disabled by default to avoid AppTraces ingestion cost.')
param enableObservability bool = false
param apiImageTag string = 'dev'
param sqlLocation string = 'centralus'
param apiIdentityPrincipalId string = ''

@description('Optional custom domain aliasing the Static Web App (e.g. demo.example.com), added to every CORS allowlist alongside the generated hostname.')
param dashboardCustomDomain string = ''

var suffix = take(uniqueString(subscription().id, resourceGroup().id), 8)
var storageAccountName = 'sttfl${suffix}'
var keyVaultName = 'kv-tfl-${suffix}'
var logAnalyticsName = 'log-${projectName}-${environmentName}-${suffix}'
var applicationInsightsName = 'appi-${projectName}-${environmentName}-${suffix}'
var cosmosAccountName = 'cosmos-${projectName}-${environmentName}-${suffix}'
var cosmosDatabaseName = 'tfl-analytics'
var sqlServerName = 'sql-${projectName}-${environmentName}-${suffix}'
var sqlDatabaseName = 'tfl-analytics'
var signalRName = 'sigr-${projectName}-${environmentName}-${suffix}'
var signalRHostname = '${signalRName}.service.signalr.net'
var cosmosEndpoint = 'https://${cosmosAccountName}.documents.azure.com:443/'
var sqlServerFqdn = '${sqlServerName}${environment().suffixes.sqlServerHostname}'
var apiIdentityName = 'id-${projectName}-api-${environmentName}-${suffix}'
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

module observability 'modules/observability.bicep' = if (enableObservability) {
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
    applicationInsightsName: observability.?outputs.applicationInsightsName ?? ''
    keyVaultName: keyVaultName
    cosmosAccountName: cosmosAccountName
    cosmosDatabaseName: cosmosDatabaseName
    cosmosLiveEventsContainerName: 'live-events'
    cosmosLineStatusContainerName: 'line-status'
    cosmosRawEventsContainerName: 'raw-events'
    cosmosLeasesContainerName: 'leases'
    sqlServerFqdn: sqlServerFqdn
    sqlDatabaseName: sqlDatabaseName
    apiIdentityName: apiIdentityName
    apiIdentityPrincipalId: apiIdentityPrincipalId
    dashboardCustomDomain: dashboardCustomDomain
    tags: commonTags
  }
  dependsOn: [
    storage
    keyVault
  ]
}

var dashboardOrigins = concat(
  ['https://${compute.outputs.staticWebAppHostname}'],
  empty(dashboardCustomDomain) ? [] : ['https://${dashboardCustomDomain}']
)

module apiHosting 'modules/api-hosting.bicep' = {
  name: 'api-hosting'
  params: {
    location: location
    registryName: 'acrtfl${suffix}'
    environmentName: 'cae-${projectName}-${environmentName}-${suffix}'
    apiAppName: 'ca-tfl-api-${environmentName}-${suffix}'
    apiIdentityName: 'id-${projectName}-api-${environmentName}-${suffix}'
    applicationInsightsName: observability.?outputs.applicationInsightsName ?? ''
    keyVaultName: keyVaultName
    dashboardOrigins: dashboardOrigins
    signalRHostname: signalRHostname
    cosmosEndpoint: cosmosEndpoint
    storageAccountName: storageAccountName
    sqlServerFqdn: sqlServerFqdn
    deployApiContainer: deployApiContainer
    apiImageTag: apiImageTag
    tags: commonTags
  }
  dependsOn: [
    keyVault
  ]
}

module cosmos 'modules/cosmos.bicep' = {
  name: 'cosmos'
  params: {
    location: location
    accountName: cosmosAccountName
    databaseName: cosmosDatabaseName
    apiPrincipalId: apiHosting.outputs.apiPrincipalId
    ingestionPrincipalId: compute.outputs.ingestionDeploymentIdentityPrincipalId
    processingPrincipalId: compute.outputs.processingDeploymentIdentityPrincipalId
    tags: commonTags
  }
}

module sql 'modules/sql.bicep' = if (enableSql) {
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
    name: signalRName
    dashboardOrigins: dashboardOrigins
    apiPrincipalId: apiHosting.outputs.apiPrincipalId
    processingPrincipalId: compute.outputs.processingDeploymentIdentityPrincipalId
    tags: commonTags
  }
}

module workloadRbac 'modules/workload-rbac.bicep' = {
  name: 'workload-rbac'
  params: {
    keyVaultName: keyVault.outputs.keyVaultName
    apiPrincipalId: apiHosting.outputs.apiPrincipalId
    ingestionPrincipalId: compute.outputs.ingestionDeploymentIdentityPrincipalId
    processingPrincipalId: compute.outputs.processingDeploymentIdentityPrincipalId
  }
}

module diagnostics 'modules/diagnostics.bicep' = if (enableObservability) {
  name: 'diagnostics'
  params: {
    logAnalyticsWorkspaceId: observability.?outputs.logAnalyticsWorkspaceId ?? ''
    keyVaultName: keyVault.outputs.keyVaultName
    cosmosAccountName: cosmos.outputs.accountName
    signalRName: realtime.outputs.name
    sqlServerName: sql.?outputs.serverName ?? ''
    sqlDatabaseName: sql.?outputs.databaseName ?? ''
    enableSqlDiagnostics: enableSql
  }
}

output storageAccountName string = storage.outputs.storageAccountName
output keyVaultName string = keyVault.outputs.keyVaultName
output eventHubsNamespaceName string = ''
output eventHubName string = ''
output applicationInsightsName string = observability.?outputs.applicationInsightsName ?? ''
output logAnalyticsWorkspaceName string = observability.?outputs.logAnalyticsWorkspaceName ?? ''
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
output cosmosRawEventsContainerName string = cosmos.outputs.rawEventsContainerName
output cosmosLeasesContainerName string = cosmos.outputs.leasesContainerName
output sqlServerName string = sql.?outputs.serverName ?? ''
output sqlServerFqdn string = sql.?outputs.serverFqdn ?? ''
output sqlDatabaseName string = sql.?outputs.databaseName ?? ''
output signalRName string = realtime.outputs.name
output signalRHostname string = realtime.outputs.hostname
