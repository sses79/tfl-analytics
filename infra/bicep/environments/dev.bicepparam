using '../main.bicep'

param location = 'uksouth'
param environmentName = 'dev'
param projectName = 'tfl-analytics'
param deployApiContainer = true
param enableProcessingObservability = true
param observabilityDailyQuotaGb = '0.1'
param enableArrivalIngestion = true
param enableAlerts = false
param ingestionArrivalsSchedule = '0 * * * * *'
param ingestionLineStatusSchedule = '0 */10 * * * *'
param processedEventsTtlSeconds = 86400
param apiImageTag = '2ca20127e55e196d37b421d5422c0071939df13d'
param dashboardCustomDomain = 'demo.ti5g.com'
