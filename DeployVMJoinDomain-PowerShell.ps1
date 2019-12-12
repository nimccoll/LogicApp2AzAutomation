param (
    [Parameter(Mandatory=$True)]
    [string]
    $resourceGroupName,
    [Parameter(Mandatory=$True)]
    [string]
    $virtualMachineName,
    [Parameter(Mandatory=$True)]
    [string]
    $existingVNetName,
    [Parameter(Mandatory=$True)]
    [string]
    $existinSubnetName,
    [Parameter(Mandatory=$True)]
    [string]
    $vmAdminUsername,
    [Parameter(Mandatory=$True)]
    [string]
    $vmAdminPassword,
    [Parameter(Mandatory=$True)]
    [string]
    $existingDiagnosticsStorageUri,
    [Parameter(Mandatory=$True)]
    [string]
    $domainToJoin,
    [Parameter(Mandatory=$True)]
    [string]
    $domainUsername,
    [Parameter(Mandatory=$True)]
    [string]
    $domainPassword,
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

# Create a new context
$context = New-AzureStorageContext -StorageAccountName $storageAccountName -StorageAccountKey $storageAccountKey

Get-AzureStorageFileContent -ShareName 'templates' -Context $context -path $templateFilePath -Destination 'C:\Temp'

$templateFile = Join-Path -Path 'C:\Temp' -ChildPath $templateFilePath

# Check for existing resource group
$resourceGroup = Get-AzureRmResourceGroup -Name $resourceGroupName -ErrorAction SilentlyContinue
if(!$resourceGroup)
{
    Write-Host "Resource group '$resourceGroupName' does not exist. Deployment halted!";
}
else{
    Write-Host "Using existing resource group '$resourceGroupName'";
    $resourceGroupLocation = $resourceGroup.Location;

    # Set the parameter values for the Resource Manager template
    $Parameters = @{
        "virtualMachine_name"=$virtualMachineName;
        "existingVNETName"=$existingVNetName;
        "existingSubnetName"=$existinSubnetName;
        "vmAdminUsername"=$vmAdminUsername;
        "vmAdminPassword"=$vmAdminPassword;
        "diagnosticsStorageUri"=$existingDiagnosticsStorageUri;
        "domainToJoin"=$domainToJoin;
        "domainUsername"=$domainUsername;
        "domainPassword"=$domainPassword
        }
    
    # Start the deployment
    Write-Host "Starting deployment...";
    New-AzureRmResourceGroupDeployment -ResourceGroupName $resourceGroupName -TemplateFile $templateFile -TemplateParameterObject $Parameters


    # Create Just-in-Time access policy
    Write-Host "Creating Just-in-Time access policy...";
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
    
    Set-AzureRmJitNetworkAccessPolicy -Kind "Basic" -Location $resourceGroupLocation -Name "default" -ResourceGroupName $resourceGroupName -VirtualMachine $JitPolicyArr
}
