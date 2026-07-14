[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Staged,

    [Parameter()]
    [switch]$History
)

$ErrorActionPreference = 'Stop'

if ($Staged -and $History) {
    throw 'Use either -Staged or -History, not both.'
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$originalLocation = Get-Location
Set-Location $repoRoot

$findings = [Collections.Generic.List[object]]::new()
$seenFindings = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$scannedSources = 0

$secretPatterns = [ordered]@{
    'OpenAI API key' = '\bsk-(?:proj-)?[A-Za-z0-9_-]{20,}\b'
    'GitHub token' = '\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})\b'
    'AWS access key' = '\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'
    'Google API key' = '\bAIza[A-Za-z0-9_-]{35}\b'
    'Slack token' = '\bxox[baprs]-[A-Za-z0-9-]{10,}\b'
    'Stripe secret key' = '\bsk_(?:live|test)_[A-Za-z0-9]{20,}\b'
    'Bearer token' = '(?i)\bBearer\s+[A-Za-z0-9._+/=-]{20,}'
    'Private key block' = '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'
    'Credential assignment' = '(?im)^\s*(?:OPENAI_API_KEY|API_KEY|ACCESS_TOKEN|CLIENT_SECRET|PASSWORD)\s*[:=]\s*["'']?(?!PASTE_|YOUR_|CHANGEME|EXAMPLE|PLACEHOLDER|<)[^\s"''#]{12,}'
}

$binaryExtensions = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
@(
    '.png', '.jpg', '.jpeg', '.gif', '.webp', '.ico',
    '.exe', '.dll', '.pdb', '.bin',
    '.zip', '.7z', '.rar', '.nupkg',
    '.pdf', '.mp3', '.wav'
) | ForEach-Object { $null = $binaryExtensions.Add($_) }

function Add-Finding {
    param(
        [Parameter(Mandatory)] [string]$Rule,
        [Parameter(Mandatory)] [string]$Source
    )

    $key = $Rule + '|' + $Source
    if ($seenFindings.Add($key)) {
        $findings.Add([pscustomobject]@{
            Rule = $Rule
            Source = $Source
        })
    }
}

function Test-PathSafety {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$Source
    )

    $normalized = $Path.Replace('\', '/')
    $leaf = [IO.Path]::GetFileName($normalized)
    $extension = [IO.Path]::GetExtension($leaf)

    if ($leaf -like '.env*' -and $leaf -ne '.env.example') {
        Add-Finding -Rule 'Environment file is tracked' -Source $Source
    }

    if ($leaf -in @('settings.json', 'diagnostics.log', 'credentials.json')) {
        Add-Finding -Rule 'Local state or credential file is tracked' -Source $Source
    }

    if ($extension -in @('.pfx', '.p12', '.pem', '.key', '.snk', '.jks', '.keystore')) {
        Add-Finding -Rule 'Private key or certificate container is tracked' -Source $Source
    }

    if ($extension -in @('.zip', '.7z', '.rar')) {
        Add-Finding -Rule 'Archive is tracked; inspect and release it outside Git' -Source $Source
    }

    if ($normalized -match '(^|/)(secrets?|credentials?)(/|$)') {
        Add-Finding -Rule 'Secret or credential directory is tracked' -Source $Source
    }
}

function Get-LocalOpenAiKey {
    $envPath = Join-Path $repoRoot '.env'
    if (-not (Test-Path -LiteralPath $envPath)) {
        return $null
    }

    $keyLine = Get-Content -LiteralPath $envPath |
        Where-Object { $_ -match '^\s*OPENAI_API_KEY\s*=' } |
        Select-Object -First 1
    if (-not $keyLine) {
        return $null
    }

    $key = $keyLine.Substring($keyLine.IndexOf('=') + 1).Trim().Trim('"').Trim("'")
    if ([string]::IsNullOrWhiteSpace($key) -or $key -eq 'PASTE_YOUR_OPENAI_API_KEY_HERE') {
        return $null
    }

    return $key
}

$localOpenAiKey = Get-LocalOpenAiKey

function Test-ContentSafety {
    param(
        [Parameter(Mandatory)] [string]$Content,
        [Parameter(Mandatory)] [string]$Source
    )

    if ($localOpenAiKey -and $Content.Contains($localOpenAiKey)) {
        Add-Finding -Rule 'Exact local OpenAI key' -Source $Source
    }

    foreach ($rule in $secretPatterns.GetEnumerator()) {
        if ([regex]::IsMatch($Content, $rule.Value)) {
            Add-Finding -Rule $rule.Key -Source $Source
        }
    }
}

function Test-WorkingTreeFile {
    param([Parameter(Mandatory)] [string]$Path)

    Test-PathSafety -Path $Path -Source $Path

    $fullPath = Join-Path $repoRoot $Path
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        return
    }

    if ($binaryExtensions.Contains([IO.Path]::GetExtension($Path))) {
        return
    }

    $script:scannedSources++
    $content = [IO.File]::ReadAllText($fullPath)
    Test-ContentSafety -Content $content -Source $Path
}

function Test-StagedFile {
    param([Parameter(Mandatory)] [string]$Path)

    Test-PathSafety -Path $Path -Source ('staged:' + $Path)

    if ($binaryExtensions.Contains([IO.Path]::GetExtension($Path))) {
        return
    }

    $script:scannedSources++
    $content = (git show --no-ext-diff (':' + $Path) | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to read staged file: $Path"
    }

    Test-ContentSafety -Content $content -Source ('staged:' + $Path)
}

function Test-History {
    $objects = git rev-list --objects --all
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to enumerate Git history.'
    }

    foreach ($objectLine in $objects) {
        $parts = $objectLine -split ' ', 2
        $objectId = $parts[0]
        $path = if ($parts.Count -gt 1 -and -not [string]::IsNullOrWhiteSpace($parts[1])) { $parts[1] } else { '(no path)' }
        $source = $objectId + ':' + $path

        if ($path -ne '(no path)') {
            Test-PathSafety -Path $path -Source $source
        }

        $type = git cat-file -t $objectId
        if ($type -ne 'blob') {
            continue
        }

        if ($binaryExtensions.Contains([IO.Path]::GetExtension($path))) {
            continue
        }

        $script:scannedSources++
        $content = (git cat-file -p $objectId | Out-String)
        if ($LASTEXITCODE -ne 0) {
            throw "Unable to read Git object: $objectId"
        }

        Test-ContentSafety -Content $content -Source $source
    }
}

try {
    if ($History) {
        Test-History
        $mode = 'history'
    }
    elseif ($Staged) {
        $paths = @(git diff --cached --name-only --diff-filter=ACMR --)
        if ($LASTEXITCODE -ne 0) {
            throw 'Unable to enumerate staged files.'
        }

        foreach ($path in $paths) {
            if (-not [string]::IsNullOrWhiteSpace($path)) {
                Test-StagedFile -Path $path
            }
        }
        $mode = 'staged'
    }
    else {
        $paths = @(git ls-files)
        if ($LASTEXITCODE -ne 0) {
            throw 'Unable to enumerate tracked files.'
        }

        foreach ($path in $paths) {
            Test-WorkingTreeFile -Path $path
        }
        $mode = 'tracked'
    }

    if ($findings.Count -gt 0) {
        Write-Host 'Secret scan failed. Values are intentionally redacted.' -ForegroundColor Red
        foreach ($finding in $findings | Sort-Object Rule, Source) {
            Write-Host ('- [' + $finding.Rule + '] ' + $finding.Source) -ForegroundColor Red
        }
        exit 1
    }

    Write-Host ("Secret scan passed: mode=$mode; scanned=$scannedSources; findings=0") -ForegroundColor Green
}
finally {
    Set-Location $originalLocation
    $localOpenAiKey = $null
}
