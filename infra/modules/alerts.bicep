// Production alerts, by email: the availability target's error budget, and 80% of capacity.

param environmentName string
param tags object
param location string = resourceGroup().location

@minLength(3)
param alertEmail string

param appInsightsId string
param cosmosAccountId string
param planId string

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-stratego-api-${environmentName}'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'stratego-api'
    enabled: true
    emailReceivers: [
      {
        name: 'owner'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

// 99.9% availability leaves 0.1% of a month's requests to fail. Spending 2% of that budget in an
// hour means failing 14.4 times faster than the budget allows; 5% in six hours, 6 times faster.
// Request telemetry is sampled, so failures and totals are counted by itemCount.
var budgetAlerts = [
  {
    name: 'fast'
    description: 'An hour spent over 2% of the month\'s error budget.'
    severity: 1
    windowSize: 'PT1H'
    evaluationFrequency: 'PT15M'
    failedShare: '0.0144'
  }
  {
    name: 'slow'
    description: 'Six hours spent over 5% of the month\'s error budget.'
    severity: 2
    windowSize: 'PT6H'
    evaluationFrequency: 'PT1H'
    failedShare: '0.006'
  }
]

resource errorBudget 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = [
  for alert in budgetAlerts: {
    name: 'error-budget-${alert.name}-${environmentName}'
    location: location
    tags: tags
    properties: {
      displayName: 'Error budget burning (${alert.name})'
      description: alert.description
      severity: alert.severity
      enabled: true
      scopes: [
        appInsightsId
      ]
      windowSize: alert.windowSize
      evaluationFrequency: alert.evaluationFrequency
      criteria: {
        allOf: [
          {
            query: 'requests | summarize total = sum(itemCount), failed = sumif(itemCount, toint(resultCode) >= 500) | where total > 0 | extend failedShare = todouble(failed) / total'
            timeAggregation: 'Maximum'
            metricMeasureColumn: 'failedShare'
            operator: 'GreaterThan'
            threshold: json(alert.failedShare)
            failingPeriods: {
              numberOfEvaluationPeriods: 1
              minFailingPeriodsToAlert: 1
            }
          }
        ]
      }
      autoMitigate: true
      actions: {
        actionGroups: [
          actionGroup.id
        ]
      }
    }
  }
]

var capacityAlerts = [
  {
    name: 'cosmos-throughput'
    description: 'Cosmos DB used over 80% of its throughput, on average, for an hour.'
    scope: cosmosAccountId
    metricNamespace: 'Microsoft.DocumentDB/databaseAccounts'
    metricName: 'NormalizedRUConsumption'
    windowSize: 'PT1H'
    evaluationFrequency: 'PT15M'
  }
  {
    name: 'plan-cpu'
    description: 'The App Service Plan used over 80% of its CPU, on average, for 15 minutes.'
    scope: planId
    metricNamespace: 'Microsoft.Web/serverfarms'
    metricName: 'CpuPercentage'
    windowSize: 'PT15M'
    evaluationFrequency: 'PT5M'
  }
  {
    name: 'plan-memory'
    description: 'The App Service Plan used over 80% of its memory, on average, for 15 minutes.'
    scope: planId
    metricNamespace: 'Microsoft.Web/serverfarms'
    metricName: 'MemoryPercentage'
    windowSize: 'PT15M'
    evaluationFrequency: 'PT5M'
  }
]

resource capacity 'Microsoft.Insights/metricAlerts@2018-03-01' = [
  for alert in capacityAlerts: {
    name: '${alert.name}-${environmentName}'
    location: 'global'
    tags: tags
    properties: {
      description: alert.description
      severity: 2
      enabled: true
      scopes: [
        alert.scope
      ]
      windowSize: alert.windowSize
      evaluationFrequency: alert.evaluationFrequency
      criteria: {
        'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
        allOf: [
          {
            criterionType: 'StaticThresholdCriterion'
            name: 'over-80-percent'
            metricNamespace: alert.metricNamespace
            metricName: alert.metricName
            operator: 'GreaterThan'
            threshold: 80
            timeAggregation: 'Average'
          }
        ]
      }
      autoMitigate: true
      actions: [
        {
          actionGroupId: actionGroup.id
        }
      ]
    }
  }
]
