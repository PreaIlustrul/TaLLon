# End-to-end smoke test against a RUNNING TaLLon. Drives it with `TaLLon.exe --send`, opens Notepad
# inside the environment, screenshots each stage into $OutDir, then kills TaLLon while active to prove
# the watchdog restores the desktop. Prints the tail of the log.
#   powershell -ExecutionPolicy Bypass -File tools/smoke-test.ps1 -OutDir C:\temp\tallon-shots
param([string]$OutDir = "$env:TEMP\tallon-shots", [string]$Configuration = "Release", [switch]$SkipKill)
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -Namespace Native -Name U -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern IntPtr FindWindow(string c, string w);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
'@
[Native.U]::SetProcessDPIAware() | Out-Null
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "src\TaLLon.App\bin\$Configuration\net8.0-windows10.0.19041.0\TaLLon.exe"
$log = Join-Path $env:APPDATA "TaLLon\TaLLon.log"
$session = Join-Path $env:APPDATA "TaLLon\session.json"
New-Item -ItemType Directory -Force $OutDir | Out-Null

function Shot($name) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    $small = New-Object System.Drawing.Bitmap $bmp, ([int]($b.Width / 2)), ([int]($b.Height / 2))
    $small.Save((Join-Path $OutDir "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose(); $small.Dispose()
    Write-Host "shot: $name"
}
function Send($chord) { & $exe --send $chord; Start-Sleep -Milliseconds 900; Write-Host "sent: $chord" }
function TaskbarVisible { [Native.U]::IsWindowVisible([Native.U]::FindWindow("Shell_TrayWnd", $null)) }

$main = Get-Process TaLLon -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -First 1
if (-not $main) { Write-Error "TaLLon not running"; exit 1 }
$mark = (Get-Content $log).Count
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds

Send "Special"                          # tap -> ENTER (infinite mode, everything cascaded)
Start-Sleep -Milliseconds 1200
Shot "1-entered-infinite"
Start-Process notepad.exe; Start-Sleep -Seconds 2
Shot "2-new-window-spawned"
Send "Special+M"                        # -> tiling
Start-Sleep -Milliseconds 600
Shot "3-tiling"
Send "Special+Tab"                      # overview
Start-Sleep -Milliseconds 600
Shot "4-overview"
Send "Special+Tab"                      # back
Send "Special+M"                        # -> infinite (positions restored)
Start-Sleep -Milliseconds 600
Shot "5-back-to-infinite"
[Native.U]::SetCursorPos($b.Width - 1, [int]($b.Height / 2)) | Out-Null   # edge pan right
Start-Sleep -Milliseconds 900
[Native.U]::SetCursorPos([int]($b.Width / 2), [int]($b.Height / 2)) | Out-Null
Start-Sleep -Milliseconds 300
Shot "6-edge-panned"
Send "Special+Home"
Send "Special+Space"
Start-Sleep -Milliseconds 800
Shot "7-menu"
[System.Windows.Forms.SendKeys]::SendWait("{ESC}"); Start-Sleep -Milliseconds 400
Send "Special+Q"                        # close focused (notepad)
Send "Special"                          # tap -> EXIT
Start-Sleep -Milliseconds 1500
Shot "8-desktop-restored"
Write-Host ("taskbar visible after exit: " + (TaskbarVisible))
Get-Process notepad -ErrorAction SilentlyContinue | Stop-Process -Force

if (-not $SkipKill) {
    Write-Host "--- kill test: enter, then kill TaLLon; the watchdog must restore the desktop ---"
    Send "Special"; Start-Sleep -Milliseconds 1200
    Write-Host ("taskbar visible while active: " + (TaskbarVisible) + "  session.json: " + (Test-Path $session))
    Stop-Process -Id $main.Id -Force
    Start-Sleep -Seconds 3
    Write-Host ("taskbar visible after kill: " + (TaskbarVisible) + "  session.json: " + (Test-Path $session))
    Shot "9-after-kill"
    Start-Process $exe -ArgumentList "--tray"
    Start-Sleep -Seconds 3
}

Write-Host "----- log since test start -----"
Get-Content $log | Select-Object -Skip $mark
