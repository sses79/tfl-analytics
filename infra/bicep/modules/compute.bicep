param location string
param staticWebAppLocation string
param ingestionPlanName string
param ingestionAppName string
param ingestionIdentityName string
param processingPlanName string
param processingAppName string
param processingIdentityName string
param staticWebAppName string
param storageAccountName string
param ingestionDeploymentContainerName string
param processingDeploymentContainerName string
param applicationInsightsName string
param keyVaultName string
param tags object

param maximumFunctionInstanceCount int = 20
param functionInstanceMemoryMb int = 2048

var storageBlobDataOwnerRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
)
var storageQueueDataContributorRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
)
var storageTableDataContributorRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
)

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: applicationInsightsName
}

var ingestionDeploymentContainerUri = '${storageAccount.properties.primaryEndpoints.blob}${ingestionDeploymentContainerName}'
var processingDeploymentContainerUri = '${storageAccount.properties.primaryEndpoints.blob}${processingDeploymentContainerName}'
var applicationInsightsConnectionString = applicationInsights.properties.ConnectionString

resource ingestionIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: ingestionIdentityName
  location: location
  tags: tags
}

resource processingIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: processingIdentityName
  location: location
  tags: tags
}

resource ingestionBlobStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, ingestionIdentity.id, storageBlobDataOwnerRoleDefinitionId)
  scope: storageAccount
  properties: {
    principalId: ingestionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageBlobDataOwnerRoleDefinitionId
  }
}

resource ingestionQueueStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, ingestionIdentity.id, storageQueueDataContributorRoleDefinitionId)
  scope: storageAccount
  properties: {
    principalId: ingestionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageQueueDataContributorRoleDefinitionId
  }
}

resource ingestionTableStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, ingestionIdentity.id, storageTableDataContributorRoleDefinitionId)
  scope: storageAccount
  properties: {
    principalId: ingestionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageTableDataContributorRoleDefinitionId
  }
}

resource processingBlobStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, processingIdentity.id, storageBlobDataOwnerRoleDefinitionId)
  scope: storageAccount
  properties: {
    principalId: processingIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageBlobDataOwnerRoleDefinitionId
  }
}

resource processingQueueStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, processingIdentity.id, storageQueueDataContributorRoleDefinitionId)
  scope: storageAccount
  properties: {
    principalId: processingIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageQueueDataContributorRoleDefinitionId
  }
}

resource processingTableStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, processingIdentity.id, storageTableDataContributorRoleDefinitionId)
  scope: storageAccount
  properties: {
    principalId: processingIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageTableDataContributorRoleDefinitionId
  }
}

resource ingestionPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: ingestionPlanName
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource processingPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: processingPlanName
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource ingestionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: ingestionAppName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${ingestionIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: ingestionPlan.id
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: ingestionDeploymentContainerUri
          authentication: {
            type: 'UserAssignedIdentity'
            userAssignedIdentityResourceId: ingestionIdentity.id
          }
        }
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
      scaleAndConcurrency: {
        maximumInstanceCount: maximumFunctionInstanceCount
        instanceMemoryMB: functionInstanceMemoryMb
      }
    }
    siteConfig: {
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storageAccountName
        }
        {
          name: 'AzureWebJobsStorage__credential'
          value: 'managedidentity'
        }
        {
          name: 'AzureWebJobsStorage__clientId'
          value: ingestionIdentity.properties.clientId
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: ingestionIdentity.properties.clientId
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsConnectionString
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
          name: 'DD_ENV'
          value: 'dev'
        }
        {
          name: 'DD_SERVICE'
          value: 'tfl-analytics-ingestion'
        }
      ]
    }
  }
  dependsOn: [
    ingestionBlobStorageRole
    ingestionQueueStorageRole
    ingestionTableStorageRole
  ]
}

resource processingApp 'Microsoft.Web/sites@2024-04-01' = {
  name: processingAppName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${processingIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: processingPlan.id
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: processingDeploymentContainerUri
          authentication: {
            type: 'UserAssignedIdentity'
            userAssignedIdentityResourceId: processingIdentity.id
          }
        }
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
      scaleAndConcurrency: {
        maximumInstanceCount: maximumFunctionInstanceCount
        instanceMemoryMB: functionInstanceMemoryMb
      }
    }
    siteConfig: {
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storageAccountName
        }
        {
          name: 'AzureWebJobsStorage__credential'
          value: 'managedidentity'
        }
        {
          name: 'AzureWebJobsStorage__clientId'
          value: processingIdentity.properties.clientId
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: processingIdentity.properties.clientId
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsConnectionString
        }
        {
          name: 'KeyVault__Name'
          value: keyVaultName
        }
        {
          name: 'DD_ENV'
          value: 'dev'
        }
        {
          name: 'DD_SERVICE'
          value: 'tfl-analytics-processing'
        }
      ]
    }
  }
  dependsOn: [
    processingBlobStorageRole
    processingQueueStorageRole
    processingTableStorageRole
  ]
}

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: staticWebAppLocation
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    allowConfigFileUpdates: true
    enterpriseGradeCdnStatus: 'Disabled'
    stagingEnvironmentPolicy: 'Enabled'
  }
}

output ingestionAppName string = ingestionApp.name
output ingestionPrincipalId string = ingestionApp.identity.principalId
output ingestionIdentityName string = ingestionIdentity.name
output ingestionDeploymentIdentityPrincipalId string = ingestionIdentity.properties.principalId
output processingAppName string = processingApp.name
output processingPrincipalId string = processingApp.identity.principalId
output processingIdentityName string = processingIdentity.name
output processingDeploymentIdentityPrincipalId string = processingIdentity.properties.principalId
output staticWebAppName string = staticWebApp.name
output staticWebAppHostname string = staticWebApp.properties.defaultHostname
