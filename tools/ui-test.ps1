# Screenshots the TaLLon window (opened through the running instance) and exercises a few chords.
#   powershell -ExecutionPolicy Bypass -File tools/ui-test.ps1 -OutDir C:\temp\tallon-shots
param([string]$OutDir = "$env:TEMP\tallon-shots", [string]$Configuration = "Release")
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -Namespace Native -Name Dpi -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();'
[Native.Dpi]::SetProcessDPIAware() | Out-Null
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "src\TaLLon.App\bin\$Configuration\net8.0-windows10.0.19041.0\TaLLon.exe"
$log = Join-Path $env:APPDATA "TaLLon\TaLLon.log"
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

# A second launch just asks the running instance to show its window.
Start-Process $exe; Start-Sleep -Seconds 3
Shot "10-main-window"
[System.Windows.Forms.SendKeys]::SendWait("%{F4}"); Start-Sleep -Milliseconds 600   # closes to tray

& $exe --send Special+9; Start-Sleep -Milliseconds 600                                # unbound: OSD only

Write-Host "----- log since test start -----"
Get-Content $log | Select-Object -Skip $mark
