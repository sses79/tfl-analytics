targetScope = 'resourceGroup'

param environmentName string = 'dev'
param projectName string = 'tfl-analytics'

var suffix = take(uniqueString(subscription().id, resourceGroup().id), 8)
var processingIdentityName = 'id-${projectName}-processing-${environmentName}-${suffix}'
var signalRName = 'sigr-${projectName}-${environmentName}-${suffix}'
var signalRRestApiOwnerRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'fd53cd77-2268-407a-8f46-7e7863d0f521'
)

resource processingIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: processingIdentityName
}

resource signalR 'Microsoft.SignalRService/signalR@2024-03-01' existing = {
  name: signalRName
}

resource processingSignalRRestRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(signalR.id, processingIdentity.id, signalRRestApiOwnerRoleDefinitionId)
  scope: signalR
  properties: {
    principalId: processingIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: signalRRestApiOwnerRoleDefinitionId
  }
}

output processingPrincipalId string = processingIdentity.properties.principalId
output roleAssignmentId string = processingSignalRRestRole.id
