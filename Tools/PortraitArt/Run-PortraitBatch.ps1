# Run-PortraitBatch.ps1, for Rimfolk's portraits.
#
# Sends a whole batch of portrait prompts to ComfyUI on this computer, so no prompt has to be copied by hand. It reads a
# batch list (batches\<name>.csv: one row per person, with the columns name, subject and optionally count and seed) and a
# style file (batches\style-<x>.json: the style words, the framing words, the negative words, the size and how many of
# each), builds each prompt as style + subject + framing, fills it into your exported ComfyUI workflow
# (rimfolk-workflow-api.json, whose prompt box says %PROMPT% and negative box %NEGATIVE%) and queues it in ComfyUI.
# ComfyUI then makes the pictures into its output\Rimfolk\<batch> folder. A log of every prompt and seed is written to
# logs\ next to this script. Nothing else on the computer is changed.
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
if ($Workflow -eq '') { $Workflow = Join-Path $Folder 'rimfolk-workflow-api.json' }

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

function Set-Inputs($wf, [string] $prompt, [string] $negative, [long] $seed, [int] $w, [int] $h, [string] $prefix) {
    $found = $false
    foreach ($node in $wf.PSObject.Properties) {
        $n = $node.Value
        if ($n.inputs -eq $null) { continue }
        $cls = [string]$n.class_type
        foreach ($inp in @($n.inputs.PSObject.Properties)) {
            $v = $inp.Value
            if ($inp.Name -eq 'filename_prefix' -and $cls -match 'Save') {
                $inp.Value = $prefix
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
            $prompt = (@($st.style, $r.subject, $st.frame) | Where-Object { $_ }) -join ' '
            $negative = [string]$st.negative
            $name = ($r.name -replace '[^A-Za-z0-9_\-]', '_')
            $prefix = 'Rimfolk/{0}/{1}_{2}_{3:00}' -f $Batch, $name, $Style, $i
            $wf = $wfText | ConvertFrom-Json
            if (-not (Set-Inputs $wf $prompt $negative $seed $w $h $prefix)) { throw 'No prompt box with %PROMPT% was found in the workflow.' }
            $body = @{ prompt = $wf; client_id = $client } | ConvertTo-Json -Depth 64 -Compress
            try {
                $res = Invoke-RestMethod -Uri ($Server.TrimEnd('/') + '/prompt') -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
            }
            catch {
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
