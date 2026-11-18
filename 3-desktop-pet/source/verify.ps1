$ErrorActionPreference = 'Stop'
$report = Join-Path $PSScriptRoot 'docs\verification.txt'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fixture = Join-Path $PSScriptRoot 'TaskbarTails.Checks.exe'
$options = @('/nologo','/target:winexe','/optimize+','/main:TaskbarTails.ClipboardFixture',"/out:$fixture",
    '/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll',"/reference:$PSScriptRoot\TaskbarTails.exe",
    "/win32manifest:$PSScriptRoot\src\TaskbarTails.manifest")
try {
    & $compiler @options "$PSScriptRoot\tools\ClipboardFixture.cs" "$PSScriptRoot\tools\Verification.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Could not compile the behavioral fixture.' }
    $run = Start-Process -FilePath $fixture -ArgumentList '--overlay docs\verification.txt docs\field-guide.png' -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru -Wait
    Get-Content -LiteralPath $report
    if ($run.ExitCode -ne 0) { throw 'Taskbar Tails verification failed.' }
    $private = Start-Process -FilePath $fixture -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru -Wait
    Get-Content -LiteralPath "$PSScriptRoot\docs\clipboard-verification.txt"
    if ($private.ExitCode -ne 0) { throw 'Real-time desktop fixture verification failed.' }
} finally {
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture }
}
