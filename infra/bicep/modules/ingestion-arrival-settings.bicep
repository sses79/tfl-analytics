param ingestionAppName string
param enableArrivalIngestion bool

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
      Arrival__Enabled: string(enableArrivalIngestion)
    }
  )
}
