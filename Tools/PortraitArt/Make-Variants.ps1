# Make-Variants.ps1, for Rimfolk's portraits.
#
# Makes the variants of people's pictures: the same picture with the eyes closed (blink), a small smile (smile), a
# frown (frown) and a wince of pain (pain), and with a mark the person got in the game (a lost eye, an eye patch, a
# bionic eye, a scar, a burn, a missing ear, an artificial arm), the same size and framing with the same outline, next
# to the picture under its name with _v- and the word added (library\colonists\<name>.png gets <name>_v-blink.png,
# <name>_v-lostEyeLeft.png ...). Rimfolk shows them on the large portrait: a blink now and then, the face by the
# person's mood, and the mark once the person has it; any person given the picture later, in any save, gets them too.
# - What to make: first what the game wants, library\variants-wanted.csv, which Rimfolk writes when it saves (the
#   player: "variants are first and foremost relevant for my actual colonists"): each picture someone in the save holds,
#   with the variants it lacks, the colony's own people first (priority 1, then 2), the longest wanted first, each with
#   the exact instruction for the edit model, used as given (an expression without one gets this script's own). A mark
#   variant gets its expressions the same way: X_v-lostEyeLeft.png gets X_v-lostEyeLeft_v-blink.png.
# - A variant is made once (the player: "a variant is then marked as done once it's created and in the game, not that
#   it will get created over and over. unless I delete the variant through the ingame about screen"): one already in the
#   library is skipped, whatever the list says. One deleted for good in the game (listed in library\deleted.csv) may be
#   wanted again: the new one is <name>_v-<word>-2.png (-3 ...), since a deleted name stays deleted.
# - Without that list (an older Rimfolk, or the file taken away), or with -Which: by default the pictures the game has
#   given someone, your colonists first (pictures made for one colonist, with a name in their row's pawn column), then
#   the people your newest save has pictures for (the colony's own first, then the notable, then the rest, the most
#   lately shown first); or every person in the library. It asks when it starts. The save is only read, never changed.
#   These get the four expressions only.
# - The workflow: rimfolk-variant-workflow-api.json next to this script, exported from ComfyUI with Workflow, Export
#   (API) from ComfyUI's own FLUX.1 Kontext Dev template: a Load Image box, the edit box with %INSTRUCTION% in its prompt,
#   a Join Image with Alpha box that gives the edited picture the outline of the one it was made from, and a Preview
#   Image (or Save Image) box. See docs/portrait-art/SETUP-ComfyUI.md, step 10.
# - A proper queue (the player: "it should finish all the analysis first and only then start upcoming jobs so it's a
#   proper queue"): each picture is handed to ComfyUI once (into input\rimfolk-variant\, one file for each picture,
#   written over on later runs), every variant is queued at once, and they are collected in order: fetched back,
#   checked to be the picture's size and a cut-out like it, and written next to it. A job queued in ComfyUI after this
#   started waits until these are done. An expression already there is never made again (delete its file to have it
#   made again). Stop it with Ctrl+C at any time: what is made is kept, the variants still waiting are taken out of
#   ComfyUI's queue, and the next run carries on (closing the window instead leaves them in ComfyUI's queue).
# - It never renames, moves or deletes a picture and changes nothing else in the library; at the end it writes the
#   records again (Update-PortraitLibrary.ps1), which give each expression a record of its own. Then "Reload the
#   library" in the game.
# - A picture deleted for good in the game (listed in library\deleted.csv) gets no variants.
#
# Run it: keep ComfyUI open and double-click Make-Variants.cmd (or right-click this file, "Run with PowerShell"). -Which
# a (every person) or -Which s (the save's) chooses without asking, as well as the game's list; -Words blink,smile makes
# only those.

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

# ---------------- The variants ----------------
# The expressions, with the instruction for each as the edit model reads it: plainly what changes, then what stays.

$Keep = 'Keep everything else exactly the same: the same person and face, hair, clothes, pose, position, framing, lighting and painting style.'
$Instructions = [ordered]@{
    'blink' = 'Close the person''s eyes, as in the middle of a blink, the eyelids fully shut and relaxed. ' + $Keep
    'smile' = 'Give the person a small, warm smile with the mouth closed. ' + $Keep
    'frown' = 'Make the person frown, the brows drawn together and down, the mouth set in a slight scowl. ' + $Keep
    'pain'  = 'Make the person wince in pain, the eyes squeezed half shut, the brows drawn up and together, the teeth clenched. ' + $Keep
}
# The mark variants the game may want, each with the game's own instruction (variants-wanted.csv), spelled as the game
# spells them; the side is the person's own.
$MarkWords = @('lostEyeLeft', 'lostEyeRight', 'eyePatchLeft', 'eyePatchRight', 'bionicEyeLeft', 'bionicEyeRight', 'scar', 'burn', 'missingEarLeft', 'missingEarRight', 'artificialArmLeft', 'artificialArmRight')
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
function Is-Extra([string] $stem) { return $stem -match '_(depth|v-[A-Za-z]+(-\d+)?)$' }
function Field($row, [string] $name) { if (Has $row $name) { return [string]$row.$name } else { return '' } }
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

function Upload-Name([string] $key) {
    # A picture's name in ComfyUI's input folder, from its place in the library: the same on every run, so a later run
    # writes over it and the folder holds at most one copy of each picture.
    $sha = [System.Security.Cryptography.SHA1]::Create()
    try { $h = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($key.ToLowerInvariant())) } finally { $sha.Dispose() }
    return ((@($h | Select-Object -First 8 | ForEach-Object { $_.ToString('x2') }) -join '') + '.png')
}

function Upload-Picture([string] $path, [string] $as, [string] $subfolder) {
    # The picture goes to ComfyUI's input folder (input\<subfolder>\<as>), written over if it is there.
    $content = New-Object System.Net.Http.MultipartFormDataContent
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $file = [System.Net.Http.ByteArrayContent]::new($bytes)
    $file.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('image/png')
    $content.Add($file, 'image', $as)
    $content.Add([System.Net.Http.StringContent]::new('input'), 'type')
    $content.Add([System.Net.Http.StringContent]::new('true'), 'overwrite')
    if ($subfolder -ne '') { $content.Add([System.Net.Http.StringContent]::new($subfolder), 'subfolder') }
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

function Queue-Ahead {
    # How many jobs ComfyUI has running or waiting now, or -1 when it doesn't say.
    try {
        $r = Http-Send $script:Http 'GET' ($script:Url + '/prompt') $null
        if ($r.ok) {
            $j = $r.text | ConvertFrom-Json
            if ((Has $j 'exec_info') -and (Has $j.exec_info 'queue_remaining')) { return [int]$j.exec_info.queue_remaining }
        }
    }
    catch { }
    return -1
}

function Queue-Job($wf) {
    # Queues the workflow at the end of ComfyUI's queue; its job's id.
    $body = @{ prompt = $wf; client_id = $script:Client } | ConvertTo-Json -Depth 64 -Compress
    $json = [System.Net.Http.StringContent]::new($body, [System.Text.Encoding]::UTF8, 'application/json')
    $r = Http-Send $script:Http 'POST' ($script:Url + '/prompt') $json
    if (-not $r.ok) { throw ('ComfyUI refused the workflow (is a model file it names missing from ComfyUI''s models folder?): {0}' -f (Error-Of $r.text)) }
    return [string](($r.text | ConvertFrom-Json).prompt_id)
}

function Leave-Queue($ids, [string] $one, [string] $many) {
    # Stopped before its jobs are done (Ctrl+C or an error): the ones still waiting are taken out of ComfyUI's queue, so
    # they don't run for nothing (only these, never another job); the one ComfyUI is working on finishes by itself.
    $list = @($ids)
    if ($list.Count -eq 0) { return }
    $ok = $false
    try {
        $body = @{ delete = [string[]]$list } | ConvertTo-Json -Compress
        $json = [System.Net.Http.StringContent]::new($body, [System.Text.Encoding]::UTF8, 'application/json')
        $r = Http-Send $script:Probe 'POST' ($script:Url + '/queue') $json
        $ok = $r.ok
    }
    catch { }
    if ($ok) { Write-Host ('Stopped early: took the {0} still waiting out of ComfyUI''s queue.' -f (Count-Of $list.Count $one $many)) -ForegroundColor Yellow }
    else { Write-Host ('Stopped early: the {0} still waiting may be in ComfyUI''s queue. Remove them there (its Queue list), or Clear queue if nothing else is waiting.' -f (Count-Of $list.Count $one $many)) -ForegroundColor Yellow }
}

function Wait-Job([string] $id, [bool] $first) {
    # Waits until ComfyUI has run a queued job: its outputs, as ComfyUI's history keeps them. Waiting behind other jobs
    # is fine however long; 15 minutes on this one is the limit.
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

# ---------------- The pictures ----------------

function Read-People {
    # The people's pictures in library\records.xml, in its order: defName, file, the colonist a picture was made for
    # (pawn), and for a variant its word and the picture it is a variant of.
    $path = Join-Path $library 'records.xml'
    if (-not (Test-Path -LiteralPath $path)) { throw 'The library has no records yet: run Update-PortraitLibrary.ps1 (or Copy-ToLibrary.cmd) first, then this again.' }
    $doc = New-Object System.Xml.XmlDocument
    $doc.Load($path)
    $list = New-Object System.Collections.ArrayList
    foreach ($n in @($doc.DocumentElement.ChildNodes)) {
        if ($n.NodeType -ne [System.Xml.XmlNodeType]::Element -or $n.Name -notmatch 'PortraitDef') { continue }
        $one = { param($tag) $x = $n.SelectSingleNode($tag); if ($x -eq $null) { '' } else { $x.InnerText.Trim() } }
        $tex = (& $one 'texPath').Replace('\', '/')
        $parts = $tex -split '/'
        if ($parts.Count -ne 2) { continue }
        $dir = Join-Path $library $parts[0]
        [void]$list.Add([pscustomobject]@{
            defName = (& $one 'defName'); pawn = (& $one 'pawn'); stem = $parts[1]; dir = $dir; tex = $tex.ToLowerInvariant()
            path = (Join-Path $dir ($parts[1] + '.png')); show = ($parts[0] + '\' + $parts[1] + '.png')
            variant = (& $one 'variant'); variantOf = (& $one 'variantOf')
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

# ---------------- What the game wants ----------------
# library\variants-wanted.csv, written by Rimfolk when it saves: one line for each variant a picture held by someone in
# the save lacks: picture (its place in the library, colonists/<name>.png), word (blink, smile, frown, pain, or a mark
# such as lostEyeLeft), instruction (what the edit model is told, used as given), who (whom it is for), priority (1 for
# the colony's own people, then 2) and first (when it was first wanted). The game lists only what is missing, but the
# list may be older than the library: what is there already is skipped.

function Read-Csv-Any([string] $path) {
    # A CSV as written by the game or the scripts, or saved again by Excel (with semicolons in some countries).
    $first = ''
    foreach ($l in @(Get-Content -LiteralPath $path -Encoding UTF8 -TotalCount 1)) { $first = [string]$l }
    $delim = ','
    if ($first.IndexOf(';') -ge 0 -and $first.IndexOf(',') -lt 0) { $delim = ';' }
    return @(Import-Csv -LiteralPath $path -Encoding UTF8 -Delimiter $delim)
}

function First-Key([string] $v) {
    # A "first" value to sort by: a number (a game tick), else a date, else after the rest.
    $inv = [Globalization.CultureInfo]::InvariantCulture
    $t = $v.Trim()
    $d = 0.0
    if ([double]::TryParse($t, [Globalization.NumberStyles]::Float, $inv, [ref]$d)) { return $d }
    $when = [datetime]::MinValue
    if ([datetime]::TryParse($t, $inv, [Globalization.DateTimeStyles]::None, [ref]$when)) { return [double]$when.Ticks }
    return [double]::MaxValue
}

function Read-Wanted([string] $path) {
    # The game's list in the order to make it: priority 1 (the colony's own) before 2, the longest wanted first, then
    # as listed; exists is false when there is no such file.
    $r = @{ exists = $false; list = @() }
    if (-not (Test-Path -LiteralPath $path)) { return $r }
    $r.exists = $true
    try { $rows = @(Read-Csv-Any $path) }
    catch { throw ('{0} could not be read ({1}): is it open in Excel? Close it and run this again.' -f $path, $_.Exception.Message) }
    $list = New-Object System.Collections.ArrayList
    $i = 0
    foreach ($row in $rows) {
        $i++
        $pic = (Field $row 'picture').Trim()
        if ($pic -eq '') { continue }
        $prio = 9; $pv = 0
        if ([int]::TryParse((Field $row 'priority').Trim(), [ref]$pv)) { $prio = $pv }
        [void]$list.Add([pscustomobject]@{
            picture = $pic; word = (Field $row 'word').Trim(); instruction = (Field $row 'instruction').Trim(); who = (Field $row 'who').Trim()
            priority = $prio; first = (First-Key (Field $row 'first')); line = $i
        })
    }
    $r.list = @($list | Sort-Object priority, first, line)
    return $r
}

function Tex-Of([string] $picture) {
    # A wanted picture's place in the library as the records write it, in small letters: "colonists/<name>".
    $t = $picture.Trim().Replace('\', '/').Trim('/')
    if ($t.ToLowerInvariant().StartsWith('library/')) { $t = $t.Substring(8) }
    if ($t.ToLowerInvariant().EndsWith('.png')) { $t = $t.Substring(0, $t.Length - 4) }
    return $t.ToLowerInvariant()
}

function Word-Of([string] $w) {
    # A variant's word as the game and the scripts spell it ("lostEyeLeft", "blink"), or '' for one not known here.
    $t = $w.Trim()
    foreach ($k in @(@($Instructions.Keys) + $MarkWords)) { if ($k -eq $t) { return $k } }
    return ''
}

# ---------------- The variants a picture has ----------------

function Variants-In([string] $dir) {
    # The variant files in a library folder by picture and word ("<picture>_v-<word>" in small letters, without the -2,
    # -3 ... of one made again), each with its files. Read once for each folder.
    if ($script:VariantDirs.ContainsKey($dir)) { return $script:VariantDirs[$dir] }
    $idx = @{}
    foreach ($f in @(Get-ChildItem -LiteralPath $dir -Filter '*.png' -File)) {
        $m = [regex]::Match([System.IO.Path]::GetFileNameWithoutExtension($f.Name), '^(?<base>.+_v-[A-Za-z]+)(-\d+)?$')
        if (-not $m.Success) { continue }
        $k = $m.Groups['base'].Value.ToLowerInvariant()
        if (-not $idx.ContainsKey($k)) { $idx[$k] = New-Object System.Collections.ArrayList }
        [void]$idx[$k].Add($f.FullName)
    }
    $script:VariantDirs[$dir] = $idx
    return $idx
}

function Has-Variant($p, [string] $word) {
    # Whether the picture has this variant: a file of its name, or of a name made again (-2 ...), not deleted for good
    # in the game.
    $idx = Variants-In $p.dir
    $k = ($p.stem + '_v-' + $word).ToLowerInvariant()
    if (-not $idx.ContainsKey($k)) { return $false }
    foreach ($path in $idx[$k]) { if (-not (Is-Deleted $path $script:Deleted)) { return $true } }
    return $false
}

function Variant-Path($p, [string] $word) {
    # Where a new variant goes: <picture>_v-<word>.png, or -2, -3 ... past a name deleted for good in the game (a
    # deleted name stays deleted) or one taken.
    $n = 1
    while ($true) {
        $name = $p.stem + '_v-' + $word
        if ($n -gt 1) { $name += '-' + $n }
        $path = Join-Path $p.dir ($name + '.png')
        if (-not (Test-Path -LiteralPath $path) -and -not (Is-Deleted $path $script:Deleted)) { return $path }
        $n++
    }
}

try {
    Add-Type -AssemblyName System.Net.Http
    if (-not (Test-Path -LiteralPath $library)) { throw ('There is no library folder next to this script ({0}): see SETUP-ComfyUI.md, step 7.' -f $library) }
    $known = @(@($Instructions.Keys) + $MarkWords)
    $only = $null
    if ($Words -ne '') {
        $only = New-Object System.Collections.ArrayList
        foreach ($w in @(($Words -split '[,; ]+') | Where-Object { $_.Trim() -ne '' })) {
            $k = Word-Of $w
            if ($k -eq '') { throw ('"{0}" is not a variant this script makes ({1}).' -f $w, ($known -join ', ')) }
            [void]$only.Add($k)
        }
    }
    $people = @(Read-People)
    # Pictures deleted for good in the game (deleted.csv) get no variants, even where records.xml still names them.
    $script:Deleted = Read-Deleted $library
    $before = $people.Count
    $people = @($people | Where-Object { -not (Is-Deleted $_.path $script:Deleted) })
    if ($people.Count -lt $before) { Write-Host ('{0} left out: deleted for good in the game (deleted.csv).' -f (Count-Of ($before - $people.Count) 'picture' 'pictures')) }
    if ($people.Count -eq 0) { throw 'The records have no pictures of people yet (library\records.xml).' }
    # The game may want the expressions of a mark variant (its picture X_v-lostEyeLeft.png, the expression
    # X_v-lostEyeLeft_v-blink.png): a mark variant is a picture of its own for the wanted list; the old way takes the
    # plain pictures only.
    $byDef = @{}; $byTex = @{}
    foreach ($p in $people) {
        if ($p.variantOf -eq '') { $byDef[$p.defName] = $p; $byTex[$p.tex] = $p }
        elseif ($MarkWords -contains $p.variant) { $byTex[$p.tex] = $p }
    }
    $people = @($people | Where-Object { $_.variantOf -eq '' })
    $script:VariantDirs = @{}
    $jobs = New-Object System.Collections.ArrayList
    $listed = @{}
    $notes = New-Object System.Collections.ArrayList

    # What the game wants first, in its order.
    $wantedFile = Join-Path $library 'variants-wanted.csv'
    $wanted = Read-Wanted $wantedFile
    if ($wanted.exists) {
        $count = 0; $there = 0; $cannot = 0
        foreach ($w in $wanted.list) {
            $word = Word-Of $w.word
            if ($word -eq '') { [void]$notes.Add(('variants-wanted.csv, {0}: "{1}" is not a variant this script knows, left out' -f $w.picture, $w.word)); $count++; $cannot++; continue }
            if ($only -ne $null -and -not $only.Contains($word)) { continue }
            $p = $byTex[(Tex-Of $w.picture)]
            $k = (Tex-Of $w.picture) + '|' + $word.ToLowerInvariant()
            if ($listed.ContainsKey($k)) { continue }
            $listed[$k] = $true
            $count++
            if ($p -eq $null) { [void]$notes.Add(('variants-wanted.csv, {0}: not among the library''s people or their mark variants (left out, deleted, or without a record: run Update-PortraitLibrary.ps1), {1} left out' -f $w.picture, $word)); $cannot++; continue }
            if (-not (Test-Path -LiteralPath $p.path)) { [void]$notes.Add(('{0}: in the records but not in the library folder (run Update-PortraitLibrary.ps1), {1} left out' -f $p.show, $word)); $cannot++; continue }
            if (Has-Variant $p $word) { $there++; continue }
            $instruction = $w.instruction
            if ($instruction -eq '') {
                if ($Instructions.Contains($word)) { $instruction = $Instructions[$word] }
                else { [void]$notes.Add(('variants-wanted.csv, {0}: {1} has no instruction, left out' -f $w.picture, $word)); $cannot++; continue }
            }
            [void]$jobs.Add([pscustomobject]@{ pic = $p; word = $word; instruction = $instruction; who = $w.who; colony = ($w.priority -eq 1); target = ''; id = ''; wf = $null })
        }
        Write-Host ''
        if ($count -eq 0) { Write-Host 'The game wants no variants at the moment (library\variants-wanted.csv lists none).' }
        else {
            $line = 'The game wants {0} (library\variants-wanted.csv): {1} there already, {2} to make' -f (Count-Of $count 'variant' 'variants'), $there, $jobs.Count
            $colony = @($jobs | Where-Object { $_.colony }).Count
            if ($colony -gt 0) { $line += (' ({0} of them for your colonists)' -f $colony) }
            if ($cannot -gt 0) { $line += (', {0} that can''t be made (see the notes at the end)' -f $cannot) }
            Write-Host ($line + '.')
        }
    }

    # Without the game's list, or with -Which: the pictures chosen as before, given the four expressions.
    if (-not $wanted.exists -or $Which -ne '') {
        $expressions = @($Instructions.Keys)
        if ($only -ne $null) { $expressions = @($expressions | Where-Object { $only.Contains($_) }) }
        if ($Which -eq '') {
            Write-Host ''
            Write-Host 'Expressions for which pictures?'
            Write-Host '  Enter: the pictures your game has given someone, your colonists first'
            Write-Host '  a:     every person in the library'
            $Which = Read-Host 'Your choice'
        }
        $all = ($Which -match '^(?i)a')

        # The order: pictures made for one colonist, then the ones the newest save has given the colony's own people,
        # then someone notable, then the rest of the save's (the most lately shown first); with every person, the rest
        # after.
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
        $complete = 0; $missingFile = 0; $lacking = 0
        foreach ($p in $order) {
            if (-not (Test-Path -LiteralPath $p.path)) { $missingFile++; continue }
            $lack = @($expressions | Where-Object { -not $listed.ContainsKey($p.tex + '|' + $_) -and -not (Has-Variant $p $_) })
            if ($lack.Count -eq 0) { $complete++; continue }
            $lacking++
            foreach ($w in $lack) {
                $listed[$p.tex + '|' + $w] = $true
                [void]$jobs.Add([pscustomobject]@{ pic = $p; word = $w; instruction = $Instructions[$w]; who = $p.pawn; colony = ($p.pawn -ne ''); target = ''; id = ''; wf = $null })
            }
        }
        Write-Host ''
        Write-Host ('{0} chosen ({1} made for your colonists, {2} from the save{3}); {4} of them have every expression already.' -f (Count-Of $order.Count 'picture' 'pictures'), $pinned, $fromSave, $(if ($all) { ', the rest of the library' } else { '' }), $complete)
        if ($missingFile -gt 0) { Write-Host ('{0} of them are in the records but not in the library folder: run Update-PortraitLibrary.ps1.' -f $missingFile) -ForegroundColor Yellow }
    }

    $made = 0
    $byWord = [ordered]@{}
    $problems = New-Object System.Collections.ArrayList
    if ($jobs.Count -gt 0) {
        if (-not (Test-Path -LiteralPath $Workflow)) {
            throw ('The variants'' workflow {0} is missing. Make it in ComfyUI and save it with Workflow, Export (API) under that name (SETUP-ComfyUI.md, step 10).' -f $Workflow)
        }
        $wfText = Get-Content -LiteralPath $Workflow -Raw -Encoding UTF8
        $probe = $wfText | ConvertFrom-Json
        if ($probe.PSObject.Properties.Name -contains 'nodes') { throw 'This is a saved workflow, not an API export. In ComfyUI use Workflow, Export (API) and save it again under the same name.' }
        $classes = @($probe.PSObject.Properties | ForEach-Object { [string]$_.Value.class_type })
        if (@($classes | Where-Object { $_ -match '(?i)^load.?image' }).Count -eq 0) { throw 'The variants'' workflow has no Load Image box. Add one, wire it to the edit box, and export it again (step 10).' }
        if (-not $wfText.Contains('%INSTRUCTION%')) { throw 'The variants'' workflow has no %INSTRUCTION% in its prompt box. Type %INSTRUCTION% there and export it again (step 10).' }
        if (@($classes | Where-Object { $_ -match $CutPattern }).Count -eq 0) { throw 'The variants'' workflow gives back the edited picture without the person''s outline. Add a Join Image with Alpha box between the edit and the Preview Image box (step 10) and export it again.' }
        if (@($classes | Where-Object { $_ -match '(?i)save|preview' -and $_ -notmatch '(?i)mask|any|text' }).Count -eq 0) { throw 'The variants'' workflow has no Preview Image or Save Image box to give the picture back. Add one at the end and export it again (step 10).' }

        $script:Probe = New-Http 4
        $script:Http = New-Http 300
        $script:Url = Find-Server
        $script:Client = [guid]::NewGuid().ToString()
        $script:Said = ''
        # Each picture is handed to ComfyUI once, under a name of its own; then every variant is queued at once and
        # collected in order, so ComfyUI makes them one after the other before any job queued after them (the player:
        # "it should finish all the analysis first and only then start upcoming jobs so it's a proper queue").
        $uploaded = @{}
        foreach ($j in $jobs) { if (-not $uploaded.ContainsKey($j.pic.path)) { $uploaded[$j.pic.path] = $null } }
        Write-Host ('Handing {0} to ComfyUI ...' -f (Count-Of $uploaded.Count 'picture' 'pictures'))
        foreach ($j in $jobs) {
            if ($uploaded[$j.pic.path] -ne $null) { continue }
            $info = Png-Info ([System.IO.File]::ReadAllBytes($j.pic.path))
            if ($info -eq $null) { $uploaded[$j.pic.path] = @{ info = $null }; [void]$problems.Add(('{0}: not a PNG picture, skipped' -f $j.pic.show)); continue }
            $uploaded[$j.pic.path] = @{ info = $info; image = (Upload-Picture $j.pic.path (Upload-Name $j.pic.show) 'rimfolk-variant') }
        }
        $queue = @($jobs | Where-Object { $uploaded[$_.pic.path].info -ne $null })
        $ahead = Queue-Ahead
        $waiting = New-Object System.Collections.ArrayList
        try {
            foreach ($j in $queue) {
                $u = $uploaded[$j.pic.path]
                $j.target = Variant-Path $j.pic $j.word
                $j.wf = $wfText | ConvertFrom-Json
                [void](Prepare-Workflow $j.wf $u.image $j.instruction $u.info.w $u.info.h ('Rimfolk-extras/expressions/' + [System.IO.Path]::GetFileNameWithoutExtension($j.target)))
                $j.id = Queue-Job $j.wf
                [void]$waiting.Add($j.id)
            }
            if ($ahead -eq 1) { Write-Host 'ComfyUI has 1 job ahead of these; they start when that is done.' }
            elseif ($ahead -gt 1) { Write-Host ('ComfyUI has {0} jobs ahead of these; they start when those are done.' -f $ahead) }
            Write-Host ('Queued {0} in ComfyUI, about half a minute each; the first takes longer while the model loads.' -f (Count-Of $queue.Count 'variant' 'variants'))
            Write-Host 'What is made is kept, and the next run carries on. To stop, press Ctrl+C (closing the window instead leaves the variants in ComfyUI''s queue).'
            $k = 0
            $last = Get-Date
            foreach ($j in $queue) {
                $k++
                $u = $uploaded[$j.pic.path]
                $outputs = Wait-Job $j.id ($k -eq 1 -and $ahead -le 0)
                [void]$waiting.Remove($j.id)
                $ref = Pick-Image $j.wf $outputs $CutPattern
                if ($ref -eq $null) { throw 'ComfyUI gave no picture back. Check that the Join Image with Alpha box is wired into a Preview Image or Save Image box, and export the workflow again (step 10).' }
                $bytes = Fetch-Image $ref
                $out = Png-Info $bytes
                $why = ''
                if ($out -eq $null) { $why = 'ComfyUI gave back something that is not a PNG picture' }
                elseif ($out.w -ne $u.info.w -or $out.h -ne $u.info.h) { $why = 'it came out {0} x {1}, the picture is {2} x {3}' -f $out.w, $out.h, $u.info.w, $u.info.h }
                elseif ($u.info.alpha -and -not $out.alpha) { $why = 'it came out without a clear background, the picture is a cut-out' }
                $secs = ((Get-Date) - $last).TotalSeconds
                $last = Get-Date
                if ($why -ne '') {
                    if ($made -eq 0) { throw ('{0}, {1}: {2}. Check the workflow against step 10 (no box that changes the size, the Join Image with Alpha box before the Preview Image box) and export it again.' -f $j.pic.show, $j.word, $why) }
                    [void]$problems.Add(('{0}, {1}: {2}; not kept' -f $j.pic.show, $j.word, $why))
                    Write-Host ('[{0}/{1}] {2}: {3} not kept ({4})' -f $k, $queue.Count, $j.pic.show, $j.word, $why) -ForegroundColor Yellow
                    continue
                }
                if (Test-Path -LiteralPath $j.target) { [void]$problems.Add(('{0}: there already, not written over' -f $j.target)); continue }
                $part = $j.target + '.part'
                [System.IO.File]::WriteAllBytes($part, $bytes)
                [System.IO.File]::Move($part, $j.target)
                $made++
                if ($byWord.Contains($j.word)) { $byWord[$j.word]++ } else { $byWord[$j.word] = 1 }
                $for = ''; if ($j.who -ne '') { $for = ' for ' + $j.who }
                Write-Host ('[{0}/{1}] {2}: {3}{4}, {5} ({6:0} s)' -f $k, $queue.Count, $j.pic.show, $j.word, $for, [System.IO.Path]::GetFileName($j.target), $secs)
            }
        }
        finally { if ($waiting.Count -gt 0) { Leave-Queue $waiting 'variant' 'variants' } }
    }

    Write-Host ''
    if ($jobs.Count -eq 0) { Write-Host 'Nothing to make: every variant wanted is there.' -ForegroundColor Green }
    else {
        $parts = @($byWord.Keys | ForEach-Object { '{0} {1}' -f $_, $byWord[$_] })
        $line = 'Made {0} of the {1} to make' -f $made, $jobs.Count
        if ($parts.Count -gt 0) { $line += (' (' + ($parts -join ', ') + ')') }
        Write-Host ($line + '.') -ForegroundColor Green
    }
    foreach ($q in @($notes) + @($problems)) { Write-Host ('  Note: ' + $q) -ForegroundColor Yellow }
    if ($made -gt 0) {
        $update = Join-Path $Folder 'Update-PortraitLibrary.ps1'
        if (Test-Path -LiteralPath $update) {
            Write-Host 'Writing the library records again ...'
            & $update -Folder $Folder -NoPause
        }
        else { Write-Host 'Run Update-PortraitLibrary.ps1 now, so the variants get their records; then "Reload the library" in the game.' }
    }
}
catch {
    Write-Host ''
    Write-Host ('Something went wrong: ' + $_.Exception.Message) -ForegroundColor Red
}
Write-Host ''
if (-not $NoPause) { Read-Host 'Press Enter to close' }
