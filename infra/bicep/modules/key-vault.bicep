param location string
param name string
param tenantId string
param tags object

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    enableRbacAuthorization: true
    enableSoftDelete: true
    publicNetworkAccess: 'Enabled'
    sku: {
      family: 'A'
      name: 'standard'
    }
    softDeleteRetentionInDays: 7
    tenantId: tenantId
  }
}

output keyVaultName string = keyVault.name
output keyVaultId string = keyVault.id
output vaultUri string = keyVault.properties.vaultUri
