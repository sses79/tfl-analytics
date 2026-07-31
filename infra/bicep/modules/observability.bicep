param location string
param logAnalyticsName string
param applicationInsightsName string
param dailyQuotaGb string = '0.1'
param tags object

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: json(dailyQuotaGb)
    }
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    RetentionInDays: 30
    WorkspaceResourceId: logAnalytics.id
  }
}

output logAnalyticsWorkspaceName string = logAnalytics.name
output logAnalyticsWorkspaceId string = logAnalytics.id
output applicationInsightsName string = applicationInsights.name
output applicationInsightsId string = applicationInsights.id
