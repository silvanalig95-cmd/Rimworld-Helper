# Review-Pictures.ps1, for Rimfolk's portraits.
#
# Shows the pictures the check held back or left out, one at a time, large, with what is wrong, and lets you keep each
# or leave it out (the player, 23 November 2026: "ideally i'd have some toolkit that provides me the preview of the
# image, what is wrong and whether I want to keep it. while i approved that most corrections can be applied right away,
# things like extra limbs should be blocked by default").
#
# Put it next to Update-PortraitLibrary.ps1 and library\ (Review-Pictures.cmd starts it with a double-click). It opens a
# page in your browser, served by this script on your own computer only (http://localhost), and reads:
# - library\corrections.csv: each picture's "out" lines from Check-Pictures. Marked hold (limbs that don't make sense:
#   an extra arm, hand or finger, a hand belonging to no one, a limb growing from the wrong place): out of the game until
#   you keep it. Marked ask (cut at the side, no one in it, a body its kind doesn't have): out, the check less sure.
#   Marked yes (two people in a cut-out): out. Marked no: kept.
# - library\check-report.csv, where it is there: what the check found, what the model saw and what the row says.
# Keep marks the picture's out lines no; Leave out marks them yes. Keep as the model saw it (the player, 23 November
# 2026: "Keep as the model saw it - please go ahead with this!") is there where an out line is about the body (pointed
# or long ears, horns, a tail, glowing eyes, scales or fur): it keeps the picture and writes what the model saw into its
# description instead (features add:pointedEars ...), so it still goes to people of its kind, those who have what it
# shows first. Each choice is written to corrections.csv at once (close it in Excel first), every other line as it was.
# Lines set in the game are never touched. Two buttons above the pictures do it for many at once (the player, 23
# November 2026: "could I have the option for mass "keep as the model saw it" and also mass "import" for all with
# cutoff borders? as most of them still look very good and i can still delete them in the game after"): Keep all cut at
# the side (every "cut at the side" reason marked no) and Keep all as the model saw it (every reason about the body
# marked no, what the model saw written into the description). Each clears only its own kind of reason, so a picture also
# out for something else (two people, limbs that don't make sense, no one in it) stays out for that.
# When you are done, press Done on the page: the script asks whether to run Update-PortraitLibrary.ps1, which puts the
# choices into the records; then "Reload the library" in the game.

[CmdletBinding()]
param(
    [string] $Folder = '',
    [int] $Port = 8765,
    [switch] $NoBrowser,
    [switch] $NoPause
)

$ErrorActionPreference = 'Stop'
if ($Folder -eq '') { $Folder = Split-Path -Parent $PSCommandPath }
$library = Join-Path $Folder 'library'
$correctionsFile = Join-Path $library 'corrections.csv'
$reportFile = Join-Path $library 'check-report.csv'

function Pause-End {
    if (-not $NoPause) { Write-Host ''; [void](Read-Host 'Press Enter to close') }
}

function Low([string] $t) { if ($t -eq $null) { return '' } return $t.ToLowerInvariant() }

# ---------------- corrections.csv, line by line ----------------
# The file is kept as its lines: only the apply cell of the lines a choice changes is written anew, every other line
# stays exactly as it was (Excel's semicolons, quotes and all).

function Split-CsvLine([string] $line, [char] $delim) {
    $cells = New-Object System.Collections.ArrayList
    $sb = New-Object System.Text.StringBuilder
    $quoted = $false
    for ($i = 0; $i -lt $line.Length; $i++) {
        $ch = $line[$i]
        if ($quoted) {
            if ($ch -eq '"') {
                if ($i + 1 -lt $line.Length -and $line[$i + 1] -eq '"') { [void]$sb.Append('"'); $i++ }
                else { $quoted = $false }
            }
            else { [void]$sb.Append($ch) }
        }
        elseif ($ch -eq '"') { $quoted = $true }
        elseif ($ch -eq $delim) { [void]$cells.Add($sb.ToString()); [void]$sb.Clear() }
        else { [void]$sb.Append($ch) }
    }
    [void]$cells.Add($sb.ToString())
    return , $cells
}

function Csv-Cell([string] $v, [char] $delim) {
    if ($v -match ('["\r\n' + [regex]::Escape([string]$delim) + ']')) { return '"' + $v.Replace('"', '""') + '"' }
    return $v
}

function Read-Lines {
    # corrections.csv as its lines, its separator, its columns, and each line's cells.
    if (-not (Test-Path -LiteralPath $correctionsFile)) { return $null }
    $text = [System.IO.File]::ReadAllText($correctionsFile, [System.Text.Encoding]::UTF8)
    if ($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) { $text = $text.Substring(1) }
    $nl = "`r`n"; if ($text.IndexOf("`r`n") -lt 0 -and $text.IndexOf("`n") -ge 0) { $nl = "`n" }
    $lines = New-Object System.Collections.ArrayList
    foreach ($l in ($text -split "`r?`n")) { [void]$lines.Add($l) }
    while ($lines.Count -gt 0 -and $lines[$lines.Count - 1] -eq '') { $lines.RemoveAt($lines.Count - 1) }
    if ($lines.Count -eq 0) { return $null }
    $delim = [char]','
    if ($lines[0].IndexOf(';') -ge 0 -and $lines[0].IndexOf(',') -lt 0) { $delim = [char]';' }
    $cols = @{}
    $head = Split-CsvLine $lines[0] $delim
    for ($i = 0; $i -lt $head.Count; $i++) { $cols[(Low ([string]$head[$i]).Trim())] = $i }
    foreach ($c in @('picture', 'field', 'apply')) { if (-not $cols.ContainsKey($c)) { throw ('corrections.csv has no column "{0}".' -f $c) } }
    return @{ lines = $lines; delim = $delim; cols = $cols; nl = $nl }
}

function Cell($cells, $cols, [string] $name) {
    if (-not $cols.ContainsKey($name)) { return '' }
    $i = $cols[$name]
    if ($i -ge $cells.Count) { return '' }
    return ([string]$cells[$i]).Trim()
}

function Picture-Key([string] $p) {
    $k = (Low $p).Trim().Replace('/', '\')
    if ($k.StartsWith('library\')) { $k = $k.Substring(8) }
    return $k
}

function Is-Game($cells, $cols) { return (Low (Cell $cells $cols 'why')).StartsWith('set in the game') }

function Out-Lines($csv) {
    # Each picture's out lines (not set in the game): the line's number, its apply and its why, by the picture's key.
    $by = [ordered]@{}
    for ($n = 1; $n -lt $csv.lines.Count; $n++) {
        $l = [string]$csv.lines[$n]
        if ($l.Trim() -eq '') { continue }
        $cells = Split-CsvLine $l $csv.delim
        $field = Low (Cell $cells $csv.cols 'field')
        $why = Cell $cells $csv.cols 'why'
        $own = $field -eq 'features' -and (Low $why) -eq (Low $SeenWhy)
        if (($field -ne 'out' -and -not $own) -or (Is-Game $cells $csv.cols)) { continue }
        $pic = Cell $cells $csv.cols 'picture'
        if ($pic -eq '') { continue }
        $key = Picture-Key $pic
        if (-not $by.Contains($key)) { $by[$key] = @{ picture = $pic; lines = (New-Object System.Collections.ArrayList); seen = (New-Object System.Collections.ArrayList) } }
        $line = @{ n = $n; apply = (Low (Cell $cells $csv.cols 'apply')); why = $why; value = (Cell $cells $csv.cols 'value') }
        if ($own) { [void]$by[$key].seen.Add($line) } else { [void]$by[$key].lines.Add($line) }
    }
    return $by
}

# What the model saw, for an out line about the body: the corrections that write it into the picture's description
# (by the reason Check-Pictures gave), or none.
$SeenWhy = 'kept as the model saw it (Review-Pictures)'
$FeatureWords = @{ pointedEars = 'pointed ears'; longEars = 'long ears'; horns = 'horns'; tail = 'a tail'; glowingEyes = 'glowing eyes'; scales = 'scales'; fur = 'fur' }

function Seen-Of([string] $why) {
    $t = Low $why
    if ($t -match '^shows (.+?) ears; its row has no pointed or long ears') {
        if ($Matches[1] -match '\blong\b' -and $Matches[1] -notmatch 'pointed') { return @('add:longEars') }
        return @('add:pointedEars')
    }
    if ($t -match '^its row says pointed or long ears; the model saw normal ones') { return @('remove:pointedEars', 'remove:longEars') }
    if ($t -match '^shows horns\b') { return @('add:horns') }
    if ($t -match '^its row says horns; the model saw none') { return @('remove:horns') }
    if ($t -match '^shows a tail\b') { return @('add:tail') }
    if ($t -match '^shows glowing eyes\b') { return @('add:glowingEyes') }
    if ($t -match '^its row says glowing eyes; the model saw ordinary ones') { return @('remove:glowingEyes') }
    if ($t -match '^shows (scales|fur) on the body') { return @('add:' + $Matches[1]) }
    if ($t -match '^its row says (scales|fur); the model saw none') { return @('remove:' + $Matches[1]) }
    return @()
}

function Is-SideCut([string] $why) { return (Low $why).StartsWith('cut at the side') }

function Seen-Words([string] $value) {
    # "add:pointedEars" in words: "adds pointed ears".
    $m = [regex]::Match($value, '^(add|remove):(\w+)$')
    if (-not $m.Success) { return $value }
    $w = $m.Groups[2].Value; if ($FeatureWords.ContainsKey($w)) { $w = $FeatureWords[$w] }
    if ($m.Groups[1].Value -eq 'add') { return 'adds ' + $w } else { return 'takes out ' + $w }
}

function State-Of($lines) {
    # held (any line hold), less sure (any ask), out (any yes), kept (all no).
    $a = @($lines | ForEach-Object { $_.apply })
    if ($a -contains 'hold') { return 'held' }
    if ($a -contains 'ask') { return 'unsure' }
    if ($a -contains 'yes') { return 'out' }
    return 'kept'
}

function Set-Apply($csv, $numbers, [string] $apply) {
    # The apply cell of these lines set, each line written again in the file's own way; then the file written safely.
    Change-Apply $csv $numbers $apply
    Save-Lines $csv
}

function Add-Line($csv, [string] $picture, [string] $field, [string] $value, [string] $apply, [string] $why) {
    # A new line at the file's end, its cells in the file's own columns and separator.
    $cells = New-Object string[] ([Math]::Max(5, ($csv.cols.Values | Measure-Object -Maximum).Maximum + 1))
    foreach ($kv in @(@('picture', $picture), @('field', $field), @('value', $value), @('apply', $apply), @('why', $why))) {
        if ($csv.cols.ContainsKey($kv[0])) { $cells[$csv.cols[$kv[0]]] = $kv[1] }
    }
    [void]$csv.lines.Add((@($cells | ForEach-Object { Csv-Cell ([string]$_) $csv.delim }) -join [string]$csv.delim))
}

function Change-Apply($csv, $numbers, [string] $apply) {
    foreach ($n in $numbers) {
        $cells = Split-CsvLine ([string]$csv.lines[$n]) $csv.delim
        $i = $csv.cols['apply']
        while ($cells.Count -le $i) { [void]$cells.Add('') }
        $cells[$i] = $apply
        $csv.lines[$n] = (@($cells | ForEach-Object { Csv-Cell ([string]$_) $csv.delim }) -join [string]$csv.delim)
    }
}

function Save-Lines($csv) {
    $text = (($csv.lines -join $csv.nl) + $csv.nl)
    $tmp = $correctionsFile + '.review-tmp'
    [System.IO.File]::WriteAllText($tmp, $text, (New-Object System.Text.UTF8Encoding($true)))
    try {
        if (Test-Path -LiteralPath $correctionsFile) { [System.IO.File]::Replace($tmp, $correctionsFile, $null) }
        else { [System.IO.File]::Move($tmp, $correctionsFile) }
    }
    catch {
        # Replace may be refused on some drives (a cloud drive's folder): written over instead.
        try { [System.IO.File]::Copy($tmp, $correctionsFile, $true); Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
        catch {
            Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
            throw 'corrections.csv could not be written: is it open in Excel? Close it and press the button again.'
        }
    }
}

# ---------------- check-report.csv ----------------

function Read-Report {
    $r = @{}
    if (-not (Test-Path -LiteralPath $reportFile)) { return $r }
    try {
        $first = ''
        foreach ($l in @(Get-Content -LiteralPath $reportFile -Encoding UTF8 -TotalCount 1)) { $first = [string]$l }
        $d = ','; if ($first.IndexOf(';') -ge 0 -and $first.IndexOf(',') -lt 0) { $d = ';' }
        foreach ($row in @(Import-Csv -LiteralPath $reportFile -Encoding UTF8 -Delimiter $d)) {
            $k = Picture-Key ([string]$row.picture)
            if ($k -eq '') { continue }
            $r[$k] = @{ what = [string]$row.'what is wrong'; seen = [string]$row.'what the model saw'; says = [string]$row.'its row says' }
        }
    }
    catch { Write-Host ('check-report.csv could not be read ({0}); the page shows the reasons from corrections.csv only.' -f $_.Exception.Message) -ForegroundColor Yellow }
    return $r
}

# ---------------- The pictures' files ----------------

$libraryFull = [System.IO.Path]::GetFullPath($library).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$fileCache = @{}

function File-Of([string] $picture) {
    # The picture's file in the library, as corrections.csv names it: by its batch's folder and name, or (a line naming
    # the file alone) found in any batch's folder. Only a .png inside the library.
    $key = Picture-Key $picture
    if ($fileCache.ContainsKey($key)) { return $fileCache[$key] }
    $found = $null
    $rel = $picture.Trim().Replace('/', '\')
    if ((Low $rel).StartsWith('library\')) { $rel = $rel.Substring(8) }
    $rel = $rel.Replace('\', [string][System.IO.Path]::DirectorySeparatorChar)
    if ((Low $rel).EndsWith('.png')) {
        $full = [System.IO.Path]::GetFullPath((Join-Path $library $rel))
        if ($full.StartsWith($libraryFull, [System.StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $full -PathType Leaf)) { $found = $full }
        elseif ($rel.IndexOf([System.IO.Path]::DirectorySeparatorChar) -lt 0) {
            $hit = @(Get-ChildItem -LiteralPath $library -Recurse -File -Filter $rel -ErrorAction SilentlyContinue | Select-Object -First 1)
            if ($hit.Count -gt 0) { $found = $hit[0].FullName }
        }
    }
    $fileCache[$key] = $found
    return $found
}

# ---------------- What the page gets ----------------

function Items-Json {
    $csv = Read-Lines
    $list = New-Object System.Collections.ArrayList
    if ($csv -ne $null) {
        $report = Read-Report
        $by = Out-Lines $csv
        foreach ($key in $by.Keys) {
            $e = $by[$key]
            $rep = $null; if ($report.ContainsKey($key)) { $rep = $report[$key] }
            $why = @($e.lines | ForEach-Object { $_.why } | Where-Object { $_ -ne '' } | Select-Object -Unique)
            $seen = @($e.lines | ForEach-Object { Seen-Of $_.why } | Select-Object -Unique)
            $asSeen = @($e.seen | Where-Object { $_.apply -eq 'yes' } | ForEach-Object { $_.value } | Select-Object -Unique)
            $state = 'kept'; if ($e.lines.Count -gt 0) { $state = State-Of $e.lines }
            [void]$list.Add([ordered]@{
                key = $key; picture = $e.picture; state = $state; reasons = $why
                fix = @($seen | ForEach-Object { Seen-Words $_ }); asSeen = @($asSeen | ForEach-Object { Seen-Words $_ })
                sideOpen = (@($e.lines | Where-Object { $_.apply -ne 'no' -and (Is-SideCut $_.why) }).Count -gt 0)
                bodyOpen = (@($e.lines | Where-Object { $_.apply -ne 'no' -and (Seen-Of $_.why).Count -gt 0 }).Count -gt 0)
                what = $(if ($rep -ne $null) { $rep.what } else { '' }); seen = $(if ($rep -ne $null) { $rep.seen } else { '' })
                says = $(if ($rep -ne $null) { $rep.says } else { '' }); file = ((File-Of $e.picture) -ne $null)
            })
        }
    }
    return (ConvertTo-Json -InputObject @($list) -Depth 5 -Compress)
}

function Decide([string] $key, [string] $how) {
    # keep: its out lines no, and what the model saw (an earlier "as the model saw it") no again; seen: its out lines no
    # and what the model saw written into its description (features lines, yes); out: its out lines yes.
    $csv = Read-Lines
    if ($csv -eq $null) { throw 'corrections.csv is not there.' }
    $by = Out-Lines $csv
    if (-not $by.Contains($key) -or $by[$key].lines.Count -eq 0) { throw 'this picture has no out line in corrections.csv any more.' }
    $e = $by[$key]
    $apply = 'no'; if ($how -eq 'out') { $apply = 'yes' }
    Change-Apply $csv @($e.lines | Where-Object { $_.apply -ne $apply } | ForEach-Object { $_.n }) $apply
    $values = @($e.lines | ForEach-Object { Seen-Of $_.why } | Select-Object -Unique)
    if ($how -eq 'seen') {
        if ($values.Count -eq 0) { throw 'the model saw nothing about this picture that its description can hold.' }
        foreach ($v in $values) {
            $had = @($e.seen | Where-Object { (Low $_.value) -eq (Low $v) })
            if ($had.Count -gt 0) { Change-Apply $csv @($had | ForEach-Object { $_.n }) 'yes' }
            else { Add-Line $csv $e.picture 'features' $v 'yes' $SeenWhy }
        }
        # What it saw before and no longer: not applied.
        Change-Apply $csv @($e.seen | Where-Object { $values -notcontains $_.value -and $_.apply -ne 'no' } | ForEach-Object { $_.n }) 'no'
    }
    elseif ($how -eq 'keep') { Change-Apply $csv @($e.seen | Where-Object { $_.apply -ne 'no' } | ForEach-Object { $_.n }) 'no' }
    Save-Lines $csv
    $state = 'kept'; if ($how -eq 'out') { $state = 'out' }
    $asSeen = @(); if ($how -eq 'seen') { $asSeen = @($values | ForEach-Object { Seen-Words $_ }) } elseif ($how -eq 'out') { $asSeen = @($e.seen | Where-Object { $_.apply -eq 'yes' } | ForEach-Object { Seen-Words $_.value }) }
    $script:decided++
    $said = @{ keep = 'kept'; seen = 'kept as the model saw it'; out = 'left out' }[$how]
    Write-Host ('{0}: {1}' -f $e.picture, $said)
    return (ConvertTo-Json -InputObject ([ordered]@{ key = $key; state = $state; asSeen = $asSeen }) -Compress)
}

function Mass([string] $what) {
    # Every picture's out lines of one kind marked no at once: "side", the pictures cut at the side; "seen", the reasons
    # about the body, with what the model saw written into the description. Returns how many pictures it changed, how
    # many of them are kept now, and how many are still out for another reason.
    $csv = Read-Lines
    if ($csv -eq $null) { throw 'corrections.csv is not there.' }
    $by = Out-Lines $csv
    $pics = 0; $kept = 0; $still = 0
    foreach ($key in @($by.Keys)) {
        $e = $by[$key]
        if ($what -eq 'side') { $hit = @($e.lines | Where-Object { $_.apply -ne 'no' -and (Is-SideCut $_.why) }) }
        else { $hit = @($e.lines | Where-Object { $_.apply -ne 'no' -and (Seen-Of $_.why).Count -gt 0 }) }
        if ($hit.Count -eq 0) { continue }
        $pics++
        $numbers = @($hit | ForEach-Object { $_.n })
        Change-Apply $csv $numbers 'no'
        if ($what -eq 'seen') {
            foreach ($v in @($hit | ForEach-Object { Seen-Of $_.why } | Select-Object -Unique)) {
                $had = @($e.seen | Where-Object { (Low $_.value) -eq (Low $v) })
                if ($had.Count -gt 0) { Change-Apply $csv @($had | ForEach-Object { $_.n }) 'yes' }
                else { Add-Line $csv $e.picture 'features' $v 'yes' $SeenWhy }
            }
        }
        $rest = @($e.lines | Where-Object { $_.apply -ne 'no' -and $numbers -notcontains $_.n })
        if ($rest.Count -eq 0) { $kept++ } else { $still++ }
    }
    if ($pics -gt 0) { Save-Lines $csv }
    $script:decided += $pics
    $said = @{ side = 'kept (cut at the side)'; seen = 'kept as the model saw it' }[$what]
    Write-Host ('{0} {1}; {2} of them still left out for another reason.' -f $(if ($pics -eq 1) { '1 picture' } else { "$pics pictures" }), $said, $still)
    return (ConvertTo-Json -InputObject ([ordered]@{ pictures = $pics; kept = $kept; still = $still }) -Compress)
}

$Page = @'
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Review pictures</title>
<style>
:root { --bg: #1d1f21; --panel: #26292c; --edge: #3a3e42; --ink: #e6e6e6; --faint: #9aa0a6; --good: #6cbf6c; --bad: #d9655b; --held: #e0a840; --accent: #8fb6e0; }
* { box-sizing: border-box; }
body { margin: 0; background: var(--bg); color: var(--ink); font: 15px/1.4 system-ui, sans-serif; height: 100vh; display: flex; flex-direction: column; }
header { display: flex; gap: 12px; align-items: center; padding: 10px 16px; border-bottom: 1px solid var(--edge); flex-wrap: wrap; }
header h1 { font-size: 17px; margin: 0 12px 0 0; font-weight: 600; }
.tabs button, .act, .done { background: var(--panel); color: var(--ink); border: 1px solid var(--edge); border-radius: 6px; padding: 6px 12px; font: inherit; cursor: pointer; }
.tabs button.on { border-color: var(--accent); color: var(--accent); }
.done { margin-left: auto; }
#bulk { display: flex; gap: 8px; align-items: center; padding: 8px 16px; border-bottom: 1px solid var(--edge); flex-wrap: wrap; }
#bulk .label { margin: 0 4px 0 0; }
#bulk button { background: var(--panel); color: var(--good); border: 1px solid var(--good); border-radius: 6px; padding: 6px 12px; font: inherit; cursor: pointer; }
#bulk button.seen { color: var(--accent); border-color: var(--accent); }
#bulk button:disabled { color: var(--faint); border-color: var(--edge); cursor: default; }
main { flex: 1; display: flex; min-height: 0; }
#list { width: 240px; overflow-y: auto; border-right: 1px solid var(--edge); padding: 8px; display: grid; grid-template-columns: 1fr 1fr; gap: 6px; align-content: start; }
#list .t { position: relative; aspect-ratio: 3 / 4; background: var(--panel); border: 2px solid transparent; border-radius: 6px; overflow: hidden; cursor: pointer; }
#list .t.on { border-color: var(--accent); }
#list img { width: 100%; height: 100%; object-fit: contain; }
#list .dot { position: absolute; top: 4px; right: 4px; width: 10px; height: 10px; border-radius: 50%; }
#view { flex: 1; display: flex; min-width: 0; }
#big { flex: 1; display: flex; align-items: center; justify-content: center; padding: 12px; min-width: 0; background: #17181a; }
#big img { max-width: 100%; max-height: calc(100vh - 90px); object-fit: contain; cursor: zoom-in; }
#big img.zoom { max-width: none; max-height: none; cursor: zoom-out; }
#big.scroll { overflow: auto; align-items: flex-start; justify-content: flex-start; }
#info { width: 360px; padding: 16px; overflow-y: auto; border-left: 1px solid var(--edge); }
#info h2 { font-size: 14px; margin: 0 0 4px; word-break: break-all; font-weight: 600; }
.pill { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 13px; margin: 6px 0 12px; color: #111; }
.label { color: var(--faint); font-size: 13px; margin-top: 12px; }
.text { white-space: pre-wrap; }
.btns { display: flex; gap: 8px; margin: 16px 0 6px; }
.act { flex: 1; padding: 12px; font-weight: 600; }
.act.keep { border-color: var(--good); color: var(--good); }
.act.out { border-color: var(--bad); color: var(--bad); }
.act.seen { border-color: var(--accent); color: var(--accent); flex-basis: 100%; }
.btns { flex-wrap: wrap; }
.seenNote { color: var(--faint); font-size: 13px; }
.keys { color: var(--faint); font-size: 13px; }
#status { color: var(--good); font-size: 13px; }
#status.err { color: var(--bad); }
.empty { margin: auto; color: var(--faint); text-align: center; padding: 24px; }
@media (max-width: 900px) { main { flex-direction: column; } #list { width: auto; grid-template-columns: repeat(auto-fill, minmax(70px, 1fr)); max-height: 25vh; border-right: 0; border-bottom: 1px solid var(--edge); } #view { flex-direction: column; } #info { width: auto; border-left: 0; } #big img { max-height: 60vh; } }
</style></head>
<body>
<header><h1>Review pictures</h1><div class="tabs" id="tabs"></div><span id="status"></span><button class="done" id="done">Done</button></header>
<div id="bulk"><span class="label">For many at once:</span><button id="massSide"></button><button id="massSeen" class="seen"></button></div>
<main><div id="list"></div><div id="view"><div id="big"></div><div id="info"></div></div></main>
<script>
const NAMES = { held: 'Held back', unsure: 'Left out, less sure', out: 'Left out', kept: 'Kept' };
const COLOURS = { held: 'var(--held)', unsure: 'var(--held)', out: 'var(--bad)', kept: 'var(--good)' };
const TELLS = {
  held: 'Held back by the check: out of the game until you keep it.',
  unsure: 'Left out by the check, which was less sure.',
  out: 'Left out by the check.',
  kept: 'Kept by you.'
};
let items = [], tab = 'held', at = 0, msg = '', err = false;
const $ = id => document.getElementById(id);
function el(tag, cls, text) { const e = document.createElement(tag); if (cls) e.className = cls; if (text != null) e.textContent = text; return e; }
function shown() { return items.filter(i => i.state === tab); }
function img(i) { return '/img?p=' + encodeURIComponent(i.picture); }
function draw() {
  const tabs = $('tabs'); tabs.innerHTML = '';
  for (const s of ['held', 'unsure', 'out', 'kept']) {
    const n = items.filter(i => i.state === s).length;
    const b = el('button', s === tab ? 'on' : '', NAMES[s] + ' (' + n + ')');
    b.onclick = () => { tab = s; at = 0; msg = ''; draw(); };
    tabs.appendChild(b);
  }
  const nSide = items.filter(i => i.sideOpen).length, nSeen = items.filter(i => i.bodyOpen).length;
  $('massSide').textContent = 'Keep all cut at the side (' + nSide + ')';
  $('massSide').disabled = nSide === 0;
  $('massSeen').textContent = 'Keep all as the model saw it (' + nSeen + ')';
  $('massSeen').disabled = nSeen === 0;
  const list = $('list'); list.innerHTML = '';
  const xs = shown();
  if (at >= xs.length) at = Math.max(0, xs.length - 1);
  xs.forEach((i, k) => {
    const t = el('div', 't' + (k === at ? ' on' : ''));
    if (i.file) { const m = el('img'); m.loading = 'lazy'; m.src = img(i); m.alt = i.picture; t.appendChild(m); }
    const d = el('span', 'dot'); d.style.background = COLOURS[i.state]; t.appendChild(d);
    t.onclick = () => { at = k; msg = ''; draw(); };
    list.appendChild(t);
  });
  const st = $('status'); st.textContent = msg; st.className = err ? 'err' : '';
  const big = $('big'), info = $('info'); big.innerHTML = ''; info.innerHTML = ''; big.className = '';
  const i = xs[at];
  if (!i) { big.appendChild(el('div', 'empty', tab === 'held' ? 'Nothing held back: every picture here has been looked at.' : 'No pictures here.')); return; }
  if (i.file) {
    const m = el('img'); m.src = img(i); m.alt = i.picture;
    m.onclick = () => { m.classList.toggle('zoom'); big.classList.toggle('scroll'); };
    big.appendChild(m);
  } else big.appendChild(el('div', 'empty', 'The file is not in the library folder.'));
  info.appendChild(el('h2', '', i.picture));
  const p = el('span', 'pill', NAMES[i.state]); p.style.background = COLOURS[i.state]; info.appendChild(p);
  info.appendChild(el('div', 'text', TELLS[i.state]));
  const add = (label, text) => { if (!text) return; info.appendChild(el('div', 'label', label)); info.appendChild(el('div', 'text', text)); };
  add('Why it is out', i.reasons.join('\n'));
  add('What the check found', i.what);
  add('What the model saw', i.seen);
  add('Its row says', i.says);
  if (i.state === 'kept' && i.asSeen.length) add('Kept as the model saw it', 'Its description ' + i.asSeen.join(', ') + '.');
  const btns = el('div', 'btns');
  const keep = el('button', 'act keep', 'Keep (K)'); keep.onclick = () => decide('keep');
  const out = el('button', 'act out', 'Leave out (O)'); out.onclick = () => decide('out');
  btns.appendChild(keep); btns.appendChild(out);
  if (i.fix.length) {
    const seen = el('button', 'act seen', 'Keep as the model saw it (S)'); seen.onclick = () => decide('seen');
    btns.appendChild(seen);
  }
  info.appendChild(btns);
  if (i.fix.length) info.appendChild(el('div', 'seenNote', 'Keep as the model saw it: ' + i.fix.join(', ') + ' to its description. It still goes to people of its kind, those who have what it shows first.'));
  info.appendChild(el('div', 'keys', 'K keeps it as its row says, O leaves it out' + (i.fix.length ? ', S keeps it as the model saw it' : '') + '; the arrows go to the one before or after. A picture you decide moves to Kept or Left out, and the next one shows. Click the picture to see it at full size.'));
}
async function load() {
  const r = await fetch('/items'); items = await r.json();
  // The first tab with pictures in it: held back, then less sure, then left out, then kept.
  tab = ['held', 'unsure', 'out', 'kept'].find(s => items.some(i => i.state === s)) || 'held';
  draw();
}
async function decide(how) {
  const i = shown()[at]; if (!i) return;
  if (how === 'seen' && !i.fix.length) return;
  try {
    const r = await fetch('/decide', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ key: i.key, how: how }) });
    const j = await r.json();
    if (!r.ok) throw new Error(j.error || r.statusText);
    i.state = j.state; i.asSeen = j.asSeen || []; err = false;
    msg = ({ keep: 'Kept: ', seen: 'Kept as the model saw it: ', out: 'Left out: ' })[how] + i.picture + '.';
  } catch (e) { err = true; msg = String(e.message || e); }
  draw();
}
document.addEventListener('keydown', e => {
  const k = e.key.toLowerCase();
  if (k === 'k') decide('keep');
  else if (k === 'o') decide('out');
  else if (k === 's') decide('seen');
  else if (e.key === 'ArrowRight' || e.key === 'ArrowDown') { at = Math.min(at + 1, shown().length - 1); msg = ''; draw(); }
  else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') { at = Math.max(at - 1, 0); msg = ''; draw(); }
});
async function mass(what) {
  const n = items.filter(i => what === 'side' ? i.sideOpen : i.bodyOpen).length;
  if (!n) return;
  const ask = what === 'side'
    ? 'Keep all ' + n + ' pictures that are out for being cut at the side?'
    : 'Keep all ' + n + ' pictures that are out for their body (ears, horns, a tail, glowing eyes, scales or fur), each with what the model saw added to its description?';
  if (!confirm(ask + '\n\nA picture also out for something else (two people, limbs that don\'t make sense) stays out for that. You can still change any one of them here, or delete it in the game.')) return;
  try {
    const r = await fetch('/mass', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ what: what }) });
    const j = await r.json();
    if (!r.ok) throw new Error(j.error || r.statusText);
    items = await (await fetch('/items')).json();
    at = 0; err = false;
    const pics = n => n + (n === 1 ? ' picture' : ' pictures');
    msg = 'Kept ' + pics(j.kept) + (what === 'side' ? ' cut at the side' : ' as the model saw ' + (j.kept === 1 ? 'it' : 'them'))
      + (j.still ? '; ' + pics(j.still) + ' still left out for another reason.' : '.');
  } catch (e) { err = true; msg = String(e.message || e); }
  draw();
}
$('massSide').onclick = () => mass('side');
$('massSeen').onclick = () => mass('seen');
$('done').onclick = async () => {
  try { await fetch('/done', { method: 'POST' }); } catch (e) {}
  document.body.innerHTML = '<div class="empty">Done. Your choices are in corrections.csv; the window where Review-Pictures runs asks whether to put them into the records now. You can close this page.</div>';
};
load();
</script></body></html>
'@

# ---------------- The server ----------------

function Send($ctx, [int] $code, [string] $type, [byte[]] $bytes) {
    try {
        $ctx.Response.StatusCode = $code
        $ctx.Response.ContentType = $type
        $ctx.Response.Headers['Cache-Control'] = 'no-store'
        $ctx.Response.ContentLength64 = $bytes.Length
        $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
    }
    catch { }
    finally { try { $ctx.Response.OutputStream.Close() } catch { } }
}

function Send-Text($ctx, [int] $code, [string] $type, [string] $text) { Send $ctx $code $type ([System.Text.Encoding]::UTF8.GetBytes($text)) }

function Error-Json([string] $m) { return (ConvertTo-Json -InputObject ([ordered]@{ error = $m }) -Compress) }

if (-not (Test-Path -LiteralPath $library)) {
    Write-Host ('There is no library folder here: {0}' -f $library) -ForegroundColor Yellow
    Pause-End
    exit 1
}
if (-not (Test-Path -LiteralPath $correctionsFile)) {
    Write-Host 'There is no library\corrections.csv yet: run Check-Pictures first.' -ForegroundColor Yellow
    Pause-End
    exit 1
}

$listener = $null
$url = ''
for ($p = $Port; $p -lt $Port + 20; $p++) {
    $l = New-Object System.Net.HttpListener
    $l.Prefixes.Add(('http://localhost:{0}/' -f $p))
    try { $l.Start(); $listener = $l; $url = ('http://localhost:{0}/' -f $p); break }
    catch { try { $l.Close() } catch { } }
}
if ($listener -eq $null) {
    Write-Host ('No free port from {0} to {1} for the page.' -f $Port, ($Port + 19)) -ForegroundColor Yellow
    Pause-End
    exit 1
}

$csv0 = Read-Lines
$counts = @{ held = 0; unsure = 0; out = 0; kept = 0 }
if ($csv0 -ne $null) { $by0 = Out-Lines $csv0; foreach ($k in $by0.Keys) { $counts[(State-Of $by0[$k].lines)]++ } }
Write-Host ('{0} held back, {1} left out where the check was less sure, {2} left out, {3} kept.' -f $counts.held, $counts.unsure, $counts.out, $counts.kept)
Write-Host ('The page: {0} (open it in your browser if it did not open). Press Done there when you are finished.' -f $url)
if (-not $NoBrowser) { try { Start-Process $url } catch { } }

$script:decided = 0
$finished = $false
try {
    while (-not $finished) {
        $wait = $listener.GetContextAsync()
        while (-not $wait.Wait(500)) { }
        $ctx = $wait.Result
        $path = $ctx.Request.Url.AbsolutePath
        try {
            if ($path -eq '/' -and $ctx.Request.HttpMethod -eq 'GET') { Send-Text $ctx 200 'text/html; charset=utf-8' $Page }
            elseif ($path -eq '/items' -and $ctx.Request.HttpMethod -eq 'GET') { Send-Text $ctx 200 'application/json; charset=utf-8' (Items-Json) }
            elseif ($path -eq '/img' -and $ctx.Request.HttpMethod -eq 'GET') {
                $file = File-Of ([string]$ctx.Request.QueryString['p'])
                if ($file -eq $null) { Send-Text $ctx 404 'text/plain' 'not found' }
                else { Send $ctx 200 'image/png' ([System.IO.File]::ReadAllBytes($file)) }
            }
            elseif ($path -eq '/decide' -and $ctx.Request.HttpMethod -eq 'POST') {
                $reader = New-Object System.IO.StreamReader($ctx.Request.InputStream, [System.Text.Encoding]::UTF8)
                $body = $reader.ReadToEnd() | ConvertFrom-Json
                $reader.Close()
                $how = [string]$body.how
                if ($how -ne 'keep' -and $how -ne 'seen' -and $how -ne 'out') { throw 'no such choice: ' + $how }
                try { Send-Text $ctx 200 'application/json; charset=utf-8' (Decide (Picture-Key ([string]$body.key)) $how) }
                catch { Send-Text $ctx 409 'application/json; charset=utf-8' (Error-Json $_.Exception.Message) }
            }
            elseif ($path -eq '/mass' -and $ctx.Request.HttpMethod -eq 'POST') {
                $reader = New-Object System.IO.StreamReader($ctx.Request.InputStream, [System.Text.Encoding]::UTF8)
                $body = $reader.ReadToEnd() | ConvertFrom-Json
                $reader.Close()
                $what = [string]$body.what
                if ($what -ne 'side' -and $what -ne 'seen') { throw 'no such choice: ' + $what }
                try { Send-Text $ctx 200 'application/json; charset=utf-8' (Mass $what) }
                catch { Send-Text $ctx 409 'application/json; charset=utf-8' (Error-Json $_.Exception.Message) }
            }
            elseif ($path -eq '/done' -and $ctx.Request.HttpMethod -eq 'POST') {
                Send-Text $ctx 200 'application/json; charset=utf-8' '{}'
                $finished = $true
            }
            else { Send-Text $ctx 404 'text/plain' 'not found' }
        }
        catch { Send-Text $ctx 500 'application/json; charset=utf-8' (Error-Json $_.Exception.Message) }
    }
}
finally {
    try { $listener.Stop(); $listener.Close() } catch { }
}

Write-Host ''
Write-Host ('{0} chosen on the page, written to corrections.csv.' -f $script:decided)
$update = Join-Path $Folder 'Update-PortraitLibrary.ps1'
if ($script:decided -gt 0 -and (Test-Path -LiteralPath $update) -and -not $NoPause) {
    $a = Read-Host 'Put them into the records now (Update-PortraitLibrary)? (Enter for yes, n for no)'
    if ((Low $a) -notmatch '^n') { & $update -Folder $Folder -NoPause }
}
elseif ($script:decided -gt 0) { Write-Host 'Run Update-PortraitLibrary.ps1 to put them into the records, then "Reload the library" in the game.' }
Pause-End
