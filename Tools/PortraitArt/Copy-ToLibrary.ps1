# Copy-ToLibrary.ps1, for Rimfolk's portraits.
#
# Put this file (and Copy-ToLibrary.cmd, which starts it with a double-click) into ComfyUI's output\Rimfolk folder (put
# into one batch's folder in it, it still does every batch). It copies the pictures there into Rimfolk's library, each
# batch into the library folder of the same name (output\Rimfolk\colonists -> library\colonists), and then writes the
# library's records again (Update-PortraitLibrary.ps1), so the game finds them after "Reload the library".
# - People: only the cut-outs (..._cut_...png); the pictures with their painted background stay in ComfyUI's output.
#   Backdrops (a batch whose list has a "place" column): every picture.
# - A picture already in the library (the same picture under its name or a "(2)" copy of it) is skipped, so running it
#   again copies only what is new.
# - Nothing is renamed: a picture's name ties it to its batch row. If the library holds another picture of the same
#   name, the new one is copied as "name (2).png", which the library script still reads as its row's.
# - Nothing is deleted or moved: throw out the pictures you don't want before running it (or delete them from the
#   library afterwards and run Update-PortraitLibrary.ps1 again).
# - A picture deleted for good in the game (listed in library\deleted.csv) is never copied again, nor its depth map or
#   expressions, even though it is still in ComfyUI's output.
# - A depth map (<picture>_depth.png) or an expression (<picture>_v-blink.png ...) here belongs to its picture: it is
#   copied only with its picture, under its picture's name in the library, and never counts as a picture of its own.
#   (Make-DepthMaps.ps1 and Make-Variants.ps1 write them straight into the library, so they are seldom here.)
# The first time it asks where your Rimfolk-portraits folder is (the one with Update-PortraitLibrary.ps1 and library\)
# and remembers it in copy-settings.json next to this file.

[CmdletBinding()]
param(
    [string] $Kit = ''
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $PSCommandPath
$settingsFile = Join-Path $here 'copy-settings.json'

function Has($o, [string] $name) { return ($o -ne $null) -and ($o.PSObject.Properties.Name -contains $name) }
function Is-Kit([string] $p) { return ($p -ne '') -and (Test-Path -LiteralPath (Join-Path $p 'Update-PortraitLibrary.ps1')) }

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
    if ($Kit -eq '' -and (Test-Path -LiteralPath $settingsFile)) {
        $s = Get-Content -LiteralPath $settingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ((Has $s 'kit') -and (Is-Kit ([string]$s.kit))) { $Kit = [string]$s.kit }
    }
    while (-not (Is-Kit $Kit)) {
        Write-Host 'Where is your Rimfolk-portraits folder (the one with Update-PortraitLibrary.ps1 and the library folder)?'
        Write-Host '(In Explorer: open it, click the address bar, copy the path, paste it here.)'
        $Kit = (Read-Host 'Folder').Trim('"', ' ')
        if (-not (Is-Kit $Kit)) { Write-Host "Update-PortraitLibrary.ps1 isn't in that folder; try again." -ForegroundColor Yellow }
    }
    @{ kit = $Kit } | ConvertTo-Json | Set-Content -LiteralPath $settingsFile -Encoding UTF8
    $library = Join-Path $Kit 'library'
    $deleted = Read-Deleted $library

    # Every batch folder of output\Rimfolk, wherever in it this file sits: in output\Rimfolk itself, or in one batch's
    # folder (then its folder's parent is output\Rimfolk, and all the batches in it are copied, not only that one).
    $root = $here
    if (@(Get-ChildItem -LiteralPath $here -Filter '*.png' -File).Count -gt 0) { $root = Split-Path -Parent $here }
    # A style test (style-test: the same people in several styles, only to compare) is never copied into the library.
    $sources = @(Get-ChildItem -LiteralPath $root -Directory | Where-Object { $_.Name -notmatch '(?i)^style-test' -and @(Get-ChildItem -LiteralPath $_.FullName -Filter '*.png' -File).Count -gt 0 })
    if ($sources.Count -eq 0) { throw ('No batch folders with pictures in {0}: put this file into ComfyUI''s output\Rimfolk folder.' -f $root) }
    Write-Host ('Batch folders in {0}: {1}' -f $root, (($sources | ForEach-Object { $_.Name }) -join ', '))

    $copied = 0; $skipped = 0; $renamed = 0; $extraCopied = 0; $held = 0
    $extraPattern = '_(depth|v-[A-Za-z]+)\.png$'
    foreach ($dir in $sources) {
        $all = @(Get-ChildItem -LiteralPath $dir.FullName -Filter '*.png' -File)
        $files = @($all | Where-Object { $_.Name -notmatch $extraPattern })
        $extras = @($all | Where-Object { $_.Name -match $extraPattern })
        if ($files.Count -eq 0) { continue }
        # People's batches (their list has no "place" column): only the cut-outs, never the pictures with their painted
        # background, which stay in ComfyUI's output to look at. Backdrops (a "place" column): every picture.
        $csv = Join-Path (Join-Path $Kit 'batches') ($dir.Name + '.csv')
        $isBackdrops = $false
        if (Test-Path -LiteralPath $csv) { $isBackdrops = ((Get-Content -LiteralPath $csv -TotalCount 1 -Encoding UTF8) -split ',') -contains 'place' }
        if (-not $isBackdrops) {
            $files = @($files | Where-Object { $_.Name -match '_cut_' })
            if ($files.Count -eq 0) { Write-Host ('{0}: no cut-outs here (..._cut_...png), nothing copied' -f $dir.Name) -ForegroundColor Yellow; continue }
        }
        $target = Join-Path $library $dir.Name
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        $n = 0
        $inLibrary = @{}   # each picture here (its name without .png, small letters) -> its name in the library
        foreach ($f in $files) {
            $stem = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
            # Deleted for good in the game: never copied again (its depth map and expressions stay out with it).
            if (Is-Deleted $f.FullName $deleted $true) { $held++; continue }
            $to = Join-Path $target $f.Name
            if (Test-Path -LiteralPath $to) {
                # Already there under this name or as one of its "(2)" copies, with the same content: skip it.
                $pattern = '^' + [regex]::Escape($stem) + '( \(\d+\))?\.png$'
                $hash = $null; $same = $null
                foreach ($e in @(Get-ChildItem -LiteralPath $target -Filter '*.png' -File | Where-Object { $_.Name -match $pattern -and $_.Length -eq $f.Length })) {
                    if ($hash -eq $null) { $hash = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash }
                    if ((Get-FileHash -LiteralPath $e.FullName -Algorithm SHA256).Hash -eq $hash) { $same = $e; break }
                }
                if ($same -ne $null) { $skipped++; $inLibrary[$stem.ToLowerInvariant()] = [System.IO.Path]::GetFileNameWithoutExtension($same.Name); continue }
                $k = 2
                do { $to = Join-Path $target ('{0} ({1}).png' -f $stem, $k); $k++ } while (Test-Path -LiteralPath $to)
                $renamed++
            }
            Copy-Item -LiteralPath $f.FullName -Destination $to
            $inLibrary[$stem.ToLowerInvariant()] = [System.IO.Path]::GetFileNameWithoutExtension($to)
            $copied++; $n++
        }
        # Depth maps and expressions go with their picture, under its name in the library; one already there stays.
        $xn = 0
        foreach ($x in $extras) {
            $m = [regex]::Match([System.IO.Path]::GetFileNameWithoutExtension($x.Name), '^(?<base>.+)(?<end>_(depth|v-[A-Za-z]+))$')
            $base = $m.Groups['base'].Value.ToLowerInvariant()
            if (-not $inLibrary.ContainsKey($base)) { continue }
            $to = Join-Path $target ($inLibrary[$base] + $m.Groups['end'].Value + '.png')
            if (Test-Path -LiteralPath $to) { continue }
            Copy-Item -LiteralPath $x.FullName -Destination $to
            $extraCopied++; $xn++
        }
        if ($n -gt 0 -or $xn -gt 0) {
            $line = '{0}: {1} new pictures into library\{0}' -f $dir.Name, $n
            if ($xn -gt 0) { $line += (', and {0} depth maps or expressions with their pictures' -f $xn) }
            Write-Host $line
        }
    }
    Write-Host ''
    Write-Host ('Copied {0} pictures ({1} already there, {2} under a "(2)" name).' -f $copied, $skipped, $renamed) -ForegroundColor Green
    if ($held -gt 0) { Write-Host ('{0} pictures deleted for good in the game (library\deleted.csv) were not copied again.' -f $held) }
    if ($copied -gt 0 -or $extraCopied -gt 0) {
        Write-Host 'Writing the library records ...'
        & (Join-Path $Kit 'Update-PortraitLibrary.ps1') -NoPause
    }
    Write-Host 'In the game: Rimfolk settings, "Reload the library" (or restart).'
}
catch {
    Write-Host ''
    Write-Host ('Something went wrong: ' + $_.Exception.Message) -ForegroundColor Red
}
Write-Host ''
Read-Host 'Press Enter to close'
