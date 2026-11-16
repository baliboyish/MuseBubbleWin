[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    # Optional offline path containing the two WebView2 managed DLLs and x64 loader.
    [string]$WebView2SdkPath
)
$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot "artifacts/$Configuration"
New-Item -ItemType Directory -Force -Path $output | Out-Null
Push-Location $PSScriptRoot
try {
    if ($WebView2SdkPath) {
        $sdk = (Resolve-Path -LiteralPath $WebView2SdkPath).Path
        foreach ($file in @('Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll', 'WebView2Loader.dll')) {
            if (!(Test-Path -LiteralPath (Join-Path $sdk $file))) { throw "Missing dependency: $file" }
        }
        $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
        if (!(Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler is required.' }
        $arguments = @('/nologo', '/target:winexe', '/platform:x64', '/win32manifest:app.manifest',
            '/resource:MuseTheme.js,MuseTheme.js', '/resource:MuseTheme.css,MuseTheme.css',
            "/out:$output/MuseBubbleWin.exe", '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll',
            '/r:System.Web.Extensions.dll', "/r:$sdk/Microsoft.Web.WebView2.Core.dll",
            "/r:$sdk/Microsoft.Web.WebView2.WinForms.dll", 'MuseBubbleWin.cs', 'FilePickerBridge.cs',
            'AppleTheme.cs', 'MuseOfficialIcon.cs')
        & $compiler @arguments
        if ($LASTEXITCODE -ne 0) { throw 'C# compilation failed.' }
        foreach ($file in @('Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll', 'WebView2Loader.dll')) {
            Copy-Item -LiteralPath (Join-Path $sdk $file) -Destination $output
        }
    } else {
        if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the .NET 10 SDK, or provide -WebView2SdkPath for an offline compiler build.' }
        & dotnet build MuseBubbleWin.csproj --configuration $Configuration --output $output
        if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
    }
    $assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $output 'MuseBubbleWin.exe')))
    foreach ($resource in @('MuseTheme.js', 'MuseTheme.css')) {
        if ($assembly.GetManifestResourceNames() -notcontains $resource) { throw "Missing embedded resource: $resource" }
    }
    Write-Output "Built: $output/MuseBubbleWin.exe"
} finally { Pop-Location }
