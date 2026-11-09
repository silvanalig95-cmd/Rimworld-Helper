# Collect-ModCode.ps1, for Rimfolk.
#
# Collects the code files (DLLs) of the mods whose weapons, armour and gear work through their own code, and of the
# weapon and inventory mods Rimfolk fits itself to (Simple Sidearms, Dual Wield, Nice Inventory Tab, RunAndGun, Melee
# Animation and others), plus those mods' settings and the game's own Assembly-CSharp.dll, so Rimfolk can rate every
# weapon and piece of armour by what it really does. Only the folders RimWorld 1.7 loads. Nothing is changed: it only
# reads, and writes one zip, RimfolkModCode.zip, to your Desktop. Upload that zip.
#
# Run it: right-click this file and choose "Run with PowerShell". It finds RimWorld through the folder the Rimfolk
# installer remembered, or through Steam; if neither works it asks for the RimWorld folder.

$ErrorActionPreference = 'Stop'

# The mods, by Steam Workshop id.
$wanted = [ordered]@{
    # Fitted to: the weapon and inventory mods.
    '927155256'  = 'Simple sidearms'
    '3630049419' = 'Dual Wield - Continued'
    '3609897594' = 'Nice Inventory Tab'
    '3562365100' = 'RunAndGun - Continued'
    '2944488802' = 'Melee Animation'
    '3707263149' = 'Unique Weapons Unbound'
    '2982391372' = 'Possessed Weapons'
    # Weapons and armour that work through their own code.
    '2023507013' = 'Vanilla Expanded Framework'
    '1201382956' = 'A RimWorld of Magic'
    '2826922787' = 'Vanilla Persona Weapons Expanded'
    '3287469135' = 'Vanilla Weapons Expanded - Bioferrite'
    '3444347874' = 'Vanilla Factions Expanded - Medieval 2'
    '1845154007' = 'Vanilla Furniture Expanded - Security'
    '2723801948' = 'Vanilla Factions Expanded - Pirates'
    '3025493377' = 'Vanilla Factions Expanded - Deserters'
    '3079786283' = 'Vanilla Factions Expanded - Tribals'
    '2052918119' = 'Vanilla Factions Expanded - Settlers'
    '2787850474' = 'Vanilla Factions Expanded - Classical'
    '2938820380' = 'Vanilla Factions Expanded - Empire'
    '3609835606' = 'Vanilla Gravship Expanded - Chapter 1'
    '3799737423' = 'Vanilla Gravship Expanded - Chapter 2'
    '1127530465' = 'Dubs Rimatomics'
    '3497226454' = 'Vanilla Quests Expanded - Deadlife'
    '3461526070' = 'Vanilla Quests Expanded - Cryptoforge'
    '2973169158' = 'Alpha Mechs'
    '2891845502' = 'Alpha Genes'
    '3812921116' = 'Fairycore - Carillon'
    '3814383460' = 'Fairycore - Magic Wand'
    '3812924898' = 'Fairycore - Apparel 2'
    '3140248688' = "Det's Xenotypes - Venators"
    '3112549163' = 'EBSG Framework'
    '3067715093' = 'Vanilla Races Expanded - Archon'
    '1541287769' = 'Field Medic'
    '2521176396' = 'Vanilla Apparel Expanded - Accessories'
    '2842502659' = 'Vanilla Psycasts Expanded'
    '736139206'  = 'Rimsenal - Security Pack'
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

$rimworld = Find-RimWorld
while (-not (Test-RimWorld $rimworld)) {
    $rimworld = (Read-Host 'Where is RimWorld? Paste the folder with RimWorld.exe in it').Trim('"', ' ')
}
Write-Host "RimWorld: $rimworld"

$work = Join-Path ([IO.Path]::GetTempPath()) 'RimfolkModCode'
if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Path $work | Out-Null

function Copy-Into([string]$from, [string]$relative) {
    $target = Join-Path $work $relative
    New-Item -ItemType Directory -Path (Split-Path -Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $from -Destination $target
}

# The folders of a mod that RimWorld 1.7 loads: those its LoadFolders.xml names for 1.7 (else for the newest version
# before it), else its root, Common and its newest version folder up to 1.7.
function Get-LoadFolders([string]$modDir) {
    $folders = @('')
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
                foreach ($li in $best.SelectNodes('li')) { $list += ($li.InnerText.Trim().Trim('/', '\') -replace '/', '\') }
                if ($list.Count -gt 0) { return $list }
            }
        } catch { }
    }
    $folders += 'Common'
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

# A mod's DLLs in the folders 1.7 loads (Assemblies), its About.xml and LoadFolders.xml, and its source code if it
# ships any (a Source folder).
function Copy-Mod([string]$modDir, [string]$prefix) {
    $n = 0
    foreach ($name in @('About\About.xml', 'LoadFolders.xml')) {
        $f = Join-Path $modDir $name
        if (Test-Path -LiteralPath $f) { Copy-Into $f (Join-Path $prefix $name) }
    }
    foreach ($folder in Get-LoadFolders $modDir) {
        $base = if ($folder) { Join-Path $modDir $folder } else { $modDir }
        $dir = Join-Path $base 'Assemblies'
        if (-not (Test-Path -LiteralPath $dir)) { continue }
        foreach ($f in Get-ChildItem -LiteralPath $dir -Recurse -Filter '*.dll' -File -ErrorAction SilentlyContinue) {
            Copy-Into $f.FullName (Join-Path $prefix $f.FullName.Substring($modDir.Length).TrimStart('\', '/'))
            $n++
        }
    }
    foreach ($d in Get-ChildItem -LiteralPath $modDir -Recurse -Directory -Filter 'Source' -ErrorAction SilentlyContinue) {
        foreach ($f in Get-ChildItem -LiteralPath $d.FullName -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue) {
            Copy-Into $f.FullName (Join-Path $prefix $f.FullName.Substring($modDir.Length).TrimStart('\', '/'))
        }
    }
    return $n
}

$sources = @()
$local = Join-Path $rimworld 'Mods'
if (Test-Path -LiteralPath $local) { $sources += $local }
$steamapps = Split-Path -Path (Split-Path -Path $rimworld -Parent) -Parent
$workshop = Join-Path (Join-Path (Join-Path $steamapps 'workshop') 'content') '294100'
if (Test-Path -LiteralPath $workshop) { $sources += $workshop }
Write-Host ("Looking in: " + ($sources -join ', '))

$lines = @("folder`tname`tdlls")
$missing = @()
foreach ($id in $wanted.Keys) {
    $found = $null
    foreach ($source in $sources) {
        $dir = Join-Path $source $id
        if (Test-Path -LiteralPath $dir) { $found = $dir; break }
    }
    if (-not $found) { $missing += "$id ($($wanted[$id]))"; continue }
    $n = Copy-Mod $found (Join-Path 'Mods' $id)
    $lines += "$id`t$($wanted[$id])`t$n"
    Write-Host ("  {0,-45} {1} DLLs" -f $wanted[$id], $n)
}
$lines | Set-Content -LiteralPath (Join-Path $work 'mods.txt') -Encoding UTF8

# The game's own code, to check 1.7's combat rules.
$game = Join-Path $rimworld 'RimWorldWin64_Data\Managed\Assembly-CSharp.dll'
if (Test-Path -LiteralPath $game) { Copy-Into $game 'Game\Assembly-CSharp.dll' } else { Write-Host 'The game''s Assembly-CSharp.dll was not found; the zip has the mods only.' }

# The mods' settings (Mod_*.xml in the game's config folder): which weapons Dual Wield allows in the offhand and its
# penalties, Simple Sidearms' limits, and the like.
$configDir = Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config'
if (Test-Path -LiteralPath $configDir) {
    foreach ($f in Get-ChildItem -LiteralPath $configDir -Filter 'Mod_*.xml' -File -ErrorAction SilentlyContinue) {
        Copy-Into $f.FullName (Join-Path 'Config' $f.Name)
    }
}

$desktop = [Environment]::GetFolderPath('Desktop')
$zip = Join-Path $desktop 'RimfolkModCode.zip'
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $work '*') -DestinationPath $zip -CompressionLevel Optimal
Remove-Item -LiteralPath $work -Recurse -Force
$size = [math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 1)
Write-Host ""
if ($missing.Count -gt 0) { Write-Host ("Not found (not subscribed or elsewhere): " + ($missing -join ', ')) }
Write-Host "Done: $size MB."
Write-Host "Upload this file: $zip"
Read-Host 'Press Enter to close'
