$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:winexe /platform:x64 /win32manifest:app.manifest /win32icon:avatar.ico /resource:avatar.png,avatar.png /out:MuseBubbleWin-candidate.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:Microsoft.Web.WebView2.Core.dll /r:Microsoft.Web.WebView2.WinForms.dll MuseBubbleWin.cs FilePickerBridge.cs
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
    $assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'MuseBubbleWin-candidate.exe')))
    if ($assembly.GetManifestResourceNames() -notcontains 'avatar.png') { throw 'Missing embedded avatar.png' }
    Copy-Item -LiteralPath MuseBubbleWin-candidate.exe -Destination MuseBubbleWin.exe
    Write-Output 'Build succeeded; embedded avatar.png verified.'
} finally { Pop-Location }
