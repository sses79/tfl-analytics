param location string
param accountName string
param databaseName string
param apiPrincipalId string
param ingestionPrincipalId string
param processingPrincipalId string
param tags object

param databaseThroughput int = 1000
param defaultTtlSeconds int = 604800
param rawEventsTtlSeconds int = 14400

var cosmosDataContributorRoleDefinitionId = '${cosmosAccount.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: accountName
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    databaseAccountOfferType: 'Standard'
    disableKeyBasedMetadataWriteAccess: true
    enableAutomaticFailover: false
    enableFreeTier: true
    locations: [
      {
        failoverPriority: 0
        isZoneRedundant: false
        locationName: location
      }
    ]
    minimalTlsVersion: 'Tls12'
    publicNetworkAccess: 'Enabled'
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    options: {
      throughput: databaseThroughput
    }
    resource: {
      id: databaseName
    }
  }
}

resource liveEvents 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'live-events'
  properties: {
    options: {}
    resource: {
      defaultTtl: defaultTtlSeconds
      id: 'live-events'
      indexingPolicy: {
        automatic: true
        indexingMode: 'consistent'
        includedPaths: [
          {
            path: '/*'
          }
        ]
      }
      partitionKey: {
        kind: 'Hash'
        paths: [
          '/stationId'
        ]
        version: 2
      }
    }
  }
}

resource lineStatus 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'line-status'
  properties: {
    options: {}
    resource: {
      defaultTtl: defaultTtlSeconds
      id: 'line-status'
      indexingPolicy: {
        automatic: true
        indexingMode: 'consistent'
        includedPaths: [
          {
            path: '/*'
          }
        ]
      }
      partitionKey: {
        kind: 'Hash'
        paths: [
          '/lineId'
        ]
        version: 2
      }
    }
  }
}

resource rawEvents 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'raw-events'
  properties: {
    options: {}
    resource: {
      defaultTtl: rawEventsTtlSeconds
      id: 'raw-events'
      indexingPolicy: {
        automatic: true
        indexingMode: 'consistent'
        includedPaths: [
          {
            path: '/*'
          }
        ]
      }
      partitionKey: {
        kind: 'Hash'
        paths: [
          '/partitionKey'
        ]
        version: 2
      }
    }
  }
}

resource leases 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'leases'
  properties: {
    options: {}
    resource: {
      id: 'leases'
      partitionKey: {
        kind: 'Hash'
        paths: [
          '/id'
        ]
        version: 2
      }
    }
  }
}

resource apiDataRole 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, apiPrincipalId, cosmosDataContributorRoleDefinitionId)
  properties: {
    principalId: apiPrincipalId
    roleDefinitionId: cosmosDataContributorRoleDefinitionId
    scope: cosmosAccount.id
  }
}

resource ingestionDataRole 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, ingestionPrincipalId, cosmosDataContributorRoleDefinitionId)
  properties: {
    principalId: ingestionPrincipalId
    roleDefinitionId: cosmosDataContributorRoleDefinitionId
    scope: cosmosAccount.id
  }
}

resource processingDataRole 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, processingPrincipalId, cosmosDataContributorRoleDefinitionId)
  properties: {
    principalId: processingPrincipalId
    roleDefinitionId: cosmosDataContributorRoleDefinitionId
    scope: cosmosAccount.id
  }
}

output accountName string = cosmosAccount.name
output accountEndpoint string = cosmosAccount.properties.documentEndpoint
output databaseName string = database.name
output liveEventsContainerName string = liveEvents.name
output lineStatusContainerName string = lineStatus.name
output rawEventsContainerName string = rawEvents.name
output leasesContainerName string = leases.name
