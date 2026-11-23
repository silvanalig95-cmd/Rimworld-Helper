# Make-TrainingSet.ps1, for Rimfolk's portraits.
#
# Makes the pictures you kept for training ready for FluxGym, to train the style LoRA (rmwstyle) again with them
# (docs/portrait-art/STYLE-RIMWORLD.md, "Training again"). In the game, right-click the large portrait, "Keep for
# training": Rimfolk copies the picture to training\<batch>\ beside the library (in this Rimfolk-portraits folder) and adds
# a line to training\training.csv (file, batch, record, date). The player: "maybe I can right click and then already flag
# for training ... or it's get copied to a special location from which I can take the image to further train the model
# later".
# - For each kept picture (the lines of training.csv, and any other picture in training\<batch>\), it finds the picture's
#   row in the batch lists (batches\*.csv; the row's name is the picture's name before "_style") and writes into
#   training\set\:
#   - the picture on a plain dark grey ground (red 58, green 60, blue 68), saved without transparency (an RGB PNG): the
#     cut-out's empty background would train badly, and the first training set's pictures are on plain grey too;
#   - a caption of the same name (.txt): "rmwstyle, " and the row's subject and pose, the words of what it shows (the
#     style's and the frame's words are not in the row, so the caption describes the content only, as the first set's).
# - Pictures already in the set are left as they are. It never changes or moves a kept picture, the library or a list.
# - The pictures are made with Windows' own System.Drawing.
#
# Run it: double-click Make-TrainingSet.cmd (or right-click this file, "Run with PowerShell"). Then in FluxGym upload the
# set together with the first training set's 47 pictures and their captions.

[CmdletBinding()]
param(
    [string] $Folder = '',
    [switch] $NoPause
)

$ErrorActionPreference = 'Stop'
if ($Folder -eq '') { $Folder = Split-Path -Parent $PSCommandPath }
$training = Join-Path $Folder 'training'
$setDir = Join-Path $training 'set'
$batches = Join-Path $Folder 'batches'
$Trigger = 'rmwstyle'
$Grey = @(58, 60, 68)

# ---------------- Small helpers ----------------

function Has($o, [string] $name) { return ($o -ne $null) -and ($o.PSObject.Properties.Name -contains $name) }
function Field($row, [string] $name) { if (Has $row $name) { return ([string]$row.$name).Trim() } else { return '' } }
function Clean([string] $s) { return ($s -replace '[^A-Za-z0-9_\-]', '_') }
function Count-Of([int] $n, [string] $one, [string] $many) {
    if ($n -eq 1) { return ('1 ' + $one) } else { return ('{0} {1}' -f $n, $many) }
}

function Read-Csv-Any([string] $path) {
    # A CSV as written by Rimfolk, or saved again by Excel (with semicolons in some countries).
    $first = ''
    foreach ($l in @(Get-Content -LiteralPath $path -Encoding UTF8 -TotalCount 1)) { $first = [string]$l }
    $delim = ','
    if ($first.IndexOf(';') -ge 0 -and $first.IndexOf(',') -lt 0) { $delim = ';' }
    return @(Import-Csv -LiteralPath $path -Encoding UTF8 -Delimiter $delim)
}

# ---------------- The rows ----------------

function Read-Rows([string] $csv) {
    $byName = @{}
    foreach ($r in @(Import-Csv -LiteralPath $csv -Encoding UTF8)) {
        if ([string]::IsNullOrWhiteSpace($r.name)) { continue }
        $byName[(Clean $r.name)] = $r
    }
    return $byName
}

function Row-Name([string] $file) {
    # ComfyUI's names: <name>_<style>_[<run stamp>_]<NN>[_cut]_<counter>_.png (as Update-PortraitLibrary.ps1 reads them);
    # the row's name is the part before "_style". A copy Windows renamed ("... (2).png") still counts.
    $base = [System.IO.Path]::GetFileNameWithoutExtension($file)
    $base = ($base -replace '\s*\(\d+\)$', '') -replace '\s*-\s*Copy( \(\d+\))?$', ''
    $m = [regex]::Match($base, '^(?<name>.+?)_(?<style>style[^_]*)_')
    if ($m.Success) { return $m.Groups['name'].Value }
    return ''
}

function Caption-Of($row) {
    # "rmwstyle, " and the row's subject and pose, on one line; "A thin man" starts "a thin man" after the comma.
    $c = ((Field $row 'subject') + ' ' + (Field $row 'pose')) -replace '\s+', ' '
    $c = $c.Trim()
    if ($c -cmatch '^(A|An|The)\b') { $c = $c.Substring(0, 1).ToLowerInvariant() + $c.Substring(1) }
    return ($Trigger + ', ' + $c)
}

# ---------------- The picture on grey ----------------

function Compose-OnGrey([string] $from, [string] $to) {
    # The kept picture drawn over a plain dark grey ground the same size, saved as an RGB PNG (no transparency), with
    # Windows' own System.Drawing. Throws when the picture can't be read or written.
    $src = New-Object System.Drawing.Bitmap($from)
    try {
        $w = $src.Width; $h = $src.Height
        $dst = New-Object System.Drawing.Bitmap -ArgumentList $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        try {
            $g = [System.Drawing.Graphics]::FromImage($dst)
            try {
                $g.Clear([System.Drawing.Color]::FromArgb($Grey[0], $Grey[1], $Grey[2]))
                $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
                $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                # One picture pixel on one pixel: nothing smoothed or shifted.
                $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
                $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
                $g.DrawImage($src, $rect, $rect, [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally { $g.Dispose() }
            $dst.Save($to, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $dst.Dispose() }
    }
    finally { $src.Dispose() }
}

# ---------------- The run ----------------

function Make-Set {
    if (-not (Test-Path -LiteralPath $training)) {
        Write-Host ('There is no training folder yet ({0}). In the game, right-click the large portrait and choose "Keep for training": Rimfolk copies the picture there.' -f $training) -ForegroundColor Yellow
        return
    }
    try { Add-Type -AssemblyName System.Drawing } catch { }

    # The kept pictures: training.csv's lines, then any other picture in training\<batch>\.
    $kept = New-Object System.Collections.ArrayList
    $seen = @{}
    $missing = New-Object System.Collections.ArrayList
    $list = Join-Path $training 'training.csv'
    if (Test-Path -LiteralPath $list) {
        try { $lines = @(Read-Csv-Any $list) }
        catch { throw ('{0} could not be read ({1}): is it open in Excel? Close it and run this again.' -f $list, $_.Exception.Message) }
        foreach ($l in $lines) {
            $file = Field $l 'file'
            $parts = @($file -split '[\\/]' | Where-Object { $_ -ne '' })
            if ($parts.Count -eq 0) { continue }
            $name = $parts[-1]
            $batch = Field $l 'batch'
            if ($batch -eq '' -and $parts.Count -ge 2) { $batch = $parts[-2] }
            $path = Join-Path (Join-Path $training $batch) $name
            if ($batch -eq '' -or -not (Test-Path -LiteralPath $path -PathType Leaf)) {
                # Not where its line says: looked for by its name in every folder of training\.
                $hit = @(Get-ChildItem -LiteralPath $training -Directory | Where-Object { $_.Name -ne 'set' } | ForEach-Object { Join-Path $_.FullName $name } | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
                if ($hit.Count -eq 0) { [void]$missing.Add($name); continue }
                $path = $hit[0]
                $batch = Split-Path -Leaf (Split-Path -Parent $path)
            }
            if ($seen.ContainsKey($path.ToLowerInvariant())) { continue }
            $seen[$path.ToLowerInvariant()] = $true
            [void]$kept.Add([pscustomobject]@{ path = $path; name = $name; batch = $batch })
        }
    }
    foreach ($dir in @(Get-ChildItem -LiteralPath $training -Directory | Where-Object { $_.Name -ne 'set' } | Sort-Object Name)) {
        foreach ($f in @(Get-ChildItem -LiteralPath $dir.FullName -Filter '*.png' -File | Sort-Object Name)) {
            if ($seen.ContainsKey($f.FullName.ToLowerInvariant())) { continue }
            $seen[$f.FullName.ToLowerInvariant()] = $true
            [void]$kept.Add([pscustomobject]@{ path = $f.FullName; name = $f.Name; batch = $dir.Name })
        }
    }
    if ($kept.Count -eq 0) {
        $why = 'No pictures kept for training yet.'
        if ($missing.Count -gt 0) { $why = ('training.csv lists {0}, but none of them is in the training folder.' -f (Count-Of $missing.Count 'picture' 'pictures')) }
        Write-Host ($why + ' In the game, right-click the large portrait and choose "Keep for training".') -ForegroundColor Yellow
        return
    }

    # The rows: each batch's own list first, then every list (a row may have moved to another list).
    if (-not (Test-Path -LiteralPath $batches)) { throw ('There is no batches folder next to this script ({0}): the captions are taken from the batch lists.' -f $batches) }
    $rowsOf = @{}
    $allRows = @{}
    foreach ($csv in @(Get-ChildItem -LiteralPath $batches -Filter '*.csv' -File | Sort-Object Name)) {
        try { $one = Read-Rows $csv.FullName } catch { continue }
        $rowsOf[$csv.BaseName.ToLowerInvariant()] = $one
        foreach ($k in $one.Keys) { if (-not $allRows.ContainsKey($k)) { $allRows[$k] = $one[$k] } }
    }

    if (-not (Test-Path -LiteralPath $setDir)) { [void](New-Item -ItemType Directory -Path $setDir) }
    $made = 0; $already = 0
    $noRow = New-Object System.Collections.ArrayList
    $failed = New-Object System.Collections.ArrayList
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    Write-Host ('{0} kept for training.' -f (Count-Of $kept.Count 'picture' 'pictures'))
    foreach ($k in $kept) {
        $stem = [System.IO.Path]::GetFileNameWithoutExtension($k.name)
        $png = Join-Path $setDir ($stem + '.png')
        $txt = Join-Path $setDir ($stem + '.txt')
        if ((Test-Path -LiteralPath $png) -and (Test-Path -LiteralPath $txt)) { $already++; continue }
        $name = Row-Name $k.name
        $row = $null
        $rows = $rowsOf[$k.batch.ToLowerInvariant()]
        if ($name -ne '' -and $rows -ne $null -and $rows.ContainsKey($name)) { $row = $rows[$name] }
        elseif ($name -ne '' -and $allRows.ContainsKey($name)) { $row = $allRows[$name] }
        if ($row -eq $null -or (Field $row 'subject') -eq '') { [void]$noRow.Add(($k.batch + '\' + $k.name)); continue }
        try {
            if (-not (Test-Path -LiteralPath $png)) { Compose-OnGrey $k.path $png }
            # The caption beside its picture, as FluxGym pairs them: one line, UTF-8 without a mark.
            [System.IO.File]::WriteAllText($txt, (Caption-Of $row), $utf8)
            $made++
            Write-Host ('  {0}\{1}' -f $k.batch, $k.name)
        }
        catch {
            # Nothing half made stays: a picture without its caption is made again next time.
            if ((Test-Path -LiteralPath $png) -and -not (Test-Path -LiteralPath $txt)) { Remove-Item -LiteralPath $png -Force -ErrorAction SilentlyContinue }
            $msg = $_.Exception.Message
            if ($_.Exception.InnerException -ne $null) { $msg = $_.Exception.InnerException.Message }
            [void]$failed.Add(('{0}\{1} ({2})' -f $k.batch, $k.name, $msg))
        }
    }

    $inSet = @(Get-ChildItem -LiteralPath $setDir -Filter '*.png' -File | Where-Object { Test-Path -LiteralPath ([System.IO.Path]::ChangeExtension($_.FullName, '.txt')) }).Count
    Write-Host ''
    Write-Host ('{0} added to the set, {1} there already.' -f (Count-Of $made 'picture' 'pictures'), $already)
    if ($missing.Count -gt 0) {
        Write-Host ('{0} in training.csv but not in the training folder (moved or deleted): {1}' -f (Count-Of $missing.Count 'picture is' 'pictures are'), (($missing | Select-Object -First 5) -join ', ')) -ForegroundColor Yellow
    }
    if ($noRow.Count -gt 0) {
        Write-Host ('{0} no row in the batch lists, so no caption (left out): {1}' -f (Count-Of $noRow.Count 'picture has' 'pictures have'), (($noRow | Select-Object -First 5) -join ', ')) -ForegroundColor Yellow
    }
    if ($failed.Count -gt 0) {
        Write-Host ('{0} could not be made: {1}' -f (Count-Of $failed.Count 'picture' 'pictures'), (($failed | Select-Object -First 3) -join '; ')) -ForegroundColor Red
    }
    Write-Host ''
    Write-Host ('The set: {0} ({1}, each with its caption).' -f $setDir, (Count-Of $inSet 'picture' 'pictures')) -ForegroundColor Green
    Write-Host 'Next, in FluxGym: upload these pictures and .txt files together with the first training set''s 47 pictures and'
    Write-Host 'their captions, and train as in STYLE-RIMWORLD.md, "Training again".'
}

try { Make-Set }
catch {
    Write-Host ''
    Write-Host ('Something went wrong: ' + $_.Exception.Message) -ForegroundColor Red
}
Write-Host ''
if (-not $NoPause) { Read-Host 'Press Enter to close' }
