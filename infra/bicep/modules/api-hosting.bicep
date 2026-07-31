param location string
param environmentName string
param apiAppName string
param apiIdentityName string
param applicationInsightsName string = ''
param keyVaultName string
param dashboardOrigins array
param signalRHostname string
param cosmosEndpoint string
param storageAccountName string
param tags object

param deployApiContainer bool = false
param apiImageTag string = 'latest'
param apiImageRepository string = 'ghcr.io/sses79/tfl-analytics-api'

var storageTableDataReaderRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '76199698-9eea-4c19-bc75-cec21354c6b6'
)

var observabilityEnabled = !empty(applicationInsightsName)

var corsOriginSettings = [for (origin, i) in dashboardOrigins: {
  name: 'Cors__AllowedOrigins__${i}'
  value: origin
}]

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = if (observabilityEnabled) {
  name: observabilityEnabled ? applicationInsightsName : 'unused'
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' existing = {
  parent: storageAccount
  name: 'default'
}

resource alertsTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' existing = {
  parent: tableService
  name: 'alerts'
}

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: apiIdentityName
  location: location
  tags: tags
}

resource apiTableStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(alertsTable.id, apiIdentity.id, storageTableDataReaderRoleDefinitionId)
  scope: alertsTable
  properties: {
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageTableDataReaderRoleDefinitionId
  }
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: observabilityEnabled ? {
      destination: 'azure-monitor'
    } : null
    zoneRedundant: false
  }
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = if (deployApiContainer) {
  name: apiAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        allowInsecure: false
        external: true
        targetPort: 8080
        transport: 'auto'
      }
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${apiImageRepository}:${apiImageTag}'
          env: concat([
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
          ], corsOriginSettings, [
            {
              name: 'AZURE_CLIENT_ID'
              value: apiIdentity.properties.clientId
            }
            {
              name: 'KeyVault__Name'
              value: keyVaultName
            }
            {
              name: 'TflApi__BaseUrl'
              value: 'https://api.tfl.gov.uk/'
            }
            {
              // ClientId selects the user-assigned identity when multiple are present.
              name: 'SignalR__ConnectionString'
              value: 'Endpoint=https://${signalRHostname};AuthType=aad;ClientId=${apiIdentity.properties.clientId};Version=1.0;'
            }
            {
              name: 'SignalR__Endpoint'
              value: 'https://${signalRHostname}'
            }
            {
              name: 'Ingestion__StationIds__0'
              value: '940GZZLUVIC'
            }
            {
              name: 'Ingestion__StationIds__1'
              value: '940GZZLUOXC'
            }
            {
              name: 'Ingestion__StationIds__2'
              value: '940GZZLUGPK'
            }
            {
              name: 'Ingestion__StationIds__3'
              value: '940GZZLUKSX'
            }
            {
              name: 'Ingestion__StationIds__4'
              value: '940GZZLULNB'
            }
            {
              name: 'Cosmos__Endpoint'
              value: cosmosEndpoint
            }
            {
              name: 'ProcessingStorage__AccountName'
              value: storageAccountName
            }
            {
              name: 'ProcessingStorage__AlertsTableName'
              value: 'alerts'
            }
            {
              name: 'DD_ENV'
              value: 'dev'
            }
            {
              name: 'DD_SERVICE'
              value: 'tfl-analytics-api'
            }
          ], observabilityEnabled ? [
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: applicationInsights.?properties.ConnectionString ?? ''
            }
          ] : [])
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 10
              periodSeconds: 30
              timeoutSeconds: 5
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 2
        rules: [
          {
            name: 'http'
            http: {
              metadata: {
                concurrentRequests: '20'
              }
            }
          }
        ]
      }
    }
  }
}

output containerAppsEnvironmentName string = environment.name
output apiContainerAppName string = apiApp.?name ?? ''
output apiContainerAppFqdn string = apiApp.?properties.configuration.ingress.fqdn ?? ''
output apiIdentityName string = apiIdentity.name
output apiPrincipalId string = apiIdentity.properties.principalId
