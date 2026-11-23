# Check-Pictures.ps1, for Rimfolk's portraits.
#
# Looks at the pictures in the library (library\<batch>\*.png next to this script) with a describing model in ComfyUI on
# this computer, and compares what the model saw with each picture's record (library\records.xml, written from its batch
# row by Update-PortraitLibrary.ps1). A record has to match its picture: the game gives a picture with a look or a role
# only to pawns who have it, so a picture that shows more than its row says (a weapon, a flame, wings, a crown) or less
# (a woman drawn as a man) goes to the wrong people.
# - The workflow: rimfolk-check-workflow-api.json next to this script, exported from ComfyUI with Workflow, Export
#   (API): a Load Image box, ComfyUI's own Generate Text box with a Qwen3.5 model and %QUESTION% in its prompt box, and
#   a Preview as Text box after it (docs/portrait-art/SETUP-ComfyUI.md, step 8). Another describing box (the QwenVL box
#   of the ComfyUI-QwenVL pack) works the same way, with %QUESTION% in its prompt box.
# - It asks only what it hasn't (the player: "it only analyzes things that have not yet been analyzed ... not that I
#   have like a really long analyze job each time I add new images"). A new picture, or one changed since (another
#   size), is asked the whole form, a fixed list of questions. Each line of the form has a version of its own
#   ($LineVersions): when a line's wording changes its number is raised, and the next run asks only the changed lines,
#   in a short form, about the pictures asked with the older wording, and puts the answers with the ones kept. The
#   answers are kept in library\checked.csv (the picture's name and size, the version each line was asked with, the
#   answer item by item, and the answer as given): the description database. -Again asks every picture the whole form
#   again.
# - A proper queue (the player: "it should finish all the analysis first and only then start upcoming jobs so it's a
#   proper queue"): every picture to ask is handed to ComfyUI first (into input\rimfolk-check\, one file for each
#   picture, written over on later runs), all the questions are queued at once, and the answers are collected in order.
#   A job queued in ComfyUI after the check started (a batch of portraits) waits until the check is done; a check
#   started after other jobs waits behind them. Stopped early with Ctrl+C or by an error, it takes its questions still
#   waiting out of ComfyUI's queue.
# - Every run reads the kept answers again and compares all the checked pictures with their records, so a change to how
#   answers are read or compared needs no model. It writes library\check-report.csv: what is wrong, what the model saw,
#   what the row says, and a suggested fix, the worst first.
# - The record (from the row) is the baseline: the model is not told the row, and only where what it saw differs is a
#   correction written. The sex is never changed without asking (the player: the check "should really rely first on the
#   data that is already provided kind of by the CSV files").
# - And library\corrections.csv: one line per change to one picture's record (age, sex, looks add:/remove:, role or
#   clear, marks add:/remove:, headwear, out), marked yes where it is safe or plainly right (an age three bands or more
#   off, a role for the weapon it holds, the wings look for its wings, a lost eye, a bionic eye or an artificial arm
#   shown, out for two people in a cut-out) and ask where you decide (the sex, fire or magic, anything taken away, a
#   body its kind doesn't have, a cut-out cut at the side). Update-PortraitLibrary.ps1 puts the lines marked yes and
#   those marked ask into the records (the player, 23 November 2026: "the corrections awaiting approval should just be
#   applied without approval ... I'd rather just later correct the image ingame once it actually appears"); ask only
#   says the check was less sure, and a line you mark no is never applied. The yes lines count as done when comparing, and an issue marked no stays
#   out of the report. A later scan may change the check's own line for a picture and field when the model answers
#   otherwise now (its value, and yes or ask by the rules; a no stays no for the same value). The corrections made in
#   the game (the why "set in the game") are never changed and always win: no correction is written against them, and
#   the report only says when the model sees otherwise.
# - The side check needs no model: a cut-out whose figure runs along the left or right edge is cut at the side (the
#   player: "if anything is cut off, it's just from the bottom ... not from the side"). It reads the picture's
#   transparency with Windows' own System.Drawing; a picture it can't read is left to the model's check alone.
# - At the end it offers a zip of examples to send to the lead (library\check-examples-<date>-<time>.zip): the three CSV
#   files, examples.csv, and up to 30 pictures, the flagged ones first.
# - It never deletes, moves or renames a picture and never changes a batch list or the records: the name ties a picture
#   to its row, and the row's other pictures may be right.
# - A picture deleted for good in the game (listed in library\deleted.csv) is never looked at.
#
# Run it: keep ComfyUI open and double-click Check-Pictures.cmd (or right-click this file, "Run with PowerShell"). Then
# look at corrections.csv's ask lines, run Update-PortraitLibrary.ps1 and "Reload the library" in the game.
# To ask about every picture again (after changing the model), run it with -Again, or delete library\checked.csv first.
# To stop it, press Ctrl+C: closing the window instead leaves its questions in ComfyUI's queue.
# -Examples yes or no answers the zip's question without asking.

[CmdletBinding()]
param(
    [string] $Folder = '',
    [string] $Workflow = '',
    [string] $Server = '',
    [switch] $Again,
    [ValidateSet('ask', 'yes', 'no')] [string] $Examples = 'ask',
    [switch] $NoPause
)

$ErrorActionPreference = 'Stop'
if ($Folder -eq '') { $Folder = Split-Path -Parent $PSCommandPath }
$library = Join-Path $Folder 'library'
$checkedFile = Join-Path $library 'checked.csv'
$reportFile = Join-Path $library 'check-report.csv'
$correctionsFile = Join-Path $library 'corrections.csv'
$settingsFile = Join-Path $Folder 'check-settings.json'
if ($Workflow -eq '') { $Workflow = Join-Path $Folder 'rimfolk-check-workflow-api.json' }

# ---------------- The question ----------------

# The form. Each line has a version of its own in $LineVersions below: when a line's wording changes, raise its number,
# and the next run asks that line again (with any other changed lines, in a short form of only those) about every
# picture asked with the older wording; the other answers are kept. A new line gets a key in $FieldKeys and $FieldNames
# and a version, and is asked the same way. The words before the lines go with every line: when they change, raise every
# line's number. How the answers are read and compared is worked out again from the kept answers on every run, so a
# change there needs no new number.
$Question = @'
This is a comic-style drawn portrait of a person, made for a game. Look at the picture closely and fill in the form below about the person in it. Write each line's name, a colon, and a short answer of a few words. Answer every line, in this order, and write nothing else. Write none when the thing is not in the picture.

people: how many people are in the picture (a number)
sex: man or woman
age: how old the person looks, as if they were human: one of baby (under 1), toddler (1 to 3), child (4 to 12), teen (13 to 17), young adult (18 to 29), adult (30 to 44), middle-aged (45 to 59), old (60 to 74), elderly (75 and over). Judge it by grey or white hair, wrinkles at the eyes, mouth and forehead, and sagging skin at the jaw and neck; for a child, by the size of the head and the proportions of the face. Write only the name
skin: the colour of the skin, or of the scales or fur
hair: the colour and style of the hair, or bald
beard: none, stubble, short beard, full beard or moustache
ears: normal, pointed, long, or hidden
horns: none, or the horns they have
tail: none, or the tail they have
wings: none, or the wings they have
eyes: normal, or glowing (and the colour)
body: does the skin itself have scales or fur (not the clothes): none, scales or fur
eye marks: none, or an eye patch, a missing eye or an artificial eye
arms: normal, or an artificial arm: bionic, mechanical or prosthetic
hands: what they hold in their hands, or nothing
weapons: any weapon in the picture (sword, axe, spear, bow, gun, knife), or none
armour: any armour they wear, or none
fire or magic: any flame, glowing light, spell or magic in the picture, or none
headwear: any hat, helmet, hood, crown or other headwear, or none
book or tools: any book, scroll or tool in the picture, or none
unusual: anything else unusual about the person, or none
'@

# Each line's version (raise a line's number when its wording changes). All are 3: until 23 November 2026 the form had
# one version for all its lines, the last 3, and a checked.csv from then counts each line as asked with that.
$LineVersions = [ordered]@{
    people = 3; sex = 3; age = 3; skin = 3; hair = 3; beard = 3; ears = 3; horns = 3; tail = 3; wings = 3; eyes = 3
    body = 3; eyemarks = 3; arms = 3; hands = 3; weapons = 3; armour = 3; magic = 3; headwear = 3; tools = 3; unusual = 3
}

# The answer's items, in the question's order (also checked.csv's columns), and the names a model may give them.
$FieldKeys = @('people', 'sex', 'age', 'skin', 'hair', 'beard', 'ears', 'horns', 'tail', 'wings', 'eyes', 'body', 'eyemarks', 'arms', 'hands', 'weapons', 'armour', 'magic', 'headwear', 'tools', 'unusual')
$FieldNames = @{
    'people' = 'people'; 'persons' = 'people'; 'number of people' = 'people'; 'how many people' = 'people'; 'people count' = 'people'; 'figures' = 'people'
    'sex' = 'sex'; 'gender' = 'sex'
    'age' = 'age'; 'apparent age' = 'age'; 'age in years' = 'age'; 'estimated age' = 'age'
    'skin' = 'skin'; 'skin colour' = 'skin'; 'skin color' = 'skin'; 'complexion' = 'skin'
    'hair' = 'hair'; 'hair colour' = 'hair'; 'hair color' = 'hair'; 'hairstyle' = 'hair'; 'hair colour and style' = 'hair'; 'hair color and style' = 'hair'
    'beard' = 'beard'; 'facial hair' = 'beard'
    'ears' = 'ears'; 'ear' = 'ears'
    'horns' = 'horns'; 'horn' = 'horns'
    'tail' = 'tail'
    'wings' = 'wings'; 'wing' = 'wings'
    'eyes' = 'eyes'; 'eye' = 'eyes'; 'glowing eyes' = 'eyes'; 'eye colour' = 'eyes'; 'eye color' = 'eyes'
    'body' = 'body'; 'scales or fur' = 'body'; 'scales' = 'body'; 'fur' = 'body'; 'fur or scales' = 'body'; 'skin covering' = 'body'
    'eye marks' = 'eyemarks'; 'eye mark' = 'eyemarks'; 'eyemarks' = 'eyemarks'; 'eye patch' = 'eyemarks'; 'marks' = 'eyemarks'
    'arms' = 'arms'; 'arm' = 'arms'; 'artificial arms' = 'arms'; 'artificial limbs' = 'arms'; 'limbs' = 'arms'; 'prosthetics' = 'arms'
    'hands' = 'hands'; 'hand' = 'hands'; 'in hands' = 'hands'; 'in their hands' = 'hands'; 'holding' = 'hands'; 'holds' = 'hands'; 'held' = 'hands'
    'weapons' = 'weapons'; 'weapon' = 'weapons'
    'armour' = 'armour'; 'armor' = 'armour'
    'fire or magic' = 'magic'; 'magic or fire' = 'magic'; 'magic' = 'magic'; 'fire' = 'magic'; 'fire magic' = 'magic'
    'headwear' = 'headwear'; 'head wear' = 'headwear'; 'hat' = 'headwear'; 'hat or helmet' = 'headwear'; 'helmet' = 'headwear'; 'headgear' = 'headwear'
    'book or tools' = 'tools'; 'books or tools' = 'tools'; 'book or tool' = 'tools'; 'tools' = 'tools'; 'tool' = 'tools'; 'book' = 'tools'
    'unusual' = 'unusual'; 'anything unusual' = 'unusual'; 'other' = 'unusual'; 'anything else' = 'unusual'; 'notes' = 'unusual'
}

# ---------------- What a shown thing needs in the record ----------------
# Look words grow with the library (Rimfolk's PortraitLookDefs and the catalogue's), so a look is matched by the words it
# is made of ("batWings" is "bat wings", "fireMage" "fire mage"): any look with the word "wings" allows wings, any with
# "mage", "magic", "pyromaniac", "fire", "ice" ... allows fire or magic. Add words here when new kinds of looks come.

$FightRoles = @('soldier', 'warrior', 'hunter', 'scout')
$ArmourRoles = @('soldier', 'warrior')
$WorkRoles = @('smith', 'miner', 'builder', 'crafter', 'farmer', 'herder', 'cook', 'doctor', 'artist', 'labourer')
$WingLooks = '\bwings?\b'
$MagicLooks = '\b(mage|magic|magical|psycaster|psychic|witch|wizard|sorcerer|sorceress|caster|arcane|necromancer|shaman|druid|pyromaniac|pyro|fire|flame|ice|frost|storm|lightning|spell|warlock|elemental|mystic|occult)\b'
# Looks that should show fire or magic in the picture (a pyromaniac's or a psycaster's look may be only in the face).
$MagicShownLooks = '\b(mage|magic|witch|wizard|sorcerer|sorceress|warlock|elemental|necromancer|arcane|spell)\b'
$FighterLooks = '\b(knight|soldier|warrior|guard|mercenary|raider|bandit|pirate|gladiator|berserker|hunter|archer|barbarian|veteran|fighter|duelist)\b'
$NobleLooks = '\b(noble|royal|king|queen|emperor|empress|crown|crowned|lord|lady|prince|princess|regal|baron|duke)\b'
$BookLooks = '\b(scholar|scribe|sage|monk|priest|bookish|librarian)\b'
$TwinLooks = '\b(conjoined|twins?)\b'

$WeaponWords = '\b(swords?|sabres?|sabers?|blades?|axes?|hatchets?|spears?|pikes?|halberds?|lances?|bows?|crossbows?|arrows?|guns?|rifles?|pistols?|revolvers?|muskets?|shotguns?|firearms?|knife|knives|daggers?|maces?|clubs?|warhammers?|war hammers?|flails?|shields?|whips?|glaives?|tridents?|katanas?|rapiers?|longswords?|greatswords?|machetes?|javelins?|slings?|grenades?|blasters?|carbines?)\b'
$MagicWords = '\b(flames?|fire|fireballs?|burning|torch|torches|embers?|blaze|spells?|magic|magical|orbs?|lightning|sparks|aura|runes?|wands?|glowing|frost|arcane|psychic)\b'
$CrownWords = '\b(crowns?|tiaras?|circlets?|diadems?|coronets?)\b'
$ArmourWords = '\b(armou?r|armoured|armored|chain ?mail|mail shirt|breastplate|cuirass|gambeson|flak (vest|jacket)|brigandine|pauldrons?|vambraces?|arm guard|military gear|power armou?r)\b'
$MagicAskWords = '\b(flames?|fire|fireballs?|embers?|sparks?|lightning|glow\w*|runes?|spells?|magic\w*|halo|aura|shimmer\w*|mist|holy light|light about)\b' 
$ToolRoles = [ordered]@{
    'scholar'  = '\b(books?|tomes?|scrolls?|quills?|pens?|journals?|notebooks?|maps?|ledgers?|papers?|manuscripts?|slates?)\b'
    'smith'    = '\b(hammers?|tongs|anvils?|horseshoes?)\b'
    'miner'    = '\b(picks?|pickaxes?|pick-axes?)\b'
    'builder'  = '\b(saws?|chisels?|trowels?|mallets?|bricks?|planks?)\b'
    'farmer'   = '\b(hoes?|rakes?|sickles?|scythes?|baskets?|seeds?|wheat|pitchforks?|vegetables?)\b'
    'herder'   = '\b(crooks?|staffs?|staves|leash|leashes|lambs?|sheep|goats?|reins?)\b'
    'cook'     = '\b(ladles?|spoons?|pans?|pots?|cleavers?|whisks?|bowls?)\b'
    'artist'   = '\b(brush|brushes|paintbrush|paintbrushes|palettes?|lutes?|flutes?|harps?|drums?|instruments?|fiddles?|violins?)\b'
    'doctor'   = '\b(scalpels?|bandages?|syringes?|medicine|vials?|flasks?|potions?|herbs?|stethoscopes?|medkits?)\b'
    'crafter'  = '\b(needles?|thread|wrench|wrenches|spanners?|screwdrivers?|tools?|looms?|yarn)\b'
    'labourer' = '\b(shovels?|spades?|buckets?|sacks?|crates?)\b'
}

# The look word for a body part seen that its row lacks, when the catalogue has one (looks.csv has none yet for plain
# horns, a tail or glowing eyes: such a picture is marked out, ask, as a body its kind doesn't have). Put the word here
# when one is added, and the check adds that look instead.
$ShownLooks = @{ horns = ''; tail = ''; glowEyes = '' }

$BandWords = @('baby', 'toddler', 'child', 'teen', 'young', 'adult', 'middle', 'old', 'elder')
$BandLabels = @('baby (under 1)', 'toddler (1 to 3)', 'child (4 to 12)', 'teen (13 to 17)', 'young (18 to 29)', 'adult (30 to 44)', 'middle (45 to 59)', 'old (60 to 74)', 'elder (75 and over)')

# ---------------- Small helpers ----------------

function Has($o, [string] $name) { return ($o -ne $null) -and ($o.PSObject.Properties.Name -contains $name) }
function Low([string] $v) { if ($v -eq $null) { return '' } else { return $v.Trim().ToLowerInvariant() } }
function Csv-Field([string] $v) { if ($v -eq $null) { $v = '' }; return '"' + $v.Replace('"', '""') + '"' }
function Csv-Line([string[]] $values) { return (($values | ForEach-Object { Csv-Field $_ }) -join ',') }
function Field($row, [string] $name) { if (Has $row $name) { return [string]$row.$name } else { return '' } }

function Key-Of([string] $text) {
    # A field's name as a model may write it ("Hair (colour and style)", "**Sex**", "2. age") to its key, or $null.
    if ($text -eq $null) { return $null }
    $t = [regex]::Replace($text, '\([^)]*\)', ' ').ToLowerInvariant()
    $t = (($t -replace '[^a-z ]', ' ') -replace '\s+', ' ').Trim()
    if ($t.StartsWith('the ')) { $t = $t.Substring(4) }
    if ($t -ne '' -and $FieldNames.ContainsKey($t)) { return $FieldNames[$t] }
    return $null
}

# ---------------- The form's lines ----------------

function Form-Parts {
    # The question in parts: the words before the lines, each line by its key, the lines' names as the form writes them
    # ("eye marks", "fire or magic"), and any line that has no key.
    if ($script:FormParts -ne $null) { return $script:FormParts }
    $intro = New-Object System.Collections.ArrayList
    $lines = @{}; $labels = @{}; $loose = New-Object System.Collections.ArrayList
    foreach ($l in ($Question -split "`r?`n")) {
        $c = $l.IndexOf(':')
        $key = $null
        if ($c -gt 0) { $key = Key-Of $l.Substring(0, $c) }
        if ($key -ne $null -and -not $lines.ContainsKey($key)) { $lines[$key] = $l; $labels[$key] = $l.Substring(0, $c).Trim() }
        elseif ($lines.Count -eq 0) { [void]$intro.Add($l) }
        elseif ($l.Trim() -ne '') { [void]$loose.Add($l) }
    }
    $script:FormParts = @{ intro = (($intro -join "`n").TrimEnd()); lines = $lines; labels = $labels; loose = $loose }
    return $script:FormParts
}

function Test-Form {
    # Every key has a line in the form and a version, and every line of the form a key: else the question was changed
    # without its tables, and the answers couldn't be kept apart.
    $f = Form-Parts
    $wrong = New-Object System.Collections.ArrayList
    foreach ($k in $FieldKeys) {
        if (-not $f.lines.ContainsKey($k)) { [void]$wrong.Add(('{0} has no line in the question' -f $k)) }
        if (-not $LineVersions.Contains($k)) { [void]$wrong.Add(('{0} has no version in $LineVersions' -f $k)) }
    }
    foreach ($l in $f.loose) { [void]$wrong.Add(('the line "{0}" has no key in $FieldNames' -f (Short $l))) }
    if ($wrong.Count -gt 0) { throw ('The question and its tables don''t match (' + ($wrong -join '; ') + '). Send this to the lead.') }
}

function Form-Of($keys) {
    # The question for these lines: the whole form, or a short form of only these lines, in the form's order.
    $keys = @($keys)
    if ($keys.Count -ge $FieldKeys.Count) { return $Question }
    $f = Form-Parts
    $ask = @($FieldKeys | Where-Object { $keys -contains $_ } | ForEach-Object { $f.lines[$_] })
    return ($f.intro + "`n`n" + ($ask -join "`n"))
}

function Lines-Text($keys) {
    # "the arms line", "the age and arms lines", in the form's order.
    $f = Form-Parts
    $keys = @($keys)
    $names = @($FieldKeys | Where-Object { $keys -contains $_ } | ForEach-Object { $f.labels[$_] })
    if ($names.Count -eq 1) { return ('the {0} line' -f $names[0]) }
    return ('the {0} and {1} lines' -f (@($names | Select-Object -First ($names.Count - 1)) -join ', '), $names[$names.Count - 1])
}

# ---------------- Reading an answer ----------------

function Read-Answer([string] $text, $keys = $null, [bool] $byNumber = $true) {
    # The model's answer to the question, item by item (a hashtable key -> text). It takes the answer as asked (one
    # "name: answer" line each) and the shapes a model drifts into: numbered or bulleted lines, bold names, names with
    # their explanation in brackets, everything on one line, JSON, a table, "name - answer", lines with numbers only, and
    # a thinking part before the answer.
    # $keys: the lines asked, in the form's order (all of them when not given); only those are taken, and lines with
    # numbers and no names are read by them. $byNumber false leaves numbered lines out (a kept answer read again, whose
    # form may have had other lines). The answer to a short form of one line may be the answer alone ("normal").
    if ($keys -eq $null) { $keys = $FieldKeys }
    $keys = @($keys)
    $found = @{}
    if ([string]::IsNullOrWhiteSpace($text)) { return $found }
    $t = [regex]::Replace($text, '(?is)<think>.*?</think>', '')
    $i = $t.IndexOf('</think>')
    if ($i -ge 0) { $t = $t.Substring($i + 8) }
    $t = $t -replace '\*\*|__|`', ''
    $t = $t -replace '["{}\[\]]', ''
    $lines = New-Object System.Collections.ArrayList
    foreach ($raw in ($t -split "`r?`n")) {
        $line = $raw
        if ($line -match '^\s*\|(.*)\|\s*$') {
            $cells = @($Matches[1] -split '\|' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
            if ($cells.Count -lt 2 -or $cells[0] -match '^[-: ]+$') { continue }
            $line = $cells[0] + ': ' + (@($cells | Select-Object -Skip 1) -join ', ')
        }
        elseif ($line.IndexOf(':') -lt 0) {
            $m = [regex]::Match($line, '^\s*(?:[-*+\u2022]\s*|\d+[.)]\s*)?([A-Za-z][A-Za-z /&]{0,30}?)\s+(?:-|=|\u2013|\u2014)\s+(.*)$')
            if ($m.Success -and (Key-Of $m.Groups[1].Value)) { $line = $m.Groups[1].Value + ': ' + $m.Groups[2].Value }
        }
        [void]$lines.Add($line)
    }
    $t = $lines -join "`n"
    # Every colon with an item's name just before it starts that item; its answer runs to where the next item starts.
    $starts = New-Object System.Collections.ArrayList
    foreach ($m in [regex]::Matches($t, ':')) {
        $c = $m.Index
        $from = [Math]::Max(0, $c - 48)
        $before = $t.Substring($from, $c - $from)
        $cut = $before.LastIndexOfAny([char[]]"`n,;|.:")
        if ($cut -ge 0) { $before = $before.Substring($cut + 1) }
        $offset = $c - $before.Length
        foreach ($w in [regex]::Matches($before, '[A-Za-z]\S*')) {
            $key = Key-Of ($before.Substring($w.Index))
            # An item's name right after the same item's is part of its answer ("arms: an artificial arm: bionic").
            if ($key -and $starts.Count -gt 0 -and $starts[$starts.Count - 1].key -eq $key) { break }
            if ($key) { [void]$starts.Add([pscustomobject]@{ key = $key; at = $offset + $w.Index; value = $c + 1 }); break }
        }
    }
    for ($s = 0; $s -lt $starts.Count; $s++) {
        $st = $starts[$s]
        $end = $t.Length
        if ($s + 1 -lt $starts.Count) { $end = $starts[$s + 1].at }
        $v = ''
        if ($end -gt $st.value) { $v = $t.Substring($st.value, $end - $st.value) }
        # An answer is one line (on the line after its name, if the model put it there): the next item's number or bullet
        # ("Man\n3.") and any words after the form are not part of it.
        $v = $v.TrimStart()
        $nl = $v.IndexOf("`n")
        if ($nl -ge 0) { $v = $v.Substring(0, $nl) }
        $v = [regex]::Replace($v, '(?<=\S)\s+(?:\d+\s*[.)]|[-*+\u2022])\s*$', '')
        $v = (($v -replace '\s+', ' ').Trim()).Trim([char[]]' ,;|/.-*')
        if (-not $found.ContainsKey($st.key) -and $keys -contains $st.key) { $found[$st.key] = $v }
    }
    # Lines with numbers and no names: by their number, in the question's order (for the whole form: tried when fewer
    # than 8 lines were named, taken when 15 are numbered; a short form in proportion).
    $few = [Math]::Max(1, [int][Math]::Floor($keys.Count * 8 / 21))
    $most = [Math]::Max(1, [int][Math]::Ceiling($keys.Count * 15 / 21))
    if ($byNumber -and $found.Count -lt $few) {
        $numbered = @{}
        foreach ($line in $lines) { if ($line -match '^\s*(\d+)[.)]\s*(.+)$') { $numbered[[int]$Matches[1]] = $Matches[2] } }
        if ($numbered.Count -ge $most) {
            for ($n = 1; $n -le $keys.Count; $n++) {
                $key = $keys[$n - 1]
                if (-not $found.ContainsKey($key) -and $numbered.ContainsKey($n)) { $found[$key] = ([string]$numbered[$n]).Trim().Trim([char[]]' ,;|/.-*') }
            }
        }
    }
    # A short form of one line answered with the answer alone: its first line.
    if ($byNumber -and $keys.Count -eq 1 -and $found.Count -eq 0) {
        foreach ($line in $lines) {
            $v = (($line -replace '\s+', ' ').Trim()).Trim([char[]]' ,;|/.-*')
            if ($v -eq '') { continue }
            if ($v.Length -le 120 -and $v.IndexOf(':') -lt 0) { $found[$keys[0]] = $v }
            break
        }
    }
    return $found
}

# ---------------- What an answer says ----------------

function Says([string] $v) {
    # 'yes' when the answer names something, 'no' when it says there is nothing, '' when it doesn't say.
    $t = Low $v
    $t = $t.TrimStart([char[]]' -*:')
    if ($t -eq '') { return '' }
    if ($t -match '^(none|no|nothing|nope|n/?a|nil|not|absent|empty|bare|bareheaded|bare-headed|without|zero|neither)\b') {
        if ($t -match '\b(but|except)\b(.+)$') { return (Says $Matches[2]) }
        return 'no'
    }
    if ($t -match '^(unclear|unknown|hidden|unsure|uncertain|cannot|can''t|hard to|obscured)') { return '' }
    return 'yes'
}

function Held([string] $v) {
    # What the hands hold, or '' when they hold nothing (empty, clasped, folded, at the sides) or the answer doesn't say.
    if ((Says $v) -ne 'yes') { return '' }
    $t = Low $v
    $empty = '\b(nothing|empty|empty-handed|free|clasped|folded|crossed|at (the|her|his|their) sides?|on (the|her|his|their) hips?)\b'
    if ($t -match $empty -and $t -notmatch $WeaponWords -and $t -notmatch $MagicWords -and (Tool-Role $t) -eq '') { return '' }
    return $v
}

function People-Of([string] $v) {
    $t = Low $v
    if ($t -eq '') { return -1 }
    $m = [regex]::Match($t, '\d+')
    if ($m.Success) { return [int]$m.Value }
    if ($t -match '^(none|no one|nobody|zero|no people|no person)') { return 0 }
    if ($t -match '\b(one|single|a person|alone|1)\b') { return 1 }
    if ($t -match '\btwo\b') { return 2 }
    if ($t -match '\bthree\b') { return 3 }
    if ($t -match '\b(several|many|group|crowd|four|five)\b') { return 4 }
    return -1
}

function Sex-Of([string] $v) {
    $t = Low $v
    if ($t -eq '' -or $t -match '^(unclear|unknown|uncertain|none|n/?a|both|ambiguous|androgynous)') { return '' }
    $f = $t -match '\b(woman|women|female|girl|lady|feminine)\b'
    $m = $t -match '\b(man|men|male|boy|gentleman|masculine)\b'
    if ($f -and -not $m) { return 'female' }
    if ($m -and -not $f) { return 'male' }
    return ''
}

function Years-Of([string] $v) {
    # How old they look, in years, from "48", "about 48 years", "40-50", "late 40s", "mid-thirties", "elderly"; -1 for none.
    $t = Low $v
    if ($t -eq '') { return -1.0 }
    $inv = [Globalization.CultureInfo]::InvariantCulture
    $m = [regex]::Match($t, '(\d+(?:\.\d+)?)\s*(?:-|to|\u2013)\s*(\d+(?:\.\d+)?)')
    if ($m.Success) { return ([double]::Parse($m.Groups[1].Value, $inv) + [double]::Parse($m.Groups[2].Value, $inv)) / 2.0 }
    $m = [regex]::Match($t, '(early|mid|late)?[\s-]*(\d)0\s*''?s\b')
    if ($m.Success) {
        $y = [double]([int]$m.Groups[2].Value * 10)
        switch ($m.Groups[1].Value) { 'early' { $y += 2 } 'late' { $y += 8 } default { $y += 5 } }
        return $y
    }
    $m = [regex]::Match($t, '\d+(?:\.\d+)?')
    if ($m.Success) {
        $y = [double]::Parse($m.Value, $inv)
        if ($t -match '\bmonths?\b') { $y = $y / 12.0 }
        return $y
    }
    $tens = @{ 'twenties' = 20; 'thirties' = 30; 'forties' = 40; 'fifties' = 50; 'sixties' = 60; 'seventies' = 70; 'eighties' = 80; 'nineties' = 90 }
    foreach ($k in $tens.Keys) {
        if ($t -match ('(early|mid|late)?[\s-]*' + $k)) {
            $y = [double]$tens[$k]
            switch ($Matches[1]) { 'early' { $y += 2 } 'late' { $y += 8 } default { $y += 5 } }
            return $y
        }
    }
    if ($t -match '\b(baby|infant|newborn)\b') { return 0.5 }
    if ($t -match '\btoddler\b') { return 2.0 }
    if ($t -match '\b(teen|teenager|teenage|adolescent)\b') { return 15.0 }
    if ($t -match '\b(child|kid|boy|girl)\b') { return 8.0 }
    if ($t -match '\bmiddle') { return 50.0 }
    if ($t -match '\b(elder|elderly|very old|ancient)\b') { return 80.0 }
    if ($t -match '\b(old|senior)\b') { return 67.0 }
    if ($t -match '\byoung\b') { return 24.0 }
    if ($t -match '\badult\b') { return 35.0 }
    return -1.0
}

function Band-Of-Years([double] $y) {
    if ($y -lt 1) { return 0 }
    if ($y -lt 4) { return 1 }
    if ($y -lt 13) { return 2 }
    if ($y -lt 18) { return 3 }
    if ($y -lt 30) { return 4 }
    if ($y -lt 45) { return 5 }
    if ($y -lt 60) { return 6 }
    if ($y -lt 75) { return 7 }
    return 8
}

function Band-Of-Seen([string] $v) {
    # The age band the model saw, 0 to 8, or -1 for none. The question asks for a band by name ("young adult", "elderly",
    # perhaps with its years after it): a name at the start of the answer counts first, so "baby (under 1)" isn't read
    # as 1 year; any other answer ("48", "late 40s", "about 40 years old") goes by its years.
    $t = (Low $v) -replace '^[^a-z0-9]+', ''
    if ($t -eq '' -or $t -match '^none\b') { return -1 }
    $t = $t -replace '^(an?|the)\s+', ''
    if ($t -match '^(baby|infant|newborn)\b') { return 0 }
    if ($t -match '^toddler') { return 1 }
    if ($t -match '^(child|kid|boy|girl)\b') { return 2 }
    if ($t -match '^(teen|teenager|teenage|adolescent)\b') { return 3 }
    if ($t -match '^young\s+(child|kid|boy|girl)\b') { return 2 }
    if ($t -match '^young\s+(teen|teenager|adolescent)\b') { return 3 }
    if ($t -match '^young\b') { return 4 }
    if ($t -match '^adult\b') { return 5 }
    if ($t -match '^middle') { return 6 }
    if ($t -match '^(elderly|elder|very old|ancient)\b') { return 8 }
    if ($t -match '^(old|senior)\b') { return 7 }
    $y = Years-Of $v
    if ($y -lt 0) { return -1 }
    return (Band-Of-Years $y)
}

function Band-Of-Word([string] $w) {
    $t = (Low $w) -replace '[^a-z]', ''
    if ($t -eq '') { $t = 'adult' }
    if ($t -eq 'teenager') { $t = 'teen' }
    if ($t -eq 'middleaged') { $t = 'middle' }
    if ($t -eq 'elderly') { $t = 'elder' }
    if ($t -eq 'infant' -or $t -eq 'newborn') { $t = 'baby' }
    return [array]::IndexOf($BandWords, $t)
}

function Ears-Of([string] $v) {
    $t = Low $v
    $r = @{ pointed = $false; long = $false; normal = $false }
    if ($t -eq '' -or $t -match '\b(hidden|covered|not visible|unclear|unknown|cannot|can''t)\b') { return $r }
    $r.pointed = $t -match '\b(point\w*|elf|elven|elfin|tapered)\b'
    $r.long = $t -match '\b(long|elongated)\b'
    if (-not $r.pointed -and -not $r.long) { $r.normal = $t -match '\b(normal|human|round\w*|ordinary|small|regular)\b' }
    return $r
}

function Glow-Of([string] $v) {
    $t = Low $v
    if ($t -eq '') { return '' }
    if ($t -match '\b(not|no|without)\b[^,;]*\bglow') { return 'no' }
    if ($t -match '\bglow') { return 'yes' }
    if ($t -match '^(normal|no|none|ordinary|regular)\b') { return 'no' }
    return ''
}

function Body-Of([string] $v) {
    $t = Low $v
    $r = @{ scales = $false; fur = $false; none = $false }
    if ($t -eq '') { return $r }
    $r.scales = $t -match '\bscal(e|es|ed|y)\b'
    $r.fur = $t -match '\bfur(red|ry)?\b'
    if (-not $r.scales -and -not $r.fur) { $r.none = (Says $v) -eq 'no' -or $t -match '^(skin|smooth|plain|normal)' }
    return $r
}

function Eyemarks-Of([string] $v) {
    $t = Low $v
    $r = @{ lost = $false; bionic = $false; none = $false }
    if ($t -eq '') { return $r }
    $r.lost = $t -match '\b(patch|missing|lost|empty socket|one eye|sewn|scarred shut)\b'
    $r.bionic = $t -match '\b(artificial|bionic|mechanical|cyber\w*|prosthetic|glass|robotic|metal)\b'
    if (-not $r.lost -and -not $r.bionic) { $r.none = (Says $v) -eq 'no' }
    return $r
}

# An artificial arm in the answer to "arms": the word for it, or a made arm or hand ("a steel forearm").
$ArtificialArmWords = '\b(artificial|bionics?|mechanical|mechani[sz]ed|cybernetic|cyborg|prosthetics?|prosthesis|robotic|android)\b|\b(metal|steel|iron|chrome|wooden)\s+((left|right)\s+)?(arms?|forearms?|hands?|limbs?)\b|\bpower claws?\b|\bhook (hand|for a hand)\b'
$ArmPartWords = '\b(arms?|forearms?|hands?|limbs?)\b'
# Kinds whose own arms may look made (Vanilla Races Expanded's androids): an artificial arm on them is not a mark.
$MadeKinds = '(?i)android|robot|synth|mechanoid'
# The record mark for an artificial arm (Rimfolk's matcher: any added part on an arm or hand), as the check writes it.
$ArmMark = 'artificialArm'
# The looks that are an artificial arm (Rimfolk's PortraitLookDefs: bionicArm, prostheticArm, archotechArm), by their words.
$ArmLooks = '\b(bionic|prosthetic|archotech|mechanical|artificial|cybernetic) arms?\b'

function Arms-Of([string] $v, [string] $unusual) {
    # An artificial arm the model saw (artificial), or plainly none (none): from its answer to "arms" ("a bionic left
    # arm", "mechanical", "normal", "normal, no prosthetics"), else from "unusual" naming one ("his left arm is
    # mechanical"). An answer repeating the question's choices ("normal, or an artificial arm: bionic, mechanical or
    # prosthetic") says nothing, and "no ..." is taken out first, so "normal, no bionics" is none.
    $r = @{ artificial = $false; none = $false }
    $neg = '\b(no|not|without|neither|nor|none)\b\s+(an?\s+|any\s+|visible\s+|obvious\s+)*'
    $t = Low $v
    if ($t -match 'bionic, mechanical,? or prosthetic|normal,? or an? artificial') { $t = '' }
    if ($t -ne '' -and $t -notmatch '\b(hidden|covered|not visible|not shown|out of (the )?(picture|frame)|unclear|unknown|cannot|can''t)\b') {
        $u = [regex]::Replace($t, $neg + '(' + $ArtificialArmWords + ')[^,;.]*', ' ')
        if ($u -match $ArtificialArmWords) { $r.artificial = $true }
        else { $r.none = ((Says $t) -eq 'no') -or ($t -match '^(both\s+)?(arms?\s+)?(are\s+)?(normal|natural|human|flesh|ordinary|regular)\b') }
    }
    if (-not $r.artificial) {
        $x = [regex]::Replace((Low $unusual), $neg + '(' + $ArtificialArmWords + ')[^,;.]*', ' ')
        $made = '\b(artificial|bionic|mechanical|mechani[sz]ed|cybernetic|prosthetic|robotic)\b'
        if ($x -match ($made + '[^.,;]{0,20}' + $ArmPartWords) -or $x -match ($ArmPartWords + '[^.,;]{0,20}' + $made) -or $x -match '\bprosthesis\b|\b(metal|steel|iron|chrome|wooden)\s+((left|right)\s+)?(arms?|forearms?|hands?|limbs?)\b|\bpower claws?\b|\bhook (hand|for a hand)\b') {
            $r.artificial = $true; $r.none = $false
        }
    }
    return $r
}

function Wing-Word([string] $v, $kinds) {
    # The wings look (Rimfolk's PortraitLookDefs, from docs/portrait-art/catalogue/looks.csv) for the wings the model saw,
    # and whether its words make it plain (bat, feathered, dragon, a single malformed wing, glider membranes) or not
    # (thin insect wings may be a fairy's or an insector's: the row's kind decides, and it is asked; wings for ears).
    $t = Low $v
    if ($t -match '\bears?\b') { return @{ word = 'earWings'; sure = $false } }
    if ($t -match 'malformed|single small|one small|stunted') { return @{ word = 'malformedWing'; sure = $true } }
    if ($t -match 'glid|membrane between|stretched between') { return @{ word = 'glidewings'; sure = $true } }
    if ($t -match 'dragon|scaled|scaly') { return @{ word = 'drakonoriWings'; sure = $true } }
    if ($t -match '\bbat') { return @{ word = 'batWings'; sure = $true } }
    if ($t -match 'feather|bird|angel|swan|raven|crow|eagle') { return @{ word = 'featheredWings'; sure = $true } }
    $fairy = $false
    foreach ($k in @($kinds)) { if ([string]$k -match '(?i)^fg_|fairy|pixie') { $fairy = $true } }
    if ($t -match 'insect|dragonfly|beetle|\bbee|wasp|\bfly\b|gossamer|translucent|veined|fairy|butterfly|moth|cicada|damselfly') {
        if ($fairy) { return @{ word = 'fairyWings'; sure = $false } }
        return @{ word = 'insectWings'; sure = $false }
    }
    if ($t -match 'leathery') { return @{ word = 'batWings'; sure = $false } }
    if ($fairy) { return @{ word = 'fairyWings'; sure = $false } }
    return @{ word = ''; sure = $false }
}

function Strip-Glow([string] $t) {
    # Glowing clothes (a spacer's soft-glowing seams) and glowing eyes are not fire or magic.
    $t = [regex]::Replace($t, '\b(soft-?|faintly |softly )?glow\w*\s+(seams?|trim|lines|panels?|piping|stitch\w*|circuit\w*|visor|lenses?|buttons?|screens?)\b', ' ')
    $t = [regex]::Replace($t, 'glow\w*[^.,;]{0,30}\beyes?\b|\beyes?\b[^.,;]{0,30}\bglow\w*', ' ')
    return $t
}

function Asks-Of([string] $asked) {
    # What a row's prompt asks the picture to show.
    $t = Low $asked
    $r = @{ weapon = $false; armour = $false; magic = $false; tools = $false; wings = $false; horns = $false; tail = $false; crown = $false; glowEyes = $false; bionicEye = $false; bionicArm = $false }
    if ($t -eq '') { return $r }
    $r.weapon = $t -match $WeaponWords
    $r.armour = $t -match $ArmourWords
    $r.magic = (Strip-Glow $t) -match $MagicAskWords
    $r.tools = (Tool-Role $t) -ne ''
    $r.wings = $t -match '\bwings?\b|glider membranes'
    $r.horns = $t -match '\b(horns?|antlers?)\b'
    $r.tail = $t -match '\btails?\b'
    $r.crown = $t -match $CrownWords
    $r.glowEyes = $t -match 'glow\w*[^.,;]{0,30}\beyes?\b|\beyes?\b[^.,;]{0,30}\bglow'
    $r.bionicEye = $t -match '\b(bionic|archotech|artificial|mechanical)\b[^.,;]{0,20}\beye'
    $made = '\b(bionic|archotech|artificial|mechanical|prosthetic|cybernetic|robotic)\b'
    $r.bionicArm = $t -match ($made + '[^.,;]{0,20}' + $ArmPartWords) -or $t -match ($ArmPartWords + '[^.,;]{0,30}' + $made) -or $t -match '\bpower claws?\b'
    return $r
}

function Magic-Seen($seen, [string] $hands) {
    # The fire or magic the model saw ('' for none): its answer to "fire or magic", else magic in the hands.
    $t = Low $seen.magic
    if ((Says $t) -eq 'yes') {
        $u = Strip-Glow $t
        if ($u -match $MagicWords) { return $seen.magic }
        if ($u -eq $t -and $t -notmatch '\b(seams?|trim|lamps?|lanterns?|candles?|screens?)\b') { return $seen.magic }
    }
    if ((Strip-Glow (Low $hands)) -match $MagicWords) { return $hands }
    return ''
}

function Look-Words([string] $w) {
    # A look word in the words it is made of, for matching: "batWings" -> "bat wings", "fireMage" -> "fire mage".
    return (($w -creplace '([a-z0-9])([A-Z])', '$1 $2') -replace '[_\-]', ' ').ToLowerInvariant()
}

function Any-Look($looks, [string] $pattern) {
    foreach ($l in $looks) { if ($l -match $pattern) { return $true } }
    return $false
}

function Tool-Role([string] $text) {
    $t = Low $text
    foreach ($k in $ToolRoles.Keys) { if ($t -match $ToolRoles[$k]) { return $k } }
    return ''
}

function Count-Of([int] $n, [string] $one, [string] $many) {
    if ($n -eq 1) { return ('1 ' + $one) } else { return ('{0} {1}' -f $n, $many) }
}

function Short([string] $v) {
    $t = ($v -replace '\s+', ' ').Trim()
    if ($t.Length -gt 80) { $t = $t.Substring(0, 77) + '...' }
    return $t
}

# ---------------- The records ----------------

function Read-Records([string] $path) {
    # library\records.xml, by texPath (batch/name without .png): what each picture's row says.
    $byTex = @{}
    $doc = New-Object System.Xml.XmlDocument
    $doc.Load($path)
    foreach ($n in @($doc.DocumentElement.ChildNodes)) {
        if ($n.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
        $one = { param($tag) $x = $n.SelectSingleNode($tag); if ($x -eq $null) { '' } else { $x.InnerText.Trim() } }
        $list = { param($tag) @($n.SelectNodes($tag + '/li') | ForEach-Object { $_.InnerText.Trim() } | Where-Object { $_ -ne '' }) }
        $tex = & $one 'texPath'
        if ($tex -eq '') { continue }
        $r = [pscustomobject]@{
            kind      = $(if ($n.Name -match 'Backdrop') { 'backdrop' } else { 'person' })
            defName   = & $one 'defName'
            pawn      = & $one 'pawn'
            sex       = Low (& $one 'sex')
            age       = Low (& $one 'age')
            dress     = Low (& $one 'dress')
            era       = Low (& $one 'era')
            role      = Low (& $one 'role')
            headwear  = ((Low (& $one 'headwear')) -eq 'true')
            kinds     = @(& $list 'xenotypes') + @(& $list 'races')
            features  = @(& $list 'features' | ForEach-Object { $_.ToLowerInvariant() })
            featureWords = @(& $list 'features')
            marks     = @(& $list 'marks' | ForEach-Object { $_.ToLowerInvariant() })
            looks     = @(& $list 'looks' | ForEach-Object { Look-Words $_ })
            lookWords = @(& $list 'looks')
            place     = & $one 'place'
        }
        $byTex[$tex.Replace('\', '/').ToLowerInvariant()] = $r
    }
    return $byTex
}

function Row-Says($rec) {
    if ($rec.kind -eq 'backdrop') { return ('a backdrop: ' + $rec.place) }
    $parts = New-Object System.Collections.ArrayList
    $who = 'any sex'
    if ($rec.sex -eq 'female') { $who = 'woman' } elseif ($rec.sex -eq 'male') { $who = 'man' }
    $age = $rec.age; if ($age -eq '') { $age = 'adult' }
    [void]$parts.Add($who + ', ' + $age)
    if ($rec.kinds.Count -gt 0) { [void]$parts.Add(($rec.kinds -join ', ')) } else { [void]$parts.Add('baseliner') }
    if ($rec.featureWords.Count -gt 0) { [void]$parts.Add('features: ' + ($rec.featureWords -join ', ')) }
    if ($rec.marks.Count -gt 0) { [void]$parts.Add('marks: ' + ($rec.marks -join ', ')) }
    if ($rec.lookWords.Count -gt 0) { [void]$parts.Add('looks: ' + ($rec.lookWords -join ', ')) }
    if ($rec.role -ne '') { [void]$parts.Add('role: ' + $rec.role) }
    if ($rec.dress -ne '') { [void]$parts.Add('dress: ' + $rec.dress) }
    if ($rec.headwear) { [void]$parts.Add('headwear') }
    if ($rec.pawn -ne '') { [void]$parts.Add('only for ' + $rec.pawn) }
    return ($parts -join '; ')
}

function Seen-Says($seen) {
    # What the model saw, short: the count, sex and age, then only what it found there.
    $parts = New-Object System.Collections.ArrayList
    $p = People-Of $seen.people
    if ($p -eq 1) { [void]$parts.Add('1 person') } elseif ($p -ge 0) { [void]$parts.Add(('{0} people' -f $p)) }
    $who = ''
    if ((Says $seen.sex) -eq 'yes') { $who = Short $seen.sex }
    if ((Band-Of-Seen $seen.age) -ge 0) { $who = ($who + ', looks ' + (Short $seen.age)).Trim(' ', ',') }
    if ($who -ne '') { [void]$parts.Add($who) }
    foreach ($k in @('hands', 'weapons', 'armour', 'magic', 'headwear', 'tools', 'wings', 'horns', 'tail', 'eyemarks', 'unusual')) {
        if ((Says $seen.$k) -eq 'yes') {
            $label = $k
            switch ($k) { 'hands' { $label = 'holds' } 'magic' { $label = 'fire or magic' } 'tools' { $label = 'book or tools' } 'eyemarks' { $label = 'eye marks' } }
            [void]$parts.Add($label + ': ' + (Short $seen.$k))
        }
    }
    if ((Arms-Of $seen.arms '').artificial) { [void]$parts.Add('arms: ' + (Short $seen.arms)) }
    $e = Ears-Of $seen.ears
    if ($e.pointed -or $e.long) { [void]$parts.Add('ears: ' + (Short $seen.ears)) }
    if ((Glow-Of $seen.eyes) -eq 'yes') { [void]$parts.Add('eyes: ' + (Short $seen.eyes)) }
    $b = Body-Of $seen.body
    if ($b.scales -or $b.fur) { [void]$parts.Add('body: ' + (Short $seen.body)) }
    return ($parts -join '; ')
}

# ---------------- The comparison ----------------

function Add-Issue($list, [int] $level, [string] $what, [string] $fix, [string] $field = '', [string] $value = '', [string] $apply = '') {
    # One thing that doesn't fit, and the correction for library\corrections.csv that would fix it (field, value, and
    # yes for a change that is safe or plainly right, ask for one you decide), or none.
    [void]$list.Add([pscustomobject]@{ level = $level; what = $what; fix = $fix; field = $field; value = $value; apply = $apply })
}

function Compare-Picture($rec, $seen, [bool] $cut, [string] $asked, $side = $null) {
    # What doesn't fit between a picture's record and what the model saw, each with a level: 1 wrong (throw it out), 2
    # shows more than its row says (add a look or role to the row, or throw it out), 3 shows less (look at it).
    # $asked is the row's own prompt (its subject and pose; '' when the batch list isn't there): a thing the prompt asked
    # for belongs to the row's look or role ("a long rifle resting on one shoulder" of sniperClass, "small dark horns" of
    # succubus), so a row with a look or role may show it, and should.
    # $side is the side check's finding for a cut-out (Side-Cut-Of), or $null where it couldn't be read.
    # Corrections marked yes only narrow who gets the picture or set plainly what it shows (an age band three or more
    # away, a role for the weapon it holds, the wings look for its wings, out for two people in a cut-out); anything that
    # would give it to more people, or can't be told from the answer, is marked ask. So is the sex: the row says it, and
    # the model's look at a drawing is the weaker word.
    $issues = New-Object System.Collections.ArrayList
    $people = People-Of $seen.people
    if ($rec.kind -eq 'backdrop') {
        if ($people -ge 1) { Add-Issue $issues 1 'a person in the backdrop' 'throw it out (a backdrop shows no people)' 'out' '' 'ask' }
        return $issues
    }

    # Cut at the side: the figure's own pixels run along the left or right edge (a portrait is only ever cut at the
    # bottom). Found from the picture itself, not the model; asked, since a hand or a weapon may reach the edge.
    if ($side -ne $null -and $side.side -ne '') {
        $where = 'the {0} edge' -f $side.side
        if ($side.side -eq 'both') { $where = 'the left and right edges' }
        $share = @()
        if ($side.left -gt $SideCutShare) { $share += ('the left edge along {0:0}%' -f (100 * $side.left)) }
        if ($side.right -gt $SideCutShare) { $share += ('the right edge along {0:0}%' -f (100 * $side.right)) }
        Add-Issue $issues 1 ('cut at the side: the figure runs along {0}' -f $where) ('look at it (the figure touches {0} of the picture''s height): if it is cut off, mark out yes in corrections.csv (or throw it out)' -f ($share -join ' and ')) 'out' '' 'ask'
    }
    $looks = $rec.looks
    $role = $rec.role
    $pinned = $rec.pawn -ne ''
    $tie = ($looks.Count -gt 0) -or ($role -ne '')
    $ask = Asks-Of $asked
    $outAsk = 'look at it: if it is wrong, mark out yes in corrections.csv (or throw it out)'

    # Wrong: more than one person, the other sex, two age bands or more apart (three or more changed without asking).
    if ($people -eq 0) { Add-Issue $issues 1 'no person seen' 'look at it; throw it out if there is no one in it' 'out' '' 'ask' }
    elseif ($people -ge 2 -and -not ($people -eq 2 -and (Any-Look $looks $TwinLooks))) {
        $fix = 'left out of the records'
        if ($cut) { $fix = 'left out of the records (the cut-out holds all of them)' }
        Add-Issue $issues 1 ('{0} people in the picture' -f $people) $fix 'out' '' 'yes'
    }
    $sex = Sex-Of $seen.sex
    # A baby's or toddler's sex can't be told from a painting (the model called a baby girl a man): not compared.
    $little = $rec.age -eq 'baby' -or $rec.age -eq 'toddler'
    if (-not $little -and $sex -ne '' -and ($rec.sex -eq 'male' -or $rec.sex -eq 'female') -and $sex -ne $rec.sex) {
        $drawn = 'man'; if ($sex -eq 'female') { $drawn = 'woman' }
        $want = 'man'; if ($rec.sex -eq 'female') { $want = 'woman' }
        $what = 'drawn as a {0}; its row says {1}' -f $drawn, $want
        if ($pinned) { Add-Issue $issues 1 $what ('its colonist is a {0}: look at it, and mark out yes in corrections.csv if it is wrong' -f $want) 'out' '' 'ask' }
        else { Add-Issue $issues 1 $what ('look at it: yes gives its record the sex {0}, no keeps {1}; or throw it out' -f $sex, $rec.sex) 'sex' $sex 'ask' }
    }
    # The age: two bands from the row's is asked (a drawing's lines can read older or younger than they are meant),
    # three or more is plainly another age and changed without asking.
    $bandSeen = Band-Of-Seen $seen.age
    $bandRec = Band-Of-Word $rec.age
    if ($bandSeen -ge 0 -and $bandRec -ge 0) {
        $apart = [Math]::Abs($bandSeen - $bandRec)
        if ($apart -ge 3) {
            Add-Issue $issues 1 ('looks {0}; its row says {1}' -f $BandLabels[$bandSeen], $BandLabels[$bandRec]) ('its record gets the age {0}; or throw it out' -f $BandWords[$bandSeen]) 'age' $BandWords[$bandSeen] 'yes'
        }
        elseif ($apart -eq 2) {
            Add-Issue $issues 1 ('looks {0}; its row says {1}' -f $BandLabels[$bandSeen], $BandLabels[$bandRec]) ('look at it: yes gives its record the age {0}, no keeps {1}; or throw it out' -f $BandWords[$bandSeen], $BandWords[$bandRec]) 'age' $BandWords[$bandSeen] 'ask'
        }
    }

    # Headwear: the portraits are bare-headed unless the record says headwear (a crown is a look of its own below).
    $hwText = $seen.headwear
    $hw = (Says $hwText) -eq 'yes'
    $crown = ((Low $hwText) -match $CrownWords) -or ((Low $seen.unusual) -match $CrownWords)
    if ($hw -and (Low $hwText) -match '\bhood' -and (Low $hwText) -match '\b(down|lowered|pushed back|thrown back|back)\b') { $hw = $false }
    if ($hw -and -not $rec.headwear -and -not ($crown -and -not $pinned)) {
        if ($pinned) { Add-Issue $issues 2 ('wears headwear ({0})' -f (Short $hwText)) 'with headwear in its record it shows only while its colonist wears something on the head; or throw it out' 'headwear' 'true' 'ask' }
        else { Add-Issue $issues 2 ('wears headwear ({0})' -f (Short $hwText)) 'its record gets headwear, so it goes only to people wearing something on the head; or throw it out' 'headwear' 'true' 'yes' }
    }

    # The body: features and marks shown that the row lacks (unless its prompt asked for them for its look), or that the
    # row has and the picture doesn't show. A body its kind doesn't have can't be fixed in the record: out, ask.
    $feat = $rec.features
    $ears = Ears-Of $seen.ears
    $rowEars = ($feat -contains 'pointedears') -or ($feat -contains 'longears')
    if (($ears.pointed -or $ears.long) -and -not $rowEars) {
        Add-Issue $issues 2 ('shows {0} ears; its row has no pointed or long ears' -f (Short $seen.ears)) $outAsk 'out' '' 'ask'
    }
    elseif ($ears.normal -and $rowEars) {
        Add-Issue $issues 3 'its row says pointed or long ears; the model saw normal ones' $outAsk 'out' '' 'ask'
    }
    $horns = Says $seen.horns
    if ($horns -eq 'yes' -and -not ($feat -contains 'horns') -and -not ($ask.horns -and $tie)) {
        $what = 'shows horns ({0}); its row has none' -f (Short $seen.horns)
        if ($ShownLooks.horns -ne '') { Add-Issue $issues 2 $what ('its record gets the look ' + $ShownLooks.horns) 'looks' ('add:' + $ShownLooks.horns) 'yes' }
        else { Add-Issue $issues 2 $what $outAsk 'out' '' 'ask' }
    }
    elseif ($horns -eq 'no' -and (($feat -contains 'horns') -or ($ask.horns -and $tie))) { Add-Issue $issues 3 'its row says horns; the model saw none' $outAsk 'out' '' 'ask' }
    if ((Says $seen.tail) -eq 'yes' -and -not ($feat -contains 'tail') -and -not ($ask.tail -and $tie)) {
        $what = 'shows a tail ({0}); its row has none' -f (Short $seen.tail)
        if ($ShownLooks.tail -ne '') { Add-Issue $issues 2 $what ('its record gets the look ' + $ShownLooks.tail) 'looks' ('add:' + $ShownLooks.tail) 'yes' }
        else { Add-Issue $issues 2 $what $outAsk 'out' '' 'ask' }
    }
    $glow = Glow-Of $seen.eyes
    $glowOk = ($feat -contains 'glowingeyes') -or (Any-Look $looks $MagicLooks) -or ($ask.glowEyes -and $tie)
    if ($glow -eq 'yes' -and -not $glowOk) {
        $what = 'shows glowing eyes ({0}); its row has none' -f (Short $seen.eyes)
        if ($ShownLooks.glowEyes -ne '') { Add-Issue $issues 2 $what ('its record gets the look ' + $ShownLooks.glowEyes) 'looks' ('add:' + $ShownLooks.glowEyes) 'yes' }
        else { Add-Issue $issues 2 $what $outAsk 'out' '' 'ask' }
    }
    elseif ($glow -eq 'no' -and (($feat -contains 'glowingeyes') -or ($ask.glowEyes -and $tie))) { Add-Issue $issues 3 'its row says glowing eyes; the model saw ordinary ones' $outAsk 'out' '' 'ask' }
    $body = Body-Of $seen.body
    foreach ($b in @('scales', 'fur')) {
        if ($body.$b -and -not ($feat -contains $b)) { Add-Issue $issues 2 ('shows {0} on the body; its row has none' -f $b) $outAsk 'out' '' 'ask' }
        elseif ($body.none -and ($feat -contains $b)) { Add-Issue $issues 3 ('its row says {0}; the model saw none' -f $b) $outAsk 'out' '' 'ask' }
    }
    $marks = Eyemarks-Of $seen.eyemarks
    $rowLost = ($rec.marks -contains 'losteye') -or ($rec.marks -contains 'eyepatch')
    $rowBionic = $rec.marks -contains 'bioniceye'
    if ($marks.lost -and -not $rowLost) {
        $word = 'lostEye'; if ((Low $seen.eyemarks) -match '\bpatch') { $word = 'eyePatch' }
        Add-Issue $issues 2 ('shows {0}; its row has no lost eye' -f (Short $seen.eyemarks)) ('its record gets the mark {0}, so it goes only to people who lost an eye' -f $word) 'marks' ('add:' + $word) 'yes'
    }
    if ($marks.bionic -and -not $rowBionic -and -not ($ask.bionicEye -and $tie)) {
        Add-Issue $issues 2 ('shows {0}; its row has no bionic eye' -f (Short $seen.eyemarks)) 'its record gets the mark bionicEye, so it goes only to people with one' 'marks' 'add:bionicEye' 'yes'
    }
    if ($marks.none -and $rowLost) {
        $word = 'lostEye'; if ($rec.marks -contains 'eyepatch') { $word = 'eyePatch' }
        Add-Issue $issues 3 'its row says a lost eye; the model saw both eyes' ('look at it: taking {0} out of its record gives it to people with both eyes' -f $word) 'marks' ('remove:' + $word) 'ask'
    }
    if ($marks.none -and $rowBionic) { Add-Issue $issues 3 'its row says a bionic eye; the model saw none' 'look at it: taking bionicEye out of its record gives it to people without one' 'marks' 'remove:bionicEye' 'ask' }
    # An artificial arm (bionic, mechanical or prosthetic): the mark artificialArm, as the bionic eye. A row with one of the
    # arm looks (bionicArm, prostheticArm, archotechArm) has it already, and an android's own arms may look made, so its
    # kind is left alone.
    $arms = Arms-Of $seen.arms $seen.unusual
    $rowArm = $rec.marks -contains $ArmMark.ToLowerInvariant()
    $armLook = Any-Look $looks $ArmLooks
    $madeKind = @($rec.kinds | Where-Object { [string]$_ -match $MadeKinds }).Count -gt 0
    if ($arms.artificial -and -not $rowArm -and -not $armLook -and -not $madeKind -and -not ($ask.bionicArm -and $tie)) {
        $armText = $seen.arms; if (-not (Arms-Of $seen.arms '').artificial) { $armText = $seen.unusual }
        Add-Issue $issues 2 ('shows an artificial arm ({0}); its row has none' -f (Short $armText)) ('its record gets the mark {0}, so it goes only to people with one' -f $ArmMark) 'marks' ('add:' + $ArmMark) 'yes'
    }
    if ($arms.none -and $rowArm) { Add-Issue $issues 3 'its row says an artificial arm; the model saw none' ('look at it: taking {0} out of its record gives it to people without one' -f $ArmMark) 'marks' ('remove:' + $ArmMark) 'ask' }

    # A picture made for one colonist goes to them only: its looks and role need no checking.
    if ($pinned) { return $issues }

    # Shows more than its row says: what goes only to pawns with a look or a role. Adding the role or look narrows who
    # gets the picture, so it is marked yes where the role or look is plain from what it shows.
    $hands = Held $seen.hands
    $weaponText = ''
    if ((Says $seen.weapons) -eq 'yes') { $weaponText = $seen.weapons } elseif ((Low $hands) -match $WeaponWords) { $weaponText = $hands }
    $magicLook = Any-Look $looks $MagicLooks
    $weaponOk = ($FightRoles -contains $role) -or (Any-Look $looks $FighterLooks) -or $magicLook -or ($ask.weapon -and $tie)
    if ($weaponText -ne '' -and -not $weaponOk) {
        $t = Low $weaponText
        $guess = 'warrior'
        if ($t -match '\b(guns?|rifles?|pistols?|revolvers?|muskets?|shotguns?|firearms?|carbines?|blasters?|grenades?)\b') { $guess = 'soldier' }
        elseif ($t -match '\b(bows?|crossbows?|arrows?|spears?|javelins?|slings?)\b') { $guess = 'hunter' }
        $now = 'its row has no fighting role or look'; if ($role -ne '') { $now = 'its row''s role is ' + $role }
        if ($ask.weapon) { $now += ' (its prompt asks for a weapon)' }
        $apply = 'yes'; if ($role -ne '') { $apply = 'ask' }
        Add-Issue $issues 2 ('shows a weapon ({0}); {1}' -f (Short $weaponText), $now) ('its record gets the role {0}, so it goes only to fighters; or throw it out' -f $guess) 'role' $guess $apply
    }
    $armour = (Says $seen.armour) -eq 'yes'
    $armourOk = ($rec.dress -eq 'armour') -or ($ArmourRoles -contains $role) -or (Any-Look $looks $FighterLooks) -or ($ask.armour -and $tie)
    if ($armour -and -not $armourOk) {
        $guess = 'soldier'; if ($rec.era -eq 'tribal' -or $rec.era -eq 'medieval') { $guess = 'warrior' }
        $dress = $rec.dress; if ($dress -eq '') { $dress = 'not set' }
        $apply = 'yes'; if ($role -ne '') { $apply = 'ask' }
        Add-Issue $issues 2 ('wears armour ({0}); its row''s dress is {1} and it has no fighting role' -f (Short $seen.armour), $dress) ('its record gets the role {0}; or throw it out' -f $guess) 'role' $guess $apply
    }
    $magicText = Magic-Seen $seen $hands
    $magicOk = $magicLook -or ($ask.magic -and $tie)
    if ($magicText -ne '' -and -not $magicOk) {
        $t = Low $magicText
        $guess = ''
        if ($t -match '\b(fire|flames?|burning|embers?|blaze|fireballs?|torch|torches)\b') { $guess = 'fireMage' }
        elseif ($t -match '\b(ice|frost|snow|cold|frozen)\b') { $guess = 'iceMage' }
        elseif ($t -match '\b(lightning|sparks?|static|storm)\b') { $guess = 'stormMage' }
        $fix = 'which magic it is can''t be told: write the look''s word after add: in corrections.csv and mark it yes, or throw it out'
        if ($guess -ne '') { $fix = ('which magic it is can''t be told for sure: mark add:{0} yes in corrections.csv if it fits (or write another look''s word), or throw it out' -f $guess) }
        Add-Issue $issues 2 ('shows fire or magic ({0}); its row has no look for it' -f (Short $magicText)) $fix 'looks' ('add:' + $guess) 'ask'
    }
    $wings = Says $seen.wings
    if ($wings -eq 'yes' -and -not (Any-Look $looks $WingLooks) -and -not ($ask.wings -and $tie)) {
        $w = Wing-Word $seen.wings $rec.kinds
        $what = 'shows wings ({0}); its row''s looks have no wings' -f (Short $seen.wings)
        if ($w.sure) { Add-Issue $issues 2 $what ('its record gets the look {0}, so it goes only to people with those wings; or throw it out' -f $w.word) 'looks' ('add:' + $w.word) 'yes' }
        else { Add-Issue $issues 2 $what 'which wings these are can''t be told for sure: check the look word in corrections.csv and mark it yes, or throw it out' 'looks' ('add:' + $w.word) 'ask' }
    }
    if ($crown -and $role -ne 'leader' -and -not (Any-Look $looks $NobleLooks) -and -not ($ask.crown -and $tie)) {
        $apply = 'yes'; if ($role -ne '') { $apply = 'ask' }
        Add-Issue $issues 2 'wears a crown; its row has no leader role or noble look' 'its record gets the role leader; or throw it out' 'role' 'leader' $apply
    }
    # A book or tools go with any role or look (a scholar's book, a drinker's tankard, a wayfarer's staff).
    $toolText = ''
    if ((Says $seen.tools) -eq 'yes') { $toolText = $seen.tools }
    elseif ((Tool-Role $hands) -ne '') { $toolText = $hands }
    if ($toolText -ne '' -and -not $tie) {
        $guess = Tool-Role ($toolText + ' ' + $hands)
        $what = 'holds or shows {0}; its row has no role' -f (Short $toolText)
        if ($guess -ne '') { Add-Issue $issues 2 $what ('its record gets the role {0}; or throw it out' -f $guess) 'role' $guess 'yes' }
        else { Add-Issue $issues 2 $what 'which work it is can''t be told: write the role in corrections.csv and mark it yes, or throw it out' 'role' '' 'ask' }
    }

    # Shows less than its row says: a look or role whose things the picture doesn't show. Taking it out of the record
    # would give the picture to anyone, so these are asked.
    foreach ($w in $rec.lookWords) {
        $l = Look-Words $w
        if ($l -match $WingLooks -and $wings -eq 'no') {
            Add-Issue $issues 3 ('its row''s looks say {0}; the model saw no wings' -f $w) ('look at it: taking {0} out of its record gives it to people without wings; or throw it out' -f $w) 'looks' ('remove:' + $w) 'ask'
        }
        elseif ($l -match $TwinLooks -and $people -eq 1) {
            Add-Issue $issues 3 ('its row''s looks say {0}; the model saw one person' -f $w) ('look at it: taking {0} out of its record gives it to anyone; or throw it out' -f $w) 'looks' ('remove:' + $w) 'ask'
        }
    }
    $look = 'look at it: the clothes or the face may still show it; taking it out of the record gives the picture to anyone'
    $missing = New-Object System.Collections.ArrayList
    if ($tie -and $asked -ne '') {
        # The things its prompt asked for, one by one.
        if ($ask.weapon -and $weaponText -eq '' -and (Says $seen.weapons) -eq 'no') { [void]$missing.Add('a weapon') }
        if ($ask.armour -and (Says $seen.armour) -eq 'no') { [void]$missing.Add('armour') }
        if ($ask.magic -and $magicText -eq '' -and (Says $seen.magic) -eq 'no' -and $glow -ne 'yes') { [void]$missing.Add('fire or magic') }
        if ($ask.tools -and $toolText -eq '' -and $hands -eq '' -and (Says $seen.hands) -ne '') { [void]$missing.Add('a book or tools in the hands') }
        if ($ask.crown -and -not $crown -and (Says $seen.headwear) -eq 'no') { [void]$missing.Add('a crown') }
        if ($ask.bionicArm -and $arms.none) { [void]$missing.Add('an artificial arm') }
        if ($missing.Count -gt 0) {
            $for = (@($rec.lookWords) + @(@($role) | Where-Object { $_ -ne '' })) -join ', '
            $what = 'its prompt asks for {0} (for {1}); the model saw none' -f ($missing -join ', '), $for
            foreach ($w in $rec.lookWords) { if (-not ((Look-Words $w) -match $WingLooks -or (Look-Words $w) -match $TwinLooks)) { Add-Issue $issues 3 $what $look 'looks' ('remove:' + $w) 'ask' } }
            if ($role -ne '') { Add-Issue $issues 3 $what $look 'role' 'clear' 'ask' }
        }
    }
    elseif ($tie) {
        # No batch list to read the prompt from: what the look's or role's name suggests.
        foreach ($w in $rec.lookWords) {
            if ((Look-Words $w) -match $MagicShownLooks -and $magicText -eq '' -and (Says $seen.magic) -eq 'no') {
                Add-Issue $issues 3 ('its row''s looks say {0}; the model saw no fire or magic' -f $w) $look 'looks' ('remove:' + $w) 'ask'
            }
        }
        $nothingHeld = ($hands -eq '') -and ((Says $seen.hands) -ne '') -and ((Says $seen.tools) -ne 'yes')
        if (@('soldier', 'warrior', 'hunter') -contains $role -and $weaponText -eq '' -and (Says $seen.weapons) -eq 'no' -and (Says $seen.armour) -eq 'no') {
            Add-Issue $issues 3 ('its row''s role is {0}; the model saw no weapon or armour' -f $role) $look 'role' 'clear' 'ask'
        }
        elseif (($role -eq 'scholar' -or $WorkRoles -contains $role) -and $nothingHeld) {
            Add-Issue $issues 3 ('its row''s role is {0}; the model saw nothing of that work in the hands' -f $role) $look 'role' 'clear' 'ask'
        }
    }
    return $issues
}

# ---------------- The side check (no model) ----------------
# A person's picture is cut at the bottom only (the frame: "cut off by the bottom edge of the picture at the belt"). A
# cut-out whose figure runs along the left or right edge was cut at the side. The numbers, for a 1,024 by 1,280 picture:
# - only the top 85% of the height is looked at: below that the body is cut at the bottom and the elbows and the belt
#   may run out to the sides;
# - the outermost columns: 4 at 1,024 pixels wide (a 256th of the width, at least 2), so a soft or uneven edge left by
#   the cut-out still counts and one stray pixel row doesn't decide;
# - opaque: alpha over 128 (half), so the cut-out's faint fringe and soft hair ends don't count;
# - a side is cut when its opaque rows add up to more than a tenth of the height (128 rows). A shoulder or upper arm cut
#   by the edge runs from about 40% of the height down past the 85% line (30% to 45% of the height); fingertips or a
#   hand reaching the edge cover 2% to 8%, a strand of hair less.
# A picture whose two top corners are both opaque has no cut-out background, and is not judged.

$SideCutTop = 0.85
$SideCutShare = 0.10

function Side-Cut-Of($src) {
    # The side check's finding for one picture: @{ side = 'left', 'right', 'both' or ''; left; right (the share of the
    # height touching each edge) }, or $null when it isn't judged. $src is a pixel source: width, height, stride and its
    # bytes, 4 to a pixel with the alpha fourth (System.Drawing's 32-bit ARGB, B G R A in memory), as Read-Pixels gives.
    if ($null -eq $src) { return $null }
    $w = [int]$src.width; $h = [int]$src.height; $stride = [int]$src.stride; $d = $src.data
    if ($w -lt 8 -or $h -lt 8 -or $null -eq $d -or $d.Length -lt $stride * $h) { return $null }
    if ($d[3] -gt 128 -and $d[($w - 1) * 4 + 3] -gt 128) { return $null }
    $band = [Math]::Max(2, [int][Math]::Round($w / 256.0))
    $rows = [int][Math]::Floor($h * $SideCutTop)
    $left = 0; $right = 0
    # Each row's alpha bytes in the band: from 3 to $leftEnd on the left, from $rightAt to $rightEnd on the right.
    $leftEnd = ($band - 1) * 4 + 3
    $rightAt = ($w - $band) * 4 + 3
    $rightEnd = ($w - 1) * 4 + 3
    for ($at = 0; $at -lt $rows * $stride; $at += $stride) {
        for ($i = $at + 3; $i -le $at + $leftEnd; $i += 4) { if ($d[$i] -gt 128) { $left++; break } }
        for ($i = $at + $rightAt; $i -le $at + $rightEnd; $i += 4) { if ($d[$i] -gt 128) { $right++; break } }
    }
    $r = @{ side = ''; left = ($left / [double]$h); right = ($right / [double]$h) }
    $l = $r.left -gt $SideCutShare; $rt = $r.right -gt $SideCutShare
    if ($l -and $rt) { $r.side = 'both' } elseif ($l) { $r.side = 'left' } elseif ($rt) { $r.side = 'right' }
    return $r
}

function Read-Pixels([string] $path) {
    # A picture's pixels for Side-Cut-Of, read with System.Drawing (Windows' own; it doesn't run elsewhere), or $null
    # when it can't be read: the side check then leaves that picture alone. Its types are named in strings, so the
    # function itself loads where System.Drawing doesn't.
    if ($script:DrawingOk -eq $false) { return $null }
    try {
        if ($script:DrawingOk -ne $true) { Add-Type -AssemblyName System.Drawing; $script:DrawingOk = $true }
        $bmp = New-Object System.Drawing.Bitmap($path)
    }
    catch {
        # No System.Drawing here at all (not Windows): no picture is tried again. One picture that can't be read: only it.
        if ($script:DrawingOk -ne $true -or $_.Exception.ToString() -match 'PlatformNotSupported|TypeInitialization') { $script:DrawingOk = $false }
        return $null
    }
    try {
        $rect = New-Object System.Drawing.Rectangle(0, 0, $bmp.Width, $bmp.Height)
        $data = $bmp.LockBits($rect, 'ReadOnly', 'Format32bppArgb')
        try {
            if ($data.Stride -le 0) { return $null }
            $bytes = New-Object byte[] ($data.Stride * $bmp.Height)
            [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
            return @{ width = $bmp.Width; height = $bmp.Height; stride = $data.Stride; data = $bytes }
        }
        finally { $bmp.UnlockBits($data) }
    }
    catch { return $null }
    finally { $bmp.Dispose() }
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

function Test-Comfy([string] $url) {
    try {
        $r = Http-Send $script:Probe 'GET' ($url.TrimEnd('/') + '/system_stats') $null
        return $r.ok
    }
    catch { return $false }
}

function Find-Server {
    # The address given, the one that worked last time, then ComfyUI's two usual ones; else ask for it.
    $tries = New-Object System.Collections.ArrayList
    if ($Server -ne '') { [void]$tries.Add($Server) }
    if (Test-Path -LiteralPath $settingsFile) {
        try {
            $s = Get-Content -LiteralPath $settingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
            if ((Has $s 'server') -and $s.server) { [void]$tries.Add([string]$s.server) }
        }
        catch { }
    }
    [void]$tries.Add('http://127.0.0.1:8188'); [void]$tries.Add('http://127.0.0.1:8000')
    while ($true) {
        foreach ($t in $tries) {
            $u = $t.Trim().TrimEnd('/')
            if ($u -notmatch '^https?://') { $u = 'http://' + $u }
            if (Test-Comfy $u) {
                try { @{ server = $u } | ConvertTo-Json | Set-Content -LiteralPath $settingsFile -Encoding UTF8 } catch { }
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

$AskerPattern = '(?i)TextGenerate|QwenVL|Qwen.*VL|Florence|JoyCaption|Caption|Llava|VLM|Ollama|Gemma'
$TextOutPattern = '(?i)PreviewAny|Show.?Text|Display.?(Any|Text)|Preview.?Text|ShowAny'

function Fill-Workflow($wf, [string] $image, [string] $Question) {
    # The picture into every Load Image box, the question (the whole form or a short one) where the prompt says
    # %QUESTION% (else into the describing box's prompt), thinking off and room for the whole answer.
    $loaded = $false; $asked = $false
    foreach ($node in @($wf.PSObject.Properties)) {
        $n = $node.Value
        if ($n.inputs -eq $null) { continue }
        $cls = [string]$n.class_type
        foreach ($inp in @($n.inputs.PSObject.Properties)) {
            $v = $inp.Value
            if ($inp.Name -eq 'image' -and $v -is [string] -and $cls -match '(?i)^load.?image' -and $cls -notmatch '(?i)mask|batch|folder|url|path') { $inp.Value = $image; $loaded = $true }
            elseif ($v -is [string] -and $v.Contains('%QUESTION%')) { $inp.Value = $v.Replace('%QUESTION%', $Question); $asked = $true }
            elseif ($inp.Name -eq 'thinking' -and $v -is [bool]) { $inp.Value = $false }
            elseif (@('max_length', 'max_tokens', 'max_new_tokens') -contains $inp.Name -and ($v -is [int] -or $v -is [long] -or $v -is [double]) -and $v -lt 500) { $inp.Value = 600 }
        }
    }
    if (-not $asked) {
        foreach ($node in @($wf.PSObject.Properties)) {
            $n = $node.Value
            if ($n.inputs -eq $null -or -not ([string]$n.class_type -match $AskerPattern)) { continue }
            foreach ($name in @('prompt', 'custom_prompt', 'user_prompt', 'question', 'text_input', 'text')) {
                if ((Has $n.inputs $name) -and $n.inputs.$name -is [string]) { $n.inputs.$name = $Question; $asked = $true; break }
            }
            if ($asked) { break }
        }
    }
    return @($loaded, $asked)
}

function Model-Of($wf) {
    foreach ($node in @($wf.PSObject.Properties)) {
        $n = $node.Value
        if ($n.inputs -eq $null) { continue }
        foreach ($name in @('clip_name', 'clip_name1', 'model_name', 'model')) {
            $cls = [string]$n.class_type
            if ((Has $n.inputs $name) -and $n.inputs.$name -is [string] -and ($cls -match '(?i)clip' -or $cls -match $AskerPattern)) { return [string]$n.inputs.$name }
        }
    }
    return ''
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

function Queue-Question([string] $image, [string] $question) {
    # Queues the question about an uploaded picture at the end of ComfyUI's queue; its job's id.
    $wf = $script:WfText | ConvertFrom-Json
    [void](Fill-Workflow $wf $image $question)
    $body = @{ prompt = $wf; client_id = $script:Client } | ConvertTo-Json -Depth 64 -Compress
    $json = [System.Net.Http.StringContent]::new($body, [System.Text.Encoding]::UTF8, 'application/json')
    $r = Http-Send $script:Http 'POST' ($script:Url + '/prompt') $json
    if (-not $r.ok) { throw ('ComfyUI refused the question: {0}' -f (Error-Of $r.text)) }
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

function Wait-Answer([string] $id, [bool] $first) {
    # Waits for a queued question's answer: the texts of the workflow's text boxes. Waiting behind other jobs is fine
    # however long; 15 minutes on this one is the limit.
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
                    throw ('ComfyUI could not answer: {0}' -f $why)
                }
                $texts = New-Object System.Collections.ArrayList
                if ((Has $entry 'outputs') -and $entry.outputs) {
                    foreach ($o in @($entry.outputs.PSObject.Properties)) {
                        foreach ($k in @('text', 'string', 'STRING')) {
                            if (Has $o.Value $k) { foreach ($s in @($o.Value.$k)) { if ($s -is [string] -and $s.Trim() -ne '') { [void]$texts.Add($s) } } }
                        }
                    }
                }
                return $texts
            }
        }
        if (((Get-Date) - $lastQueue).TotalSeconds -ge 5) {
            $lastQueue = Get-Date
            # Where the question is in ComfyUI's queue, read from the text: the queue holds every waiting job's whole
            # workflow, too much for Windows PowerShell's JSON reader when a long batch of portraits waits there.
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
                if ($first -and $script:Said -ne $id) { Write-Host 'ComfyUI is still busy with other work; the check waits for it.'; $script:Said = $id }
            }
            else {
                $gone++
                if ($gone -ge 3) { throw 'ComfyUI dropped the question (was it restarted?). Run this again.' }
            }
        }
        Start-Sleep -Milliseconds 700
    }
}

# ---------------- The description database ----------------

# library\checked.csv, one line for each picture asked: its place in the library and size, the version each line of the
# form was asked with (versions: "people=3 sex=3 ..."), when and with which model, each line's answer as read, and the
# model's answer as given (answer: its lines joined by " / "; the answer to a later short form after it, behind " // "
# and the lines it asked in brackets: "... // [arms] arms: a bionic left arm").

$CheckedColumns = @('picture', 'size', 'versions', 'checked', 'model') + $FieldKeys + @('answer')

function Versions-Text($versions) {
    $parts = @(foreach ($k in $FieldKeys) { if ($versions.ContainsKey($k) -and [string]$versions[$k] -ne '') { $k + '=' + $versions[$k] } })
    return ($parts -join ' ')
}

function Versions-Of([string] $text) {
    $v = @{}
    foreach ($p in ($text -split '\s+')) {
        $m = [regex]::Match($p, '^([a-z]+)=(\S+)$')
        if ($m.Success) { $v[$m.Groups[1].Value] = $m.Groups[2].Value }
    }
    return $v
}

function Current-Versions {
    $v = @{}
    foreach ($k in $FieldKeys) { $v[$k] = [string]$LineVersions[$k] }
    return $v
}

function Entry-Of($row, $columns) {
    # One line of checked.csv. A file from before the lines had versions of their own (a question column, version 3 or
    # earlier): each line it has a column for counts as asked with that question's number.
    $versions = @{}
    if ($columns -contains 'versions') { $versions = Versions-Of (Field $row 'versions') }
    elseif ($columns -contains 'question') {
        $q = (Field $row 'question').Trim()
        if ($q -ne '') { foreach ($k in $FieldKeys) { if ($columns -contains $k) { $versions[$k] = $q } } }
    }
    $values = @{}
    foreach ($k in $FieldKeys) { $values[$k] = Field $row $k }
    return [pscustomobject]@{
        picture = (Field $row 'picture'); size = (Field $row 'size'); versions = $versions; checked = (Field $row 'checked')
        model = (Field $row 'model'); values = $values; answer = (Field $row 'answer')
    }
}

function Entry-Line($e) {
    $values = New-Object System.Collections.ArrayList
    foreach ($v in @($e.picture, $e.size, (Versions-Text $e.versions), $e.checked, $e.model)) { [void]$values.Add([string]$v) }
    foreach ($k in $FieldKeys) { [void]$values.Add([string]$e.values[$k]) }
    [void]$values.Add([string]$e.answer)
    return (Csv-Line ([string[]]$values.ToArray()))
}

function Read-Checked {
    # Every picture's line, by its place in the library (small letters); a picture asked again keeps its last line.
    $map = @{}
    $rows = 0
    if (Test-Path -LiteralPath $checkedFile) {
        $all = @(Import-Csv -LiteralPath $checkedFile -Encoding UTF8)
        $columns = @()
        if ($all.Count -gt 0) { $columns = @($all[0].PSObject.Properties.Name) }
        foreach ($r in $all) {
            if ([string]::IsNullOrWhiteSpace($r.picture)) { continue }
            $map[$r.picture.ToLowerInvariant()] = Entry-Of $r $columns
            $rows++
        }
    }
    return @{ map = $map; rows = $rows }
}

function Write-Checked($map) {
    Write-CsvFile $checkedFile $CheckedColumns @($map.Keys | Sort-Object | ForEach-Object { Entry-Line $map[$_] })
}

function Answer-Text([string] $text) {
    # The model's answer on one line: its lines joined by " / " (a slash within a line loses its spaces, so the lines
    # can be told apart again when it is read again).
    $lines = @(foreach ($l in (([string]$text -replace "`r", '') -split "`n")) { $t = ($l -replace '\s*/+\s*', '/').Trim(); if ($t -ne '') { $t } })
    return ($lines -join ' / ')
}

function Add-Short([string] $answer, $keys, [string] $text) {
    # The kept answer with a short form's answer added: " // [arms, age] arms: ... / age: ...". An earlier short form's
    # answer for no other lines than these is left out.
    $keys = @($keys)
    $parts = New-Object System.Collections.ArrayList
    $i = 0
    foreach ($s in ($answer -split ' // ')) {
        $i++
        $m = [regex]::Match($s, '^\s*\[([a-z, ]+)\]')
        if ($i -gt 1 -and $m.Success) {
            $had = @($m.Groups[1].Value -split '[, ]+' | Where-Object { $_ -ne '' })
            if (@($had | Where-Object { $keys -notcontains $_ }).Count -eq 0) { continue }
        }
        if ($s.Trim() -ne '') { [void]$parts.Add($s) }
    }
    [void]$parts.Add('[' + (@($FieldKeys | Where-Object { $keys -contains $_ }) -join ', ') + '] ' + (Answer-Text $text))
    return ($parts -join ' // ')
}

function Seen-Of($e) {
    # What the model saw, line by line: the kept answer read again with today's reading, so a change to how answers are
    # read needs no model (a later short form's answer for the lines it asked). Where the kept answer can't be read
    # again (an answer by numbers alone beside a short form's, or none kept), the answers as read when it was asked.
    $vals = @{}
    foreach ($k in $FieldKeys) { $vals[$k] = [string]$e.values[$k] }
    if ([string]::IsNullOrWhiteSpace($e.answer)) { return $vals }
    $parts = New-Object System.Collections.ArrayList
    $listed = @{}
    $i = 0
    foreach ($s in ($e.answer -split ' // ')) {
        $i++
        $keys = $null
        $m = [regex]::Match($s, '^\s*\[([a-z, ]+)\]\s*')
        if ($i -gt 1 -and $m.Success) {
            $keys = @($FieldKeys | Where-Object { @($m.Groups[1].Value -split '[, ]+') -contains $_ })
            $s = $s.Substring($m.Length)
            foreach ($k in $keys) { $listed[$k] = $true }
        }
        elseif ($i -gt 1) { continue }
        [void]$parts.Add([pscustomobject]@{ keys = $keys; text = (($s -split ' / ') -join "`n") })
    }
    $set = @{}
    for ($i = $parts.Count - 1; $i -ge 0; $i--) {
        $p = $parts[$i]
        if ($p.keys -ne $null) {
            # A short form's answer: the lines it asked that can be read again.
            if ($p.keys.Count -eq 0) { continue }
            $found = Read-Answer $p.text $p.keys
            foreach ($k in $p.keys) { if (-not $set.ContainsKey($k) -and $found.ContainsKey($k)) { $vals[$k] = [string]$found[$k]; $set[$k] = $true } }
            continue
        }
        # The whole form: read by the lines it had (those with a version), by numbers too when no short form came after;
        # the lines a short form asked again keep that answer.
        $had = @($FieldKeys | Where-Object { $e.versions.ContainsKey($_) })
        if ($had.Count -eq 0) { continue }
        $found = Read-Answer $p.text $had ($parts.Count -eq 1)
        if ($found.Count -lt 10) { continue }
        foreach ($k in $had) {
            if ($set.ContainsKey($k) -or $listed.ContainsKey($k)) { continue }
            $v = ''; if ($found.ContainsKey($k)) { $v = [string]$found[$k] }
            $vals[$k] = $v
        }
    }
    return $vals
}

function Write-CsvFile([string] $path, [string[]] $header, $lines) {
    $all = New-Object System.Collections.ArrayList
    [void]$all.Add((Csv-Line $header))
    foreach ($l in $lines) { [void]$all.Add($l) }
    try { [System.IO.File]::WriteAllText($path, (($all -join "`r`n") + "`r`n"), (New-Object System.Text.UTF8Encoding($true))) }
    catch { throw ('Could not write {0}: is it open in Excel? Close it and run this again.' -f $path) }
}

# ---------------- The corrections ----------------

function Read-Csv-Any([string] $path) {
    # A CSV as written here, or saved again by Excel (with semicolons in some countries).
    $first = ''
    foreach ($l in @(Get-Content -LiteralPath $path -Encoding UTF8 -TotalCount 1)) { $first = [string]$l }
    $delim = ','
    if ($first.IndexOf(';') -ge 0 -and $first.IndexOf(',') -lt 0) { $delim = ';' }
    return @(Import-Csv -LiteralPath $path -Encoding UTF8 -Delimiter $delim)
}

function Read-Corrections([string] $path) {
    # library\corrections.csv: picture, field, value, apply (yes, ask, no), why; each line also as it was written (raw),
    # so a line the check doesn't change is written back exactly as it is.
    $list = New-Object System.Collections.ArrayList
    if (-not (Test-Path -LiteralPath $path)) { return $list }
    foreach ($c in (Read-Csv-Any $path)) {
        $pic = (Field $c 'picture').Trim().Replace('/', '\')
        if ($pic.ToLowerInvariant().StartsWith('library\')) { $pic = $pic.Substring(8) }
        [void]$list.Add([pscustomobject]@{
            picture = $pic; field = (Field $c 'field').Trim().ToLowerInvariant(); value = (Field $c 'value').Trim()
            apply = (Field $c 'apply').Trim().ToLowerInvariant(); why = (Field $c 'why').Trim(); changed = $false
            raw = [string[]]@((Field $c 'picture'), (Field $c 'field'), (Field $c 'value'), (Field $c 'apply'), (Field $c 'why'))
        })
    }
    return $list
}

# ---------------- Corrections made in the game ----------------
# The player, 23 November 2026: "the corrections i make through the game client are always prioritized. for example if
# an image is generating a man, qwen thinks it's a woman and writes that in the corrections file, i edit the file within
# the game detail screen to be a man, the library import or other scripts should not override that again with the
# "correction" from gwen of it being a woman". Rimfolk's "About this picture" writes its lines with the why "set in the
# game": such a line wins over every other line for the same picture and field (for looks, marks and features: the same
# word), whatever their order, and the check never writes a correction against it.

$ListFields = @('looks', 'marks', 'features')

function Is-Game($c) { return (Low $c.why).StartsWith('set in the game') }

function List-Word([string] $v) {
    # The word of "add:word" or "remove:word" (small letters), or ''.
    $m = [regex]::Match([string]$v, '^\s*(add|remove)\s*:\s*(\S+)\s*$', 'IgnoreCase')
    if ($m.Success) { return $m.Groups[2].Value.ToLowerInvariant() }
    return ''
}

function Same-Target($c, $i) {
    # Whether a correction line is about the same thing as an issue: the same field, for a list the same word (an issue
    # with no word: the same why), for out the same reason (the game's out: any).
    if ($c.field -ne $i.field) { return $false }
    if ($ListFields -contains $i.field) {
        $w = List-Word $i.value
        if ($w -eq '') { return (Why-Key $c.why) -eq (Why-Key $i.what) }
        return (List-Word $c.value) -eq $w
    }
    if ($i.field -eq 'out' -and -not (Is-Game $c)) { return (Why-Key $c.why) -eq (Why-Key $i.what) }
    return $true
}

function Game-Last($lines) {
    # A picture's lines marked yes in the order they are applied: the game's last, so they win; a line for a field the
    # game set (for a list, the same word) is left out.
    $yes = @($lines | Where-Object { $_.apply -eq 'yes' })
    $set = @{}
    foreach ($g in @($yes | Where-Object { Is-Game $_ })) {
        if ($ListFields -contains $g.field) { $w = List-Word $g.value; if ($w -ne '') { $set[$g.field + '|' + $w] = $true } }
        else { $set[$g.field] = $true }
    }
    $other = @($yes | Where-Object { -not (Is-Game $_) } | Where-Object {
        if ($ListFields -contains $_.field) { -not $set.ContainsKey($_.field + '|' + (List-Word $_.value)) } else { -not $set.ContainsKey($_.field) }
    })
    return @($other) + @($yes | Where-Object { Is-Game $_ })
}

function Sex-Word([string] $v) {
    $t = Low $v
    if ($t -match '^(f|female|woman)$') { return 'female' }
    if ($t -match '^(m|male|man)$') { return 'male' }
    return ''
}

function Change-List($list, [string] $v, [bool] $lower) {
    # "add:word" or "remove:word" on a list of words (letters' case aside).
    $m = [regex]::Match($v, '^\s*(add|remove)\s*:\s*(\S+)\s*$', 'IgnoreCase')
    if (-not $m.Success) { return }
    $w = $m.Groups[2].Value
    if ($lower) { $w = $w.ToLowerInvariant() }
    $have = @($list | Where-Object { $_ -eq $w })
    if ($m.Groups[1].Value -eq 'add') { if ($have.Count -eq 0) { [void]$list.Add($w) } }
    else { foreach ($x in $have) { $list.Remove($x) } }
}

function Apply-Yes($rec, $lines) {
    # The record with this picture's corrections marked yes, as Update-PortraitLibrary.ps1 writes it (the game's last).
    $r = $rec.PSObject.Copy()
    $lw = New-Object System.Collections.ArrayList
    foreach ($w in $rec.lookWords) { [void]$lw.Add($w) }
    $mk = New-Object System.Collections.ArrayList
    foreach ($w in $rec.marks) { [void]$mk.Add($w) }
    $ft = New-Object System.Collections.ArrayList
    foreach ($w in $rec.featureWords) { [void]$ft.Add($w) }
    foreach ($c in @(Game-Last $lines)) {
        $v = [string]$c.value
        switch ($c.field) {
            'age' { if ($v -ne '') { $r.age = $v.ToLowerInvariant() } }
            'sex' { $sx = Sex-Word $v; if ($sx -ne '') { $r.sex = $sx } }
            'role' { if ($v -match '^(clear|none|-)$') { $r.role = '' } elseif ($v -ne '') { $r.role = $v.ToLowerInvariant() } }
            'headwear' { $r.headwear = ($v -match '^(true|yes|1)$') }
            'looks' { Change-List $lw $v $false }
            'marks' { Change-List $mk $v $true }
            'features' { Change-List $ft $v $false }
        }
    }
    $r.lookWords = @($lw)
    $r.looks = @($lw | ForEach-Object { Look-Words $_ })
    $r.marks = @($mk)
    $r.featureWords = @($ft)
    $r.features = @($ft | ForEach-Object { $_.ToLowerInvariant() })
    return $r
}

function Why-Key([string] $t) {
    # A line's why for matching: its letters and digits only, so a line saved again by Excel (which may change the
    # model's own characters, such as a curly apostrophe) still matches its issue.
    return ((Low $t) -replace '[^a-z0-9]', '')
}

function Corr-Text($c, [string] $note) {
    # A correction line in short: "age child (yes)", "out (ask, new)".
    $t = $c.field
    if ($c.value -ne '') { $t += ' ' + $c.value }
    $a = $c.apply
    if ($note -ne '') { $a += ', ' + $note }
    return ($t + ' (' + $a + ')')
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

# ---------------- A zip of examples for the lead ----------------
# The player: "Could the check picture CMD perhaps also offer me the option to directly create a zip file with examples
# and the CSV files ... that it asks me after doing the check to create these files, that I can immediately send them to
# you?" After the check it asks; the zip goes into the library: check-report.csv, corrections.csv, checked.csv,
# examples.csv (each picture in the zip with its row's name, subject and pose and the model's answer), and up to 30
# pictures: the flagged ones first, the worst first, then up to 8 that match their records, of different kinds and ages.

$ExampleMost = 30
$ExampleFine = 8

function Kind-Of($rec) {
    if ($rec -eq $null -or @($rec.kinds).Count -eq 0) { return 'baseliner' }
    return ([string]@($rec.kinds)[0]).ToLowerInvariant()
}

function Pick-Fine($fine, [int] $most) {
    # Pictures that match their records: one of each kind first, then one of each age within a kind, then any.
    $picked = New-Object System.Collections.ArrayList
    $kinds = @{}; $pairs = @{}
    foreach ($pass in 1..3) {
        foreach ($f in $fine) {
            if ($picked.Count -ge $most) { break }
            if ($picked.Contains($f)) { continue }
            $k = Kind-Of $f.rec
            $pair = $k + '|' + $f.rec.age
            if ($pass -eq 1 -and $kinds.ContainsKey($k)) { continue }
            if ($pass -eq 2 -and $pairs.ContainsKey($pair)) { continue }
            [void]$picked.Add($f); $kinds[$k] = $true; $pairs[$pair] = $true
        }
    }
    return @($picked)
}

function Zip-File($zip, [string] $path, [string] $name) {
    # One file into the zip; read even while Excel has it open.
    $to = $zip.CreateEntry($name, [System.IO.Compression.CompressionLevel]::Optimal).Open()
    try {
        $from = New-Object System.IO.FileStream($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        try { $from.CopyTo($to) } finally { $from.Dispose() }
    }
    finally { $to.Dispose() }
}

function Zip-Text($zip, [string] $name, [string] $text) {
    # A text written into the zip as a file (UTF-8 with its mark, so Excel reads the letters right).
    $enc = New-Object System.Text.UTF8Encoding($true)
    $bytes = [byte[]]($enc.GetPreamble() + $enc.GetBytes($text))
    $to = $zip.CreateEntry($name, [System.IO.Compression.CompressionLevel]::Optimal).Open()
    try { $to.Write($bytes, 0, $bytes.Length) } finally { $to.Dispose() }
}

function Make-Examples($sorted, $fine, $answerOf, $rowOf, $levelNames) {
    # Writes library\check-examples-<yyyyMMdd-HHmm>.zip and returns its path and how many pictures it holds.
    $fineSet = @(Pick-Fine $fine $ExampleFine)
    $room = $ExampleMost - $fineSet.Count
    $chosen = New-Object System.Collections.ArrayList
    foreach ($o in $sorted) {
        if ($chosen.Count -ge $room) { break }
        $path = Join-Path $library $o.picture
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        $batch = @($o.picture -split '\\')[0]
        [void]$chosen.Add([pscustomobject]@{ show = $o.picture; path = $path; row = $o.row; batch = $batch; check = ($levelNames[$o.level] + ': ' + $o.what) })
    }
    foreach ($f in $fineSet) {
        [void]$chosen.Add([pscustomobject]@{ show = $f.pic.show; path = $f.pic.path; row = $f.pic.row; batch = $f.pic.batch; check = 'matches its record' })
    }
    $lines = New-Object System.Collections.ArrayList
    [void]$lines.Add((Csv-Line @('picture', 'row', 'subject', 'pose', 'check', 'what the model answered')))
    foreach ($c in $chosen) {
        $row = $rowOf[$c.batch + '|' + $c.row]
        $subject = ''; $pose = ''
        if ($row -ne $null) { $subject = $row.subject; $pose = $row.pose }
        $answer = ''
        if ($answerOf.ContainsKey($c.show.ToLowerInvariant())) { $answer = $answerOf[$c.show.ToLowerInvariant()] }
        [void]$lines.Add((Csv-Line ([string[]]@($c.show, $c.row, $subject, $pose, $c.check, $answer))))
    }
    $zipPath = Join-Path $library ('check-examples-' + (Get-Date -Format 'yyyyMMdd-HHmm') + '.zip')
    Add-Type -AssemblyName System.IO.Compression
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    $stream = New-Object System.IO.FileStream($zipPath, [System.IO.FileMode]::CreateNew)
    try {
        $zip = New-Object System.IO.Compression.ZipArchive -ArgumentList $stream, ([System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($f in @($reportFile, $correctionsFile, $checkedFile)) {
                if (Test-Path -LiteralPath $f) { Zip-File $zip $f ([System.IO.Path]::GetFileName($f)) }
            }
            Zip-Text $zip 'examples.csv' (($lines -join "`r`n") + "`r`n")
            foreach ($c in $chosen) { Zip-File $zip $c.path ('pictures/' + $c.batch + '/' + [System.IO.Path]::GetFileName($c.path)) }
        }
        finally { $zip.Dispose() }
    }
    catch {
        $stream.Dispose()
        Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
        throw
    }
    finally { $stream.Dispose() }
    return @{ path = $zipPath; pictures = $chosen.Count; flagged = $chosen.Count - $fineSet.Count; fine = $fineSet.Count }
}

function Size-Text([long] $bytes) {
    if ($bytes -ge 1048576) { return ('{0:0.0} MB' -f ($bytes / 1048576.0)) }
    return ('{0:0} KB' -f [Math]::Ceiling($bytes / 1024.0))
}

# ---------------- The run ----------------

try {
    Add-Type -AssemblyName System.Net.Http
    if (-not (Test-Path -LiteralPath $library)) { throw ('There is no library folder next to this script ({0}): it checks the pictures in library\, see SETUP-ComfyUI.md step 7.' -f $library) }
    $recordsFile = Join-Path $library 'records.xml'
    if (-not (Test-Path -LiteralPath $recordsFile)) { throw 'The library has no records yet: run Update-PortraitLibrary.ps1 (or Copy-ToLibrary.cmd) first, then this again.' }
    $records = Read-Records $recordsFile

    # The pictures: library\<batch>\*.png. A backdrop's depth map (<picture>_depth.png) and a person's expressions
    # (<picture>_v-blink.png, _v-lostEyeLeft-2.png ...) belong to their picture, which is checked; they are not looked
    # at.
    Test-Form
    $pictures = New-Object System.Collections.ArrayList
    $skippedBackdrops = 0
    # Pictures deleted for good in the game (deleted.csv) are not looked at, even where their file is still here.
    $deleted = Read-Deleted $library
    $skippedDeleted = 0
    foreach ($dir in @(Get-ChildItem -LiteralPath $library -Directory | Sort-Object Name)) {
        foreach ($f in @(Get-ChildItem -LiteralPath $dir.FullName -Filter '*.png' -File | Sort-Object Name)) {
            $stem = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
            if ($stem -match '_(depth|v-[A-Za-z]+(-\d+)?)$') { continue }
            if (Is-Deleted $f.FullName $deleted) { $skippedDeleted++; continue }
            # Backdrops are not looked at (the player: "the image analysis ... also analyses the backdrops"): the questions
            # are about a person, and on an empty scene the model made one up. Only people's pictures are checked.
            $rec = $records[($dir.Name + '/' + $stem).ToLowerInvariant()]
            if ($rec -ne $null -and $rec.kind -eq 'backdrop') { $skippedBackdrops++; continue }
            $row = ''
            $m = [regex]::Match($stem, '^(?<name>.+?)_(?<style>style[^_]*)_')
            if ($m.Success) { $row = $m.Groups['name'].Value }
            [void]$pictures.Add([pscustomobject]@{
                key = ($dir.Name + '/' + $f.Name); show = ($dir.Name + '\' + $f.Name); tex = ($dir.Name + '/' + $stem).ToLowerInvariant(); batch = $dir.Name
                path = $f.FullName; size = [string]$f.Length; row = $row; cut = ($stem -match '_cut(_|$)')
            })
        }
    }
    if ($skippedBackdrops -gt 0) { Write-Host ('{0} backdrops left out: only people are checked.' -f $skippedBackdrops) }
    if ($skippedDeleted -gt 0) { Write-Host ('{0} pictures left out: deleted for good in the game (deleted.csv).' -f $skippedDeleted) }
    if ($pictures.Count -eq 0) { throw ('No pictures of people in the library ({0}) yet.' -f $library) }

    $db = Read-Checked
    # A checked.csv with another header (written before the lines had versions of their own, or before a line was
    # added) is written again with today's columns, each line's answer kept with the version it was asked with.
    $dirty = $db.rows -gt $db.map.Count
    if (Test-Path -LiteralPath $checkedFile) {
        $head = ''
        foreach ($l in @(Get-Content -LiteralPath $checkedFile -Encoding UTF8 -TotalCount 1)) { $head = ([string]$l).TrimStart([char]0xFEFF) }
        if ($head -ne (Csv-Line $CheckedColumns)) { Write-Checked $db.map; $dirty = $false }
    }

    # What to ask: a new picture, or one changed since (another size), the whole form; a picture asked before only the
    # lines whose version is newer than the one it was asked with, in a short form (the whole form when every line is);
    # with -Again every picture the whole form.
    $todo = New-Object System.Collections.ArrayList
    $newCount = 0; $againCount = 0; $complete = 0
    $groups = @{}
    foreach ($pic in $pictures) {
        $e = $db.map[$pic.key.ToLowerInvariant()]
        $keys = $FieldKeys; $whole = $true
        if ($e -eq $null) { $newCount++ }
        elseif ($Again -or $e.size -ne $pic.size) { $againCount++ }
        else {
            $old = @($FieldKeys | Where-Object { [string]$e.versions[$_] -ne [string]$LineVersions[$_] })
            if ($old.Count -eq 0) { $complete++; continue }
            if ($old.Count -lt $FieldKeys.Count) {
                $keys = $old; $whole = $false
                $g = $old -join ','
                if ($groups.ContainsKey($g)) { $groups[$g]++ } else { $groups[$g] = 1 }
            }
            else { $againCount++ }
        }
        [void]$todo.Add([pscustomobject]@{ pic = $pic; keys = @($keys); whole = $whole; image = ''; id = '' })
    }
    Write-Host ''
    if ($todo.Count -eq 0) { Write-Host ('Nothing to ask the model: all {0} have every answer.' -f (Count-Of $pictures.Count 'picture' 'pictures')) }
    else {
        $say = New-Object System.Collections.ArrayList
        [void]$say.Add((Count-Of $newCount 'new picture' 'new pictures') + ' (the whole form)')
        if ($groups.Count -eq 0) { [void]$say.Add('0 to look at again for single lines') }
        $firstGroup = $true
        foreach ($g in @($groups.GetEnumerator() | Sort-Object @{ Expression = { $_.Value }; Descending = $true }, @{ Expression = { $_.Key } })) {
            $which = Lines-Text ($g.Key -split ',')
            if ($firstGroup) { [void]$say.Add(('{0} to look at again for {1} only' -f $g.Value, $which)) } else { [void]$say.Add(('{0} for {1}' -f $g.Value, $which)) }
            $firstGroup = $false
        }
        if ($Again) { [void]$say.Add(('{0} asked again (-Again: the whole form)' -f $againCount)) }
        else { [void]$say.Add(('{0} asked again (the whole form)' -f $againCount)) }
        Write-Host ('What the model is asked: ' + ($say -join ', ') + '.')
        if ($complete -gt 0) { Write-Host ('{0} every answer already.' -f (Count-Of $complete 'picture has' 'pictures have')) }
    }
    $unread = New-Object System.Collections.ArrayList
    $described = 0

    if ($todo.Count -gt 0) {
        if (-not (Test-Path -LiteralPath $Workflow)) {
            throw ('The check''s workflow {0} is missing. Make it in ComfyUI and save it with Workflow, Export (API) under that name (SETUP-ComfyUI.md, step 8).' -f $Workflow)
        }
        $script:WfText = Get-Content -LiteralPath $Workflow -Raw -Encoding UTF8
        $probeWf = $script:WfText | ConvertFrom-Json
        if ($probeWf.PSObject.Properties.Name -contains 'nodes') { throw 'This is a saved workflow, not an API export. In ComfyUI use Workflow, Export (API) and save it again under the same name.' }
        $classes = @($probeWf.PSObject.Properties | ForEach-Object { [string]$_.Value.class_type })
        if (-not (@($classes | Where-Object { $_ -match '(?i)^load.?image' }).Count -gt 0)) { throw 'The check''s workflow has no Load Image box. Add one, wire it to the describing box, and export it again (step 8).' }
        if (-not (@($classes | Where-Object { $_ -match $TextOutPattern }).Count -gt 0)) { throw 'The check''s workflow has no Preview as Text box (Preview Any in older versions) to show the answer. Add one after the Generate Text box, wire the text to it, and export it again (step 8).' }
        $fill = Fill-Workflow ($script:WfText | ConvertFrom-Json) 'test.png' $Question
        if (-not $fill[1]) { throw 'The check''s workflow has no prompt box with %QUESTION% in it. Type %QUESTION% into the Generate Text box''s prompt and export it again (step 8).' }
        $model = Model-Of $probeWf

        $script:Probe = New-Http 4
        $script:Http = New-Http 300
        $script:Url = Find-Server
        $script:Client = [guid]::NewGuid().ToString()
        $script:Said = ''
        if (-not (Test-Path -LiteralPath $checkedFile)) {
            [System.IO.File]::WriteAllText($checkedFile, (Csv-Line $CheckedColumns) + "`r`n", (New-Object System.Text.UTF8Encoding($true)))
        }
        # Every picture to ask is handed to ComfyUI first, each under a name of its own, then all the questions are
        # queued at once and the answers collected in order: ComfyUI answers them one after the other, before any job
        # queued after them (the player: "it should finish all the analysis first and only then start upcoming jobs so
        # it's a proper queue").
        Write-Host ('Handing {0} to ComfyUI ...' -f (Count-Of $todo.Count 'picture' 'pictures'))
        foreach ($t in $todo) { $t.image = Upload-Picture $t.pic.path (Upload-Name $t.pic.key) 'rimfolk-check' }
        $ahead = Queue-Ahead
        $waiting = New-Object System.Collections.ArrayList
        $forms = Form-Parts
        try {
            foreach ($t in $todo) { $t.id = Queue-Question $t.image (Form-Of $t.keys); [void]$waiting.Add($t.id) }
            if ($ahead -eq 1) { Write-Host 'ComfyUI has 1 job ahead of the check; it starts when that is done.' }
            elseif ($ahead -gt 1) { Write-Host ('ComfyUI has {0} jobs ahead of the check; it starts when they are done.' -f $ahead) }
            Write-Host ('Queued {0} in ComfyUI. The first takes longer while the model loads.' -f (Count-Of $todo.Count 'question' 'questions'))
            Write-Host 'To stop, press Ctrl+C (closing the window instead leaves the questions in ComfyUI''s queue).'
            $n = 0
            foreach ($t in $todo) {
                $n++
                $pic = $t.pic
                $texts = @(Wait-Answer $t.id ($n -eq 1 -and $ahead -le 0))
                [void]$waiting.Remove($t.id)
                if ($texts.Count -eq 0) {
                    if ($described -eq 0) { throw 'ComfyUI answered with no text. Check that the Generate Text box''s text is wired into a Preview as Text box, and export the workflow again (step 8).' }
                    [void]$unread.Add([pscustomobject]@{ pic = $pic; text = '(no text)'; keys = $t.keys; whole = $t.whole })
                    continue
                }
                $best = $null; $bestText = ''
                foreach ($tx in $texts) {
                    $parsed = Read-Answer $tx $t.keys
                    if ($best -eq $null -or $parsed.Count -gt $best.Count) { $best = $parsed; $bestText = $tx }
                }
                $need = 10; if (-not $t.whole) { $need = 1 }
                if ($best.Count -lt $need) {
                    [void]$unread.Add([pscustomobject]@{ pic = $pic; text = $bestText; keys = $t.keys; whole = $t.whole })
                    Write-Host ('[{0}/{1}] {2}: the answer could not be read' -f $n, $todo.Count, $pic.show) -ForegroundColor Yellow
                    continue
                }
                $when = Get-Date -Format 'yyyy-MM-dd HH:mm'
                $key = $pic.key.ToLowerInvariant()
                if ($t.whole) {
                    $values = @{}
                    foreach ($k in $FieldKeys) { $v = ''; if ($best.ContainsKey($k)) { $v = [string]$best[$k] }; $values[$k] = $v }
                    $e = [pscustomobject]@{ picture = $pic.key; size = $pic.size; versions = (Current-Versions); checked = $when; model = $model; values = $values; answer = (Answer-Text $bestText) }
                    $said = Short (Seen-Says ([pscustomobject]$values))
                }
                else {
                    # The lines asked again go with the answers kept; a line the answer doesn't give is asked again next
                    # time.
                    $e = $db.map[$key]
                    $got = @($t.keys | Where-Object { $best.ContainsKey($_) })
                    foreach ($k in $got) { $e.values[$k] = [string]$best[$k]; $e.versions[$k] = [string]$LineVersions[$k] }
                    $e.answer = Add-Short $e.answer $t.keys $bestText
                    $e.checked = $when; $e.model = $model
                    $said = (@($got | ForEach-Object { $forms.labels[$_] + ': ' + (Short ([string]$best[$_])) }) -join '; ')
                    $missed = @($t.keys | Where-Object { -not $best.ContainsKey($_) })
                    if ($missed.Count -gt 0) { $said += (' (no answer for {0}: asked again next time)' -f (Lines-Text $missed)) }
                    $said = 'again, ' + $said
                }
                $db.map[$key] = $e
                [System.IO.File]::AppendAllText($checkedFile, (Entry-Line $e) + "`r`n", (New-Object System.Text.UTF8Encoding($false)))
                $described++; $dirty = $true
                Write-Host ('[{0}/{1}] {2}: {3}' -f $n, $todo.Count, $pic.show, $said)
            }
        }
        finally { if ($waiting.Count -gt 0) { Leave-Queue $waiting 'question' 'questions' } }
    }

    # The rows' own prompts (subject and pose, from batches\<batch>.csv), which say what a look or role asked to show.
    $askedOf = @{}
    $rowOf = @{}
    foreach ($b in @($pictures | ForEach-Object { $_.batch } | Select-Object -Unique)) {
        $csv = Join-Path (Join-Path $Folder 'batches') ($b + '.csv')
        if (-not (Test-Path -LiteralPath $csv)) { continue }
        foreach ($r in @(Import-Csv -LiteralPath $csv -Encoding UTF8)) {
            if ([string]::IsNullOrWhiteSpace($r.name)) { continue }
            $rk = $b + '|' + ($r.name -replace '[^A-Za-z0-9_\-]', '_')
            $askedOf[$rk] = ((Field $r 'subject') + ' ' + (Field $r 'pose')).Trim()
            $rowOf[$rk] = @{ subject = (Field $r 'subject'); pose = (Field $r 'pose') }
        }
    }

    # The corrections (library\corrections.csv, kept from run to run; you may change apply there). Lines marked yes are
    # applied here as Update-PortraitLibrary.ps1 applies them, so a corrected picture leaves the report; a line marked
    # no keeps its issue out of the report (you decided to keep it so); ask waits for you. New lines are added at the
    # end; the check's own lines may be changed by a later scan, the game's ("set in the game") never.
    $corrections = @(Read-Corrections $correctionsFile)
    $byPic = @{}
    foreach ($c in $corrections) {
        if ($c.picture -eq '') { continue }
        $k = $c.picture.ToLowerInvariant()
        if (-not $byPic.ContainsKey($k)) { $byPic[$k] = New-Object System.Collections.ArrayList }
        [void]$byPic[$k].Add($c)
    }
    $newLines = New-Object System.Collections.ArrayList
    $changedLines = 0
    $leftOut = 0

    # The report: every checked picture in the library against its record now, and each cut-out's sides.
    $out = New-Object System.Collections.ArrayList
    $fine = New-Object System.Collections.ArrayList
    $compared = 0
    $sidesRead = 0
    $script:DrawingOk = $null
    # A short form whose answer couldn't be read: its picture is compared with the answers kept, and the report says so.
    $unreadShort = @{}
    foreach ($u in $unread) { if (-not $u.whole) { $unreadShort[$u.pic.key.ToLowerInvariant()] = $u } }
    $reread = 0
    Write-Host ''
    Write-Host 'Comparing the pictures with their records, and looking at the sides of the cut-outs...'
    foreach ($pic in $pictures) {
        $e = $db.map[$pic.key.ToLowerInvariant()]
        if ($e -eq $null -or $e.size -ne $pic.size) { continue }
        # The kept answer read again with today's reading; checked.csv is written again when it reads otherwise now.
        $vals = Seen-Of $e
        foreach ($fk in $FieldKeys) { if ([string]$vals[$fk] -cne [string]$e.values[$fk]) { $e.values = $vals; $dirty = $true; $reread++; break } }
        $seen = [pscustomobject]$vals
        $k = $pic.show.ToLowerInvariant()
        if (-not $byPic.ContainsKey($k)) { $byPic[$k] = New-Object System.Collections.ArrayList }
        $mine = $byPic[$k]
        $bare = [System.IO.Path]::GetFileName($pic.show).ToLowerInvariant()
        if ($byPic.ContainsKey($bare)) { foreach ($c in $byPic[$bare]) { if (-not $mine.Contains($c)) { [void]$mine.Add($c) } } }
        if (@($mine | Where-Object { $_.field -eq 'out' -and $_.apply -eq 'yes' }).Count -gt 0) { $leftOut++; continue }
        $rec = $records[$pic.tex]
        if ($rec -eq $null) {
            [void]$out.Add([pscustomobject]@{ level = 4; count = 1; picture = $pic.show; row = $pic.row; what = 'no record: not in records.xml'; seen = (Seen-Says $seen); says = ''; fix = 'run Update-PortraitLibrary.ps1 (if it still has no record, its batch list has no row for this name)'; corr = '' })
            continue
        }
        $compared++
        $rec = Apply-Yes $rec $mine
        $asked = ''
        if ($askedOf.ContainsKey($pic.batch + '|' + $pic.row)) { $asked = $askedOf[$pic.batch + '|' + $pic.row] }
        $side = $null
        if ($pic.cut) { $side = Side-Cut-Of (Read-Pixels $pic.path); if ($side -ne $null) { $sidesRead++ } }
        $found = @(Compare-Picture $rec $seen $pic.cut $asked $side)
        $u = $unreadShort[$pic.key.ToLowerInvariant()]
        if ($u -ne $null) { $found += [pscustomobject]@{ level = 4; what = ('the model''s answer to {0} could not be read, so the earlier answer was used; it is asked again next time' -f (Lines-Text $u.keys)); fix = 'run this again; if it keeps happening, send check-report.csv to the lead'; field = ''; value = ''; apply = '' } }
        $issues = @(foreach ($lv in 1..4) { $found | Where-Object { $_.level -eq $lv } })
        $kept = New-Object System.Collections.ArrayList
        $notes = New-Object System.Collections.ArrayList
        $touched = New-Object System.Collections.ArrayList
        foreach ($i in $issues) {
            if ($i.field -eq '') { [void]$kept.Add($i); continue }
            # Set in the game: that setting stays, and no correction is written against it; the report says what the
            # model sees.
            $game = $null
            foreach ($c in $mine) { if ($c.apply -eq 'yes' -and (Is-Game $c) -and (Same-Target $c $i)) { $game = $c } }
            if ($game -ne $null) {
                $set = $game.field; if ($game.value -ne '') { $set += ' ' + $game.value }
                [void]$kept.Add([pscustomobject]@{ level = 5; what = ('set in the game: {0}; the model sees otherwise ({1})' -f $set, $i.what); fix = 'nothing: the game''s setting is kept'; field = $i.field; value = $i.value; apply = '' })
                [void]$notes.Add(('set in the game: {0} (kept)' -f $set))
                continue
            }
            # The check's own line for it (any line not set in the game): kept as it is while the model answers the
            # same (a no stays no); given the new answer, and yes or ask by the rules, when a later scan answers
            # otherwise.
            $own = $null
            foreach ($c in $mine) { if (-not (Is-Game $c) -and (Same-Target $c $i) -and (Why-Key $c.why) -eq (Why-Key $i.what)) { $own = $c; break } }
            if ($own -eq $null) { foreach ($c in $mine) { if (-not (Is-Game $c) -and (Same-Target $c $i)) { $own = $c; break } } }
            if ($own -ne $null -and $touched.Contains($own)) { [void]$notes.Add((Corr-Text $own 'already there')); [void]$kept.Add($i); continue }
            if ($own -ne $null) {
                if ((Low $own.value) -eq (Low $i.value)) {
                    if ($own.apply -eq 'no') { continue }
                    [void]$notes.Add((Corr-Text $own '')); [void]$kept.Add($i); continue
                }
                $own.value = $i.value; $own.apply = $i.apply; $own.why = $i.what.Trim()
                if (-not $own.changed) { $own.changed = $true; $changedLines++ }
                [void]$touched.Add($own)
                [void]$notes.Add((Corr-Text $own 'changed')); [void]$kept.Add($i); continue
            }
            # One out line for a picture is enough: another reason's, not answered no, is already there.
            if ($i.field -eq 'out') {
                $other = $null
                foreach ($c in $mine) { if ($c.field -eq 'out' -and $c.apply -ne 'no') { $other = $c; break } }
                if ($other -ne $null) { [void]$notes.Add((Corr-Text $other 'already there')); [void]$kept.Add($i); continue }
            }
            $c = [pscustomobject]@{ picture = $pic.show; field = $i.field; value = $i.value; apply = $i.apply; why = $i.what.Trim(); changed = $true; raw = $null }
            [void]$mine.Add($c); [void]$newLines.Add($c); [void]$touched.Add($c)
            [void]$notes.Add((Corr-Text $c 'new')); [void]$kept.Add($i)
        }
        if ($kept.Count -eq 0) { [void]$fine.Add([pscustomobject]@{ pic = $pic; rec = $rec }); continue }
        $fixes = New-Object System.Collections.ArrayList
        foreach ($i in $kept) { if (-not $fixes.Contains($i.fix)) { [void]$fixes.Add($i.fix) } }
        [void]$out.Add([pscustomobject]@{
            level = [int](@($kept | Measure-Object -Property level -Minimum).Minimum); count = $kept.Count; picture = $pic.show; row = $pic.row
            what = (($kept | ForEach-Object { $_.what }) -join ' | '); seen = (Seen-Says $seen); says = (Row-Says $rec); fix = ($fixes -join ' | '); corr = ($notes -join ' | ')
        })
    }
    foreach ($u in @($unread | Where-Object { $_.whole })) {
        [void]$out.Add([pscustomobject]@{ level = 4; count = 1; picture = $u.pic.show; row = $u.pic.row; what = 'the model''s answer could not be read; it is asked again next time'; seen = (Short $u.text); says = ''; fix = 'run this again; if it keeps happening, send check-report.csv to the lead'; corr = '' })
    }

    # The description database without the older lines of pictures asked again, and with what reads otherwise now.
    if ($dirty) { Write-Checked $db.map }
    if ($reread -gt 0) { Write-Host ('{0} read again with today''s reading came out otherwise; checked.csv has them now.' -f (Count-Of $reread 'kept answer' 'kept answers')) }

    # corrections.csv first (the lines already there as they were, the check's own changed where a later scan answers
    # otherwise, then the new ones), then the report.
    if ($newLines.Count -gt 0 -or $changedLines -gt 0) {
        $all = @($corrections) + @($newLines)
        Write-CsvFile $correctionsFile @('picture', 'field', 'value', 'apply', 'why') @($all | ForEach-Object {
            if ($_.changed -or $null -eq $_.raw) { Csv-Line ([string[]]@($_.picture, $_.field, $_.value, $_.apply, $_.why)) } else { Csv-Line $_.raw }
        })
    }
    $LevelNames = @{ 1 = '1 wrong'; 2 = '2 shows more'; 3 = '3 shows less'; 4 = '4 not compared'; 5 = '5 set in the game' }
    $sorted = @($out | Sort-Object @{ Expression = { $_.level } }, @{ Expression = { $_.count }; Descending = $true }, @{ Expression = { $_.picture } })
    $lines = @($sorted | ForEach-Object { Csv-Line ([string[]]@($_.picture, $_.row, $LevelNames[$_.level], $_.what, $_.seen, $_.says, $_.fix, $_.corr)) })
    Write-CsvFile $reportFile @('picture', 'row', 'check', 'what is wrong', 'what the model saw', 'its row says', 'suggested fix', 'in corrections.csv') $lines

    $count = @{ 1 = 0; 2 = 0; 3 = 0; 4 = 0; 5 = 0 }
    foreach ($o in $out) { $count[$o.level]++ }
    Write-Host ''
    if ($todo.Count -gt 0) {
        $line = 'The model answered {0} of {1}.' -f $described, (Count-Of $todo.Count 'question' 'questions')
        if ($unread.Count -eq 1) { $line += ' 1 answer could not be read; that picture is asked about again next time.' }
        elseif ($unread.Count -gt 1) { $line += (' {0} answers could not be read; those pictures are asked about again next time.' -f $unread.Count) }
        Write-Host $line
    }
    else { Write-Host ('Nothing new to ask; compared the {0} with the records again.' -f (Count-Of $compared 'checked picture' 'checked pictures')) }
    $toLook = $out.Count - $count[5]
    if ($toLook -eq 0) {
        Write-Host ('All {0} match their records.' -f (Count-Of $compared 'checked picture' 'checked pictures')) -ForegroundColor Green
    }
    else {
        $parts = New-Object System.Collections.ArrayList
        if ($count[1] -gt 0) { [void]$parts.Add(('{0} wrong' -f $count[1])) }
        if ($count[2] -eq 1) { [void]$parts.Add('1 shows more than its row says') }
        elseif ($count[2] -gt 1) { [void]$parts.Add(('{0} show more than their rows say' -f $count[2])) }
        if ($count[3] -gt 0) { [void]$parts.Add(('{0} less' -f (Count-Of $count[3] 'shows' 'show'))) }
        if ($count[4] -gt 0) { [void]$parts.Add(('{0} not compared' -f $count[4])) }
        Write-Host ('{0} to look at: {1}.' -f (Count-Of $toLook 'picture' 'pictures'), ($parts -join ', '))
        Write-Host 'The worst first:'
        $colours = @{ 1 = 'Red'; 2 = 'Yellow'; 3 = 'Cyan'; 4 = 'Gray' }
        $worst = @($sorted | Where-Object { $_.level -lt 5 })
        foreach ($o in @($worst | Select-Object -First 15)) {
            Write-Host ('  {0,-14} {1}: {2}' -f $LevelNames[$o.level], $o.picture, $o.what) -ForegroundColor $colours[$o.level]
        }
        if ($worst.Count -gt 15) { Write-Host ('  ... and {0} more.' -f ($worst.Count - 15)) }
        Write-Host ''
        Write-Host ('The whole list, with what the model saw and a suggested fix for each: {0}' -f $reportFile)
    }
    if ($count[5] -gt 0) { Write-Host ('{0} the model sees otherwise than set in the game: the game''s setting is kept (check-report.csv, 5 set in the game).' -f (Count-Of $count[5] 'picture' 'pictures')) }
    $yesNew = @($newLines | Where-Object { $_.apply -eq 'yes' }).Count
    $askAll = @(@($corrections) + @($newLines) | Where-Object { $_.apply -eq 'ask' }).Count
    if ($newLines.Count -gt 0) { Write-Host ('{0} written to {1}: {2} marked yes, {3} marked ask.' -f (Count-Of $newLines.Count 'new correction' 'new corrections'), $correctionsFile, $yesNew, ($newLines.Count - $yesNew)) }
    if ($changedLines -gt 0) { Write-Host ('{0} of the check''s own changed: the model answers otherwise now (marked "changed" in check-report.csv).' -f (Count-Of $changedLines 'correction' 'corrections')) }
    if ($leftOut -gt 0) { Write-Host ('{0} left out by corrections.csv (out, yes).' -f (Count-Of $leftOut 'picture is' 'pictures are')) }
    if ($askAll -gt 0) { Write-Host ('{0} marked ask (the check was less sure): applied all the same; correct a picture in the game ("About this picture") when you see it, or mark a line no.' -f (Count-Of $askAll 'correction is' 'corrections are')) }
    $cutOuts = @($pictures | Where-Object { $_.cut }).Count
    if ($sidesRead -eq 0 -and $compared -gt 0 -and $cutOuts -gt 0) { Write-Host 'The side check could not read the pictures here (it needs Windows), so only the model''s check was made.' -ForegroundColor Yellow }
    Write-Host 'Run Update-PortraitLibrary.ps1 (Copy-ToLibrary.cmd runs it too) to put the corrections (yes and ask) into the records,'
    Write-Host 'then "Reload the library" in the game. No picture was deleted, moved or renamed, and no batch list was changed.'

    # A zip of examples to send to the lead.
    if ($compared -gt 0 -or $out.Count -gt 0) {
        $make = $Examples
        if ($make -eq 'ask') {
            Write-Host ''
            $a = Read-Host 'Make a zip of examples to send? (Enter for yes, n for no)'
            $make = 'yes'; if ((Low $a) -match '^n') { $make = 'no' }
        }
        if ($make -eq 'yes') {
            $answerOf = @{}
            foreach ($pic in $pictures) {
                $e = $db.map[$pic.key.ToLowerInvariant()]
                if ($e -ne $null) { $answerOf[$pic.show.ToLowerInvariant()] = [string]$e.answer }
            }
            foreach ($u in $unread) { $answerOf[$u.pic.show.ToLowerInvariant()] = (((([string]$u.text) -replace "`r", '') -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }) -join ' / ') }
            try {
                $z = Make-Examples $sorted $fine $answerOf $rowOf $LevelNames
                $size = Size-Text (Get-Item -LiteralPath $z.path).Length
                Write-Host ('Made {0} ({1}): the three CSV files, examples.csv and {2} ({3} flagged, {4} that match). Send it to the lead.' -f $z.path, $size, (Count-Of $z.pictures 'picture' 'pictures'), $z.flagged, $z.fine) -ForegroundColor Green
            }
            catch { Write-Host ('The zip could not be made: ' + $_.Exception.Message) -ForegroundColor Red }
        }
    }
}
catch {
    Write-Host ''
    Write-Host ('Something went wrong: ' + $_.Exception.Message) -ForegroundColor Red
}
Write-Host ''
if (-not $NoPause) { Read-Host 'Press Enter to close' }
