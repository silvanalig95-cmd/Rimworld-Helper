# Make-DepthMaps.ps1, for Rimfolk's portraits.
#
# Makes a depth map for every backdrop in the library that has none yet: a grey picture of the same size, white where
# things are near and black where they are far, next to the backdrop under its name with _depth added
# (library\backdrops\<name>.png gets library\backdrops\<name>_depth.png). Rimfolk uses it on the large portrait to move
# the near parts of a room a little more than the far wall when the mouse moves over it.
# - The backdrops: the pictures in library\backdrops\ and in any other library folder of backdrops (one whose batch list
#   has a place column, or whose pictures records.xml lists as backdrops).
# - The workflow: rimfolk-depth-workflow-api.json next to this script, exported from ComfyUI with Workflow, Export (API)
#   from ComfyUI's own Depth Anything 3 template: a Load Image box, the depth boxes and a Preview Image (or Save Image)
#   box. See docs/portrait-art/SETUP-ComfyUI.md, step 9.
# - Each backdrop without a depth map is uploaded to ComfyUI (as input\rimfolk-depth.png, the same name every time), its
#   depth map made, fetched back, checked to be the backdrop's size, and written next to it. A depth map already there is
#   never made again (delete it to have it made again).
# - It never renames, moves or deletes a picture and changes nothing else in the library; at the end it writes the
#   records again (Update-PortraitLibrary.ps1), which tell the game where each backdrop's depth map is. Then "Reload the
#   library" in the game.
#
# Run it: keep ComfyUI open and double-click Make-DepthMaps.cmd (or right-click this file, "Run with PowerShell").

[CmdletBinding()]
param(
    [string] $Folder = '',
    [string] $Workflow = '',
    [string] $Server = '',
    [switch] $NoPause
)

$ErrorActionPreference = 'Stop'
if ($Folder -eq '') { $Folder = Split-Path -Parent $PSCommandPath }
$library = Join-Path $Folder 'library'
$settingsFile = Join-Path $Folder 'check-settings.json'
if ($Workflow -eq '') { $Workflow = Join-Path $Folder 'rimfolk-depth-workflow-api.json' }

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

# ---------------- The backdrops ----------------

function Has-Place([string] $batch) {
    # Whether batches\<batch>.csv is a list of backdrops (it has a place column).
    $csv = Join-Path (Join-Path $Folder 'batches') ($batch + '.csv')
    if (-not (Test-Path -LiteralPath $csv)) { return $false }
    $first = [string](@(Get-Content -LiteralPath $csv -TotalCount 1 -Encoding UTF8) | Select-Object -First 1)
    return @(($first -replace '"', '') -split '[,;]' | ForEach-Object { $_.Trim() }) -contains 'place'
}

function Find-Backdrops {
    $listed = @{}
    $records = Join-Path $library 'records.xml'
    if (Test-Path -LiteralPath $records) {
        try {
            $doc = New-Object System.Xml.XmlDocument
            $doc.Load($records)
            foreach ($n in @($doc.DocumentElement.ChildNodes)) {
                if ($n.NodeType -ne [System.Xml.XmlNodeType]::Element -or $n.Name -notmatch 'Backdrop') { continue }
                $t = $n.SelectSingleNode('texPath')
                if ($t -ne $null) { $listed[$t.InnerText.Trim().Replace('\', '/').ToLowerInvariant()] = $true }
            }
        }
        catch { Write-Host ('Note: records.xml could not be read ({0}); the backdrops are found by their folders.' -f $_.Exception.Message) -ForegroundColor Yellow }
    }
    $found = New-Object System.Collections.ArrayList
    foreach ($dir in @(Get-ChildItem -LiteralPath $library -Directory | Sort-Object Name)) {
        $isBacks = ($dir.Name -eq 'backdrops') -or (Has-Place $dir.Name)
        foreach ($f in @(Get-ChildItem -LiteralPath $dir.FullName -Filter '*.png' -File | Sort-Object Name)) {
            $stem = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
            if (Is-Extra $stem) { continue }
            if (-not ($isBacks -or $listed.ContainsKey(($dir.Name + '/' + $stem).ToLowerInvariant()))) { continue }
            [void]$found.Add([pscustomobject]@{
                path = $f.FullName; show = ($dir.Name + '\' + $f.Name); stem = $stem
                depth = (Join-Path $dir.FullName ($stem + '_depth.png'))
            })
        }
    }
    return $found
}

try {
    Add-Type -AssemblyName System.Net.Http
    if (-not (Test-Path -LiteralPath $library)) { throw ('There is no library folder next to this script ({0}): see SETUP-ComfyUI.md, step 7.' -f $library) }
    $backs = @(Find-Backdrops)
    if ($backs.Count -eq 0) { throw ('No backdrops in the library ({0}) yet: they go into library\backdrops (SETUP-ComfyUI.md, step 7).' -f $library) }
    $todo = @($backs | Where-Object { -not (Test-Path -LiteralPath $_.depth) })
    $made = 0
    $problems = New-Object System.Collections.ArrayList
    Write-Host ''
    if ($todo.Count -eq 0) {
        Write-Host ('Every backdrop in the library ({0}) has its depth map.' -f $backs.Count) -ForegroundColor Green
    }
    else {
        if (-not (Test-Path -LiteralPath $Workflow)) {
            throw ('The depth workflow {0} is missing. Make it in ComfyUI and save it with Workflow, Export (API) under that name (SETUP-ComfyUI.md, step 9).' -f $Workflow)
        }
        $wfText = Get-Content -LiteralPath $Workflow -Raw -Encoding UTF8
        $probe = $wfText | ConvertFrom-Json
        if ($probe.PSObject.Properties.Name -contains 'nodes') { throw 'This is a saved workflow, not an API export. In ComfyUI use Workflow, Export (API) and save it again under the same name.' }
        $classes = @($probe.PSObject.Properties | ForEach-Object { [string]$_.Value.class_type })
        if (@($classes | Where-Object { $_ -match '(?i)^load.?image' }).Count -eq 0) { throw 'The depth workflow has no Load Image box. Add one, wire it to the depth box, and export it again (step 9).' }
        if (@($classes | Where-Object { $_ -match '(?i)save|preview' -and $_ -notmatch '(?i)mask|any|text' }).Count -eq 0) { throw 'The depth workflow has no Preview Image or Save Image box to give the depth map back. Add one after the depth box and export it again (step 9).' }

        $script:Probe = New-Http 4
        $script:Http = New-Http 300
        $script:Url = Find-Server
        $script:Client = [guid]::NewGuid().ToString()
        $script:Said = ''
        $line = 'Making the depth maps of {0}' -f (Count-Of $todo.Count 'backdrop' 'backdrops')
        if ($todo.Count -lt $backs.Count) { $line += (' ({0} in the library, the rest with one already)' -f $backs.Count) }
        Write-Host ($line + '. The first takes longer while the model loads.')
        $n = 0
        foreach ($b in $todo) {
            $n++
            $started = Get-Date
            $info = Png-Info ([System.IO.File]::ReadAllBytes($b.path))
            if ($info -eq $null) { [void]$problems.Add(('{0}: not a PNG picture, skipped' -f $b.show)); continue }
            $image = Upload-Picture $b.path 'rimfolk-depth.png'
            $wf = $wfText | ConvertFrom-Json
            $prep = Prepare-Workflow $wf $image '' $info.w $info.h ('Rimfolk-extras/depth/' + $b.stem + '_depth')
            $outputs = Run-Comfy $wf ($n -eq 1)
            $ref = Pick-Image $wf $outputs '(?i)DA3Render|depth|DA3'
            if ($ref -eq $null) { throw 'ComfyUI gave no picture back. Check that the depth box is wired into a Preview Image or Save Image box, and export the workflow again (step 9).' }
            $bytes = Fetch-Image $ref
            $out = Png-Info $bytes
            if ($out -eq $null) { throw 'ComfyUI gave back something that is not a PNG picture. Use a Preview Image or Save Image box at the end (step 9).' }
            if ($out.w -ne $info.w -or $out.h -ne $info.h) {
                $why = '{0}: the depth map came out {1} x {2}, the backdrop is {3} x {4}' -f $b.show, $out.w, $out.h, $info.w, $info.h
                if ($made -eq 0) { throw ($why + '. Add an Upscale Image box between the depth box and the Preview Image box (step 9) and export again.') }
                [void]$problems.Add($why + ', not kept'); continue
            }
            $part = $b.depth + '.part'
            [System.IO.File]::WriteAllBytes($part, $bytes)
            [System.IO.File]::Move($part, $b.depth)
            $made++
            Write-Host ('[{0}/{1}] {2}: made ({3:0} s)' -f $n, $todo.Count, $b.show, ((Get-Date) - $started).TotalSeconds)
        }
    }

    Write-Host ''
    if ($todo.Count -gt 0) { Write-Host ('Made {0}.' -f (Count-Of $made 'depth map' 'depth maps')) -ForegroundColor Green }
    foreach ($p in $problems) { Write-Host ('  Note: ' + $p) -ForegroundColor Yellow }
    if ($made -gt 0) {
        $update = Join-Path $Folder 'Update-PortraitLibrary.ps1'
        if (Test-Path -LiteralPath $update) {
            Write-Host 'Writing the library records again ...'
            & $update -Folder $Folder -NoPause
        }
        else { Write-Host 'Run Update-PortraitLibrary.ps1 now, so the records say where each depth map is; then "Reload the library" in the game.' }
    }
}
catch {
    Write-Host ''
    Write-Host ('Something went wrong: ' + $_.Exception.Message) -ForegroundColor Red
}
Write-Host ''
if (-not $NoPause) { Read-Host 'Press Enter to close' }
