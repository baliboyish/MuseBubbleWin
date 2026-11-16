[CmdletBinding()]
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "artifacts/$Configuration"
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path (Join-Path $output 'MuseBubbleWin.exe'))) { throw 'Build the app before running tests.' }
New-Item -ItemType Directory -Force (Join-Path $output '.debug') | Out-Null
Push-Location $root
try {
    $common = @('/nologo', '/target:winexe', '/platform:x64', '/win32manifest:app.manifest',
        '/resource:MuseTheme.js,MuseTheme.js', '/resource:MuseTheme.css,MuseTheme.css',
        '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Web.Extensions.dll',
        "/r:$output/Microsoft.Web.WebView2.Core.dll", "/r:$output/Microsoft.Web.WebView2.WinForms.dll")
    $appSources = @('MuseBubbleWin.cs', 'AppleTheme.cs', 'FilePickerBridge.cs', 'MuseOfficialIcon.cs')
    # Framework csc treats /out after source files as a new output-file boundary.
    # Keep every compiler option, including /main and /out, before every source.
    $themeArguments = @('/main:MuseBubbleWin.ThemeChecks', "/out:$output/ThemeChecks.exe") +
        $common + $appSources + @((Join-Path $PSScriptRoot 'ThemeChecks.cs'))
    & $compiler @themeArguments
    if ($LASTEXITCODE -ne 0) { throw 'Native theme test compilation failed.' }
    $process = Start-Process (Join-Path $output 'ThemeChecks.exe') -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(30000)) { $process.Kill(); throw 'Native theme tests timed out.' }
    $results = Join-Path $output '.debug/theme-checks/results.txt'
    $text = Get-Content -LiteralPath $results -Raw
    if ($process.ExitCode -ne 0 -or $text -notmatch 'PASS all 84 assertions') { throw $text }
    Write-Output ($text.Trim().Split("`n")[-1])

    & $compiler /nologo /target:winexe /platform:x64 "/out:$output/OpaquePaintCheck.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll (Join-Path $PSScriptRoot 'OpaquePaintCheck.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Paint test compilation failed.' }
    $process = Start-Process (Join-Path $output 'OpaquePaintCheck.exe') -ArgumentList 'MuseBubbleWin.exe opaque-paint' -WorkingDirectory $output -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(15000)) { $process.Kill(); throw 'Paint test timed out.' }
    $text = Get-Content (Join-Path $output '.debug/opaque-paint.txt') -Raw
    if ($process.ExitCode -ne 0 -or $text -notmatch '^PASS') { throw $text }
    Write-Output $text
} finally { Pop-Location }
