# Screenshots the settings app and exercises leader (tap-then-key) mode against a running daemon.
#   powershell -ExecutionPolicy Bypass -File tools/ui-test.ps1 -OutDir C:\temp\tallon-shots
param([string]$OutDir = "$env:TEMP\tallon-shots", [string]$Configuration = "Release")
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -Namespace Native -Name Dpi -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();'
[Native.Dpi]::SetProcessDPIAware() | Out-Null
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
$mark = (Get-Content $log).Count

# 1. settings app (separate settings-only process)
$p = Start-Process $exe -ArgumentList "--settings" -PassThru
Start-Sleep -Seconds 3
Shot "7-settings-general"
[System.Windows.Forms.SendKeys]::SendWait("%{TAB}")  # no-op guard so focus stays sane
Start-Sleep -Milliseconds 300
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue

# 2. leader mode: tap special, wait, press W -> should ENTER; then leader again -> EXIT
& $exe --send Special+W --leader; Start-Sleep -Seconds 1
Shot "8-leader-entered"
& $exe --send Special+W --leader; Start-Sleep -Seconds 1

# 3. an unbound chord should just flash the OSD, not crash
& $exe --send Special+9; Start-Sleep -Milliseconds 600

Write-Host "----- log since test start -----"
Get-Content $log | Select-Object -Skip $mark
