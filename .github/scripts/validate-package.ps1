param(
    [Parameter(Mandatory = $false)]
    [string] $PackageRoot = ".",

    [Parameter(Mandatory = $true)]
    [string] $ExpectedName
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path -LiteralPath $PackageRoot).Path
$manifestPath = Join-Path $root "package.json"

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Missing package.json at $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.name -ne $ExpectedName) {
    throw "Package name '$($manifest.name)' does not match expected name '$ExpectedName'."
}

foreach ($property in @("name", "displayName", "version", "unity", "description", "author")) {
    if ($null -eq $manifest.$property -or [string]::IsNullOrWhiteSpace([string] $manifest.$property)) {
        throw "package.json is missing required property '$property'."
    }
}

if ([string] $manifest.version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
    throw "Package version '$($manifest.version)' is not valid Semantic Versioning."
}

if ($ExpectedName -eq "com.wolfyvr.threadlight.authoring") {
    $componentsDependency = $manifest.vpmDependencies.PSObject.Properties[
        "com.wolfyvr.threadlight.components"
    ]
    if ($null -ne $componentsDependency) {
        throw "Threadlight Authoring must remain independent of Threadlight Components."
    }
    foreach ($requiredCreatorPath in @(
        "Authoring\Runtime\CreatorAuthoringComponent.cs",
        "Authoring\Runtime\CreatorPrefabSnapshot.cs",
        "Mirroring\Runtime\AuthoringLiveMirroringSystem.cs"
    )) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $requiredCreatorPath) `
                -PathType Leaf)) {
            throw "Threadlight Authoring is missing creator-side runtime '$requiredCreatorPath'."
        }
    }
    $sdkDependency = $manifest.vpmDependencies.PSObject.Properties[
        "com.vrchat.avatars"
    ]
    if ($null -eq $sdkDependency -or
        $sdkDependency.Value -ne ">=3.7.0 <4.0.0") {
        throw "Threadlight Authoring must declare its direct VRChat SDK dependency."
    }
    $criticalGuids = [ordered]@{
        "Editor UI\Threadlight.EditorUI.asmdef.meta" =
            "ba116ed4e1e542ca82aceac5f1314ca1"
        "Authoring\Editor\Threadlight.Authoring.Editor.asmdef.meta" =
            "4f840a0a69db42f6a352213083de30d4"
        "Mirroring\Editor\Threadlight.Mirroring.Editor.asmdef.meta" =
            "dc264201ea474b4eb79dbf9320738c6e"
        "Paw Heart Logo.png.meta" =
            "c6672c7f5b5d72b4c9ebf65ad64cfc37"
    }
    foreach ($entry in $criticalGuids.GetEnumerator()) {
        $path = Join-Path $root $entry.Key
        $guidLine = if (Test-Path -LiteralPath $path -PathType Leaf) {
            Select-String -LiteralPath $path -Pattern '^guid:\s*([0-9a-fA-F]{32})\s*$' |
                Select-Object -First 1
        }
        else { $null }
        $actual = if ($null -ne $guidLine) {
            $guidLine.Matches[0].Groups[1].Value.ToLowerInvariant()
        }
        else { "" }
        if ($actual -ne $entry.Value) {
            throw "Critical moved GUID changed for '$($entry.Key)'."
        }
    }
}

$contentFiles = Get-ChildItem -LiteralPath $root -Recurse -File -Force | Where-Object {
    $_.Extension -ne ".meta" -and
    $_.FullName -notmatch '[\\/]\.git[\\/]' -and
    $_.FullName -notmatch '[\\/]\.github[\\/]' -and
    $_.FullName -notmatch '[\\/]\.vpm-listing[\\/]' -and
    $_.Name -notin @(".gitignore", ".gitattributes", ".editorconfig")
}

foreach ($file in $contentFiles) {
    if (-not (Test-Path -LiteralPath "$($file.FullName).meta" -PathType Leaf)) {
        throw "Unity metadata is missing for '$($file.FullName.Substring($root.Length + 1))'."
    }
}

$metaFiles = Get-ChildItem -LiteralPath $root -Recurse -File -Filter "*.meta" -Force | Where-Object {
    $_.FullName -notmatch '[\\/]\.git[\\/]' -and
    $_.FullName -notmatch '[\\/]\.github[\\/]' -and
    $_.FullName -notmatch '[\\/]\.vpm-listing[\\/]'
}

$guids = @{}
foreach ($metaFile in $metaFiles) {
    $assetPath = $metaFile.FullName.Substring(0, $metaFile.FullName.Length - 5)
    $relative = $metaFile.FullName.Substring($root.Length + 1)
    if (-not (Test-Path -LiteralPath $assetPath)) {
        throw "Orphaned Unity metadata '$relative'."
    }

    $guidLine = Select-String `
        -LiteralPath $metaFile.FullName `
        -Pattern '^guid:\s*([0-9a-fA-F]{32})\s*$' |
        Select-Object -First 1
    if ($null -eq $guidLine) {
        throw "Unity metadata '$relative' does not contain a valid GUID."
    }

    $guid = $guidLine.Matches[0].Groups[1].Value.ToLowerInvariant()
    if ($guids.ContainsKey($guid)) {
        $first = $guids[$guid].Substring($root.Length + 1)
        throw "Duplicate Unity GUID '$guid' in '$first' and '$relative'."
    }
    $guids[$guid] = $metaFile.FullName
}

Write-Host "Validated $($manifest.displayName) $($manifest.version): $($contentFiles.Count) assets and $($metaFiles.Count) metadata files."
