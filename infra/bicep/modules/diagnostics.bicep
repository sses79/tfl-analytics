param logAnalyticsWorkspaceId string
param keyVaultName string
param eventHubsNamespaceName string
param cosmosAccountName string
param signalRName string
param sqlServerName string
param sqlDatabaseName string

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource eventHubsNamespace 'Microsoft.EventHub/namespaces@2024-01-01' existing = {
  name: eventHubsNamespaceName
}

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' existing = {
  name: cosmosAccountName
}

resource signalR 'Microsoft.SignalRService/signalR@2024-03-01' existing = {
  name: signalRName
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' existing = {
  name: sqlServerName
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01' existing = {
  parent: sqlServer
  name: sqlDatabaseName
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

resource eventHubsDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'operational'
  scope: eventHubsNamespace
  properties: {
    logAnalyticsDestinationType: 'Dedicated'
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'DiagnosticErrorLogs'
        enabled: true
      }
      {
        category: 'OperationalLogs'
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

resource sqlDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'operational'
  scope: sqlDatabase
  properties: {
    logAnalyticsDestinationType: 'Dedicated'
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'Errors'
        enabled: true
      }
      {
        category: 'Timeouts'
        enabled: true
      }
      {
        category: 'Deadlocks'
        enabled: true
      }
      {
        category: 'DevOpsOperationsAudit'
        enabled: true
      }
    ]
  }
}

output diagnosticSettingCount int = 5
