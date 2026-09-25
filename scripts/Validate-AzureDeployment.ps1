param(
    [string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = 'Stop'
$expectedSkus = @('B1', 'S1', 'P0v3', 'P1v3')
$expectedDeploymentModes = @('publicDefault', 'existingKeyVaultPrivate', 'fullPrivate')
$expectedApiControls = @{
    b1Locations = 'sku=Basic'
    s1Locations = 'sku=Standard'
    p0v3Locations = 'sku=Premium0V3'
    p1v3Locations = 'sku=PremiumV3'
}

foreach ($environment in @('dev', 'stg', 'prd')) {
    $deploymentDirectory = Join-Path $RepositoryRoot "outthink-emailinjector-app/AzureDeployment-$environment"
    $templatePath = Join-Path $deploymentDirectory 'mainTemplate.json'
    $uiPath = Join-Path $deploymentDirectory 'createUiDefinition.json'
    $metadataPath = Join-Path $deploymentDirectory 'metadata.json'

    $template = Get-Content $templatePath -Raw | ConvertFrom-Json
    $ui = Get-Content $uiPath -Raw | ConvertFrom-Json
    $metadata = Get-Content $metadataPath -Raw | ConvertFrom-Json

    $actualDeploymentModes = @($template.parameters.deploymentMode.allowedValues)
    if (($actualDeploymentModes -join ',') -ne ($expectedDeploymentModes -join ',')) {
        throw "$environment template deployment modes are '$($actualDeploymentModes -join ',')'."
    }

    if ($template.parameters.deploymentMode.defaultValue -ne 'publicDefault') {
        throw "$environment template must default to publicDefault deployment mode."
    }

    foreach ($parameterName in @('keyVaultName', 'existingKeyVaultResourceId', 'existingKeyVaultUrl', 'newVnetName', 'natGatewayName', 'natPublicIpName')) {
        if ($null -eq $template.parameters.$parameterName) {
            throw "$environment template is missing the $parameterName parameter."
        }
    }

    $actualSkus = @($template.parameters.sku.allowedValues)
    if (($actualSkus -join ',') -ne ($expectedSkus -join ',')) {
        throw "$environment template SKU values are '$($actualSkus -join ',')'."
    }

    if ($template.parameters.sku.defaultValue -ne 'P0v3') {
        throw "$environment template must default direct deployments to P0v3."
    }

    $serverFarm = @($template.resources | Where-Object type -eq 'Microsoft.Web/serverfarms')[0]
    if ($serverFarm.apiVersion -ne '2025-03-01' -or $serverFarm.sku.name -ne "[parameters('sku')]") {
        throw "$environment App Service Plan API version or SKU binding is invalid."
    }

    if ($serverFarm.sku.PSObject.Properties.Name -contains 'tier') {
        throw "$environment App Service Plan must let Azure derive sku.tier."
    }

    $webResources = @($template.resources | Where-Object type -like 'Microsoft.Web/*')
    if (@($webResources | Where-Object apiVersion -ne '2025-03-01').Count -gt 0) {
        throw "$environment contains a stale top-level Microsoft.Web API version."
    }

    $apiControls = @($ui.parameters.basics | Where-Object type -eq 'Microsoft.Solutions.ArmApiControl')
    if ($apiControls.Count -ne $expectedApiControls.Count) {
        throw "$environment UI must contain four SKU availability controls."
    }

    foreach ($controlName in $expectedApiControls.Keys) {
        $control = @($apiControls | Where-Object name -eq $controlName)[0]
        if ($null -eq $control -or $control.request.path -notlike "*$($expectedApiControls[$controlName])*" -or
            $control.request.path -notlike '*linuxWorkersEnabled=true*') {
            throw "$environment UI control '$controlName' has an invalid availability request."
        }
    }

    $skuControl = @($ui.parameters.basics | Where-Object name -eq 'sku')[0]
    if ($skuControl.PSObject.Properties.Name -contains 'defaultValue') {
        throw "$environment SKU dropdown must select its first available value dynamically."
    }

    $allowedValuesExpression = [string]$skuControl.constraints.allowedValues
    foreach ($sku in $expectedSkus) {
        if ($allowedValuesExpression -notlike "*$sku*") {
            throw "$environment SKU dropdown is missing '$sku'."
        }
    }

    if ($allowedValuesExpression -notlike '*P0v3 - recommended minimum*' -or
        $allowedValuesExpression -match 'B2|S2') {
        throw "$environment SKU dropdown guidance or candidates are invalid."
    }

    if ($ui.parameters.outputs.sku -ne "[basics('sku')]") {
        throw "$environment UI no longer preserves the sku output contract."
    }

    foreach ($outputName in @('deploymentMode', 'keyVaultName', 'existingKeyVaultResourceId', 'existingKeyVaultUrl')) {
        if ($null -eq $ui.parameters.outputs.$outputName) {
            throw "$environment UI is missing the $outputName output."
        }
    }

    $deploymentModeControl = @($ui.parameters.basics | Where-Object name -eq 'deploymentMode')[0]
    if ($null -eq $deploymentModeControl -or $deploymentModeControl.defaultValue -ne 'publicDefault') {
        throw "$environment UI deployment mode control is invalid."
    }

    $vnet = @($template.resources | Where-Object type -eq 'Microsoft.Network/virtualNetworks')[0]
    $natGateway = @($template.resources | Where-Object type -eq 'Microsoft.Network/natGateways')[0]
    if ($null -eq $vnet -or $null -eq $natGateway -or $vnet.condition -ne "[variables('isPrivateMode')]" -or $natGateway.condition -ne "[variables('isPrivateMode')]") {
        throw "$environment private networking resources are missing or not conditional."
    }

    if ($metadata.version -ne '1.1.0') {
        throw "$environment metadata version must be 1.1.0."
    }

    Write-Output "PASS AzureDeployment-$environment"
}