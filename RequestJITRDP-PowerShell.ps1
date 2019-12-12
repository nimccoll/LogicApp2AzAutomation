param (
    [Parameter(Mandatory=$True)]
    [string]
    $resourceGroupName,
    [Parameter(Mandatory=$True)]
    [string]
    $virtualMachineName,
    [Parameter(Mandatory=$true)]
    [string]
    $ipAddress
)

# Authenticate to Azure if running from Azure Automation
Write-Host "Logging in...";
$servicePrincipalConnection = Get-AutomationConnection -Name "AzureRunAsConnection"
Login-AzureRmAccount `
    -ServicePrincipal `
    -TenantId $servicePrincipalConnection.TenantId `
    -ApplicationId $servicePrincipalConnection.ApplicationId `
    -CertificateThumbprint $servicePrincipalConnection.CertificateThumbprint | Write-Verbose

# Create a JIT access request
Write-Host "Creating RDP JIT access request...";    
$ts = New-TimeSpan -Hours 3
$endTimeUtc = ((Get-Date) + $ts).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
$subscriptionId = (Get-AzureRmContext).Subscription.Id
$jitPolicyId = "/subscriptions/" + $subscriptionId + "/resourceGroups/" + $resourceGroupName + "/providers/Microsoft.Compute/virtualMachines/" + $virtualMachineName
$JitPolicyVm1 = (@{ id=$jitPolicyId
ports=(@{
   number=3389;
   endTimeUtc=$endTimeUtc;
   allowedSourceAddressPrefix=@($ipAddress)})})

$JitPolicyArr=@($JitPolicyVm1)

# Request JIT access
Write-Host "Request RDP JIT access..."
$resourceId = "/subscriptions/" + $subscriptionId + "/resourceGroups/" + $resourceGroupName + "/providers/Microsoft.Security/locations/eastus/jitNetworkAccessPolicies/default"
Start-AzureRmJitNetworkAccessPolicy -ResourceId $resourceId -VirtualMachine $JitPolicyArr