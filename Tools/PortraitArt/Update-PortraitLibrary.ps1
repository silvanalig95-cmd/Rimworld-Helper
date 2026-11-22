# Update-PortraitLibrary.ps1, for Rimfolk's portraits.
#
# Rimfolk's picture library is the folder library\ next to this script: one folder per batch (library\colonists\,
# library\backdrops\ ...: the batch's name, as in batches\<batch>.csv) holding the pictures you chose, with the names
# ComfyUI gave them. This script writes library\records.xml: one record per picture, saying what it shows, taken from
# the picture's row in batches\<batch>.csv (tied by the start of its name; the row's record columns: for people kind,
# race, sex, age, skin, hairColour, hair, beard, build, era, dress, features, marks, looks, role, facing, pawn; for backdrops place,
# rooms, outdoors, era, contents). Rimfolk reads records.xml and the pictures straight from this folder (its setting
# "Portrait library folder"), so there is no second mod to install, and keeping this whole folder in a synced folder
# (Google Drive) keeps both computers the same.
#
# Run it after adding or removing pictures: right-click it, "Run with PowerShell". Then "Reload the library" in Rimfolk's
# settings (or restart the game). Nothing outside library\records.xml is changed. If Rimfolk has written
# library\missing.csv (what the game needed and the library didn't have), its first lines are shown at the end.
# See docs/portrait-art/SETUP-ComfyUI.md, step 7.

[CmdletBinding()]
param(
    [string] $Folder = '',
    [switch] $NoPause
)

$ErrorActionPreference = 'Stop'
if ($Folder -eq '') { $Folder = Split-Path -Parent $PSCommandPath }
$batches = Join-Path $Folder 'batches'
$library = Join-Path $Folder 'library'

function Has($o, [string] $name) { return ($o -ne $null) -and ($o.PSObject.Properties.Name -contains $name) }
function Esc([string] $s) { return [System.Security.SecurityElement]::Escape($s) }
function Clean([string] $s) { return ($s -replace '[^A-Za-z0-9_\-]', '_') }
function Words([string] $s) { return @(($s -split '[;,]') | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }) }
function Field($row, [string] $name) { if (Has $row $name) { return ([string]$row.$name).Trim() } else { return '' } }

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
    # ComfyUI's names: <name>_<style>_[<run stamp>_]<NN>[_cut]_<counter>_.png; the name itself may have underscores
    # (room_bedroom_tribal). A copy Windows renamed ("... (2).png", "... - Copy.png") still counts as its picture.
    $base = [System.IO.Path]::GetFileNameWithoutExtension($file)
    $base = ($base -replace '\s*\(\d+\)$', '') -replace '\s*-\s*Copy( \(\d+\))?$', ''
    $m = [regex]::Match($base, '^(?<name>.+?)_(?<style>style[^_]*)_(?:(?<run>\d{6}-\d{4,6})_)?(?<n>\d+)(?<cut>_cut)?(_\d+_?)?$')
    if (-not $m.Success) { return $null }
    $name = $m.Groups['name'].Value
    if (-not $rows.ContainsKey($name)) { return $null }
    $n = $m.Groups['n'].Value
    if ($m.Groups['run'].Success) { $n = ($m.Groups['run'].Value -replace '-', '') + '_' + $n }
    return [pscustomobject]@{ row = $rows[$name]; name = $name; n = $n }
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
    if (-not (Test-Path -LiteralPath $library)) {
        New-Item -ItemType Directory -Force -Path (Join-Path $library 'colonists'), (Join-Path $library 'backdrops') | Out-Null
        throw ('Made the library folder {0} with colonists and backdrops in it: copy the pictures you chose there, then run this again.' -f $library)
    }
    $people = New-Object System.Collections.ArrayList
    $backs = New-Object System.Collections.ArrayList
    $problems = New-Object System.Collections.ArrayList
    $names = @{}
    foreach ($dir in @(Get-ChildItem -LiteralPath $library -Directory)) {
        $batch = $dir.Name
        $rows = Read-Rows $batch
        if ($rows -eq $null) { [void]$problems.Add(("library\{0}: no batches\{0}.csv, left out" -f $batch)); continue }
        foreach ($f in @(Get-ChildItem -LiteralPath $dir.FullName -Filter '*.png' -File)) {
            $hit = Row-For $f.Name $rows
            if ($hit -eq $null) { [void]$problems.Add(("library\{0}\{1}: no row in {0}.csv for this name, left out" -f $batch, $f.Name)); continue }
            $r = $hit.row
            $tex = $batch + '/' + [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
            $isBack = (Field $r 'place') -ne ''
            $stem = $(if ($isBack) { 'Backdrop_' } else { 'Portrait_' }) + (Clean $batch) + '_' + $hit.name + '_' + $hit.n
            $defName = $stem; $k = 2
            while ($names.ContainsKey($defName)) { $defName = $stem + '_' + $k; $k++ }
            $names[$defName] = $true
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
                if ((Field $r 'sex') -eq '') { [void]$problems.Add(("library\{0}\{1}: its row has no record (sex, age ...), left out" -f $batch, $f.Name)); continue }
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
                [void]$lines.Add((List-Xml 'looks' (Words (Field $r 'looks'))))
                [void]$lines.Add((One-Xml 'role' (Field $r 'role')))
                [void]$lines.Add((One-Xml 'facing' (Field $r 'facing')))
                [void]$lines.Add((One-Xml 'character' ((Clean $batch) + '_' + $hit.name)))
                [void]$lines.Add('  </ColonyCouncil.PortraitDef>')
                [void]$people.Add((($lines | Where-Object { $_ -ne '' }) -join "`r`n"))
            }
        }
    }
    $all = @($people) + @($backs)
    $xml = '<?xml version="1.0" encoding="utf-8"?>' + "`r`n" + '<!-- Rimfolk portrait library, written by Update-PortraitLibrary.ps1: one record per picture in this folder. -->' + "`r`n" + '<Defs>' + "`r`n" + ($all -join "`r`n") + "`r`n" + '</Defs>' + "`r`n"
    [System.IO.File]::WriteAllText((Join-Path $library 'records.xml'), $xml, (New-Object System.Text.UTF8Encoding($false)))

    Write-Host ''
    Write-Host ('Wrote {0}: {1} portraits and {2} backdrops.' -f (Join-Path $library 'records.xml'), $people.Count, $backs.Count) -ForegroundColor Green
    foreach ($p in $problems) { Write-Host ('  Note: ' + $p) -ForegroundColor Yellow }
    Write-Host ("In Rimfolk's settings, ""Portrait library folder"" is: {0}" -f $library)
    Write-Host 'Then "Reload the library" there, or restart the game.'
    $missing = Join-Path $library 'missing.csv'
    if (Test-Path -LiteralPath $missing) {
        Write-Host ''
        Write-Host 'What the game needed and the library has no picture for yet (missing.csv; send it to the lead for new batch rows):'
        Get-Content -LiteralPath $missing -Encoding UTF8 -TotalCount 15 | ForEach-Object { Write-Host ('  ' + $_) }
    }
}
catch {
    Write-Host ''
    Write-Host ('Something went wrong: ' + $_.Exception.Message) -ForegroundColor Red
}
Write-Host ''
if (-not $NoPause) { Read-Host 'Press Enter to close' }
