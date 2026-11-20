param([string]$Output=(Join-Path $PSScriptRoot ('..\work\ui-check-'+[guid]::NewGuid().ToString('N').Substring(0,8))))
$ErrorActionPreference='Stop'
$project=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x86 ('/win32manifest:'+(Join-Path $project 'src\app.manifest')) ('/out:'+(Join-Path $project 'build\release\UiTests.exe')) ('/r:'+(Join-Path $project 'build\release\QslPost.exe')) /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Core.dll (Join-Path $PSScriptRoot 'UiTests.cs')
if($LASTEXITCODE -ne 0){throw 'UI test compilation failed.'}
$process=Start-Process -FilePath (Join-Path $project 'build\release\UiTests.exe') -ArgumentList ('"'+[IO.Path]::GetFullPath($Output)+'"') -WindowStyle Normal -Wait -PassThru
if($process.ExitCode -ne 0){throw 'UI checks failed.'}
Get-Content -LiteralPath (Join-Path $Output 'ui-results.txt')
