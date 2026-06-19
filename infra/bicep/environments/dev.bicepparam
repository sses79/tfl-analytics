using '../main.bicep'

param location = 'uksouth'
param environmentName = 'dev'
param projectName = 'tfl-analytics'
param deployApiContainer = true
param apiImageTag = 'dev-20260619133429'
param sqlLocation = 'centralus'
param apiIdentityPrincipalId = 'bc35b612-691a-463f-b91e-ba32f2f2a7f8'
param dashboardCustomDomain = 'demo.ti5g.com'
