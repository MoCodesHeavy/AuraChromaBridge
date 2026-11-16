# AuraChromaBridge - DSX test. Asks DSX (over its local UDP port) to set the controller light bar
# to red, green, blue, then hands the light bar back to DSX's own settings.
# Result: logs\dsx-test.log
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$logDir = Join-Path $root 'logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir 'dsx-test.log'
$lines = New-Object System.Collections.Generic.List[string]
function W([string]$s) { $lines.Add((Get-Date -Format 'HH:mm:ss.fff') + '  ' + $s); Write-Host $s }

$port = 6969
$portFile = 'C:\Temp\DualSenseX\DualSenseX_PortNumber.txt'
if (Test-Path $portFile) {
    $txt = (Get-Content $portFile -Raw).Trim()
    W ('DSX port file says: ' + $txt)
    $p = 0
    if ([int]::TryParse($txt, [ref]$p) -and $p -gt 0) { $port = $p }
} else { W ('No DSX port file at ' + $portFile + ' - using 6969') }

$procs = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^dsx|dualsense' })
foreach ($pr in $procs) {
    $ver = ''
    try { $ver = $pr.MainModule.FileVersionInfo.FileVersion + '  ' + $pr.MainModule.FileName } catch { $ver = '(version not readable)' }
    W ('DSX process: ' + $pr.ProcessName + '  pid ' + $pr.Id + '  ' + $ver)
}
if ($procs.Count -eq 0) { W 'DSX does not seem to be running.' }

foreach ($pr in $procs) {
    try {
        $eps = @(Get-NetUDPEndpoint -OwningProcess $pr.Id -ErrorAction Stop)
        W ('UDP ports opened by ' + $pr.ProcessName + ': ' + (($eps | ForEach-Object { $_.LocalAddress + ':' + $_.LocalPort }) -join ', '))
        $tcp = @(Get-NetTCPConnection -OwningProcess $pr.Id -State Listen -ErrorAction SilentlyContinue)
        W ('TCP ports listened on by ' + $pr.ProcessName + ': ' + (($tcp | ForEach-Object { $_.LocalAddress + ':' + $_.LocalPort }) -join ', '))
    } catch { W ('Could not list ports for ' + $pr.ProcessName + ': ' + $_.Exception.Message) }
}
$listeners = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveUdpListeners()
$listening = @($listeners | Where-Object { $_.Port -eq $port }).Count -gt 0
W ('Something is listening on UDP port ' + $port + ': ' + $listening)

$udp = New-Object System.Net.Sockets.UdpClient
$udp.Client.ReceiveTimeout = 1500
$udp.Connect('127.0.0.1', $port)

function Send([string]$label, [string]$json) {
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($json)
    [void]$udp.Send($bytes, $bytes.Length)
    $reply = '(no reply)'
    try {
        $ep = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
        $data = $udp.Receive([ref]$ep)
        $reply = [System.Text.Encoding]::ASCII.GetString($data)
        if ($reply.Length -gt 400) { $reply = $reply.Substring(0, 400) + '...' }
    } catch { $reply = '(no reply: ' + $_.Exception.InnerException.Message + ')' }
    W ($label + ' sent ' + $json)
    W ('   reply: ' + $reply)
}

# DSX UDP instruction: type 2 = RGBUpdate, parameters = controller index, R, G, B
Send 'RED  ' '{"instructions":[{"type":2,"parameters":[0,255,0,0]}]}'
Start-Sleep -Seconds 3
Send 'GREEN' '{"instructions":[{"type":2,"parameters":[0,0,255,0]}]}'
Start-Sleep -Seconds 3
Send 'BLUE ' '{"instructions":[{"type":2,"parameters":[0,0,0,255]}]}'
Start-Sleep -Seconds 3
# type 7 = ResetToUserSettings
Send 'RESET' '{"instructions":[{"type":7,"parameters":[0]}]}'
$udp.Close()
W 'Done.'
[System.IO.File]::WriteAllLines($log, $lines, [System.Text.Encoding]::ASCII)
