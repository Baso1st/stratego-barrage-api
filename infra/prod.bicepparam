using 'main.bicep'

param environmentName = 'prod'
param appServicePlanSku = 'P0v3'
param telemetryDailyCapGb = '0.11'
param alertEmail = readEnvironmentVariable('ALERT_EMAIL')
