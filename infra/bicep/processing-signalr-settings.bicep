targetScope = 'resourceGroup'

param environmentName string = 'dev'
param projectName string = 'tfl-analytics'

var suffix = take(uniqueString(subscription().id, resourceGroup().id), 8)
var processingAppName = 'func-${projectName}-processing-${environmentName}-${suffix}'
var signalRName = 'sigr-${projectName}-${environmentName}-${suffix}'

resource signalR 'Microsoft.SignalRService/signalR@2024-03-01' existing = {
  name: signalRName
}

module processingSignalRSettings 'modules/processing-signalr-settings.bicep' = {
  name: 'processing-signalr-settings'
  params: {
    existingAppSettings: list('${resourceId('Microsoft.Web/sites', processingAppName)}/config/appsettings', '2024-04-01').properties
    processingAppName: processingAppName
    signalRHostname: signalR.properties.hostName
  }
}

output processingFunctionAppName string = processingAppName
output signalRHostname string = signalR.properties.hostName
