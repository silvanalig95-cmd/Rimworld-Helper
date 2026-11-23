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
# Corrections: library\corrections.csv (written by Check-Pictures.ps1, which looks at each picture; you may change it in
# Excel) holds changes for single pictures, one line each: picture (library\<batch>\<name>.png, or the name alone),
# field, value, apply (yes, ask or no), why. Each picture's record is taken from its row, then its lines marked yes are
# applied: age (an age word), sex (male or female), looks (add:word or remove:word), role (a role, or clear), marks
# (add:word or remove:word), features (add:word or remove:word), headwear (true or false), side (left or right: the side
# of its mark), and out (the picture is left out of the records; the file stays where it is). Lines marked ask wait for
# you; no lines are not applied. No picture is renamed: its name ties it to its row, and the row's other pictures may be
# right.
# The corrections made in the game ("About this picture" writes them with the why "set in the game") always win (the
# player: "the corrections i make through the game client are always prioritized"): for the same picture and field
# (for looks, marks and features the same word) any other line is left out, whatever the order in the file.
#
# Depth maps and variants belong to their picture and are not pictures of their own: a backdrop's depth map
# (<picture>_depth.png next to it, made by Make-DepthMaps.ps1) goes into the backdrop's record as its depthPath, and each
# of a person's variants (<picture>_v-blink.png, _v-smile, _v-frown, _v-pain, _v-lostEyeLeft ... next to it, made by
# Make-Variants.ps1; <picture>_v-<word>-2.png, -3 ... when one was made again after its first was deleted in the game)
# gets a record of its own: the picture's record with its corrections, its own defName and texPath, the word as its
# variant (without the -2) and the picture's defName as its variantOf; a mark's variant also has the mark in its marks
# and its side (lostEyeLeft: lostEye, left). A variant or depth map whose picture is missing or left out is left out
# too, and so is one deleted for good in the game.
#
# Pictures deleted for good in the game (Rimfolk's large portrait, right-click, "Delete this picture for good..."): Rimfolk
# moves their files to the bin (deleted pictures\ beside the library), takes their records out of records.xml at once and
# lists them in library\deleted.csv. This script never writes a record for a picture listed there, nor for its
# expressions or depth map, even if its file comes back into the library; and the names their records had stay taken,
# so the other pictures keep theirs (and the game keeps them for whoever holds them).
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

function Read-Csv-Any([string] $path) {
    # A CSV as written by the scripts, or saved again by Excel (with semicolons in some countries).
    $first = ''
    foreach ($l in @(Get-Content -LiteralPath $path -Encoding UTF8 -TotalCount 1)) { $first = [string]$l }
    $delim = ','
    if ($first.IndexOf(';') -ge 0 -and $first.IndexOf(',') -lt 0) { $delim = ';' }
    return @(Import-Csv -LiteralPath $path -Encoding UTF8 -Delimiter $delim)
}

function Read-Corrections([string] $path) {
    # library\corrections.csv: the lines marked yes by picture (library\<batch>\<name>.png or the name alone, small
    # letters), and how many are marked ask.
    $r = @{ yes = @{}; ask = 0 }
    if (-not (Test-Path -LiteralPath $path)) { return $r }
    foreach ($c in (Read-Csv-Any $path)) {
        $pic = (Field $c 'picture').Replace('/', '\').ToLowerInvariant()
        if ($pic.StartsWith('library\')) { $pic = $pic.Substring(8) }
        $apply = (Field $c 'apply').ToLowerInvariant()
        if ($pic -eq '') { continue }
        if ($apply -eq 'ask') { $r.ask++ }
        if ($apply -ne 'yes') { continue }
        if (-not $r.yes.ContainsKey($pic)) { $r.yes[$pic] = New-Object System.Collections.ArrayList }
        [void]$r.yes[$pic].Add($c)
    }
    return $r
}

function Change-List($list, [string] $v) {
    # "add:word" or "remove:word" on a list of words (letters' case aside); false when the value is neither.
    $m = [regex]::Match($v, '^\s*(add|remove)\s*:\s*(\S+)\s*$', 'IgnoreCase')
    if (-not $m.Success) { return $false }
    $w = $m.Groups[2].Value
    $have = @($list | Where-Object { $_ -eq $w })
    if ($m.Groups[1].Value -eq 'add') { if ($have.Count -eq 0) { [void]$list.Add($w) } }
    else { foreach ($x in $have) { $list.Remove($x) } }
    return $true
}

$ListFields = @('looks', 'marks', 'features')

function Is-Game($c) { return (Field $c 'why').ToLowerInvariant().StartsWith('set in the game') }

function List-Word([string] $v) {
    # The word of "add:word" or "remove:word" (small letters), or ''.
    $m = [regex]::Match([string]$v, '^\s*(add|remove)\s*:\s*(\S+)\s*$', 'IgnoreCase')
    if ($m.Success) { return $m.Groups[2].Value.ToLowerInvariant() }
    return ''
}

function Game-Last($lines) {
    # A picture's lines in the order they are applied: the ones set in the game last, so they win; any other line for a
    # field the game set (for looks, marks and features: the same word) is left out.
    $set = @{}
    $game = @($lines | Where-Object { Is-Game $_ })
    foreach ($g in $game) {
        $f = (Field $g 'field').ToLowerInvariant()
        if ($ListFields -contains $f) { $w = List-Word (Field $g 'value'); if ($w -ne '') { $set[$f + '|' + $w] = $true } }
        else { $set[$f] = $true }
    }
    $other = @($lines | Where-Object { -not (Is-Game $_) } | Where-Object {
        $f = (Field $_ 'field').ToLowerInvariant()
        if ($ListFields -contains $f) { -not $set.ContainsKey($f + '|' + (List-Word (Field $_ 'value'))) } else { -not $set.ContainsKey($f) }
    })
    return @($other) + $game
}

function Apply-Corrections($rec, $lines, $problems, [string] $where) {
    # A picture's lines marked yes on its record (a hashtable of the row's fields), the game's last; the number applied.
    $n = 0
    foreach ($c in @(Game-Last $lines)) {
        $field = (Field $c 'field').ToLowerInvariant()
        $v = Field $c 'value'
        $ok = $true
        switch ($field) {
            'out' { $rec.out = $true }
            'age' { if ($v -ne '') { $rec.age = $v.ToLowerInvariant() } else { $ok = $false } }
            'sex' {
                if ($v -match '^(?i)(f|female|woman)$') { $rec.sex = 'female' }
                elseif ($v -match '^(?i)(m|male|man)$') { $rec.sex = 'male' }
                else { $ok = $false }
            }
            'role' { if ($v -match '^(?i)(clear|none|-)$') { $rec.role = '' } elseif ($v -ne '') { $rec.role = $v.ToLowerInvariant() } else { $ok = $false } }
            'headwear' { if ($v -match '^(?i)(true|yes|1)$') { $rec.headwear = 'true' } elseif ($v -match '^(?i)(false|no|0)$') { $rec.headwear = '' } else { $ok = $false } }
            'side' { if ($v -match '^(?i)(left|right)$') { $rec.side = $v.ToLowerInvariant() } elseif ($v -match '^(?i)(clear|none|-)$') { $rec.side = '' } else { $ok = $false } }
            'looks' { $ok = Change-List $rec.looks $v }
            'marks' { $ok = Change-List $rec.marks $v }
            'features' { $ok = Change-List $rec.features $v }
            default { $ok = $false }
        }
        if ($ok) { $n++ } else { [void]$problems.Add(('corrections.csv, {0}: "{1} {2}" is not a correction this script knows, left out' -f $where, $field, $v)) }
    }
    return $n
}

function One-Xml([string] $tag, [string] $value) {
    if ($value -eq '') { return '' }
    return ('    <{0}>{1}</{0}>' -f $tag, (Esc $value))
}

# The variants' words as the game spells them; a mark's variant is the picture with that mark, on the person's own side
# where it has one.
$VariantWords = @('blink', 'smile', 'frown', 'pain', 'lostEyeLeft', 'lostEyeRight', 'eyePatchLeft', 'eyePatchRight', 'bionicEyeLeft', 'bionicEyeRight', 'scar', 'burn', 'missingEarLeft', 'missingEarRight', 'artificialArmLeft', 'artificialArmRight')
$MarkOf = @{
    lostEyeLeft = @('lostEye', 'left'); lostEyeRight = @('lostEye', 'right'); eyePatchLeft = @('eyePatch', 'left'); eyePatchRight = @('eyePatch', 'right')
    bionicEyeLeft = @('bionicEye', 'left'); bionicEyeRight = @('bionicEye', 'right'); scar = @('scar', ''); burn = @('burn', '')
    missingEarLeft = @('missingEar', 'left'); missingEarRight = @('missingEar', 'right'); artificialArmLeft = @('artificialArm', 'left'); artificialArmRight = @('artificialArm', 'right')
}

# A depth map (<picture>_depth) or a variant (<picture>_v-<word>, or <picture>_v-<word>-2 made again after the first was
# deleted in the game): the picture's own name, the kind, the word as the game spells it and the number, or $null.
function Extra-Of([string] $stem) {
    $m = [regex]::Match($stem, '^(?<base>.+)_(?:(?<depth>depth)|v-(?<word>[A-Za-z]+)(?:-(?<n>\d+))?)$')
    if (-not $m.Success) { return $null }
    $word = ''
    if ($m.Groups['word'].Success) {
        $word = $m.Groups['word'].Value.ToLowerInvariant()
        foreach ($w in $VariantWords) { if ($w -eq $word) { $word = $w } }
    }
    $n = 1; if ($m.Groups['n'].Success) { $n = [int]$m.Groups['n'].Value }
    return [pscustomobject]@{ base = $m.Groups['base'].Value; depth = $m.Groups['depth'].Success; word = $word; n = $n; stem = $stem; used = $false }
}

function Person-Xml([string] $defName, [string] $tex, $r, $rec, [string] $character, [string] $variant, [string] $variantOf) {
    # One PortraitDef: the row's fields with the picture's corrections ($rec); a variant's also says its word and the
    # defName of the picture it is a variant of.
    $kind = Field $r 'kind'
    $xenos = @(); if ($kind -ne '' -and $kind -ne 'Baseliner') { $xenos = @($kind) }
    $lines = New-Object System.Collections.ArrayList
    [void]$lines.Add('  <ColonyCouncil.PortraitDef>')
    [void]$lines.Add((One-Xml 'defName' $defName)); [void]$lines.Add((One-Xml 'texPath' $tex))
    [void]$lines.Add((One-Xml 'variant' $variant)); [void]$lines.Add((One-Xml 'variantOf' $variantOf))
    [void]$lines.Add((One-Xml 'pawn' (Field $r 'pawn')))
    [void]$lines.Add((List-Xml 'xenotypes' $xenos))
    [void]$lines.Add((List-Xml 'races' (Words (Field $r 'race'))))
    [void]$lines.Add((One-Xml 'sex' $rec.sex)); [void]$lines.Add((One-Xml 'age' $rec.age))
    [void]$lines.Add((One-Xml 'skinColor' (Field $r 'skin'))); [void]$lines.Add((One-Xml 'hairColor' (Field $r 'hairColour')))
    [void]$lines.Add((One-Xml 'hair' (Field $r 'hair'))); [void]$lines.Add((One-Xml 'beard' (Field $r 'beard')))
    [void]$lines.Add((One-Xml 'build' (Field $r 'build'))); [void]$lines.Add((One-Xml 'era' (Field $r 'era')))
    [void]$lines.Add((One-Xml 'dress' (Field $r 'dress')))
    [void]$lines.Add((List-Xml 'features' ([string[]]$rec.features.ToArray())))
    [void]$lines.Add((List-Xml 'marks' ([string[]]$rec.marks.ToArray())))
    [void]$lines.Add((One-Xml 'side' $rec.side))
    [void]$lines.Add((List-Xml 'looks' ([string[]]$rec.looks.ToArray())))
    [void]$lines.Add((One-Xml 'role' $rec.role))
    [void]$lines.Add((One-Xml 'headwear' $rec.headwear))
    [void]$lines.Add((One-Xml 'facing' (Field $r 'facing')))
    [void]$lines.Add((One-Xml 'character' $character))
    [void]$lines.Add('  </ColonyCouncil.PortraitDef>')
    return (($lines | Where-Object { $_ -ne '' }) -join "`r`n")
}

function Unique-Name([string] $stem, $names) {
    $defName = $stem; $k = 2
    while ($names.ContainsKey($defName)) { $defName = $stem + '_' + $k; $k++ }
    $names[$defName] = $true
    return $defName
}

# ---------------- Pictures deleted in the game ----------------
# library\deleted.csv: Rimfolk adds a line there when a picture is deleted for good from the large portrait's menu (its
# files go to the bin, deleted pictures\ beside the library). Such a picture is never copied, recorded, checked or given
# expressions or a depth map again, even if its file comes back. Matched by its file name, and a " (2)" copy of it by its
# content (the SHA-256 on its line), so another picture that only shares the start of the name is not held back. To bring
# a picture back, take its line out of deleted.csv and put its file back into the library.

function Read-Deleted([string] $lib) {
    # The deleted pictures by their name without .png (small letters), each with the SHA-256 of its file ('' where its
    # line has none); the names of " (2)" copies also by the name they are a copy of; and the records' names.
    $d = @{ names = @{}; copies = @{}; records = @() }
    $path = Join-Path $lib 'deleted.csv'
    if (-not (Test-Path -LiteralPath $path)) { return $d }
    try {
        $first = ''
        foreach ($l in @(Get-Content -LiteralPath $path -Encoding UTF8 -TotalCount 1)) { $first = [string]$l }
        $delim = ','
        if ($first.IndexOf(';') -ge 0 -and $first.IndexOf(',') -lt 0) { $delim = ';' }
        $rows = @(Import-Csv -LiteralPath $path -Encoding UTF8 -Delimiter $delim)
    }
    catch { throw ('{0} could not be read ({1}): is it open in Excel? Close it and run this again.' -f $path, $_.Exception.Message) }
    foreach ($r in $rows) {
        $cols = @($r.PSObject.Properties.Name)
        $f = ''; if ($cols -contains 'file') { $f = ([string]$r.file).Trim() }
        if ($f -eq '') { continue }
        $f = @($f -split '[\\/]')[-1]
        if ($f.ToLowerInvariant().EndsWith('.png')) { $f = $f.Substring(0, $f.Length - 4) }
        $stem = $f.ToLowerInvariant()
        $hash = ''; if ($cols -contains 'sha256') { $hash = ([string]$r.sha256).Trim().ToUpperInvariant() }
        if (-not $d.names.ContainsKey($stem)) { $d.names[$stem] = New-Object System.Collections.ArrayList }
        [void]$d.names[$stem].Add($hash)
        $base = $stem -replace '\s*\(\d+\)$', ''
        if ($base -ne $stem) {
            if (-not $d.copies.ContainsKey($base)) { $d.copies[$base] = New-Object System.Collections.ArrayList }
            [void]$d.copies[$base].Add($hash)
        }
        if ($cols -contains 'record') { $rec = ([string]$r.record).Trim(); if ($rec -ne '') { $d.records += $rec } }
    }
    return $d
}

function Is-Deleted([string] $path, $deleted, [bool] $fromOutput = $false) {
    # Whether deleted.csv lists this picture: its own name, or a " (2)" copy of a listed picture with the same content;
    # for a picture in ComfyUI's output (fromOutput) also one whose " (2)" copy in the library was deleted.
    if ($deleted.names.Count -eq 0) { return $false }
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($path).ToLowerInvariant()
    if ($deleted.names.ContainsKey($stem)) { return $true }
    $hashes = New-Object System.Collections.ArrayList
    $base = $stem -replace '\s*\(\d+\)$', ''
    if ($base -ne $stem -and $deleted.names.ContainsKey($base)) { $hashes.AddRange(@($deleted.names[$base])) }
    if ($fromOutput -and $deleted.copies.ContainsKey($stem)) { $hashes.AddRange(@($deleted.copies[$stem])) }
    if ($hashes.Count -eq 0) { return $false }
    if ($hashes -contains '') { return $true }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $false }
    return ($hashes -contains (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash)
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
    $corr = Read-Corrections (Join-Path $library 'corrections.csv')
    # Pictures deleted for good in the game: never recorded again, and their records' names kept taken.
    $deleted = Read-Deleted $library
    foreach ($n in $deleted.records) { $names[$n] = $true }
    $deletedOut = 0; $deletedExtras = 0
    $applied = 0; $leftOut = 0
    # Every batch list's rows by name, so a picture finds its row even when its row has moved to another list (lists
    # are mixed and renumbered now and then) or its folder's list is gone; its own folder's list comes first.
    $allRows = @{}
    foreach ($csvFile in @(Get-ChildItem -LiteralPath $batches -Filter '*.csv' -File)) {
        $one = Read-Rows $csvFile.BaseName
        if ($one -eq $null) { continue }
        foreach ($k in $one.Keys) { if (-not $allRows.ContainsKey($k)) { $allRows[$k] = $one[$k] } }
    }
    $variants = 0; $depths = 0; $extrasOut = 0
    foreach ($dir in @(Get-ChildItem -LiteralPath $library -Directory)) {
        $batch = $dir.Name
        $rows = Read-Rows $batch
        if ($rows -eq $null) { $rows = @{} }
        # The folder's depth maps and expressions by their picture's name (small letters); the rest are pictures.
        $extras = @{}
        $pics = New-Object System.Collections.ArrayList
        foreach ($f in @(Get-ChildItem -LiteralPath $dir.FullName -Filter '*.png' -File)) {
            $x = Extra-Of ([System.IO.Path]::GetFileNameWithoutExtension($f.Name))
            if ($x -eq $null) { [void]$pics.Add($f); continue }
            $key = $x.base.ToLowerInvariant()
            if (-not $extras.ContainsKey($key)) { $extras[$key] = New-Object System.Collections.ArrayList }
            [void]$extras[$key].Add($x)
        }
        foreach ($f in $pics) {
            $own = @(); $fstem = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
            if ($extras.ContainsKey($fstem.ToLowerInvariant())) { $own = @($extras[$fstem.ToLowerInvariant()]) }
            if (Is-Deleted $f.FullName $deleted) {
                $deletedOut++
                foreach ($x in $own) { $x.used = $true; $deletedExtras++ }
                continue
            }
            $hit = Row-For $f.Name $rows
            if ($hit -eq $null) { $hit = Row-For $f.Name $allRows }
            if ($hit -eq $null) { [void]$problems.Add(("library\{0}\{1}: no row of this name in any batch list, left out" -f $batch, $f.Name)); continue }
            $r = $hit.row
            # This picture's corrections marked yes (by library\<batch>\<name>.png or by the name alone).
            $fixes = @()
            foreach ($key in @(($batch + '\' + $f.Name).ToLowerInvariant(), $f.Name.ToLowerInvariant())) {
                if ($corr.yes.ContainsKey($key)) { $fixes += @($corr.yes[$key]) }
            }
            $rec = @{
                out = $false; sex = (Field $r 'sex'); age = (Field $r 'age'); role = (Field $r 'role'); headwear = ''; side = (Field $r 'side')
                looks = (New-Object System.Collections.ArrayList); marks = (New-Object System.Collections.ArrayList); features = (New-Object System.Collections.ArrayList)
            }
            foreach ($w in (Words (Field $r 'looks'))) { [void]$rec.looks.Add($w) }
            foreach ($w in (Words (Field $r 'marks'))) { [void]$rec.marks.Add($w) }
            foreach ($w in (Words (Field $r 'features'))) { [void]$rec.features.Add($w) }
            if ($fixes.Count -gt 0) { $applied += Apply-Corrections $rec $fixes $problems ($batch + '\' + $f.Name) }
            if ($rec.out) { $leftOut++; continue }
            $tex = $batch + '/' + $fstem
            $isBack = (Field $r 'place') -ne ''
            $stem = $(if ($isBack) { 'Backdrop_' } else { 'Portrait_' }) + (Clean $batch) + '_' + $hit.name + '_' + $hit.n
            $defName = Unique-Name $stem $names
            if (-not $isBack -and $rec.sex -eq '') { [void]$problems.Add(("library\{0}\{1}: its row has no record (sex, age ...), left out" -f $batch, $f.Name)); continue }
            $lines = New-Object System.Collections.ArrayList
            if ($isBack) {
                $depth = @($own | Where-Object { $_.depth } | Select-Object -First 1)
                [void]$lines.Add('  <ColonyCouncil.BackdropDef>')
                [void]$lines.Add((One-Xml 'defName' $defName)); [void]$lines.Add((One-Xml 'texPath' $tex))
                if ($depth.Count -gt 0) {
                    [void]$lines.Add((One-Xml 'depthPath' ($batch + '/' + $depth[0].stem)))
                    $depth[0].used = $true; $depths++
                }
                [void]$lines.Add((One-Xml 'place' (Field $r 'place')))
                [void]$lines.Add((List-Xml 'rooms' (Words (Field $r 'rooms'))))
                [void]$lines.Add((One-Xml 'outdoors' (Field $r 'outdoors')))
                [void]$lines.Add((One-Xml 'era' (Field $r 'era')))
                [void]$lines.Add((List-Xml 'contents' (Words (Field $r 'contents'))))
                [void]$lines.Add('  </ColonyCouncil.BackdropDef>')
                [void]$backs.Add((($lines | Where-Object { $_ -ne '' }) -join "`r`n"))
            }
            else {
                $character = (Clean $batch) + '_' + $hit.name
                [void]$people.Add((Person-Xml $defName $tex $r $rec $character '' ''))
                # Its variants, each the same record with its own name, picture, word and the defName of what it is a
                # variant of; a mark's variant with that mark and its side. A mark variant's own expressions
                # (<picture>_v-lostEyeLeft_v-blink.png) are variants of the mark variant, with its mark. One deleted for
                # good in the game is left out, with its own variants (its name stays deleted; a -2 made after it is a
                # new file).
                $next = New-Object System.Collections.Queue
                foreach ($x in @($own | Where-Object { -not $_.depth } | Sort-Object word, n)) { $next.Enqueue(@($x, $rec, $defName)) }
                while ($next.Count -gt 0) {
                    $item = $next.Dequeue()
                    $x = $item[0]; $ofRec = $item[1]; $ofName = $item[2]
                    $x.used = $true
                    if (Is-Deleted (Join-Path $dir.FullName ($x.stem + '.png')) $deleted) { $deletedExtras++; continue }
                    $vRec = $ofRec
                    if ($MarkOf.ContainsKey($x.word)) {
                        $mark = $MarkOf[$x.word]
                        $vRec = $ofRec.Clone()
                        $vRec.marks = New-Object System.Collections.ArrayList
                        foreach ($w in $ofRec.marks) { [void]$vRec.marks.Add($w) }
                        if (@($vRec.marks | Where-Object { $_ -eq $mark[0] }).Count -eq 0) { [void]$vRec.marks.Add($mark[0]) }
                        if ($mark[1] -ne '') { $vRec.side = $mark[1] }
                    }
                    $vStem = $ofName + '_' + $x.word; if ($x.n -gt 1) { $vStem += '_' + $x.n }
                    $vName = Unique-Name $vStem $names
                    [void]$people.Add((Person-Xml $vName ($batch + '/' + $x.stem) $r $vRec $character $x.word $ofName))
                    $variants++
                    $key = $x.stem.ToLowerInvariant()
                    if ($extras.ContainsKey($key)) {
                        foreach ($y in @($extras[$key] | Where-Object { -not $_.depth } | Sort-Object word, n)) { $next.Enqueue(@($y, $vRec, $vName)) }
                    }
                }
            }
        }
        # Depth maps and expressions whose picture is not in the folder, or is left out (no row, no record, out).
        foreach ($key in $extras.Keys) {
            foreach ($x in $extras[$key]) {
                if ($x.used) { continue }
                # The depth map or expression of a picture deleted in the game, left behind: out with it, quietly.
                if (Is-Deleted (Join-Path $dir.FullName ($x.base + '.png')) $deleted) { $deletedExtras++; continue }
                $extrasOut++
                $base = @($pics | Where-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) -eq $x.base })
                $ofExtra = @($extras.Values | ForEach-Object { $_ } | Where-Object { $_.stem -eq $x.base })
                if ($base.Count -eq 0 -and $ofExtra.Count -eq 0) { [void]$problems.Add(("library\{0}\{1}.png: its picture {2}.png is not in this folder, left out" -f $batch, $x.stem, $x.base)) }
            }
        }
    }
    $all = @($people) + @($backs)
    $xml = '<?xml version="1.0" encoding="utf-8"?>' + "`r`n" + '<!-- Rimfolk portrait library, written by Update-PortraitLibrary.ps1: one record per picture in this folder. -->' + "`r`n" + '<Defs>' + "`r`n" + ($all -join "`r`n") + "`r`n" + '</Defs>' + "`r`n"
    [System.IO.File]::WriteAllText((Join-Path $library 'records.xml'), $xml, (New-Object System.Text.UTF8Encoding($false)))

    Write-Host ''
    $line = 'Wrote {0}: {1} portraits' -f (Join-Path $library 'records.xml'), ($people.Count - $variants)
    if ($variants -gt 0) { $line += (' (and {0} variants of them: expressions and marks)' -f $variants) }
    $line += (' and {0} backdrops' -f $backs.Count)
    if ($depths -gt 0) { $line += (' ({0} with a depth map)' -f $depths) }
    Write-Host ($line + '.') -ForegroundColor Green
    if ($extrasOut -gt 0) { Write-Host ('{0} depth maps or variants left out with their picture (missing, left out, or of the wrong kind).' -f $extrasOut) }
    if ($deletedOut -gt 0 -or $deletedExtras -gt 0) {
        Write-Host ('{0} pictures and {1} depth maps or variants in the library are deleted for good in the game (deleted.csv), so they have no records. Move them out of the library, or take their lines out of deleted.csv to have them back.' -f $deletedOut, $deletedExtras) -ForegroundColor Yellow
    }
    if ($applied -gt 0 -or $corr.ask -gt 0) {
        $line = 'Corrections (corrections.csv): {0} applied' -f $applied
        if ($leftOut -gt 0) { $line += (', {0} of them leaving a picture out' -f $leftOut) }
        $line += ('; {0} marked ask wait for you (open it and change ask to yes or no).' -f $corr.ask)
        Write-Host $line
    }
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
