targetScope = 'resourceGroup'

param environmentName string = 'dev'
param projectName string = 'tfl-analytics'
@description('Retention in seconds for processed arrival and line-status observations.')
param processedEventsTtlSeconds int = 86400

var suffix = take(uniqueString(subscription().id, resourceGroup().id), 8)
var cosmosAccountName = 'cosmos-${projectName}-${environmentName}-${suffix}'

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' existing = {
  name: cosmosAccountName
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' existing = {
  parent: cosmosAccount
  name: 'tfl-analytics'
}

resource liveEvents 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'live-events'
  properties: {
    options: {}
    resource: {
      conflictResolutionPolicy: {
        conflictResolutionPath: '/_ts'
        mode: 'LastWriterWins'
      }
      defaultTtl: processedEventsTtlSeconds
      id: 'live-events'
      indexingPolicy: {
        automatic: true
        indexingMode: 'consistent'
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
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
      conflictResolutionPolicy: {
        conflictResolutionPath: '/_ts'
        mode: 'LastWriterWins'
      }
      defaultTtl: processedEventsTtlSeconds
      id: 'line-status'
      indexingPolicy: {
        automatic: true
        indexingMode: 'consistent'
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
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

output cosmosAccountName string = cosmosAccount.name
output processedEventsTtlSeconds int = processedEventsTtlSeconds
