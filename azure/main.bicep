param env string = 'dev'
param location string = 'germanywestcentral'

module webServer 'wits-server.bicep' = {
  name: 'wits-server-${env}'
  params: {
    webAppName: 'wits-server-${env}'
    location: location
  }
}
