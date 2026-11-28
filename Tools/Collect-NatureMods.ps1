# Collect-NatureMods.ps1, for Rimfolk.
#
# Collects what Rimfolk needs to build the living land alongside the nature mods you added (28 November 2026): for each
# of them its About.xml and LoadFolders.xml, its Defs and Patches (the XML), its code (the DLLs, and its source if it
# ships any) in the folders RimWorld 1.7 loads, and its settings; plus your mod list (ModsConfig.xml), to see that they
# are enabled and in which order. Nothing is changed: it only reads, and writes one zip, RimfolkNatureMods.zip, to your
# Desktop, then opens the Desktop with the zip selected. Upload that zip in the chat.
#
# Run it: right-click this file and choose "Run with PowerShell". It finds RimWorld through the folder the Rimfolk
# installer remembered, or through Steam; if neither works it asks for the RimWorld folder.

param(
    [string]$RimWorld = '',
    [string]$Workshop = '',
    [string]$Config = '',
    [string]$OutDir = '',
    [switch]$NoWait
)

$ErrorActionPreference = 'Stop'

# The mods, by Steam Workshop id.
$wanted = [ordered]@{
    '3822357701' = 'Quiet Wonders'
    '3834245888' = 'RimWorld: Living Waters'
    '3831078430' = 'RimWorld: Waves'
    '3838111458' = 'Weather Reimagined'
    '3776658466' = 'Wild Crops'
    '3535674283' = 'Biome Compatibility Project'
    '3259367736' = 'RimKeeper - Wild Animal Procreation'
    '3652630316' = 'Animal Traits System'
    '3808191148' = 'Dynamic Atmosphere'
}

function Test-RimWorld([string]$dir) {
    if (-not $dir) { return $false }
    return (Test-Path -LiteralPath (Join-Path (Join-Path $dir 'Data') 'Core')) -or
           @(Get-ChildItem -LiteralPath $dir -Filter 'RimWorld*.exe' -File -ErrorAction SilentlyContinue).Count -gt 0
}

function Find-RimWorld {
    $mods = (Get-ItemProperty -Path 'HKCU:\Software\Rimfolk' -ErrorAction SilentlyContinue).ModsFolder
    if ($mods) {
        $rw = Split-Path -Path $mods -Parent
        if (Test-RimWorld $rw) { return $rw }
    }
    $roots = @()
    $steam = (Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if ($steam) { $roots += ($steam -replace '/', '\') }
    foreach ($root in @($roots)) {
        foreach ($vdf in @((Join-Path (Join-Path $root 'steamapps') 'libraryfolders.vdf'), (Join-Path (Join-Path $root 'config') 'libraryfolders.vdf'))) {
            if (Test-Path -LiteralPath $vdf) {
                foreach ($m in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"([^"]+)"')) {
                    $roots += ($m.Groups[1].Value -replace '\\\\', '\')
                }
            }
        }
    }
    foreach ($root in $roots) {
        $rw = Join-Path (Join-Path (Join-Path $root 'steamapps') 'common') 'RimWorld'
        if (Test-RimWorld $rw) { return $rw }
    }
    return $null
}

$rimworld = $RimWorld
if (-not (Test-RimWorld $rimworld)) { $rimworld = Find-RimWorld }
while (-not (Test-RimWorld $rimworld)) {
    $rimworld = (Read-Host 'Where is RimWorld? Paste the folder with RimWorld.exe in it').Trim('"', ' ')
}
Write-Host "RimWorld: $rimworld"

$work = Join-Path ([IO.Path]::GetTempPath()) 'RimfolkNatureMods'
if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Path $work | Out-Null

$script:skipped = 0
function Copy-Into([string]$from, [string]$relative) {
    $target = Join-Path $work $relative
    try {
        New-Item -ItemType Directory -Path (Split-Path -Path $target -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $from -Destination $target
    } catch {
        # A path too long for Windows, or a file in use: left out and counted.
        $script:skipped++
    }
}

# Every file of the given kinds under a folder, kept at its place below the mod.
function Copy-Files([string]$dir, [string]$modDir, [string]$prefix, [string[]]$patterns) {
    $n = 0
    if (-not (Test-Path -LiteralPath $dir)) { return 0 }
    foreach ($pattern in $patterns) {
        foreach ($f in Get-ChildItem -LiteralPath $dir -Recurse -Filter $pattern -File -ErrorAction SilentlyContinue) {
            Copy-Into $f.FullName (Join-Path $prefix $f.FullName.Substring($modDir.Length).TrimStart('\', '/'))
            $n++
        }
    }
    return $n
}

# The folders of a mod that RimWorld 1.7 loads: all those its LoadFolders.xml names for 1.7 (else for the newest version
# before it), else its root, Common and its newest version folder up to 1.7.
function Get-LoadFolders([string]$modDir) {
    $lf = Join-Path $modDir 'LoadFolders.xml'
    if (Test-Path -LiteralPath $lf) {
        try {
            [xml]$x = Get-Content -LiteralPath $lf -Raw
            $best = $null; $bestV = [version]'0.0'
            foreach ($node in $x.DocumentElement.ChildNodes) {
                if ($node.NodeType -ne 'Element' -or $node.Name -notmatch '^v(\d+\.\d+)$') { continue }
                $v = [version]$Matches[1]
                if ($v -le [version]'1.7' -and $v -ge $bestV) { $best = $node; $bestV = $v }
            }
            if ($best) {
                $list = @()
                foreach ($li in $best.SelectNodes('li')) { $list += ($li.InnerText.Trim().Trim('/', '\') -replace '[\\/]', [IO.Path]::DirectorySeparatorChar) }
                if ($list.Count -gt 0) { return $list }
            }
        } catch { }
    }
    $folders = @('', 'Common')
    $newest = $null; $newestV = [version]'0.0'
    foreach ($d in Get-ChildItem -LiteralPath $modDir -Directory -ErrorAction SilentlyContinue) {
        if ($d.Name -match '^v?(\d+\.\d+)$') {
            $v = [version]$Matches[1]
            if ($v -le [version]'1.7' -and $v -ge $newestV) { $newest = $d.Name; $newestV = $v }
        }
    }
    if ($newest) { $folders += $newest }
    return $folders
}

# A folder below a mod by name, matching the name's case as Windows does.
function Get-Child([string]$base, [string]$name) {
    if (-not $name) { return $base }
    $p = Join-Path $base $name
    if (Test-Path -LiteralPath $p) { return $p }
    $match = Get-ChildItem -LiteralPath $base -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -ieq $name } | Select-Object -First 1
    if ($match) { return $match.FullName }
    return $p
}

# One mod: About, LoadFolders, and in each folder 1.7 loads its Defs and Patches (XML) and Assemblies (DLLs); its source
# code wherever it ships one. Returns the counts.
function Copy-Mod([string]$modDir, [string]$prefix) {
    $sep = [IO.Path]::DirectorySeparatorChar
    foreach ($name in @("About${sep}About.xml", 'LoadFolders.xml')) {
        $f = Join-Path $modDir $name
        if (Test-Path -LiteralPath $f) { Copy-Into $f (Join-Path $prefix $name) }
    }
    $xml = 0; $dll = 0; $done = @{}
    foreach ($folder in Get-LoadFolders $modDir) {
        $base = $modDir
        foreach ($part in ($folder -split '[\\/]' | Where-Object { $_ })) { $base = Get-Child $base $part }
        if ($done.ContainsKey($base.ToLowerInvariant())) { continue }
        $done[$base.ToLowerInvariant()] = $true
        $xml += Copy-Files (Get-Child $base 'Defs') $modDir $prefix @('*.xml')
        $xml += Copy-Files (Get-Child $base 'Patches') $modDir $prefix @('*.xml')
        $dll += Copy-Files (Get-Child $base 'Assemblies') $modDir $prefix @('*.dll')
    }
    $cs = 0
    foreach ($d in Get-ChildItem -LiteralPath $modDir -Recurse -Directory -Filter 'Source' -ErrorAction SilentlyContinue) {
        $cs += Copy-Files $d.FullName $modDir $prefix @('*.cs')
    }
    return @($xml, $dll, $cs)
}

# The zip, with forward slashes in its entry names (Windows PowerShell's own zipping writes backslashes, which other
# systems read as part of a file's name).
function Write-Zip([string]$from, [string]$zip) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $base = (Get-Item -LiteralPath $from).FullName.TrimEnd('\', '/')
    $fs = [IO.File]::Open($zip, [IO.FileMode]::Create)
    $archive = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($f in Get-ChildItem -LiteralPath $from -Recurse -File) {
            $name = $f.FullName.Substring($base.Length).TrimStart('\', '/') -replace '\\', '/'
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $f.FullName, $name, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally {
        $archive.Dispose()
        $fs.Dispose()
    }
}

$sources = @()
$local = Join-Path $rimworld 'Mods'
if (Test-Path -LiteralPath $local) { $sources += $local }
if (-not $Workshop) {
    $steamapps = Split-Path -Path (Split-Path -Path $rimworld -Parent) -Parent
    $Workshop = Join-Path (Join-Path (Join-Path $steamapps 'workshop') 'content') '294100'
}
if (Test-Path -LiteralPath $Workshop) { $sources += $Workshop }
Write-Host ("Looking in: " + ($sources -join ', '))

$lines = @("folder`tpackageId`tname`tdefsAndPatches`tdlls`tsource")
$missing = @()
$ids = @{}
foreach ($id in $wanted.Keys) {
    $found = $null
    foreach ($source in $sources) {
        $dir = Join-Path $source $id
        if (Test-Path -LiteralPath $dir) { $found = $dir; break }
    }
    if (-not $found) { $missing += "$id ($($wanted[$id]))"; continue }
    $pkg = ''
    try {
        [xml]$a = Get-Content -LiteralPath (Join-Path (Join-Path $found 'About') 'About.xml') -Raw
        $node = $a.DocumentElement.SelectSingleNode('packageId')
        if ($node -and $node.InnerText) { $pkg = $node.InnerText.Trim() }
    } catch { }
    if ($pkg) { $ids[$pkg.ToLowerInvariant()] = $true }
    $counts = Copy-Mod $found (Join-Path 'Mods' $id)
    $lines += "$id`t$pkg`t$($wanted[$id])`t$($counts[0])`t$($counts[1])`t$($counts[2])"
    Write-Host ("  {0,-38} {1} XML, {2} DLLs" -f $wanted[$id], $counts[0], $counts[1])
}
$lines | Set-Content -LiteralPath (Join-Path $work 'mods.txt') -Encoding UTF8

# Your mod list, and these mods' own settings (Mod_*.xml whose name carries their package id or their mod class).
if (-not $Config) { $Config = Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config' }
$modsConfig = Join-Path $Config 'ModsConfig.xml'
if (Test-Path -LiteralPath $modsConfig) { Copy-Into $modsConfig 'ModsConfig.xml' } else { Write-Host 'ModsConfig.xml was not found; the zip has the mods but not whether they are enabled.' }
foreach ($f in Get-ChildItem -LiteralPath $Config -Filter 'Mod_*.xml' -File -ErrorAction SilentlyContinue) {
    $n = $f.Name.ToLowerInvariant()
    if ($ids.Keys | Where-Object { $n.Contains($_) }) { Copy-Into $f.FullName (Join-Path 'Config' $f.Name); continue }
    if ($n -match 'quiet|wonder|living|water|wave|weather|wildcrop|crop|biome|rimkeeper|keeper|procreat|animaltrait|trait|atmosph|sky') { Copy-Into $f.FullName (Join-Path 'Config' $f.Name) }
}

if (-not $OutDir) { $OutDir = [Environment]::GetFolderPath('Desktop') }
$zip = Join-Path $OutDir 'RimfolkNatureMods.zip'
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Write-Zip $work $zip
Remove-Item -LiteralPath $work -Recurse -Force
$size = [math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 1)
Write-Host ""
if ($missing.Count -gt 0) { Write-Host ("Not found (not subscribed yet, or elsewhere): " + ($missing -join ', ')) }
if ($script:skipped -gt 0) { Write-Host "Left out: $($script:skipped) files Windows could not copy (a path too long or a file in use)." }
Write-Host "Done: $size MB."
Write-Host "Upload this file: $zip"
if ($env:OS -eq 'Windows_NT') { Start-Process explorer.exe -ArgumentList "/select,`"$zip`"" }
if (-not $NoWait) { Read-Host 'Press Enter to close' | Out-Null }
