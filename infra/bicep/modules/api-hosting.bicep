param location string
param registryName string
param environmentName string
param apiAppName string
param apiIdentityName string
param applicationInsightsName string = ''
param keyVaultName string
param dashboardOrigins array
param signalRHostname string
param cosmosEndpoint string
param storageAccountName string
param sqlServerFqdn string
param tags object

param deployApiContainer bool = false
param apiImageTag string = 'dev'

var acrPullRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d'
)

var observabilityEnabled = !empty(applicationInsightsName)

var corsOriginSettings = [for (origin, i) in dashboardOrigins: {
  name: 'Cors__AllowedOrigins__${i}'
  value: origin
}]

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = if (observabilityEnabled) {
  name: observabilityEnabled ? applicationInsightsName : 'unused'
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    dataEndpointEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: apiIdentityName
  location: location
  tags: tags
}

resource registryPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, apiIdentity.id, acrPullRoleDefinitionId)
  scope: registry
  properties: {
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: acrPullRoleDefinitionId
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
      registries: [
        {
          identity: apiIdentity.id
          server: registry.properties.loginServer
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${registry.properties.loginServer}/tfl-analytics-api:${apiImageTag}'
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
              name: 'AlertStorage__ServerFqdn'
              value: sqlServerFqdn
            }
            {
              name: 'AlertStorage__Initialize'
              value: 'false'
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
  dependsOn: [
    registryPullRole
  ]
}

output registryName string = registry.name
output registryLoginServer string = registry.properties.loginServer
output containerAppsEnvironmentName string = environment.name
output apiContainerAppName string = apiApp.?name ?? ''
output apiContainerAppFqdn string = apiApp.?properties.configuration.ingress.fqdn ?? ''
output apiIdentityName string = apiIdentity.name
output apiPrincipalId string = apiIdentity.properties.principalId
