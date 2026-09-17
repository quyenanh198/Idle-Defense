param(
    [string]$UnityPath = $env:UNITY_PATH,
    [string]$ResultsPath = "TestResults/editmode.xml"
)

$ErrorActionPreference = "Stop"
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $candidates = Get-ChildItem -LiteralPath "C:\Program Files\Unity\Hub\Editor" -Filter Unity.exe -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending
    $UnityPath = $candidates | Select-Object -First 1 -ExpandProperty FullName
}
if ([string]::IsNullOrWhiteSpace($UnityPath) -or -not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity Editor was not found. Pass -UnityPath or set UNITY_PATH."
}
$absoluteResults = [System.IO.Path]::GetFullPath((Join-Path $projectPath $ResultsPath))
$resultDirectory = Split-Path -Parent $absoluteResults
New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
& $UnityPath -batchmode -nographics -quit -projectPath $projectPath -runTests -testPlatform EditMode -testResults $absoluteResults -logFile "-"
if ($LASTEXITCODE -ne 0) { throw "Unity tests failed with exit code $LASTEXITCODE." }

