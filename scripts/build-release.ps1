param(
    [Parameter()]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$releaseRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts\release\v$Version"))
$expectedPrefix = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\release')) + [IO.Path]::DirectorySeparatorChar

if (-not $releaseRoot.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Release output escaped the repository artifacts directory: $releaseRoot"
}

if (Test-Path -LiteralPath $releaseRoot) {
    throw "Release output already exists: $releaseRoot"
}

$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    throw '.NET 8 SDK was not found.'
}

$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
    'C:\Program Files\Inno Setup 7\ISCC.exe',
    'C:\Program Files (x86)\Inno Setup 7\ISCC.exe',
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) {
    throw 'Inno Setup compiler (ISCC.exe) was not found.'
}

$publishDir = Join-Path $releaseRoot 'publish'
$portableDir = Join-Path $releaseRoot 'portable'
[IO.Directory]::CreateDirectory($publishDir) | Out-Null
[IO.Directory]::CreateDirectory($portableDir) | Out-Null

$projectPath = Join-Path $repoRoot 'src\VoiceButton\VoiceButton.csproj'
$publishArgs = @(
    'publish',
    $projectPath,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-o', $publishDir,
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:PublishTrimmed=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    "-p:Version=$Version"
)
& $dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    throw 'Self-contained publish failed.'
}

$publishedExe = Join-Path $publishDir 'VoiceButton.exe'
if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw 'Published VoiceButton.exe was not created.'
}

Copy-Item -LiteralPath $publishedExe -Destination (Join-Path $portableDir 'Voice Button Portable.exe')
[IO.File]::WriteAllText((Join-Path $portableDir 'portable.mode'), "portable$([Environment]::NewLine)")
Copy-Item -LiteralPath (Join-Path $repoRoot 'installer\README-PORTABLE.txt') -Destination (Join-Path $portableDir 'README.txt')

$forbiddenPortableFiles = Get-ChildItem -LiteralPath $portableDir -Recurse -Force -File |
    Where-Object { $_.Name -like '.env*' -or $_.Name -eq 'settings.json' -or $_.Name -eq 'diagnostics.log' }
if ($forbiddenPortableFiles) {
    throw 'Portable staging contains local secrets, settings, or diagnostics.'
}

$portableZip = Join-Path $releaseRoot "VoiceButton-Portable-v$Version-win-x64.zip"
Compress-Archive -Path (Join-Path $portableDir '*') -DestinationPath $portableZip -CompressionLevel Optimal

& $iscc "/DAppVersion=$Version" (Join-Path $repoRoot 'installer\VoiceButton.iss')
if ($LASTEXITCODE -ne 0) {
    throw 'Installer compilation failed.'
}

$installer = Join-Path $releaseRoot "VoiceButton-Setup-v$Version-win-x64.exe"
if (-not (Test-Path -LiteralPath $installer)) {
    throw 'Installer executable was not created.'
}

$assets = @($installer, $portableZip)
$checksumLines = foreach ($asset in $assets) {
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $asset).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($asset))"
}
$checksumPath = Join-Path $releaseRoot 'SHA256SUMS.txt'
[IO.File]::WriteAllLines($checksumPath, $checksumLines, [Text.UTF8Encoding]::new($false))

$assets + $checksumPath | Get-Item | Select-Object Name, Length, LastWriteTime
