# Run-PortraitBatch.ps1, for Rimfolk's portraits.
#
# Sends a whole batch of portrait prompts to ComfyUI on this computer, so no prompt has to be copied by hand. It reads a
# batch list (batches\<name>.csv: one row per person, with the columns name, subject and optionally pose, count and seed)
# and a style file (batches\style-<x>.json: the style words, the framing words, the negative words, the size and how many
# of each, and optionally style_end: words repeated after the framing to hold the style), builds each prompt as style +
# subject + pose + framing + style_end, fills it into your exported ComfyUI workflow
# (rimfolk-workflow-api.json, whose prompt box says %PROMPT% and negative box %NEGATIVE%) and queues it in ComfyUI.
# ComfyUI then makes the pictures into its output\Rimfolk\<batch> folder. A second Save Image box whose title has the
# word "cut" in it (the one after the background remover) saves the cut-out under the same name with _cut added. The
# style file's LoRAs and sampler settings are set on the workflow's boxes for every picture, so changing a LoRA needs no
# new export. A log of every prompt and seed is written to logs\ next to this script. Nothing else on the computer is
# changed.
#
# Run it: keep ComfyUI open, right-click this file, "Run with PowerShell", and choose the batch and the style.
# See docs/portrait-art/SETUP-ComfyUI.md.

[CmdletBinding()]
param(
    [string] $Batch = '',
    [string] $Style = '',
    [string] $Workflow = '',
    [string] $Server = 'http://127.0.0.1:8188',
    [string] $Folder = ''
)

$ErrorActionPreference = 'Stop'
if ($Folder -eq '') { $Folder = Split-Path -Parent $PSCommandPath }
$batches = Join-Path $Folder 'batches'

function Pick([string] $what, [string[]] $choices) {
    if ($choices.Count -eq 0) { throw "No $what found in $batches." }
    if ($choices.Count -eq 1) { return $choices[0] }
    Write-Host ''
    Write-Host "Which $what?"
    for ($i = 0; $i -lt $choices.Count; $i++) { Write-Host ("  {0}. {1}" -f ($i + 1), $choices[$i]) }
    while ($true) {
        $a = Read-Host 'Number'
        $n = 0
        if ([int]::TryParse($a, [ref]$n) -and $n -ge 1 -and $n -le $choices.Count) { return $choices[$n - 1] }
    }
}

function Has($o, [string] $name) { return ($o -ne $null) -and ($o.PSObject.Properties.Name -contains $name) }

function Lora-Nodes($wf) {
    return @($wf.PSObject.Properties | Where-Object { [string]$_.Value.class_type -match 'LoraLoader' } | Sort-Object { [int]($_.Name -replace '\D', '') })
}

function Set-Inputs($wf, [string] $prompt, [string] $negative, [long] $seed, [int] $w, [int] $h, [string] $prefix, $st) {
    $found = $false
    # The style file may also set the sampler (steps, cfg, sampler_name, scheduler, guidance, denoise) on any node that
    # has those inputs, and the LoRAs (loras: a list of { name, strength }) on the workflow's LoRA nodes in order.
    $tune = @('steps', 'cfg', 'sampler_name', 'scheduler', 'guidance', 'denoise')
    $loraNodes = Lora-Nodes $wf
    if ((Has $st 'loras') -and $st.loras) {
        for ($k = 0; $k -lt $loraNodes.Count; $k++) {
            $li = $loraNodes[$k].Value.inputs
            if ($k -lt @($st.loras).Count) {
                $L = @($st.loras)[$k]
                if ((Has $L 'name') -and $L.name) { $li.lora_name = [string]$L.name }
                $sv = 0.0; if (Has $L 'strength') { $sv = [double]$L.strength }
                if (Has $li 'strength_model') { $li.strength_model = $sv }
                if (Has $li 'strength_clip') { $li.strength_clip = $sv }
            }
            else {
                if (Has $li 'strength_model') { $li.strength_model = 0.0 }
                if (Has $li 'strength_clip') { $li.strength_clip = 0.0 }
            }
        }
    }
    foreach ($node in $wf.PSObject.Properties) {
        $n = $node.Value
        if ($n.inputs -eq $null) { continue }
        $cls = [string]$n.class_type
        foreach ($inp in @($n.inputs.PSObject.Properties)) {
            $v = $inp.Value
            if (($tune -contains $inp.Name) -and (Has $st $inp.Name) -and -not ($v -is [array])) {
                $inp.Value = $st.($inp.Name)
            }
            elseif ($inp.Name -eq 'filename_prefix' -and $cls -match 'Save') {
                # The Save Image box after the background remover (its title says "cut") keeps the cut-out apart.
                $title = ''; if ((Has $n '_meta') -and (Has $n._meta 'title')) { $title = [string]$n._meta.title }
                if ($title -match '(?i)cut') { $inp.Value = $prefix + '_cut' } else { $inp.Value = $prefix }
            }
            elseif ($v -is [string]) {
                if ($v.Contains('%PROMPT%')) { $inp.Value = $v.Replace('%PROMPT%', $prompt); $found = $true }
                elseif ($v.Contains('%NEGATIVE%')) { $inp.Value = $v.Replace('%NEGATIVE%', $negative) }
            }
            elseif (($inp.Name -eq 'seed' -or $inp.Name -eq 'noise_seed') -and ($v -is [int] -or $v -is [long] -or $v -is [double] -or $v -is [decimal])) {
                $inp.Value = $seed
            }
            elseif (($inp.Name -eq 'width' -or $inp.Name -eq 'height') -and $cls -match 'Latent' -and ($v -is [int] -or $v -is [long] -or $v -is [double])) {
                if ($inp.Name -eq 'width') { $inp.Value = $w } else { $inp.Value = $h }
            }
        }
    }
    return $found
}

try {
    if ($Workflow -eq '') {
        # Several exported workflows (rimfolk-workflow*.json, e.g. one for FLUX and one for Chroma): pick one.
        $wfs = @(Get-ChildItem -LiteralPath $Folder -Filter 'rimfolk-workflow*.json' | ForEach-Object { $_.Name })
        if ($wfs.Count -eq 0) { $Workflow = Join-Path $Folder 'rimfolk-workflow-api.json' }
        else { $Workflow = Join-Path $Folder (Pick 'workflow' $wfs) }
    }
    if (-not (Test-Path -LiteralPath $Workflow)) {
        throw "The workflow $Workflow is missing. Export it from ComfyUI with Workflow, Export (API) (see SETUP-ComfyUI.md)."
    }
    $wfText = Get-Content -LiteralPath $Workflow -Raw -Encoding UTF8
    if (-not $wfText.Contains('%PROMPT%')) { throw 'The workflow has no %PROMPT% in its prompt box. Type %PROMPT% there and export it again.' }

    if ($Batch -eq '') {
        $Batch = Pick 'batch' @(Get-ChildItem -LiteralPath $batches -Filter '*.csv' | ForEach-Object { $_.BaseName })
    }
    if ($Style -eq '') {
        $Style = Pick 'style' @(Get-ChildItem -LiteralPath $batches -Filter 'style*.json' | ForEach-Object { $_.BaseName })
    }
    $rows = @(Import-Csv -LiteralPath (Join-Path $batches ($Batch + '.csv')) -Encoding UTF8)
    $st = Get-Content -LiteralPath (Join-Path $batches ($Style + '.json')) -Raw -Encoding UTF8 | ConvertFrom-Json
    $w = 1024; if ($st.width) { $w = [int]$st.width }
    $h = 1024; if ($st.height) { $h = [int]$st.height }
    $each = 1; if ($st.count) { $each = [int]$st.count }
    $boxes = @(Lora-Nodes ($wfText | ConvertFrom-Json)).Count
    $wanted = 0; if ((Has $st 'loras') -and $st.loras) { $wanted = @($st.loras).Count }
    if ($wanted -gt $boxes) {
        Write-Host ("Note: the style names {0} LoRAs but the workflow has only {1} LoRA box(es), so only the first {1} are used. Add boxes in ComfyUI and export again to use all." -f $wanted, $boxes) -ForegroundColor Yellow
    }

    $logDir = Join-Path $Folder 'logs'
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null
    $log = Join-Path $logDir ('{0}-{1}-{2}.csv' -f $Batch, $Style, (Get-Date -Format 'yyyyMMdd-HHmm'))
    $logRows = New-Object System.Collections.ArrayList
    $client = [guid]::NewGuid().ToString()
    $queued = 0
    Write-Host ''
    Write-Host ("Queueing batch '{0}' in style '{1}' ..." -f $Batch, $Style)
    foreach ($r in $rows) {
        if ([string]::IsNullOrWhiteSpace($r.subject)) { continue }
        $count = $each; if ($r.count) { $count = [int]$r.count }
        for ($i = 1; $i -le $count; $i++) {
            if ($r.seed) { $seed = [long]$r.seed + $i - 1 } else { $seed = [long](Get-Random -Minimum 1 -Maximum 2147483647) }
            $prompt = (@($st.style, $r.subject, $r.pose, $st.frame, $st.style_end) | Where-Object { $_ }) -join ' '
            $negative = [string]$st.negative
            $name = ($r.name -replace '[^A-Za-z0-9_\-]', '_')
            $prefix = 'Rimfolk/{0}/{1}_{2}_{3:00}' -f $Batch, $name, $Style, $i
            $wf = $wfText | ConvertFrom-Json
            if ((Has $st 'cut') -and -not $st.cut) {
                # A style with "cut": false (the backdrops) skips the cut-out: its Save box is left out, so ComfyUI skips
                # the background remover too.
                foreach ($k in @($wf.PSObject.Properties | Where-Object { ([string]$_.Value.class_type -match 'Save') -and (Has $_.Value '_meta') -and ([string]$_.Value._meta.title -match '(?i)cut') } | ForEach-Object { $_.Name })) {
                    $wf.PSObject.Properties.Remove($k)
                }
            }
            if (-not (Set-Inputs $wf $prompt $negative $seed $w $h $prefix $st)) { throw 'No prompt box with %PROMPT% was found in the workflow.' }
            $body = @{ prompt = $wf; client_id = $client } | ConvertTo-Json -Depth 64 -Compress
            try {
                $res = Invoke-RestMethod -Uri ($Server.TrimEnd('/') + '/prompt') -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
            }
            catch {
                $why = $null
                if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $why = $_.ErrorDetails.Message }
                if ($why) {
                    if ($why.Length -gt 600) { $why = $why.Substring(0, 600) + ' ...' }
                    throw ("ComfyUI refused the picture (a file name in the style file or the workflow it doesn't have, such as a LoRA's?): {0}" -f $why)
                }
                throw ("Could not reach ComfyUI at {0}: is it open? ({1})" -f $Server, $_.Exception.Message)
            }
            $queued++
            [void]$logRows.Add([pscustomobject]@{ name = $r.name; n = $i; seed = $seed; file = $prefix; prompt = $prompt; negative = $negative; prompt_id = $res.prompt_id })
        }
    }
    $logRows | Export-Csv -LiteralPath $log -NoTypeInformation -Encoding UTF8
    Write-Host ("Queued {0} pictures. ComfyUI makes them into its output\Rimfolk\{1} folder." -f $queued, $Batch) -ForegroundColor Green
    Write-Host ("The prompts and seeds are logged in {0}." -f $log)
}
catch {
    Write-Host ''
    Write-Host ('Something went wrong: ' + $_.Exception.Message) -ForegroundColor Red
}
Write-Host ''
Read-Host 'Press Enter to close'
