# Make-Variants.ps1, for Rimfolk's portraits.
#
# Makes the expressions of people's pictures: the same picture with the eyes closed (blink), a small smile (smile), a
# frown (frown) and a wince of pain (pain), the same size and framing with the same outline, next to the picture under
# its name with _v- and the word added (library\colonists\<name>.png gets <name>_v-blink.png, <name>_v-smile.png ...).
# Rimfolk shows them on the large portrait: a blink now and then, and the face by the person's mood.
# - Which pictures: by default the ones the game has given someone, your colonists first (pictures made for one
#   colonist, with a name in their row's pawn column), then the people your newest save has pictures for (the colony's
#   own first, then the notable, then the rest, the most lately shown first); or every person in the library. It asks
#   when it starts. The save is only read, never changed.
# - The workflow: rimfolk-variant-workflow-api.json next to this script, exported from ComfyUI with Workflow, Export
#   (API) from ComfyUI's own FLUX.1 Kontext Dev template: a Load Image box, the edit box with %INSTRUCTION% in its prompt,
#   a Join Image with Alpha box that gives the edited picture the outline of the one it was made from, and a Preview
#   Image (or Save Image) box. See docs/portrait-art/SETUP-ComfyUI.md, step 10.
# - Each picture is uploaded to ComfyUI (as input\rimfolk-variant.png, the same name every time), and for each missing
#   expression the edit is run with a plain instruction ("Close the person's eyes ..."), fetched back, checked to be
#   the picture's size and a cut-out like it, and written next to it. An expression already there is never made again
#   (delete it to have it made again). The window can be closed at any time: what is made is kept, and the next run
#   carries on.
# - It never renames, moves or deletes a picture and changes nothing else in the library; at the end it writes the
#   records again (Update-PortraitLibrary.ps1), which give each expression a record of its own. Then "Reload the
#   library" in the game.
#
# Run it: keep ComfyUI open and double-click Make-Variants.cmd (or right-click this file, "Run with PowerShell").

[CmdletBinding()]
param(
    [string] $Folder = '',
    [string] $Workflow = '',
    [string] $Server = '',
    [string] $Which = '',
    [string] $Words = '',
    [string] $Save = '',
    [switch] $NoPause
)

$ErrorActionPreference = 'Stop'
if ($Folder -eq '') { $Folder = Split-Path -Parent $PSCommandPath }
$library = Join-Path $Folder 'library'
$settingsFile = Join-Path $Folder 'check-settings.json'
if ($Workflow -eq '') { $Workflow = Join-Path $Folder 'rimfolk-variant-workflow-api.json' }

# ---------------- The expressions ----------------
# The instruction for each word, as the edit model reads it: plainly what changes, then what stays.

$Keep = 'Keep everything else exactly the same: the same person and face, hair, clothes, pose, position, framing, lighting and painting style.'
$Instructions = [ordered]@{
    'blink' = 'Close the person''s eyes, as in the middle of a blink, the eyelids fully shut and relaxed. ' + $Keep
    'smile' = 'Give the person a small, warm smile with the mouth closed. ' + $Keep
    'frown' = 'Make the person frown, the brows drawn together and down, the mouth set in a slight scowl. ' + $Keep
    'pain'  = 'Make the person wince in pain, the eyes squeezed half shut, the brows drawn up and together, the teeth clenched. ' + $Keep
}
# The boxes that give a picture its cut-out: Join Image with Alpha (the outline of the picture it was made from), or a
# background remover.
$CutPattern = '(?i)JoinImageWithAlpha|rmbg|birefnet|remove.?background|rembg|inspyrenet|\bben2?\b'

# ---------------- Small helpers ----------------

function Has($o, [string] $name) { return ($o -ne $null) -and ($o.PSObject.Properties.Name -contains $name) }
function Short([string] $v) {
    if ($v -eq $null) { return '' }
    $t = ($v -replace '\s+', ' ').Trim()
    if ($t.Length -gt 300) { $t = $t.Substring(0, 297) + '...' }
    return $t
}
function Is-Link($v) { return ($v -is [array]) -and ($v.Count -eq 2) -and ($v[0] -is [string]) }
function Is-Extra([string] $stem) { return $stem -match '_(depth|v-[A-Za-z]+)$' }
function Count-Of([int] $n, [string] $one, [string] $many) { if ($n -eq 1) { return ('1 ' + $one) } else { return ('{0} {1}' -f $n, $many) } }

function Png-Info([byte[]] $b) {
    # A PNG's width, height and whether it has transparency (an alpha channel or a tRNS chunk), or $null.
    if ($b -eq $null -or $b.Length -lt 33) { return $null }
    if ($b[0] -ne 0x89 -or $b[1] -ne 0x50 -or $b[2] -ne 0x4E -or $b[3] -ne 0x47) { return $null }
    $w = ([int]$b[16] -shl 24) -bor ([int]$b[17] -shl 16) -bor ([int]$b[18] -shl 8) -bor [int]$b[19]
    $h = ([int]$b[20] -shl 24) -bor ([int]$b[21] -shl 16) -bor ([int]$b[22] -shl 8) -bor [int]$b[23]
    $type = [int]$b[25]
    $alpha = ($type -eq 4 -or $type -eq 6)
    $at = 8
    while (-not $alpha -and $at + 8 -le $b.Length) {
        $len = ([int]$b[$at] -shl 24) -bor ([int]$b[$at + 1] -shl 16) -bor ([int]$b[$at + 2] -shl 8) -bor [int]$b[$at + 3]
        $name = [System.Text.Encoding]::ASCII.GetString($b, $at + 4, 4)
        if ($name -eq 'tRNS') { $alpha = $true }
        if ($name -eq 'IDAT' -or $name -eq 'IEND' -or $len -lt 0) { break }
        $at += 12 + $len
    }
    return [pscustomobject]@{ w = $w; h = $h; alpha = $alpha; grey = ($type -eq 0 -or $type -eq 4) }
}

# ---------------- Talking to ComfyUI ----------------

function New-Http([int] $seconds) {
    $handler = New-Object System.Net.Http.HttpClientHandler
    $handler.UseProxy = $false
    $c = New-Object System.Net.Http.HttpClient($handler)
    $c.Timeout = [TimeSpan]::FromSeconds($seconds)
    return $c
}

function Http-Send($client, [string] $method, [string] $url, $content) {
    # Returns @{ ok; status; text }; throws only when ComfyUI can't be reached.
    if ($method -eq 'GET') { $task = $client.GetAsync($url) } else { $task = $client.PostAsync($url, $content) }
    $res = $task.GetAwaiter().GetResult()
    $text = $res.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    return @{ ok = $res.IsSuccessStatusCode; status = [int]$res.StatusCode; text = $text }
}

function Http-Bytes([string] $url) {
    $res = $script:Http.GetAsync($url).GetAwaiter().GetResult()
    $bytes = $res.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
    return @{ ok = $res.IsSuccessStatusCode; status = [int]$res.StatusCode; bytes = $bytes }
}

function Test-Comfy([string] $url) {
    try {
        $r = Http-Send $script:Probe 'GET' ($url.TrimEnd('/') + '/system_stats') $null
        return $r.ok
    }
    catch { return $false }
}

function Find-Server {
    # The address given, the one that worked last time (kept with the picture check's), then ComfyUI's two usual ones;
    # else ask for it.
    $tries = New-Object System.Collections.ArrayList
    if ($Server -ne '') { [void]$tries.Add($Server) }
    $saved = $null
    if (Test-Path -LiteralPath $settingsFile) {
        try {
            $saved = Get-Content -LiteralPath $settingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
            if ((Has $saved 'server') -and $saved.server) { [void]$tries.Add([string]$saved.server) }
        }
        catch { $saved = $null }
    }
    [void]$tries.Add('http://127.0.0.1:8188'); [void]$tries.Add('http://127.0.0.1:8000')
    while ($true) {
        foreach ($t in $tries) {
            $u = $t.Trim().TrimEnd('/')
            if ($u -notmatch '^https?://') { $u = 'http://' + $u }
            if (Test-Comfy $u) {
                try {
                    if ($saved -eq $null) { $saved = [pscustomobject]@{} }
                    $saved | Add-Member -NotePropertyName 'server' -NotePropertyValue $u -Force
                    $saved | ConvertTo-Json | Set-Content -LiteralPath $settingsFile -Encoding UTF8
                }
                catch { }
                return $u
            }
        }
        Write-Host ''
        Write-Host ('ComfyUI is not answering (tried {0}). Is it open?' -f (($tries | Select-Object -Unique) -join ', ')) -ForegroundColor Yellow
        Write-Host 'Open it and press Enter, or type its address (in ComfyUI: Settings, Server-Config) and press Enter. Type q to stop.'
        $a = (Read-Host 'Address').Trim()
        if ($a -eq 'q') { throw 'Stopped: ComfyUI was not reachable.' }
        if ($a -ne '') { $tries.Insert(0, $a) }
    }
}

function Upload-Picture([string] $path, [string] $as) {
    # The picture goes to ComfyUI's input folder, always under the same name, so the folder doesn't fill up.
    $content = New-Object System.Net.Http.MultipartFormDataContent
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $file = [System.Net.Http.ByteArrayContent]::new($bytes)
    $file.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('image/png')
    $content.Add($file, 'image', $as)
    $content.Add([System.Net.Http.StringContent]::new('input'), 'type')
    $content.Add([System.Net.Http.StringContent]::new('true'), 'overwrite')
    $r = Http-Send $script:Http 'POST' ($script:Url + '/upload/image') $content
    if (-not $r.ok) { throw ('ComfyUI would not take the picture ({0}): {1}' -f $r.status, (Short $r.text)) }
    $j = $r.text | ConvertFrom-Json
    if ((Has $j 'subfolder') -and $j.subfolder) { return ([string]$j.subfolder + '/' + [string]$j.name) }
    return [string]$j.name
}

function Error-Of([string] $text) {
    # ComfyUI's reason for refusing a prompt, from its error and the boxes' errors.
    try {
        $j = $text | ConvertFrom-Json
        $why = New-Object System.Collections.ArrayList
        if ((Has $j 'error') -and $j.error) { if (Has $j.error 'message') { [void]$why.Add([string]$j.error.message) } else { [void]$why.Add([string]$j.error) } }
        if ((Has $j 'node_errors') -and $j.node_errors) {
            foreach ($p in @($j.node_errors.PSObject.Properties)) {
                $cls = ''; if (Has $p.Value 'class_type') { $cls = [string]$p.Value.class_type }
                foreach ($e in @($p.Value.errors)) {
                    $line = $cls + ': ' + [string]$e.message
                    if ((Has $e 'details') -and $e.details) { $line += ' (' + [string]$e.details + ')' }
                    [void]$why.Add($line)
                }
            }
        }
        if ($why.Count -gt 0) { return ($why -join '; ') }
    }
    catch { }
    return (Short $text)
}

function Run-Comfy($wf, [bool] $first) {
    # Queues the workflow and waits until ComfyUI has run it: its outputs, as ComfyUI's history keeps them.
    $body = @{ prompt = $wf; client_id = $script:Client } | ConvertTo-Json -Depth 64 -Compress
    $json = [System.Net.Http.StringContent]::new($body, [System.Text.Encoding]::UTF8, 'application/json')
    $r = Http-Send $script:Http 'POST' ($script:Url + '/prompt') $json
    if (-not $r.ok) { throw ('ComfyUI refused the workflow (is a model file it names missing from ComfyUI''s models folder?): {0}' -f (Error-Of $r.text)) }
    $id = [string](($r.text | ConvertFrom-Json).prompt_id)
    $lastQueue = [DateTime]::MinValue
    $runningSince = $null
    $gone = 0
    while ($true) {
        $h = Http-Send $script:Http 'GET' ($script:Url + '/history/' + $id) $null
        if ($h.ok -and $h.text.Length -gt 2) {
            $all = $h.text | ConvertFrom-Json
            $p = $all.PSObject.Properties[$id]
            if ($p -ne $null) {
                $entry = $p.Value
                if ((Has $entry 'status') -and $entry.status -and [string]$entry.status.status_str -eq 'error') {
                    $why = 'no reason given'
                    foreach ($msg in @($entry.status.messages)) {
                        if (@($msg).Count -ge 2 -and [string]$msg[0] -eq 'execution_error') { $why = ('{0}: {1}' -f $msg[1].node_type, $msg[1].exception_message) }
                    }
                    throw ('ComfyUI stopped with an error: {0}' -f $why)
                }
                if ((Has $entry 'outputs') -and $entry.outputs) { return $entry.outputs }
                return [pscustomobject]@{}
            }
        }
        if (((Get-Date) - $lastQueue).TotalSeconds -ge 5) {
            $lastQueue = Get-Date
            # Where the job is in ComfyUI's queue, read from the text (the queue holds every waiting job's workflow).
            $q = Http-Send $script:Http 'GET' ($script:Url + '/queue') $null
            $state = 'gone'
            if ($q.ok) {
                $at = $q.text.IndexOf('"' + $id + '"')
                if ($at -ge 0) {
                    $run = $q.text.LastIndexOf('"queue_running"', $at)
                    $wait = $q.text.LastIndexOf('"queue_pending"', $at)
                    if ($run -gt $wait) { $state = 'running' } else { $state = 'waiting' }
                }
            }
            if ($state -eq 'running') {
                if ($runningSince -eq $null) { $runningSince = Get-Date }
                elseif (((Get-Date) - $runningSince).TotalMinutes -ge 15) { throw 'ComfyUI has worked on one picture for 15 minutes; look at ComfyUI (its queue and its window) and run this again.' }
            }
            elseif ($state -eq 'waiting') {
                if ($first -and $script:Said -ne $id) { Write-Host 'ComfyUI is still busy with other work; this waits for it.'; $script:Said = $id }
            }
            else {
                $gone++
                if ($gone -ge 3) { throw 'ComfyUI dropped the job (was it restarted?). Run this again.' }
            }
        }
        Start-Sleep -Milliseconds 500
    }
}

function Dist-To($wf, [string] $from, [string] $pattern) {
    # How many boxes back from box $from the nearest box whose kind matches $pattern is (1: wired straight into it), or 99.
    $seen = @{}
    $front = @($from)
    for ($d = 1; $d -le 40 -and $front.Count -gt 0; $d++) {
        $next = New-Object System.Collections.ArrayList
        foreach ($id in $front) {
            $p = $wf.PSObject.Properties[$id]
            if ($p -eq $null -or $p.Value.inputs -eq $null) { continue }
            foreach ($inp in @($p.Value.inputs.PSObject.Properties)) {
                if (-not (Is-Link $inp.Value)) { continue }
                $src = [string]$inp.Value[0]
                if ($seen.ContainsKey($src)) { continue }
                $seen[$src] = $true
                $sp = $wf.PSObject.Properties[$src]
                if ($sp -ne $null -and [string]$sp.Value.class_type -match $pattern) { return $d }
                [void]$next.Add($src)
            }
        }
        $front = @($next)
    }
    return 99
}

function Pick-Image($wf, $outputs, [string] $prefer) {
    # The picture to keep among the workflow's outputs: from a Save Image or Preview Image box before any other, and of
    # those the one closest after a box whose kind matches $prefer (the depth box, or the box that gives the cut-out).
    $best = $null; $bestKey = [int]::MaxValue
    foreach ($o in @($outputs.PSObject.Properties)) {
        if (-not (Has $o.Value 'images')) { continue }
        $imgs = @(@($o.Value.images) | Where-Object { $_ -ne $null -and (Has $_ 'filename') })
        if ($imgs.Count -eq 0) { continue }
        $node = $wf.PSObject.Properties[$o.Name]
        $cls = ''; if ($node -ne $null) { $cls = [string]$node.Value.class_type }
        $rank = 1; if ($cls -match '(?i)^(SaveImage|PreviewImage)$' -or ($cls -match '(?i)save|preview' -and $cls -notmatch '(?i)mask')) { $rank = 0 }
        $d = 99; if ($node -ne $null) { $d = Dist-To $wf $o.Name $prefer }
        $key = $rank * 1000 + $d
        if ($key -lt $bestKey) { $bestKey = $key; $best = $imgs[0] }
    }
    return $best
}

function Fetch-Image($ref) {
    $type = 'output'; if (Has $ref 'type') { $type = [string]$ref.type }
    $sub = ''; if (Has $ref 'subfolder') { $sub = [string]$ref.subfolder }
    $url = '{0}/view?filename={1}&subfolder={2}&type={3}' -f $script:Url, [Uri]::EscapeDataString([string]$ref.filename), [Uri]::EscapeDataString($sub), [Uri]::EscapeDataString($type)
    $r = Http-Bytes $url
    if (-not $r.ok) { throw ('ComfyUI would not give back the picture {0} ({1}).' -f $ref.filename, $r.status) }
    return , ([byte[]]$r.bytes)
}

function Bypass-Box($wf, [string] $name) {
    # Takes a box out of the picture's way: whatever took its picture takes the picture it was given instead.
    $node = $wf.PSObject.Properties[$name]
    if ($node -eq $null) { return }
    $src = $null
    foreach ($inp in @($node.Value.inputs.PSObject.Properties)) {
        if ((Is-Link $inp.Value) -and ($inp.Name -match '(?i)^(image|images|pixels)$')) { $src = $inp.Value; break }
    }
    if ($src -eq $null) { return }
    foreach ($p in @($wf.PSObject.Properties)) {
        if ($p.Name -eq $name -or $p.Value.inputs -eq $null) { continue }
        foreach ($inp in @($p.Value.inputs.PSObject.Properties)) {
            $v = $inp.Value
            if ((Is-Link $v) -and ([string]$v[0] -eq $name) -and ([int]$v[1] -eq 0)) { $inp.Value = @([string]$src[0], [int]$src[1]) }
        }
    }
    $wf.PSObject.Properties.Remove($name)
}

function Prepare-Workflow($wf, [string] $image, [string] $instruction, [int] $w, [int] $h, [string] $prefix) {
    # The picture into every Load Image box, the instruction where a box says %INSTRUCTION%, a new seed, the picture's
    # own size kept (boxes that would scale it to a size of their own are taken out of the way, sizes set to it), and
    # a Save Image box's files kept apart in ComfyUI's output\Rimfolk-extras folder. Returns what it found.
    $found = @{ loaded = 0; asked = 0 }
    foreach ($p in @($wf.PSObject.Properties)) {
        if ([string]$p.Value.class_type -match '(?i)^(FluxKontextImageScale|ImageScaleToTotalPixels|ImageScaleToMaxDimension)$') { Bypass-Box $wf $p.Name }
    }
    foreach ($node in @($wf.PSObject.Properties)) {
        $n = $node.Value
        if ($n.inputs -eq $null) { continue }
        $cls = [string]$n.class_type
        if ($cls -eq 'ReferenceMultiLatent' -and (Has $n.inputs 'resolution') -and [string]$n.inputs.resolution -eq 'resize') {
            $n.inputs.resolution = 'keep resolution'
            $n.inputs.PSObject.Properties.Remove('resolution.megapixels')
        }
        foreach ($inp in @($n.inputs.PSObject.Properties)) {
            $v = $inp.Value
            $isNumber = ($v -is [int] -or $v -is [long] -or $v -is [double] -or $v -is [decimal])
            if ($inp.Name -eq 'image' -and $v -is [string] -and $cls -match '(?i)^load.?image' -and $cls -notmatch '(?i)mask|batch|folder|url|path') { $inp.Value = $image; $found.loaded++ }
            elseif ($v -is [string] -and $v.Contains('%INSTRUCTION%')) { $inp.Value = $v.Replace('%INSTRUCTION%', $instruction); $found.asked++ }
            elseif ($inp.Name -eq 'filename_prefix' -and $cls -match '(?i)save') { $inp.Value = $prefix }
            elseif (($inp.Name -eq 'seed' -or $inp.Name -eq 'noise_seed') -and $isNumber) { $inp.Value = [long](Get-Random -Minimum 1 -Maximum 2147483647) }
            elseif (($inp.Name -eq 'width' -or $inp.Name -eq 'height') -and $isNumber -and ($cls -match 'Latent' -or $cls -eq 'ImageScale')) {
                if ($inp.Name -eq 'width') { $inp.Value = $w } else { $inp.Value = $h }
            }
        }
    }
    return $found
}

# ---------------- The pictures ----------------

function Read-People {
    # The people's pictures in library\records.xml (not their expressions), in its order: defName, file, and the
    # colonist a picture was made for (pawn).
    $path = Join-Path $library 'records.xml'
    if (-not (Test-Path -LiteralPath $path)) { throw 'The library has no records yet: run Update-PortraitLibrary.ps1 (or Copy-ToLibrary.cmd) first, then this again.' }
    $doc = New-Object System.Xml.XmlDocument
    $doc.Load($path)
    $list = New-Object System.Collections.ArrayList
    foreach ($n in @($doc.DocumentElement.ChildNodes)) {
        if ($n.NodeType -ne [System.Xml.XmlNodeType]::Element -or $n.Name -notmatch 'PortraitDef') { continue }
        $one = { param($tag) $x = $n.SelectSingleNode($tag); if ($x -eq $null) { '' } else { $x.InnerText.Trim() } }
        if ((& $one 'variantOf') -ne '') { continue }
        $tex = (& $one 'texPath').Replace('\', '/')
        $parts = $tex -split '/'
        if ($parts.Count -ne 2) { continue }
        $dir = Join-Path $library $parts[0]
        [void]$list.Add([pscustomobject]@{
            defName = (& $one 'defName'); pawn = (& $one 'pawn'); stem = $parts[1]; dir = $dir
            path = (Join-Path $dir ($parts[1] + '.png')); show = ($parts[0] + '\' + $parts[1] + '.png')
        })
    }
    return $list
}

function Newest-Save([string] $where) {
    # The save to read: the file given, or the newest .rws in the folder given, or in RimWorld's own Saves folder.
    if ($where -ne '' -and (Test-Path -LiteralPath $where -PathType Leaf)) { return (Get-Item -LiteralPath $where) }
    $dir = $where
    if ($dir -eq '') {
        $home_ = $env:USERPROFILE; if (-not $home_) { $home_ = $env:HOME }
        if (-not $home_) { return $null }
        $dir = Join-Path $home_ 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves'
    }
    if (-not (Test-Path -LiteralPath $dir -PathType Container)) { return $null }
    return (Get-ChildItem -LiteralPath $dir -Filter '*.rws' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1)
}

function Read-Given($file) {
    # The pictures Rimfolk has given people in a save (its PortraitKeeper's kept list): the picture's defName, whether it
    # belongs to the colony's own people (pinned or reserved), to someone notable, and when it was last shown. $null
    # when the save has no Rimfolk portraits. The save is only read as text, nothing in it is run.
    $fs = [System.IO.File]::OpenRead($file.FullName)
    try {
        $b1 = $fs.ReadByte(); $b2 = $fs.ReadByte(); $fs.Position = 0
        $stream = $fs
        if ($b1 -eq 0x1F -and $b2 -eq 0x8B) { $stream = New-Object System.IO.Compression.GZipStream($fs, [System.IO.Compression.CompressionMode]::Decompress) }
        $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8)
        $text = $reader.ReadToEnd()
    }
    finally { $fs.Dispose() }
    $at = $text.IndexOf('ColonyCouncil.PortraitKeeper', [System.StringComparison]::Ordinal)
    if ($at -lt 0) { return $null }
    $start = $text.IndexOf('<kept', $at, [System.StringComparison]::Ordinal)
    if ($start -lt 0) { return @() }
    $close = $text.IndexOf('>', $start)
    if ($close -lt 0 -or $text[$close - 1] -eq '/') { return @() }
    $end = $text.IndexOf('</kept>', $start, [System.StringComparison]::Ordinal)
    if ($end -lt 0) { return @() }
    $doc = New-Object System.Xml.XmlDocument
    $doc.LoadXml($text.Substring($start, $end + 7 - $start))
    $text = $null
    $given = New-Object System.Collections.ArrayList
    foreach ($li in @($doc.DocumentElement.SelectNodes('li'))) {
        $one = { param($tag) $x = $li.SelectSingleNode($tag); if ($x -eq $null) { '' } else { $x.InnerText.Trim() } }
        $pic = & $one 'picture'
        if ($pic -eq '') { continue }
        $seen = 0; [void][int]::TryParse((& $one 'seen'), [ref]$seen)
        [void]$given.Add([pscustomobject]@{
            picture = $pic; own = ((& $one 'pinned') -eq 'True' -or (& $one 'reserved') -eq 'True'); notable = ((& $one 'notable') -eq 'True'); seen = $seen
        })
    }
    return $given
}

try {
    Add-Type -AssemblyName System.Net.Http
    if (-not (Test-Path -LiteralPath $library)) { throw ('There is no library folder next to this script ({0}): see SETUP-ComfyUI.md, step 7.' -f $library) }
    $wanted = @($Instructions.Keys)
    if ($Words -ne '') {
        $wanted = @(($Words -split '[,; ]+') | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ -ne '' })
        foreach ($w in $wanted) { if (-not $Instructions.Contains($w)) { throw ('"{0}" is not an expression this script makes ({1}).' -f $w, (@($Instructions.Keys) -join ', ')) } }
    }
    $people = @(Read-People)
    if ($people.Count -eq 0) { throw 'The records have no pictures of people yet (library\records.xml).' }
    $byDef = @{}
    foreach ($p in $people) { $byDef[$p.defName] = $p }

    if ($Which -eq '') {
        Write-Host ''
        Write-Host 'Expressions for which pictures?'
        Write-Host '  Enter: the pictures your game has given someone, your colonists first'
        Write-Host '  a:     every person in the library'
        $Which = Read-Host 'Your choice'
    }
    $all = ($Which -match '^(?i)a')

    # The order: pictures made for one colonist, then the ones the newest save has given the colony's own people, then
    # someone notable, then the rest of the save's (the most lately shown first); with every person, the rest after.
    $order = New-Object System.Collections.ArrayList
    $taken = @{}
    foreach ($p in $people) { if ($p.pawn -ne '') { [void]$order.Add($p); $taken[$p.defName] = $true } }
    $pinned = $order.Count
    $fromSave = 0
    $saveFile = Newest-Save $Save
    if ($saveFile -eq $null) {
        Write-Host ''
        if ($Save -ne '') { Write-Host ('No RimWorld save at {0}.' -f $Save) -ForegroundColor Yellow }
        else { Write-Host 'No RimWorld save found in RimWorld''s Saves folder.' -ForegroundColor Yellow }
        if (-not $all) { Write-Host 'Only the pictures made for your colonists (a name in their row''s pawn column) are given expressions.' }
    }
    else {
        Write-Host ''
        Write-Host ('Reading your newest save, {0} ({1:yyyy-MM-dd HH:mm}) ...' -f $saveFile.Name, $saveFile.LastWriteTime)
        $given = Read-Given $saveFile
        if ($given -eq $null) { Write-Host 'That save has no Rimfolk portraits yet.' -ForegroundColor Yellow }
        else {
            $ranked = @($given | Sort-Object @{ Expression = { if ($_.own) { 0 } elseif ($_.notable) { 1 } else { 2 } } }, @{ Expression = { $_.seen }; Descending = $true })
            foreach ($g in $ranked) {
                if ($taken.ContainsKey($g.picture) -or -not $byDef.ContainsKey($g.picture)) { continue }
                [void]$order.Add($byDef[$g.picture]); $taken[$g.picture] = $true; $fromSave++
            }
            $inLibrary = @($given | Where-Object { $byDef.ContainsKey($_.picture) })
            Write-Host ('It has given {0} of the library to people, {1} of them to the colony''s own.' -f (Count-Of $inLibrary.Count 'picture' 'pictures'), @($inLibrary | Where-Object { $_.own }).Count)
        }
    }
    if ($all) { foreach ($p in $people) { if (-not $taken.ContainsKey($p.defName)) { [void]$order.Add($p); $taken[$p.defName] = $true } } }

    # What each picture still lacks.
    $todo = New-Object System.Collections.ArrayList
    $complete = 0; $missingFile = 0
    foreach ($p in $order) {
        if (-not (Test-Path -LiteralPath $p.path)) { $missingFile++; continue }
        $lack = @($wanted | Where-Object { -not (Test-Path -LiteralPath (Join-Path $p.dir ($p.stem + '_v-' + $_ + '.png'))) })
        if ($lack.Count -eq 0) { $complete++; continue }
        [void]$todo.Add([pscustomobject]@{ pic = $p; lack = $lack })
    }
    $edits = 0; foreach ($t in $todo) { $edits += $t.lack.Count }
    Write-Host ''
    Write-Host ('{0} chosen ({1} made for your colonists, {2} from the save{3}); {4} of them have every expression already.' -f (Count-Of $order.Count 'picture' 'pictures'), $pinned, $fromSave, $(if ($all) { ', the rest of the library' } else { '' }), $complete)
    if ($missingFile -gt 0) { Write-Host ('{0} of them are in the records but not in the library folder: run Update-PortraitLibrary.ps1.' -f $missingFile) -ForegroundColor Yellow }

    $made = 0
    $byWord = [ordered]@{}; foreach ($w in $wanted) { $byWord[$w] = 0 }
    $problems = New-Object System.Collections.ArrayList
    if ($todo.Count -gt 0) {
        if (-not (Test-Path -LiteralPath $Workflow)) {
            throw ('The expressions'' workflow {0} is missing. Make it in ComfyUI and save it with Workflow, Export (API) under that name (SETUP-ComfyUI.md, step 10).' -f $Workflow)
        }
        $wfText = Get-Content -LiteralPath $Workflow -Raw -Encoding UTF8
        $probe = $wfText | ConvertFrom-Json
        if ($probe.PSObject.Properties.Name -contains 'nodes') { throw 'This is a saved workflow, not an API export. In ComfyUI use Workflow, Export (API) and save it again under the same name.' }
        $classes = @($probe.PSObject.Properties | ForEach-Object { [string]$_.Value.class_type })
        if (@($classes | Where-Object { $_ -match '(?i)^load.?image' }).Count -eq 0) { throw 'The expressions'' workflow has no Load Image box. Add one, wire it to the edit box, and export it again (step 10).' }
        if (-not $wfText.Contains('%INSTRUCTION%')) { throw 'The expressions'' workflow has no %INSTRUCTION% in its prompt box. Type %INSTRUCTION% there and export it again (step 10).' }
        if (@($classes | Where-Object { $_ -match $CutPattern }).Count -eq 0) { throw 'The expressions'' workflow gives back the edited picture without the person''s outline. Add a Join Image with Alpha box between the edit and the Preview Image box (step 10) and export it again.' }
        if (@($classes | Where-Object { $_ -match '(?i)save|preview' -and $_ -notmatch '(?i)mask|any|text' }).Count -eq 0) { throw 'The expressions'' workflow has no Preview Image or Save Image box to give the picture back. Add one at the end and export it again (step 10).' }

        $script:Probe = New-Http 4
        $script:Http = New-Http 300
        $script:Url = Find-Server
        $script:Client = [guid]::NewGuid().ToString()
        $script:Said = ''
        Write-Host ('Making {0} ({1}) for {2}. Each takes about half a minute; the first longer while the model loads.' -f (Count-Of $edits 'expression' 'expressions'), ($wanted -join ', '), (Count-Of $todo.Count 'picture' 'pictures'))
        Write-Host 'You can close this window at any time: what is made is kept, and the next run carries on.'
        $n = 0; $k = 0
        foreach ($t in $todo) {
            $n++
            $p = $t.pic
            $info = Png-Info ([System.IO.File]::ReadAllBytes($p.path))
            if ($info -eq $null) { [void]$problems.Add(('{0}: not a PNG picture, skipped' -f $p.show)); continue }
            $image = Upload-Picture $p.path 'rimfolk-variant.png'
            $done = New-Object System.Collections.ArrayList
            foreach ($w in $t.lack) {
                $k++
                $started = Get-Date
                $wf = $wfText | ConvertFrom-Json
                [void](Prepare-Workflow $wf $image $Instructions[$w] $info.w $info.h ('Rimfolk-extras/expressions/' + $p.stem + '_v-' + $w))
                $outputs = Run-Comfy $wf ($k -eq 1)
                $ref = Pick-Image $wf $outputs $CutPattern
                if ($ref -eq $null) { throw 'ComfyUI gave no picture back. Check that the Join Image with Alpha box is wired into a Preview Image or Save Image box, and export the workflow again (step 10).' }
                $bytes = Fetch-Image $ref
                $out = Png-Info $bytes
                $why = ''
                if ($out -eq $null) { $why = 'ComfyUI gave back something that is not a PNG picture' }
                elseif ($out.w -ne $info.w -or $out.h -ne $info.h) { $why = 'it came out {0} x {1}, the picture is {2} x {3}' -f $out.w, $out.h, $info.w, $info.h }
                elseif ($info.alpha -and -not $out.alpha) { $why = 'it came out without a clear background, the picture is a cut-out' }
                if ($why -ne '') {
                    if ($made -eq 0) { throw ('{0}, {1}: {2}. Check the workflow against step 10 (no box that changes the size, the Join Image with Alpha box before the Preview Image box) and export it again.' -f $p.show, $w, $why) }
                    [void]$problems.Add(('{0}, {1}: {2}; not kept, the other expressions of this picture skipped' -f $p.show, $w, $why))
                    break
                }
                $target = Join-Path $p.dir ($p.stem + '_v-' + $w + '.png')
                $part = $target + '.part'
                [System.IO.File]::WriteAllBytes($part, $bytes)
                [System.IO.File]::Move($part, $target)
                $made++; $byWord[$w]++
                [void]$done.Add(('{0} ({1:0} s)' -f $w, ((Get-Date) - $started).TotalSeconds))
            }
            Write-Host ('[{0}/{1}] {2}: {3}' -f $n, $todo.Count, $p.show, $(if ($done.Count -gt 0) { $done -join ', ' } else { 'none made' }))
        }
    }

    Write-Host ''
    if ($todo.Count -eq 0) { Write-Host 'Every chosen picture has its expressions; nothing to make.' -ForegroundColor Green }
    else {
        $parts = @($byWord.Keys | ForEach-Object { '{0} {1}' -f $_, $byWord[$_] })
        Write-Host ('Made {0} ({1}).' -f (Count-Of $made 'expression' 'expressions'), ($parts -join ', ')) -ForegroundColor Green
    }
    foreach ($q in $problems) { Write-Host ('  Note: ' + $q) -ForegroundColor Yellow }
    if ($made -gt 0) {
        $update = Join-Path $Folder 'Update-PortraitLibrary.ps1'
        if (Test-Path -LiteralPath $update) {
            Write-Host 'Writing the library records again ...'
            & $update -Folder $Folder -NoPause
        }
        else { Write-Host 'Run Update-PortraitLibrary.ps1 now, so the expressions get their records; then "Reload the library" in the game.' }
    }
}
catch {
    Write-Host ''
    Write-Host ('Something went wrong: ' + $_.Exception.Message) -ForegroundColor Red
}
Write-Host ''
if (-not $NoPause) { Read-Host 'Press Enter to close' }
