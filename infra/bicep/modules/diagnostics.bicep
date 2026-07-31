param logAnalyticsWorkspaceId string
param keyVaultName string
param cosmosAccountName string
param signalRName string

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' existing = {
  name: cosmosAccountName
}

resource signalR 'Microsoft.SignalRService/signalR@2024-03-01' existing = {
  name: signalRName
}

resource keyVaultDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'operational'
  scope: keyVault
  properties: {
    logAnalyticsDestinationType: 'Dedicated'
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AuditEvent'
        enabled: true
      }
    ]
  }
}

resource cosmosDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'operational'
  scope: cosmosAccount
  properties: {
    logAnalyticsDestinationType: 'Dedicated'
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'ControlPlaneRequests'
        enabled: true
      }
    ]
  }
}

resource signalRDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'operational'
  scope: signalR
  properties: {
    logAnalyticsDestinationType: 'Dedicated'
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AllLogs'
        enabled: true
      }
    ]
  }
}

output diagnosticSettingCount int = 3
