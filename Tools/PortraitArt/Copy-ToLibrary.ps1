# Copy-ToLibrary.ps1, for Rimfolk's portraits.
#
# Put this file (and Copy-ToLibrary.cmd, which starts it with a double-click) into ComfyUI's output\Rimfolk folder, or
# into one batch's folder in it (output\Rimfolk\colonists). It copies the pictures there into Rimfolk's library, each
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

    # This folder holds pictures itself (one batch's folder), or one folder per batch (output\Rimfolk).
    $sources = @()
    if (@(Get-ChildItem -LiteralPath $here -Filter '*.png' -File).Count -gt 0) { $sources = @(Get-Item -LiteralPath $here) }
    else { $sources = @(Get-ChildItem -LiteralPath $here -Directory) }
    if ($sources.Count -eq 0) { throw ('No pictures here, and no batch folders: put this file into output\Rimfolk or into a batch folder in it ({0}).' -f $here) }

    $copied = 0; $skipped = 0; $renamed = 0; $extraCopied = 0
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
