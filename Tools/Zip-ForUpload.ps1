# Zip-ForUpload.ps1, for Rimfolk.
#
# Packs files and folders into zip files of at most 28 MiB each (just under 30 MB), ready to upload. If everything fits,
# you get one zip; if not, it is split into several zips, each a whole zip of its own, so they can be uploaded in turns.
# A single file too big for one zip (a large save, a video) is cut into numbered pieces (name.001, name.002, ...) spread
# over the zips; they are joined again after the upload. The zips go to a folder "For upload" on your Desktop, which
# opens when they are ready. Nothing else is changed: your files are only read.
#
# Install it once: right-click this file and choose "Run with PowerShell". That puts "Zip for upload" in the right-click
# menu's "Send to" list and an icon "Zip for upload" on your Desktop.
# Then, to zip something:
#   - right-click one or more files or folders, then "Send to", then "Zip for upload" (on Windows 11 the "Send to" list
#     is under "Show more options", or hold Shift while you right-click); or
#   - drag the files or folders onto the "Zip for upload" icon on your Desktop.
# A file the game has open (a save while RimWorld runs) is read as it is on disk.
# To remove it: delete the two "Zip for upload" shortcuts (Desktop, and the Send To folder: type shell:sendto in the
# Explorer address bar) and the folder ZipForUpload in %LOCALAPPDATA%.

[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Paths,
    # The most one zip may hold, in MiB. 28 MiB is 29.4 MB, under the 30 MB upload limit.
    [double] $LimitMiB = 28,
    # Where the zips go; the Desktop's "For upload" folder if not given.
    [string] $OutDir = ''
)

$ErrorActionPreference = 'Stop'
$AppName = 'Zip for upload'
$OnWindows = ($env:OS -eq 'Windows_NT')

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Write-Step([string] $text) { Write-Host $text }

# ---------------------------------------------------------------- install

function Install-Shortcuts {
    if (-not $OnWindows) { throw 'Installing the shortcuts needs Windows.' }
    $InstallDir = Join-Path $env:LOCALAPPDATA 'ZipForUpload'
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    $target = Join-Path $InstallDir 'Zip-ForUpload.ps1'
    if ($PSCommandPath -ne $target) { Copy-Item -LiteralPath $PSCommandPath -Destination $target -Force }
    try { Unblock-File -LiteralPath $target } catch { }
    $shell = New-Object -ComObject WScript.Shell
    $ps = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $places = @([Environment]::GetFolderPath('SendTo'), [Environment]::GetFolderPath('Desktop'))
    foreach ($dir in $places) {
        $lnk = $shell.CreateShortcut((Join-Path $dir ($AppName + '.lnk')))
        $lnk.TargetPath = $ps
        $lnk.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $target + '"'
        $lnk.WorkingDirectory = $InstallDir
        $lnk.IconLocation = (Join-Path $env:SystemRoot 'System32\zipfldr.dll') + ',0'
        $lnk.Description = 'Zip files and folders for upload, in parts of at most 28 MiB'
        $lnk.Save()
    }
    Write-Host ''
    Write-Host 'Installed.' -ForegroundColor Green
    Write-Host 'To zip something: right-click files or folders, then Send to, then "Zip for upload"'
    Write-Host '(on Windows 11 under "Show more options"), or drag them onto the "Zip for upload" icon on your Desktop.'
    Write-Host ''
    Read-Host 'Press Enter to close'
}

# ---------------------------------------------------------------- what to pack

# One file to pack: where it is, its name inside the zip, and (for a piece of a file too big for one zip) which bytes.
function New-Item2([string] $full, [string] $name, [long] $length) {
    New-Object PSObject -Property @{ Full = $full; Name = $name; Offset = [long]0; Bytes = $length; Estimate = [long]0 }
}

function Get-Files([string[]] $paths) {
    $list = New-Object System.Collections.ArrayList
    $taken = @{}
    foreach ($p in $paths) {
        if ([string]::IsNullOrWhiteSpace($p)) { continue }
        $item = Get-Item -LiteralPath $p -Force
        if ($item.PSIsContainer) {
            $base = Split-Path -Parent $item.FullName
            foreach ($f in (Get-ChildItem -LiteralPath $item.FullName -Recurse -File -Force)) {
                $rel = $f.FullName.Substring($base.Length).TrimStart('\', '/')
                [void] $list.Add((New-Item2 $f.FullName ($rel -replace '\\', '/') $f.Length))
            }
        }
        else {
            [void] $list.Add((New-Item2 $item.FullName $item.Name $item.Length))
        }
    }
    # Two files of the same name from different places keep both: the second gets " (2)".
    foreach ($x in $list) {
        $n = $x.Name
        $k = 2
        while ($taken.ContainsKey($n.ToLowerInvariant())) {
            $dot = $x.Name.LastIndexOf('.')
            if ($dot -gt $x.Name.LastIndexOf('/') + 1) { $n = $x.Name.Substring(0, $dot) + " ($k)" + $x.Name.Substring($dot) }
            else { $n = $x.Name + " ($k)" }
            $k++
        }
        $taken[$n.ToLowerInvariant()] = $true
        $x.Name = $n
    }
    return , $list
}

function Get-BaseName([string[]] $paths) {
    $first = Get-Item -LiteralPath $paths[0] -Force
    $name = if ($first.PSIsContainer) { $first.Name } else { [IO.Path]::GetFileNameWithoutExtension($first.Name) }
    if ($paths.Count -gt 1) { $name = $name + ' and ' + ($paths.Count - 1) + ' more' }
    $bad = [IO.Path]::GetInvalidFileNameChars()
    $clean = -join ($name.ToCharArray() | ForEach-Object { if ($bad -contains $_) { '_' } else { $_ } })
    if ($clean.Length -gt 60) { $clean = $clean.Substring(0, 60) }
    return $clean + ' ' + (Get-Date -Format 'yyyy-MM-dd HHmm')
}

# ---------------------------------------------------------------- writing zips

# Copies a file (or the bytes of one piece of it) into a new entry. The file is opened so that a program writing it
# (the game with a save open) doesn't stop it being read.
function Add-Entry($zip, $x) {
    $entry = $zip.CreateEntry($x.Name, [System.IO.Compression.CompressionLevel]::Optimal)
    try { $entry.LastWriteTime = [DateTimeOffset](Get-Item -LiteralPath $x.Full -Force).LastWriteTime } catch { }
    $src = New-Object IO.FileStream($x.Full, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try {
        $dst = $entry.Open()
        try {
            [void] $src.Seek($x.Offset, [IO.SeekOrigin]::Begin)
            $buf = New-Object byte[] (1MB)
            $left = $x.Bytes
            while ($left -gt 0) {
                $want = [int][Math]::Min([long]$buf.Length, $left)
                $got = $src.Read($buf, 0, $want)
                if ($got -le 0) { break }
                $dst.Write($buf, 0, $got)
                $left -= $got
            }
        }
        finally { $dst.Dispose() }
    }
    finally { $src.Dispose() }
}

function Write-Zip([string] $path, $items) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    $zip = [System.IO.Compression.ZipFile]::Open($path, [System.IO.Compression.ZipArchiveMode]::Create)
    try { foreach ($x in $items) { Add-Entry $zip $x } }
    finally { $zip.Dispose() }
    return (Get-Item -LiteralPath $path).Length
}

# Each file's size once compressed, read from a first zip of everything.
function Read-Compressed([string] $path) {
    $sizes = @{}
    $zip = [System.IO.Compression.ZipFile]::OpenRead($path)
    try { foreach ($e in $zip.Entries) { $sizes[$e.FullName] = @($e.CompressedLength, $e.Length) } }
    finally { $zip.Dispose() }
    return $sizes
}

# What one entry adds to a zip besides its data: its header and its line in the zip's index.
function Get-Overhead($x) { return 120 + 2 * [Text.Encoding]::UTF8.GetByteCount($x.Name) }

# Files in order, cut into pieces where one is too big for a zip, then put into parts of at most $budget bytes.
function Get-Parts($files, $sizes, [long] $budget) {
    $pieces = New-Object System.Collections.ArrayList
    foreach ($x in $files) {
        $s = $sizes[$x.Name]
        $packed = [long]$s[0]
        $ratio = if ($x.Bytes -gt 0) { [Math]::Max(0.02, $packed / [double]$x.Bytes) } else { 1.0 }
        if ($packed + (Get-Overhead $x) -le $budget) {
            $x.Estimate = $packed + (Get-Overhead $x)
            [void] $pieces.Add($x)
            continue
        }
        # Too big for one zip: numbered pieces, each sized to fill most of a zip by how well the file compresses.
        $chunk = [long][Math]::Floor(($budget - 4096) / ($ratio * 1.15))
        $chunk = [Math]::Max([long]1MB, [Math]::Min($chunk, $x.Bytes))
        $n = [int][Math]::Ceiling($x.Bytes / [double]$chunk)
        for ($i = 0; $i -lt $n; $i++) {
            $offset = $i * $chunk
            $count = [Math]::Min($chunk, $x.Bytes - $offset)
            $piece = New-Item2 $x.Full ($x.Name + '.' + ($i + 1).ToString('000')) $count
            $piece.Offset = $offset
            $piece.Estimate = [long][Math]::Ceiling($count * $ratio * 1.15) + (Get-Overhead $piece)
            [void] $pieces.Add($piece)
        }
    }
    $parts = New-Object System.Collections.ArrayList
    $current = New-Object System.Collections.ArrayList
    $used = [long]22
    foreach ($x in $pieces) {
        if ($current.Count -gt 0 -and $used + $x.Estimate -gt $budget) {
            [void] $parts.Add($current)
            $current = New-Object System.Collections.ArrayList
            $used = [long]22
        }
        [void] $current.Add($x)
        $used += $x.Estimate
    }
    if ($current.Count -gt 0) { [void] $parts.Add($current) }
    return , $parts
}

function Invoke-Pack([string[]] $paths) {
    $limit = [long]($LimitMiB * 1MB)
    if ($OutDir -eq '') { $OutDir = Join-Path ([Environment]::GetFolderPath('Desktop')) 'For upload' }
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
    $files = Get-Files $paths
    if ($files.Count -eq 0) { throw 'There are no files in what was given.' }
    $base = Get-BaseName $paths
    $total = ($files | Measure-Object -Property Bytes -Sum).Sum
    Write-Step ("Packing {0} file(s), {1:N1} MB ..." -f $files.Count, ($total / 1MB))

    $whole = Join-Path $OutDir ($base + '.zip')
    $size = Write-Zip $whole $files
    if ($size -le $limit) {
        Write-Step ("Done: one zip, {0:N1} MB." -f ($size / 1MB))
        return @($whole)
    }

    Write-Step ("All together it is {0:N1} MB, over the limit: splitting it ..." -f ($size / 1MB))
    $sizes = Read-Compressed $whole
    Remove-Item -LiteralPath $whole -Force
    $budget = $limit - 65536
    for ($try = 1; $try -le 4; $try++) {
        $parts = Get-Parts $files $sizes $budget
        $made = New-Object System.Collections.ArrayList
        $over = $false
        for ($i = 0; $i -lt $parts.Count; $i++) {
            $name = '{0} part {1} of {2}.zip' -f $base, ($i + 1), $parts.Count
            $path = Join-Path $OutDir $name
            $s = Write-Zip $path $parts[$i]
            [void] $made.Add($path)
            Write-Step ("  {0}: {1:N1} MB" -f $name, ($s / 1MB))
            if ($s -gt $limit) { $over = $true }
        }
        if (-not $over) {
            Write-Step ("Done: {0} zips, each under {1} MiB. Upload all of them." -f $made.Count, $LimitMiB)
            return $made.ToArray()
        }
        # A part came out larger than its estimate: try again with smaller parts.
        foreach ($m in $made) { Remove-Item -LiteralPath $m -Force }
        $budget = [long]($budget * 0.8)
        Write-Step 'One part came out too large; packing again in smaller parts ...'
    }
    throw 'Could not keep every part under the limit.'
}

# ---------------------------------------------------------------- start

try {
    if ($Paths -eq $null -or $Paths.Count -eq 0) {
        Install-Shortcuts
        return
    }
    $made = Invoke-Pack $Paths
    if ($OnWindows -and $made.Count -gt 0) { Start-Process explorer.exe -ArgumentList ('/select,"' + $made[0] + '"') }
    Start-Sleep -Seconds 3
}
catch {
    Write-Host ''
    Write-Host ('Something went wrong: ' + $_.Exception.Message) -ForegroundColor Red
    Read-Host 'Press Enter to close'
}
