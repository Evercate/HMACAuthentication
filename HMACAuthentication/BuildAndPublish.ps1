$ErrorActionPreference = "Stop"

$projectPath = ".\HMACAuthentication\HMACAuthentication.csproj"

# Read current version from .csproj
$csprojContent = Get-Content -Path $projectPath -Raw
$versionMatch = [regex]::Match($csprojContent, '<Version>(.*?)</Version>')

if (!$versionMatch.Success) {
    throw 'Version not found in .csproj file. Make sure it looks like <Version>x.x.x</Version>'
}

$lastVersionString = $versionMatch.Groups[1].Value

try {
    $lastVersion = [version]($lastVersionString)
}
catch {
    Write-Host $_ -BackgroundColor Black -ForegroundColor Red
    Write-Host "Incorrect format of version in .csproj file"
    Read-Host -Prompt "Enter to exit"
    exit
}

$automaticVersionToUse = "{0}.{1}.{2}" -f $lastVersion.Major, $lastVersion.Minor, ($lastVersion.Build + 1)

$manualVersion = Read-Host -Prompt "OPTIONAL: Manual version number (Press enter for automatic - $automaticVersionToUse)"

$newVersion = $automaticVersionToUse
if (![string]::IsNullOrEmpty($manualVersion)) {
    try {
        [System.Version]::Parse($manualVersion) | Out-Null
    }
    catch {
        Write-Host $_ -BackgroundColor Black -ForegroundColor Red
        Write-Host "Incorrect format of given version - Should have format Major.Minor.Build so for instance 1.0.2"
        Read-Host -Prompt "Enter to exit"
        exit
    }
    $newVersion = $manualVersion
}

$releaseNotes = Read-Host -Prompt "Release notes"

# Update version and release notes in .csproj
$csprojContent = $csprojContent -replace '<Version>.*?</Version>', "<Version>$newVersion</Version>"
if (![string]::IsNullOrEmpty($releaseNotes)) {
    $csprojContent = $csprojContent -replace '<PackageReleaseNotes>.*?</PackageReleaseNotes>', "<PackageReleaseNotes>$releaseNotes</PackageReleaseNotes>"
}
Set-Content -Path $projectPath -Value $csprojContent -NoNewline

# Build and pack
Write-Host "`nBuilding and packing version $newVersion..." -ForegroundColor Cyan
dotnet pack $projectPath --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build/pack failed!" -ForegroundColor Red
    Read-Host -Prompt "Enter to exit"
    exit
}

$packageFile = ".\HMACAuthentication\bin\Release\Evercate.HMACAuthentication.$newVersion.nupkg"

if (!(Test-Path $packageFile)) {
    Write-Host "Package file not found at: $packageFile" -ForegroundColor Red
    Read-Host -Prompt "Enter to exit"
    exit
}

Write-Host "`nPackage created: $packageFile" -ForegroundColor Green
Write-Host "Press enter to push to GitHub Packages (or close to not push)" -BackgroundColor Black -ForegroundColor Cyan
Read-Host

# Read API key
$nugetApiKey = ""

if ([System.IO.File]::Exists(".\NugetKey.txt")) {
    $nugetApiKey = (Get-Content -Path .\NugetKey.txt -First 1).Trim()
}
else {
    Write-Host "There must be a NugetKey.txt in the same directory as this build script that contains a GitHub Personal Access Token (classic) with write:packages scope" -ForegroundColor Red
    Read-Host -Prompt "Enter to exit"
    exit
}

# Push to GitHub Packages
$source = "https://nuget.pkg.github.com/Evercate/index.json"

Write-Host "`nPushing to GitHub Packages..." -ForegroundColor Cyan
dotnet nuget push $packageFile --api-key $nugetApiKey --source $source --skip-duplicate

if ($LASTEXITCODE -ne 0) {
    Write-Host "Push failed!" -ForegroundColor Red
    Read-Host -Prompt "Enter to exit"
    exit
}

Write-Host "`nSuccessfully pushed Evercate.HMACAuthentication $newVersion to GitHub Packages!" -ForegroundColor Green

# Clean up .nupkg file
Remove-Item -Path $packageFile -ErrorAction SilentlyContinue

Read-Host -Prompt "Enter to exit"
