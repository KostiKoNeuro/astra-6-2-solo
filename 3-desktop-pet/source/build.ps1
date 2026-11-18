$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
$options = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/checked-','/utf8output',
    '/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',"/win32manifest:$root\src\TaskbarTails.manifest",
    "/win32icon:$root\assets\pip.ico","/out:$root\TaskbarTails.exe")
foreach ($item in 'stride.png','step.png','lift.png','nap.png','biscuit.png','scout.png','count.png','debug.png','read.png','clearing.jpg','pip.ico') {
    $options += "/resource:$root\assets\$item,Art.$item"
}
$sources = @(Get-ChildItem -LiteralPath "$root\src" -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler @options @sources
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }
Write-Host 'Built TaskbarTails.exe'
