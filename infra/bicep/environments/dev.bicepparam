using '../main.bicep'

param location = 'uksouth'
param environmentName = 'dev'
param projectName = 'tfl-analytics'
param deployApiContainer = true
param enableProcessingObservability = true
param observabilityDailyQuotaGb = '0.1'
param enableArrivalIngestion = false
param enableAlerts = false
param ingestionArrivalsSchedule = '0 */5 * * * *'
param ingestionLineStatusSchedule = '0 */10 * * * *'
param apiImageTag = 'd4b7caeb97de686899e0810ff7e3f5551878e649'
param dashboardCustomDomain = 'demo.ti5g.com'
