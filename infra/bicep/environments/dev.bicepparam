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
param apiImageTag = '290b8c9b3656c606ee9a14aa578591dbadb86585'
param dashboardCustomDomain = 'demo.ti5g.com'
