# Make-PortraitPack.ps1, for Rimfolk's portraits.
#
# Builds the local mod "Rimfolk Portraits" from the pictures you chose. Put the pictures you want in the game into
# picks\<batch>\ next to this script (picks\colonists\, picks\backdrops\ ...: the batch's name, as in batches\<batch>.csv),
# with the names ComfyUI gave them. Each picture is tied to its row in batches\<batch>.csv by the start of its name, and
# the row's record columns say what it shows (for people: kind, race, sex, age, skin, hairColour, hair, beard, build, era,
# dress, features, marks, facing, pawn; for backdrops: place, rooms, outdoors, era, contents). The script writes:
#   <RimWorld>\Mods\Rimfolk Portraits\About\About.xml
#   <RimWorld>\Mods\Rimfolk Portraits\Defs\PortraitDefs.xml and BackdropDefs.xml (one record per picture)
#   <RimWorld>\Mods\Rimfolk Portraits\Portraits\<batch>\<picture>.png (the pictures, copied as they are)
# Each run builds the pack again from picks\, so to take a picture out, delete it from picks\ and run this again. Nothing
# outside the pack's own folder is changed. The RimWorld Mods folder is asked once and remembered in pack-settings.json.
#
# Run it: right-click this file, "Run with PowerShell". Then enable "Rimfolk Portraits" in RimWorld's mod list, below
# Rimfolk. See docs/portrait-art/SETUP-ComfyUI.md, step 7.

[CmdletBinding()]
param(
    [string] $Folder = '',
    [string] $ModsFolder = '',
    [string] $PackName = 'Rimfolk Portraits'
)

$ErrorActionPreference = 'Stop'
if ($Folder -eq '') { $Folder = Split-Path -Parent $PSCommandPath }
$batches = Join-Path $Folder 'batches'
$picks = Join-Path $Folder 'picks'
$settingsFile = Join-Path $Folder 'pack-settings.json'
$PackageId = 'rimfolk.portraits.local'

function Has($o, [string] $name) { return ($o -ne $null) -and ($o.PSObject.Properties.Name -contains $name) }
function Esc([string] $s) { return [System.Security.SecurityElement]::Escape($s) }
function Clean([string] $s) { return ($s -replace '[^A-Za-z0-9_\-]', '_') }
function Words([string] $s) { return @(($s -split '[;,]') | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }) }
function Field($row, [string] $name) { if (Has $row $name) { return ([string]$row.$name).Trim() } else { return '' } }

function Find-Mods {
    # Remembered, else RimWorld's usual places in Steam libraries on any drive, else asked.
    if (Test-Path -LiteralPath $settingsFile) {
        $s = Get-Content -LiteralPath $settingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ((Has $s 'mods') -and $s.mods -and (Test-Path -LiteralPath $s.mods)) { return [string]$s.mods }
    }
    $tries = New-Object System.Collections.ArrayList
    [void]$tries.Add('C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods')
    [void]$tries.Add('C:\Program Files\Steam\steamapps\common\RimWorld\Mods')
    foreach ($d in [System.IO.DriveInfo]::GetDrives()) {
        if ($d.DriveType -ne 'Fixed') { continue }
        foreach ($p in @('SteamLibrary\steamapps\common\RimWorld\Mods', 'Steam\steamapps\common\RimWorld\Mods', 'Games\RimWorld\Mods', 'RimWorld\Mods')) {
            [void]$tries.Add((Join-Path $d.RootDirectory.FullName $p))
        }
    }
    foreach ($t in $tries) {
        if (Test-Path -LiteralPath $t) {
            Write-Host ("Found RimWorld's Mods folder: {0}" -f $t)
            $a = Read-Host 'Use it? (Enter for yes, or type another path)'
            if ($a -eq '') { return $t }
            return $a.Trim('"', ' ')
        }
    }
    Write-Host "RimWorld's Mods folder wasn't found by itself (Steam: right-click RimWorld, Manage, Browse local files; it holds a Mods folder)."
    return (Read-Host 'Path of the Mods folder').Trim('"', ' ')
}

function Read-Rows([string] $batch) {
    $csv = Join-Path $batches ($batch + '.csv')
    if (-not (Test-Path -LiteralPath $csv)) { return $null }
    $byName = @{}
    foreach ($r in @(Import-Csv -LiteralPath $csv -Encoding UTF8)) {
        if ([string]::IsNullOrWhiteSpace($r.name)) { continue }
        $byName[(Clean $r.name)] = $r
    }
    return $byName
}

function Row-For([string] $file, $rows) {
    # ComfyUI's names: <name>_<style>_<NN>[_cut]_<counter>_.png; the name itself may have underscores (room_bedroom_tribal).
    $base = [System.IO.Path]::GetFileNameWithoutExtension($file)
    $m = [regex]::Match($base, '^(?<name>.+?)_(?<style>style[^_]*)_(?<n>\d+)(?<cut>_cut)?(_\d+_?)?$')
    if (-not $m.Success) { return $null }
    $name = $m.Groups['name'].Value
    if (-not $rows.ContainsKey($name)) { return $null }
    return [pscustomobject]@{ row = $rows[$name]; name = $name; n = $m.Groups['n'].Value }
}

function List-Xml([string] $tag, [string[]] $items) {
    if ($items.Count -eq 0) { return '' }
    return ('    <{0}>{1}</{0}>' -f $tag, (($items | ForEach-Object { '<li>' + (Esc $_) + '</li>' }) -join ''))
}

function One-Xml([string] $tag, [string] $value) {
    if ($value -eq '') { return '' }
    return ('    <{0}>{1}</{0}>' -f $tag, (Esc $value))
}

try {
    if (-not (Test-Path -LiteralPath $picks)) {
        New-Item -ItemType Directory -Force -Path (Join-Path $picks 'colonists'), (Join-Path $picks 'backdrops') | Out-Null
        throw ('Made the folder {0} with colonists and backdrops in it: copy the pictures you want in the game there, then run this again.' -f $picks)
    }
    if ($ModsFolder -eq '') { $ModsFolder = Find-Mods }
    if (-not (Test-Path -LiteralPath $ModsFolder)) { throw "The folder $ModsFolder doesn't exist." }
    @{ mods = $ModsFolder } | ConvertTo-Json | Set-Content -LiteralPath $settingsFile -Encoding UTF8

    $pack = Join-Path $ModsFolder $PackName
    $about = Join-Path $pack 'About\About.xml'
    if ((Test-Path -LiteralPath $pack) -and -not ((Test-Path -LiteralPath $about) -and ((Get-Content -LiteralPath $about -Raw) -match [regex]::Escape($PackageId)))) {
        throw ("A folder {0} is there already and isn't this pack: rename or move it first." -f $pack)
    }

    $people = New-Object System.Collections.ArrayList
    $backs = New-Object System.Collections.ArrayList
    $copies = New-Object System.Collections.ArrayList
    $problems = New-Object System.Collections.ArrayList
    $names = @{}
    foreach ($dir in @(Get-ChildItem -LiteralPath $picks -Directory)) {
        $batch = $dir.Name
        $rows = Read-Rows $batch
        if ($rows -eq $null) { [void]$problems.Add(("picks\{0}: no batches\{0}.csv, skipped" -f $batch)); continue }
        foreach ($f in @(Get-ChildItem -LiteralPath $dir.FullName -Filter '*.png' -File)) {
            $hit = Row-For $f.Name $rows
            if ($hit -eq $null) { [void]$problems.Add(("picks\{0}\{1}: no row in {0}.csv for this name, skipped" -f $batch, $f.Name)); continue }
            $r = $hit.row
            $tex = (Clean $batch) + '/' + [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
            $isBack = (Field $r 'place') -ne ''
            $stem = $(if ($isBack) { 'Backdrop_' } else { 'Portrait_' }) + (Clean $batch) + '_' + $hit.name + '_' + $hit.n
            $defName = $stem; $k = 2
            while ($names.ContainsKey($defName)) { $defName = $stem + '_' + $k; $k++ }
            $names[$defName] = $true
            [void]$copies.Add([pscustomobject]@{ from = $f.FullName; to = (Join-Path (Join-Path (Join-Path $pack 'Portraits') (Clean $batch)) $f.Name) })
            $lines = New-Object System.Collections.ArrayList
            if ($isBack) {
                [void]$lines.Add('  <ColonyCouncil.BackdropDef>')
                [void]$lines.Add((One-Xml 'defName' $defName)); [void]$lines.Add((One-Xml 'texPath' $tex))
                [void]$lines.Add((One-Xml 'place' (Field $r 'place')))
                [void]$lines.Add((List-Xml 'rooms' (Words (Field $r 'rooms'))))
                [void]$lines.Add((One-Xml 'outdoors' (Field $r 'outdoors')))
                [void]$lines.Add((One-Xml 'era' (Field $r 'era')))
                [void]$lines.Add((List-Xml 'contents' (Words (Field $r 'contents'))))
                [void]$lines.Add('  </ColonyCouncil.BackdropDef>')
                [void]$backs.Add((($lines | Where-Object { $_ -ne '' }) -join "`r`n"))
            }
            else {
                if ((Field $r 'sex') -eq '') { [void]$problems.Add(("picks\{0}\{1}: its row has no record (sex, age ...), skipped" -f $batch, $f.Name)); $copies.RemoveAt($copies.Count - 1); continue }
                $kind = Field $r 'kind'
                $xenos = @(); if ($kind -ne '' -and $kind -ne 'Baseliner') { $xenos = @($kind) }
                [void]$lines.Add('  <ColonyCouncil.PortraitDef>')
                [void]$lines.Add((One-Xml 'defName' $defName)); [void]$lines.Add((One-Xml 'texPath' $tex))
                [void]$lines.Add((One-Xml 'pawn' (Field $r 'pawn')))
                [void]$lines.Add((List-Xml 'xenotypes' $xenos))
                [void]$lines.Add((List-Xml 'races' (Words (Field $r 'race'))))
                [void]$lines.Add((One-Xml 'sex' (Field $r 'sex'))); [void]$lines.Add((One-Xml 'age' (Field $r 'age')))
                [void]$lines.Add((One-Xml 'skinColor' (Field $r 'skin'))); [void]$lines.Add((One-Xml 'hairColor' (Field $r 'hairColour')))
                [void]$lines.Add((One-Xml 'hair' (Field $r 'hair'))); [void]$lines.Add((One-Xml 'beard' (Field $r 'beard')))
                [void]$lines.Add((One-Xml 'build' (Field $r 'build'))); [void]$lines.Add((One-Xml 'era' (Field $r 'era')))
                [void]$lines.Add((One-Xml 'dress' (Field $r 'dress')))
                [void]$lines.Add((List-Xml 'features' (Words (Field $r 'features'))))
                [void]$lines.Add((List-Xml 'marks' (Words (Field $r 'marks'))))
                [void]$lines.Add((One-Xml 'facing' (Field $r 'facing')))
                [void]$lines.Add((One-Xml 'character' ((Clean $batch) + '_' + $hit.name)))
                [void]$lines.Add('  </ColonyCouncil.PortraitDef>')
                [void]$people.Add((($lines | Where-Object { $_ -ne '' }) -join "`r`n"))
            }
        }
    }
    if ($copies.Count -eq 0) { throw ('No pictures to pack in {0}.' -f $picks) }

    # Build the pack again: its Defs and Portraits are replaced, nothing else in the Mods folder is touched.
    foreach ($sub in @('Defs', 'Portraits')) {
        $p = Join-Path $pack $sub
        if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force }
    }
    New-Item -ItemType Directory -Force -Path (Join-Path $pack 'About'), (Join-Path $pack 'Defs') | Out-Null
    $aboutXml = @"
<?xml version="1.0" encoding="utf-8"?>
<ModMetaData>
  <name>$(Esc $PackName)</name>
  <author>The player, with Rimfolk's portrait kit</author>
  <packageId>$PackageId</packageId>
  <supportedVersions>
    <li>1.6</li>
    <li>1.7</li>
  </supportedVersions>
  <loadAfter>
    <li>silvanalig.colonycouncil</li>
  </loadAfter>
  <description>The portraits and backdrops made for Rimfolk on this computer. Built by Make-PortraitPack.ps1 from the chosen pictures; it does nothing without Rimfolk.</description>
</ModMetaData>
"@
    [System.IO.File]::WriteAllText($about, $aboutXml, (New-Object System.Text.UTF8Encoding($false)))
    $head = '<?xml version="1.0" encoding="utf-8"?>' + "`r`n" + '<Defs>' + "`r`n"
    $tail = "`r`n" + '</Defs>' + "`r`n"
    if ($people.Count -gt 0) { [System.IO.File]::WriteAllText((Join-Path $pack 'Defs\PortraitDefs.xml'), $head + ($people -join "`r`n") + $tail, (New-Object System.Text.UTF8Encoding($false))) }
    if ($backs.Count -gt 0) { [System.IO.File]::WriteAllText((Join-Path $pack 'Defs\BackdropDefs.xml'), $head + ($backs -join "`r`n") + $tail, (New-Object System.Text.UTF8Encoding($false))) }
    foreach ($c in $copies) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $c.to) | Out-Null
        Copy-Item -LiteralPath $c.from -Destination $c.to -Force
    }

    Write-Host ''
    Write-Host ('Built {0}: {1} portraits and {2} backdrops.' -f $pack, $people.Count, $backs.Count) -ForegroundColor Green
    foreach ($p in $problems) { Write-Host ('  Note: ' + $p) -ForegroundColor Yellow }
    Write-Host 'In RimWorld: enable "Rimfolk Portraits" in the mod list, below Rimfolk (once), then load your game.'
}
catch {
    Write-Host ''
    Write-Host ('Something went wrong: ' + $_.Exception.Message) -ForegroundColor Red
}
Write-Host ''
Read-Host 'Press Enter to close'
