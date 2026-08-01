param ingestionAppName string
param waterlooCityLineId string

@secure()
param existingAppSettings object

resource ingestionApp 'Microsoft.Web/sites@2024-04-01' existing = {
  name: ingestionAppName
}

resource ingestionAppSettings 'Microsoft.Web/sites/config@2024-04-01' = {
  parent: ingestionApp
  name: 'appsettings'
  properties: union(
    existingAppSettings,
    {
      Ingestion__LineIds__10: waterlooCityLineId
    }
  )
}
