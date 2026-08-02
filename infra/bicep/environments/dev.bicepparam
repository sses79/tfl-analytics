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
param apiImageTag = '436bac8e3238510e4d8842dbee4251eb0ae3569e'
param dashboardCustomDomain = 'demo.ti5g.com'
