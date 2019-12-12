param (
    [Parameter(Mandatory=$True)]
    [string]
    $resourceGroupName,
    [Parameter(Mandatory=$True)]
    [string]
    $virtualMachineName,
    [Parameter(Mandatory=$true)]
    [string]
    $templateFilePath,
    [Parameter(Mandatory=$true)]
    [string]
    $storageAccountName,
    [Parameter(Mandatory=$true)]
    [string]
    $storageAccountKey
)

# Authenticate to Azure if running from Azure Automation
Write-Host "Logging in...";
$servicePrincipalConnection = Get-AutomationConnection -Name "AzureRunAsConnection"
Login-AzureRmAccount `
    -ServicePrincipal `
    -TenantId $servicePrincipalConnection.TenantId `
    -ApplicationId $servicePrincipalConnection.ApplicationId `
    -CertificateThumbprint $servicePrincipalConnection.CertificateThumbprint | Write-Verbose

#Set the parameter values for the Resource Manager template
$parameters = @{
    "virtualMachines_nimccollpbisrvr_name"=$virtualMachineName
    }

# Create a new context
$context = New-AzureStorageContext -StorageAccountName $storageAccountName -StorageAccountKey $storageAccountKey

Get-AzureStorageFileContent -ShareName 'templates' -Context $context -path $templateFilePath -Destination 'C:\Temp'

$templateFile = Join-Path -Path 'C:\Temp' -ChildPath $templateFilePath

# Start the deployment
Write-Host "Starting deployment...";
New-AzureRmResourceGroupDeployment -ResourceGroupName $resourceGroupName -TemplateFile $templateFile -TemplateParameterObject $parameters

# Create JIT access policy
$subscriptionId = (Get-AzureRmContext).Subscription.Id
$jitPolicyId = "/subscriptions/" + $subscriptionId + "/resourceGroups/" + $resourceGroupName + "/providers/Microsoft.Compute/virtualMachines/" + $virtualMachineName
$JitPolicy = (@{ id=$jitPolicyId
ports=(@{
     number=22;
     protocol="*";
     allowedSourceAddressPrefix=@("*");
     maxRequestAccessDuration="PT3H"},
     @{
     number=3389;
     protocol="*";
     allowedSourceAddressPrefix=@("*");
     maxRequestAccessDuration="PT3H"})})

$JitPolicyArr=@($JitPolicy)

Set-AzureRmJitNetworkAccessPolicy -Kind "Basic" -Location "eastus" -Name "default" -ResourceGroupName $resourceGroupName -VirtualMachine $JitPolicyArr