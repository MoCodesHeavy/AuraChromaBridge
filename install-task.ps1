# Registers the "AuraChromaBridge" scheduled task: starts the sync when you sign in, with admin rights, no window,
# no time limit. Called by install-startup.cmd (already running as admin).
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$Data
)
$ErrorActionPreference = 'Stop'

$user = "$env:USERDOMAIN\$env:USERNAME"
$action = New-ScheduledTaskAction -Execute $Exe -Argument ('run data="{0}"' -f $Data) -WorkingDirectory $Data
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $user
$trigger.Delay = 'PT20S'   # give Synapse and Armoury Crate a moment to start first
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew

Register-ScheduledTask -TaskName 'AuraChromaBridge' `
    -Description 'Runs one lighting effect in sync on Razer Chroma and ASUS Aura.' `
    -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null

Write-Host "Scheduled task 'AuraChromaBridge' registered for $user (starts at sign-in, with admin rights)."
