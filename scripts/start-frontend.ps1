$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location (Join-Path $root 'Frontend')

$nodeCommand = Get-Command node -ErrorAction SilentlyContinue
if (-not $nodeCommand) {
    throw 'Node.js is not installed or is not available on PATH. Install Node.js 24.15.0+ (24.x), 22.22.3+ (22.x), or Node.js 26.x.'
}

$nodeVersionText = (node --version).Trim()
if ($nodeVersionText -notmatch '^v(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)') {
    throw "Could not parse Node.js version: $nodeVersionText"
}
$nodeVersion = [version]::new([int]$Matches.major, [int]$Matches.minor, [int]$Matches.patch)
$major = $nodeVersion.Major
$isSupported = ($major -eq 22 -and $nodeVersion -ge [version]'22.22.3') -or
               ($major -eq 24 -and $nodeVersion -ge [version]'24.15.0') -or
               ($major -eq 26)

Write-Host "Detected Node.js: $nodeVersionText"
if (-not $isSupported) {
    throw "Angular 22 requires Node.js ^22.22.3, ^24.15.0, or ^26.0.0. Detected $nodeVersionText. Update Node.js and run this script again."
}

Write-Host "Detected npm: $(npm --version)"
$installedAngularVersion = $null
$installedAngularPackage = '.\node_modules\@angular\core\package.json'
if (Test-Path $installedAngularPackage) {
    try {
        $installedAngularVersion = (Get-Content $installedAngularPackage -Raw | ConvertFrom-Json).version
    }
    catch {
        $installedAngularVersion = $null
    }
}

if ($installedAngularVersion -ne '22.2.7') {
    Write-Host 'Installing / upgrading frontend dependencies to Angular 22.2.7...'
    npm install
    if ($LASTEXITCODE -ne 0) {
        throw 'npm install failed. Review the npm output above and try again.'
    }
}

npm start
