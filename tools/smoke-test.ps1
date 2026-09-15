# End-to-end smoke test against a RUNNING daemon. Drives it with `TaLLon.exe --send`, opens
# Notepad inside the canvas, screenshots each stage into $OutDir and prints the tail of the log.
#   powershell -ExecutionPolicy Bypass -File tools/smoke-test.ps1 -OutDir C:\temp\tallon-shots
param([string]$OutDir = "$env:TEMP\tallon-shots", [string]$Configuration = "Release")
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -Namespace Native -Name Dpi -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();'
[Native.Dpi]::SetProcessDPIAware() | Out-Null   # capture physical pixels on scaled laptops
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "src\TaLLon.App\bin\$Configuration\net8.0-windows\TaLLon.exe"
$log = Join-Path $env:APPDATA "TaLLon\tallon.log"
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

if (-not (Get-Process TaLLon -ErrorAction SilentlyContinue)) { Write-Error "daemon not running"; exit 1 }
$mark = (Get-Content $log).Count

Send "Special+T"                       # inactive: should log the chord, do nothing
Send "Special+W"                       # enter canvas
Start-Sleep -Milliseconds 800
Shot "1-canvas"
Start-Process notepad.exe; Start-Sleep -Seconds 2
Start-Process notepad.exe; Start-Sleep -Seconds 2
Shot "2-two-windows-tiled"
Send "Special+T"; Start-Sleep -Milliseconds 500
Shot "3-restored"
Send "Special+T"; Start-Sleep -Milliseconds 500
Send "Special+Space"; Start-Sleep -Milliseconds 800
Shot "4-menu"
[System.Windows.Forms.SendKeys]::SendWait("{ESC}"); Start-Sleep -Milliseconds 400
Send "Special+Q"; Start-Sleep -Milliseconds 800     # close focused notepad
Shot "5-after-close"
Send "Special+W"                       # exit canvas
Start-Sleep -Milliseconds 1200
Shot "6-desktop-restored"
Get-Process notepad -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "----- log since test start -----"
Get-Content $log | Select-Object -Skip $mark
