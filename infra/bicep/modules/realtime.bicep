param location string
param name string
param dashboardOrigin string
param apiPrincipalId string
param processingPrincipalId string
param tags object

var signalRAppServerRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '420fcaa2-552c-430f-98ca-3264be4806c7'
)

resource signalR 'Microsoft.SignalRService/signalR@2024-03-01' = {
  name: name
  location: location
  tags: tags
  kind: 'SignalR'
  sku: {
    capacity: 1
    name: 'Free_F1'
    tier: 'Free'
  }
  properties: {
    cors: {
      allowedOrigins: [
        dashboardOrigin
      ]
    }
    disableAadAuth: false
    disableLocalAuth: true
    features: [
      {
        flag: 'ServiceMode'
        properties: {}
        value: 'Default'
      }
      {
        flag: 'EnableConnectivityLogs'
        properties: {}
        value: 'True'
      }
      {
        flag: 'EnableMessagingLogs'
        properties: {}
        value: 'False'
      }
    ]
    liveTraceConfiguration: {
      categories: [
        {
          enabled: 'false'
          name: 'ConnectivityLogs'
        }
        {
          enabled: 'false'
          name: 'MessagingLogs'
        }
        {
          enabled: 'false'
          name: 'HttpRequestLogs'
        }
      ]
      enabled: 'false'
    }
    publicNetworkAccess: 'Enabled'
    tls: {
      clientCertEnabled: false
    }
  }
}

resource apiSignalRRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(signalR.id, apiPrincipalId, signalRAppServerRoleDefinitionId)
  scope: signalR
  properties: {
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: signalRAppServerRoleDefinitionId
  }
}

resource processingSignalRRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(signalR.id, processingPrincipalId, signalRAppServerRoleDefinitionId)
  scope: signalR
  properties: {
    principalId: processingPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: signalRAppServerRoleDefinitionId
  }
}

output name string = signalR.name
output hostname string = signalR.properties.hostName
