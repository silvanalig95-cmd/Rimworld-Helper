# Collect-ModDefs.ps1, for Rimfolk.
#
# Collects what Rimfolk's catalogue of your mods (docs/player-mods) is built from: the game's own defs and the DLCs',
# every subscribed and local mod's About.xml, LoadFolders.xml and Defs (only the folders RimWorld 1.7 loads), the list
# of mod folders (mods.txt) and your mod list (ModsConfig.xml). Nothing is changed: it only reads, and writes one zip,
# RimfolkModDefs.zip, to your Desktop, then opens the Desktop with the zip selected. Upload that zip, with your latest
# save, in the chat.
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

$work = Join-Path ([IO.Path]::GetTempPath()) 'RimfolkModDefs'
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

# Every .xml file under a folder, kept at its place below the mod (or the game's data folder).
function Copy-Xml([string]$dir, [string]$baseDir, [string]$prefix) {
    $n = 0
    if (-not (Test-Path -LiteralPath $dir)) { return 0 }
    foreach ($f in Get-ChildItem -LiteralPath $dir -Recurse -Filter '*.xml' -File -ErrorAction SilentlyContinue) {
        Copy-Into $f.FullName (Join-Path $prefix $f.FullName.Substring($baseDir.Length).TrimStart('\', '/'))
        $n++
    }
    return $n
}

# The folders of a mod that RimWorld 1.7 loads: all those its LoadFolders.xml names for 1.7 (else for the newest version
# before it; whether each one's mod condition holds is worked out later against your mod list), else its root, Common
# and its newest version folder up to 1.7.
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

# A folder below a mod by name, matching the name's case as Windows does (LoadFolders.xml may write "defs" for "Defs").
function Get-Child([string]$base, [string]$name) {
    if (-not $name) { return $base }
    $p = Join-Path $base $name
    if (Test-Path -LiteralPath $p) { return $p }
    $match = Get-ChildItem -LiteralPath $base -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -ieq $name } | Select-Object -First 1
    if ($match) { return $match.FullName }
    return $p
}

# A mod's About.xml, LoadFolders.xml and the Defs of each folder 1.7 loads. Returns the number of def files.
function Copy-Mod([string]$modDir, [string]$prefix) {
    foreach ($name in @('About\About.xml', 'LoadFolders.xml')) {
        $f = Join-Path $modDir ($name -replace '\\', [IO.Path]::DirectorySeparatorChar)
        if (Test-Path -LiteralPath $f) { Copy-Into $f (Join-Path $prefix ($name -replace '\\', [IO.Path]::DirectorySeparatorChar)) }
    }
    $n = 0
    $done = @{}
    foreach ($folder in Get-LoadFolders $modDir) {
        $base = $modDir
        foreach ($part in ($folder -split '[\\/]' | Where-Object { $_ })) { $base = Get-Child $base $part }
        $defs = Get-Child $base 'Defs'
        if ($done.ContainsKey($defs.ToLowerInvariant())) { continue }
        $done[$defs.ToLowerInvariant()] = $true
        $n += Copy-Xml $defs $modDir $prefix
    }
    return $n
}

# The game's own defs and the DLCs' (Data\Core, Data\Royalty and the others).
$data = Join-Path $rimworld 'Data'
$gameDefs = 0
foreach ($d in Get-ChildItem -LiteralPath $data -Directory -ErrorAction SilentlyContinue) {
    $gameDefs += Copy-Xml (Join-Path $d.FullName 'Defs') $data 'Data'
}
Write-Host "The game and its DLCs: $gameDefs def files"

# Every mod: the game's Mods folder and Steam's workshop folder for RimWorld.
$sources = @()
$local = Join-Path $rimworld 'Mods'
if (Test-Path -LiteralPath $local) { $sources += $local }
if (-not $Workshop) {
    $steamapps = Split-Path -Path (Split-Path -Path $rimworld -Parent) -Parent
    $Workshop = Join-Path (Join-Path (Join-Path $steamapps 'workshop') 'content') '294100'
}
if (Test-Path -LiteralPath $Workshop) { $sources += $Workshop }
Write-Host ("Looking in: " + ($sources -join ', '))

$lines = @("folder`tpackageId`tname`tdefFiles")
$count = 0
foreach ($source in $sources) {
    foreach ($mod in Get-ChildItem -LiteralPath $source -Directory -ErrorAction SilentlyContinue) {
        $about = Join-Path (Join-Path $mod.FullName 'About') 'About.xml'
        if (-not (Test-Path -LiteralPath $about)) { continue }
        $id = ''; $name = $mod.Name
        try {
            [xml]$a = Get-Content -LiteralPath $about -Raw
            $idNode = $a.DocumentElement.SelectSingleNode('packageId')
            $nm = $a.DocumentElement.SelectSingleNode('name')
            if ($idNode -and $idNode.InnerText) { $id = $idNode.InnerText.Trim() }
            if ($nm -and $nm.InnerText) { $name = $nm.InnerText.Trim() }
        } catch { }
        $n = Copy-Mod $mod.FullName (Join-Path 'Mods' $mod.Name)
        $lines += "$($mod.Name)`t$id`t$($name -replace "[`t`r`n]", ' ')`t$n"
        $count++
        if ($count % 25 -eq 0) { Write-Host "  $count mods so far" }
    }
}
$lines | Set-Content -LiteralPath (Join-Path $work 'mods.txt') -Encoding UTF8
Write-Host "Mods: $count"

# Your mod list: which mods are enabled, in their load order.
if (-not $Config) { $Config = Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config' }
$modsConfig = Join-Path $Config 'ModsConfig.xml'
if (Test-Path -LiteralPath $modsConfig) { Copy-Into $modsConfig 'ModsConfig.xml' } else { Write-Host 'ModsConfig.xml was not found; the zip has every mod but not which are enabled.' }

if (-not $OutDir) { $OutDir = [Environment]::GetFolderPath('Desktop') }
$zip = Join-Path $OutDir 'RimfolkModDefs.zip'
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($work, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
Remove-Item -LiteralPath $work -Recurse -Force
$size = [math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 1)
Write-Host ""
if ($script:skipped -gt 0) { Write-Host "Left out: $($script:skipped) files Windows could not copy (a path too long or a file in use)." }
Write-Host "Done: $size MB."
Write-Host "Upload this file, with your latest save: $zip"
if ($env:OS -eq 'Windows_NT') { Start-Process explorer.exe -ArgumentList "/select,`"$zip`"" }
if (-not $NoWait) { Read-Host 'Press Enter to close' | Out-Null }
