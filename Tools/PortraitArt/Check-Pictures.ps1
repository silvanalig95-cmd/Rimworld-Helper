# Check-Pictures.ps1, for Rimfolk's portraits.
#
# Looks at the pictures in the library (library\<batch>\*.png next to this script) with a describing model in ComfyUI on
# this computer, and compares what the model saw with each picture's record (library\records.xml, written from its batch
# row by Update-PortraitLibrary.ps1). A record has to match its picture: the game gives a picture with a look or a role
# only to pawns who have it, so a picture that shows more than its row says (a weapon, a flame, wings, a crown) or less
# (a woman drawn as a man) goes to the wrong people.
# - The workflow: rimfolk-check-workflow-api.json next to this script, exported from ComfyUI with Workflow, Export (API):
#   a Load Image box, ComfyUI's own Generate Text box with a Qwen3.5 model and %QUESTION% in its prompt box, and a
#   Preview as Text box after it (docs/portrait-art/SETUP-ComfyUI.md, step 8). Another describing box (the QwenVL box of
#   the ComfyUI-QwenVL pack) works the same way, with %QUESTION% in its prompt box.
# - Each new picture is uploaded to ComfyUI (as input\rimfolk-check.png, the same name every time), asked a fixed
#   list of questions, and the answer kept in library\checked.csv (the picture's name and size, the answer item by item,
#   and the answer as given): the description database. Running it again asks only about pictures not asked about
#   before (or changed since).
# - Every run compares all the checked pictures with their records again and writes library\check-report.csv: what is
#   wrong, what the model saw, what the row says, and a suggested fix (throw it out, or add a look or a role to its row),
#   the worst first. So after fixing a row and running Update-PortraitLibrary.ps1, a run here shows the new state without
#   asking the model again.
# - It never deletes, moves or renames a picture and never changes a batch list or the records: you decide.
#
# Run it: keep ComfyUI open and double-click Check-Pictures.cmd (or right-click this file, "Run with PowerShell").
# To ask about every picture again (after changing the model), delete library\checked.csv first.

[CmdletBinding()]
param(
    [string] $Folder = '',
    [string] $Workflow = '',
    [string] $Server = '',
    [switch] $Again,
    [switch] $NoPause
)

$ErrorActionPreference = 'Stop'
if ($Folder -eq '') { $Folder = Split-Path -Parent $PSCommandPath }
$library = Join-Path $Folder 'library'
$checkedFile = Join-Path $library 'checked.csv'
$reportFile = Join-Path $library 'check-report.csv'
$settingsFile = Join-Path $Folder 'check-settings.json'
if ($Workflow -eq '') { $Workflow = Join-Path $Folder 'rimfolk-check-workflow-api.json' }

# ---------------- The question ----------------

# Raise the number when the question changes: pictures asked with an older one are asked again.
$QuestionVersion = '1'
$Question = @'
This is a painted portrait made for a game. Look at the picture closely and fill in the form below about the person in it. Write each line's name, a colon, and a short answer of a few words. Answer every line, in this order, and write nothing else. Write none when the thing is not in the picture.

people: how many people are in the picture (a number)
sex: man or woman
age: how old the person looks, as one number of years, as if they were human
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
hands: what they hold in their hands, or nothing
weapons: any weapon in the picture (sword, axe, spear, bow, gun, knife), or none
armour: any armour they wear, or none
fire or magic: any flame, glowing light, spell or magic in the picture, or none
headwear: any hat, helmet, hood, crown or other headwear, or none
book or tools: any book, scroll or tool in the picture, or none
unusual: anything else unusual about the person, or none
'@

# The answer's items, in the question's order (also checked.csv's columns), and the names a model may give them.
$FieldKeys = @('people', 'sex', 'age', 'skin', 'hair', 'beard', 'ears', 'horns', 'tail', 'wings', 'eyes', 'body', 'eyemarks', 'hands', 'weapons', 'armour', 'magic', 'headwear', 'tools', 'unusual')
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
    'hands' = 'hands'; 'hand' = 'hands'; 'in hands' = 'hands'; 'in their hands' = 'hands'; 'holding' = 'hands'; 'holds' = 'hands'; 'held' = 'hands'
    'weapons' = 'weapons'; 'weapon' = 'weapons'
    'armour' = 'armour'; 'armor' = 'armour'
    'fire or magic' = 'magic'; 'magic or fire' = 'magic'; 'magic' = 'magic'; 'fire' = 'magic'; 'fire magic' = 'magic'
    'headwear' = 'headwear'; 'head wear' = 'headwear'; 'hat' = 'headwear'; 'hat or helmet' = 'headwear'; 'helmet' = 'headwear'; 'headgear' = 'headwear'
    'book or tools' = 'tools'; 'books or tools' = 'tools'; 'book or tool' = 'tools'; 'tools' = 'tools'; 'tool' = 'tools'; 'book' = 'tools'
    'unusual' = 'unusual'; 'anything unusual' = 'unusual'; 'other' = 'unusual'; 'anything else' = 'unusual'; 'notes' = 'unusual'
}

# ---------------- What a shown thing needs in the record ----------------
# Look words grow with the library (Rimfolk's PortraitLookDefs and the catalogue's), so a look is matched by its word's
# parts: any look word with "wing" in it allows wings, any with "mage", "magic", "pyro", "fire", "ice" ... allows fire or
# magic. Add parts here when new kinds of looks come.

$FightRoles = @('soldier', 'warrior', 'hunter', 'scout')
$ArmourRoles = @('soldier', 'warrior')
$WorkRoles = @('smith', 'miner', 'builder', 'crafter', 'farmer', 'herder', 'cook', 'doctor', 'artist', 'labourer')
$WingLooks = 'wing'
$MagicLooks = 'mage|magic|psy|witch|wizard|sorcer|caster|arcan|necro|shaman|druid|pyro|fire|flame|ice|frost|storm|lightn|spell|warlock|elemental|mystic|occult'
# Looks that should show fire or magic in the picture (a pyromaniac's or a psycaster's look may be only in the face).
$MagicShownLooks = 'mage|magic|witch|wizard|sorcer|warlock|elemental|necro|arcan|spell'
$FighterLooks = 'knight|soldier|warrior|guard|merc|raider|bandit|pirate|gladiator|berserk|hunter|archer|barbarian|veteran|fighter|duel'
$NobleLooks = 'noble|royal|king|queen|emperor|empress|crown|lord|lady|prince|regal|baron|duke'
$BookLooks = 'scholar|scribe|sage|monk|priest|bookish|librar'
$TwinLooks = 'conjoin|twin'

$WeaponWords = '\b(swords?|sabres?|sabers?|blades?|axes?|hatchets?|spears?|pikes?|halberds?|lances?|bows?|crossbows?|arrows?|guns?|rifles?|pistols?|revolvers?|muskets?|shotguns?|firearms?|knife|knives|daggers?|maces?|clubs?|warhammers?|war hammers?|flails?|shields?|whips?|glaives?|tridents?|katanas?|rapiers?|longswords?|greatswords?|machetes?|javelins?|slings?|grenades?|blasters?|carbines?)\b'
$MagicWords = '\b(flames?|fire|fireballs?|burning|torch|torches|embers?|blaze|spells?|magic|magical|orbs?|lightning|sparks|aura|runes?|wands?|glowing|frost|arcane|psychic)\b'
$CrownWords = '\b(crowns?|tiaras?|circlets?|diadems?|coronets?)\b'
$ToolRoles = [ordered]@{
    'scholar'  = '\b(books?|tomes?|scrolls?|quills?|pens?|journals?|notebooks?|maps?|ledgers?|papers?|manuscripts?)\b'
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

# ---------------- Reading an answer ----------------

function Read-Answer([string] $text) {
    # The model's answer to the question, item by item (a hashtable key -> text). It takes the answer as asked (one
    # "name: answer" line each) and the shapes a model drifts into: numbered or bulleted lines, bold names, names with
    # their explanation in brackets, everything on one line, JSON, a table, "name - answer", lines with numbers only, and
    # a thinking part before the answer.
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
        foreach ($w in [regex]::Matches($before, '\S+')) {
            $key = Key-Of ($before.Substring($w.Index))
            if ($key) { [void]$starts.Add([pscustomobject]@{ key = $key; at = $offset + $w.Index; value = $c + 1 }); break }
        }
    }
    for ($s = 0; $s -lt $starts.Count; $s++) {
        $st = $starts[$s]
        $end = $t.Length
        if ($s + 1 -lt $starts.Count) { $end = $starts[$s + 1].at }
        $v = ''
        if ($end -gt $st.value) { $v = $t.Substring($st.value, $end - $st.value) }
        $v = (($v -replace '\s+', ' ').Trim()).Trim([char[]]' ,;|/.-*')
        if (-not $found.ContainsKey($st.key)) { $found[$st.key] = $v }
    }
    # Lines with numbers and no names: by their number, in the question's order.
    if ($found.Count -lt 8) {
        $numbered = @{}
        foreach ($line in $lines) { if ($line -match '^\s*(\d+)[.)]\s*(.+)$') { $numbered[[int]$Matches[1]] = $Matches[2] } }
        if ($numbered.Count -ge 15) {
            for ($n = 1; $n -le $FieldKeys.Count; $n++) {
                $key = $FieldKeys[$n - 1]
                if (-not $found.ContainsKey($key) -and $numbered.ContainsKey($n)) { $found[$key] = ([string]$numbered[$n]).Trim().Trim([char[]]' ,;|/.-*') }
            }
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
    if ($t -match '\b(elder|elderly|very old|ancient|aged)\b') { return 80.0 }
    if ($t -match '\bmiddle') { return 50.0 }
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

function Wing-Word([string] $v) {
    $t = Low $v
    if ($t -match 'butterfly|moth') { return 'butterflyWings' }
    if ($t -match '\bbat|leathery|membran') { return 'batWings' }
    if ($t -match 'insect|dragonfly|beetle|\bbee|wasp|\bfly\b|gossamer|translucent|veined|fairy') { return 'insectWings' }
    if ($t -match 'feather|bird|angel|swan|raven|crow|eagle') { return 'featheredWings' }
    if ($t -match 'dragon|scaled|scaly') { return 'dragonWings' }
    return 'otherWings'
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
            role      = Low (& $one 'role')
            headwear  = ((Low (& $one 'headwear')) -eq 'true')
            kinds     = @(& $list 'xenotypes') + @(& $list 'races')
            features  = @(& $list 'features' | ForEach-Object { $_.ToLowerInvariant() })
            marks     = @(& $list 'marks' | ForEach-Object { $_.ToLowerInvariant() })
            looks     = @(& $list 'looks' | ForEach-Object { $_.ToLowerInvariant() })
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
    if ($rec.features.Count -gt 0) { [void]$parts.Add('features: ' + ($rec.features -join ', ')) }
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
    $who = Short $seen.sex
    if ($seen.age -ne '') { $who = ($who + ', looks ' + (Short $seen.age)).Trim(' ', ',') }
    if ($who -ne '') { [void]$parts.Add($who) }
    foreach ($k in @('hands', 'weapons', 'armour', 'magic', 'headwear', 'tools', 'wings', 'horns', 'tail', 'eyemarks', 'unusual')) {
        if ((Says $seen.$k) -eq 'yes') {
            $label = $k
            switch ($k) { 'hands' { $label = 'holds' } 'magic' { $label = 'fire or magic' } 'tools' { $label = 'book or tools' } 'eyemarks' { $label = 'eye marks' } }
            [void]$parts.Add($label + ': ' + (Short $seen.$k))
        }
    }
    $e = Ears-Of $seen.ears
    if ($e.pointed -or $e.long) { [void]$parts.Add('ears: ' + (Short $seen.ears)) }
    if ((Glow-Of $seen.eyes) -eq 'yes') { [void]$parts.Add('eyes: ' + (Short $seen.eyes)) }
    $b = Body-Of $seen.body
    if ($b.scales -or $b.fur) { [void]$parts.Add('body: ' + (Short $seen.body)) }
    return ($parts -join '; ')
}

# ---------------- The comparison ----------------

function Add-Issue($list, [int] $level, [string] $what, [string] $fix) {
    [void]$list.Add([pscustomobject]@{ level = $level; what = $what; fix = $fix })
}

function Compare-Picture($rec, $seen, [bool] $cut) {
    # What doesn't fit between a picture's record and what the model saw, each with a level: 1 wrong (throw it out), 2
    # shows more than its row says (add a look or role to the row, or throw it out), 3 shows less (look at it).
    $issues = New-Object System.Collections.ArrayList
    $people = People-Of $seen.people
    if ($rec.kind -eq 'backdrop') {
        if ($people -ge 1) { Add-Issue $issues 1 'a person in the backdrop' 'throw it out (a backdrop shows no people)' }
        return $issues
    }
    $looks = $rec.looks
    $role = $rec.role
    $pinned = $rec.pawn -ne ''

    # Wrong: more than one person, the other sex, two age bands or more apart.
    if ($people -eq 0) { Add-Issue $issues 1 'no person seen' 'look at it; throw it out if there is no one in it' }
    elseif ($people -ge 2 -and -not ($people -eq 2 -and (Any-Look $looks $TwinLooks))) {
        $fix = 'throw it out'
        if ($cut) { $fix = 'throw it out (the cut-out holds all of them)' }
        Add-Issue $issues 1 ('{0} people in the picture' -f $people) $fix
    }
    $sex = Sex-Of $seen.sex
    if ($sex -ne '' -and ($rec.sex -eq 'male' -or $rec.sex -eq 'female') -and $sex -ne $rec.sex) {
        $drawn = 'man'; if ($sex -eq 'female') { $drawn = 'woman' }
        $asked = 'man'; if ($rec.sex -eq 'female') { $asked = 'woman' }
        Add-Issue $issues 1 ('drawn as a {0}; its row says {1}' -f $drawn, $asked) 'throw it out'
    }
    $years = Years-Of $seen.age
    $bandRec = Band-Of-Word $rec.age
    if ($years -ge 0 -and $bandRec -ge 0) {
        $bandSeen = Band-Of-Years $years
        if ([Math]::Abs($bandSeen - $bandRec) -ge 2) {
            Add-Issue $issues 1 ('looks about {0}, {1}; its row says {2}' -f [Math]::Round($years), $BandLabels[$bandSeen], $BandLabels[$bandRec]) 'throw it out'
        }
    }

    # Headwear: the portraits are bare-headed unless the record says headwear (a crown is a look of its own below).
    $hwText = $seen.headwear
    $hw = (Says $hwText) -eq 'yes'
    $crown = ((Low $hwText) -match $CrownWords) -or ((Low $seen.unusual) -match $CrownWords)
    if ($hw -and (Low $hwText) -match '\bhood' -and (Low $hwText) -match '\b(down|lowered|pushed back|thrown back|back)\b') { $hw = $false }
    if ($hw -and -not $rec.headwear -and -not ($crown -and -not $pinned)) {
        Add-Issue $issues 2 ('wears {0} on the head' -f (Short $hwText)) 'throw it out (the portraits are bare-headed; this one would go to people with nothing on their heads)'
    }

    # The body: features and marks shown that the row lacks, or that the row has and the picture doesn't show.
    $feat = $rec.features
    $ears = Ears-Of $seen.ears
    $rowEars = ($feat -contains 'pointedears') -or ($feat -contains 'longears')
    if (($ears.pointed -or $ears.long) -and -not $rowEars) {
        $word = 'pointedEars'; if ($ears.long -and -not $ears.pointed) { $word = 'longEars' }
        Add-Issue $issues 2 ('shows {0} ears; its row has no pointed or long ears' -f (Short $seen.ears)) ('add {0} to its row''s features if its kind has them, else throw it out' -f $word)
    }
    elseif ($ears.normal -and $rowEars) {
        Add-Issue $issues 3 'its row says pointed or long ears; the model saw normal ones' 'look at it: throw it out if they are missing'
    }
    $horns = Says $seen.horns
    if ($horns -eq 'yes' -and -not ($feat -contains 'horns')) { Add-Issue $issues 2 ('shows horns ({0}); its row has none' -f (Short $seen.horns)) 'add horns to its row''s features if its kind has them, else throw it out' }
    elseif ($horns -eq 'no' -and ($feat -contains 'horns')) { Add-Issue $issues 3 'its row says horns; the model saw none' 'look at it: throw it out if they are missing' }
    if ((Says $seen.tail) -eq 'yes' -and -not ($feat -contains 'tail')) { Add-Issue $issues 2 ('shows a tail ({0}); its row has none' -f (Short $seen.tail)) 'add tail to its row''s features if its kind has one, else throw it out' }
    $glow = Glow-Of $seen.eyes
    if ($glow -eq 'yes' -and -not ($feat -contains 'glowingeyes') -and -not (Any-Look $looks $MagicLooks)) { Add-Issue $issues 2 ('shows glowing eyes ({0}); its row has none' -f (Short $seen.eyes)) 'add glowingEyes to its row''s features if its kind has them, else throw it out' }
    elseif ($glow -eq 'no' -and ($feat -contains 'glowingeyes')) { Add-Issue $issues 3 'its row says glowing eyes; the model saw ordinary ones' 'look at it: throw it out if they are missing' }
    $body = Body-Of $seen.body
    foreach ($b in @('scales', 'fur')) {
        if ($body.$b -and -not ($feat -contains $b)) { Add-Issue $issues 2 ('shows {0} on the body; its row has none' -f $b) ('add {0} to its row''s features if its kind has them, else throw it out' -f $b) }
        elseif ($body.none -and ($feat -contains $b)) { Add-Issue $issues 3 ('its row says {0}; the model saw none' -f $b) 'look at it: throw it out if they are missing' }
    }
    $marks = Eyemarks-Of $seen.eyemarks
    $rowLost = ($rec.marks -contains 'losteye') -or ($rec.marks -contains 'eyepatch')
    $rowBionic = $rec.marks -contains 'bioniceye'
    if ($marks.lost -and -not $rowLost) { Add-Issue $issues 2 ('shows {0}; its row has no lost eye' -f (Short $seen.eyemarks)) 'add eyePatch (or lostEye) to its row''s marks, or throw it out' }
    if ($marks.bionic -and -not $rowBionic) { Add-Issue $issues 2 ('shows {0}; its row has no bionic eye' -f (Short $seen.eyemarks)) 'add bionicEye to its row''s marks, or throw it out' }
    if ($marks.none -and $rowLost) { Add-Issue $issues 3 'its row says a lost eye; the model saw both eyes' 'look at it: throw it out if the eye is there' }
    if ($marks.none -and $rowBionic) { Add-Issue $issues 3 'its row says a bionic eye; the model saw none' 'look at it: throw it out if it is missing' }

    # A picture made for one colonist goes to them only: its looks and role need no checking.
    if ($pinned) { return $issues }

    # Shows more than its row says: what goes only to pawns with a look or a role.
    $hands = Held $seen.hands
    $weaponText = ''
    if ((Says $seen.weapons) -eq 'yes') { $weaponText = $seen.weapons } elseif ((Low $hands) -match $WeaponWords) { $weaponText = $hands }
    $fighter = ($FightRoles -contains $role) -or (Any-Look $looks $FighterLooks)
    $magicLook = Any-Look $looks $MagicLooks
    if ($weaponText -ne '' -and -not $fighter -and -not $magicLook) {
        $t = Low $weaponText
        $guess = 'warrior'; $others = 'soldier, hunter or scout'
        if ($t -match '\b(guns?|rifles?|pistols?|revolvers?|muskets?|shotguns?|firearms?|carbines?|blasters?|grenades?)\b') { $guess = 'soldier'; $others = 'warrior, hunter or scout' }
        elseif ($t -match '\b(bows?|crossbows?|arrows?|spears?|javelins?|slings?)\b') { $guess = 'hunter'; $others = 'soldier, warrior or scout' }
        $now = 'it has no fighting role or look'; if ($role -ne '') { $now = 'its role is ' + $role }
        Add-Issue $issues 2 ('shows a weapon ({0}); {1}' -f (Short $weaponText), $now) ('add the role {0} to its row (or {1}), or throw it out' -f $guess, $others)
    }
    $armour = (Says $seen.armour) -eq 'yes'
    if ($armour -and $rec.dress -ne 'armour' -and -not ($ArmourRoles -contains $role) -and -not (Any-Look $looks $FighterLooks)) {
        $dress = $rec.dress; if ($dress -eq '') { $dress = 'not set' }
        Add-Issue $issues 2 ('wears armour ({0}); its row''s dress is {1} and it has no fighting role' -f (Short $seen.armour), $dress) 'set its row''s dress to armour (or add the role soldier or warrior), or throw it out'
    }
    $magicText = ''
    if ((Says $seen.magic) -eq 'yes') { $magicText = $seen.magic } elseif ((Low $hands) -match $MagicWords) { $magicText = $hands }
    if ($magicText -ne '' -and -not $magicLook) {
        $t = Low $magicText
        $guess = 'the look it shows, such as fireMage or iceMage'
        if ($t -match '\b(fire|flames?|burning|embers?|blaze|fireballs?|torch|torches)\b') { $guess = 'fireMage (or the look it shows)' }
        elseif ($t -match '\b(ice|frost|snow|cold|frozen)\b') { $guess = 'iceMage (or the look it shows)' }
        Add-Issue $issues 2 ('shows fire or magic ({0}); its row has no look for it' -f (Short $magicText)) ('add {0} to its row''s looks, or throw it out' -f $guess)
    }
    $wings = Says $seen.wings
    $wingLook = Any-Look $looks $WingLooks
    if ($wings -eq 'yes' -and -not $wingLook) {
        Add-Issue $issues 2 ('shows wings ({0}); its row''s looks have no wings' -f (Short $seen.wings)) ('add {0} to its row''s looks, or throw it out' -f (Wing-Word $seen.wings))
    }
    if ($crown -and $role -ne 'leader' -and -not (Any-Look $looks $NobleLooks)) {
        Add-Issue $issues 2 'wears a crown; its row is no leader and has no noble look' 'add the role leader to its row, or throw it out'
    }
    $toolText = ''
    if ((Says $seen.tools) -eq 'yes') { $toolText = $seen.tools }
    elseif ((Tool-Role $hands) -ne '') { $toolText = $hands }
    if ($toolText -ne '' -and $role -eq '' -and -not (Any-Look $looks $BookLooks) -and -not $magicLook) {
        $guess = Tool-Role ($toolText + ' ' + $hands)
        $fix = 'add the role of the work it shows to its row, or throw it out'
        if ($guess -ne '') { $fix = ('add the role {0} to its row (or the work it shows), or throw it out' -f $guess) }
        Add-Issue $issues 2 ('holds or shows {0}; its row has no role' -f (Short $toolText)) $fix
    }

    # Shows less than its row says: looks and roles the picture doesn't show.
    foreach ($w in $rec.lookWords) {
        $l = $w.ToLowerInvariant()
        if ($l -match $WingLooks -and $wings -eq 'no') {
            Add-Issue $issues 3 ('its row''s looks say {0}; the model saw no wings' -f $w) ('look at it: throw it out if it has none, or take {0} out of its row''s looks' -f $w)
        }
        elseif ($l -match $TwinLooks -and $people -eq 1) {
            Add-Issue $issues 3 ('its row''s looks say {0}; the model saw one person' -f $w) ('look at it: throw it out if it shows one, or take {0} out of its row''s looks' -f $w)
        }
        elseif ($l -match $MagicShownLooks -and $magicText -eq '' -and (Says $seen.magic) -eq 'no') {
            Add-Issue $issues 3 ('its row''s looks say {0}; the model saw no fire or magic' -f $w) ('look at it: the face or clothes may still show it; if not, take {0} out of its row''s looks or throw it out' -f $w)
        }
    }
    $nothingHeld = ($hands -eq '') -and ((Says $seen.hands) -ne '') -and ((Says $seen.tools) -ne 'yes')
    if (@('soldier', 'warrior', 'hunter') -contains $role -and $weaponText -eq '' -and (Says $seen.weapons) -eq 'no' -and (Says $seen.armour) -eq 'no') {
        Add-Issue $issues 3 ('its row''s role is {0}; the model saw no weapon or armour' -f $role) 'look at it: the clothes may still show the work; if not, take the role out of its row or throw it out'
    }
    elseif (($role -eq 'scholar' -or $WorkRoles -contains $role) -and $nothingHeld) {
        Add-Issue $issues 3 ('its row''s role is {0}; the model saw nothing of that work in the hands' -f $role) 'look at it: the clothes may still show the work; if not, take the role out of its row or throw it out'
    }
    return $issues
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

function Upload-Picture([string] $path) {
    # The picture goes to ComfyUI's input folder, always as rimfolk-check.png, so the folder doesn't fill up.
    $content = New-Object System.Net.Http.MultipartFormDataContent
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $file = [System.Net.Http.ByteArrayContent]::new($bytes)
    $file.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('image/png')
    $content.Add($file, 'image', 'rimfolk-check.png')
    $content.Add([System.Net.Http.StringContent]::new('input'), 'type')
    $content.Add([System.Net.Http.StringContent]::new('true'), 'overwrite')
    $r = Http-Send $script:Http 'POST' ($script:Url + '/upload/image') $content
    if (-not $r.ok) { throw ('ComfyUI would not take the picture ({0}): {1}' -f $r.status, (Short $r.text)) }
    $j = $r.text | ConvertFrom-Json
    if ((Has $j 'subfolder') -and $j.subfolder) { return ([string]$j.subfolder + '/' + [string]$j.name) }
    return [string]$j.name
}

$AskerPattern = '(?i)TextGenerate|QwenVL|Qwen.*VL|Florence|JoyCaption|Caption|Llava|VLM|Ollama|Gemma'
$TextOutPattern = '(?i)PreviewAny|Show.?Text|Display.?(Any|Text)|Preview.?Text|ShowAny'

function Fill-Workflow($wf, [string] $image) {
    # The picture into every Load Image box, the question where the prompt says %QUESTION% (else into the describing
    # box's prompt), thinking off and room for the whole answer.
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

function Ask-Comfy([string] $image, [bool] $first) {
    # Queues the question about the uploaded picture and waits for the answer: the texts of the workflow's text boxes.
    $wf = $script:WfText | ConvertFrom-Json
    [void](Fill-Workflow $wf $image)
    $body = @{ prompt = $wf; client_id = $script:Client } | ConvertTo-Json -Depth 64 -Compress
    $json = [System.Net.Http.StringContent]::new($body, [System.Text.Encoding]::UTF8, 'application/json')
    $r = Http-Send $script:Http 'POST' ($script:Url + '/prompt') $json
    if (-not $r.ok) { throw ('ComfyUI refused the question: {0}' -f (Error-Of $r.text)) }
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
            $q = Http-Send $script:Http 'GET' ($script:Url + '/queue') $null
            $state = 'gone'
            if ($q.ok) {
                $qj = $q.text | ConvertFrom-Json
                foreach ($item in @($qj.queue_running)) { if (@($item).Count -ge 2 -and [string]$item[1] -eq $id) { $state = 'running' } }
                foreach ($item in @($qj.queue_pending)) { if (@($item).Count -ge 2 -and [string]$item[1] -eq $id) { $state = 'waiting' } }
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

$CheckedColumns = @('picture', 'size', 'question', 'checked', 'model') + $FieldKeys + @('answer')

function Read-Checked {
    $map = @{}
    $rows = 0
    if (Test-Path -LiteralPath $checkedFile) {
        foreach ($r in @(Import-Csv -LiteralPath $checkedFile -Encoding UTF8)) {
            if ([string]::IsNullOrWhiteSpace($r.picture)) { continue }
            $map[$r.picture.ToLowerInvariant()] = $r
            $rows++
        }
    }
    return @{ map = $map; rows = $rows }
}

function Seen-From-Row($row) {
    $seen = @{}
    foreach ($k in $FieldKeys) { $seen[$k] = Field $row $k }
    return [pscustomobject]$seen
}

function Write-CsvFile([string] $path, [string[]] $header, $lines) {
    $all = New-Object System.Collections.ArrayList
    [void]$all.Add((Csv-Line $header))
    foreach ($l in $lines) { [void]$all.Add($l) }
    [System.IO.File]::WriteAllText($path, (($all -join "`r`n") + "`r`n"), (New-Object System.Text.UTF8Encoding($true)))
}

# ---------------- The run ----------------

try {
    Add-Type -AssemblyName System.Net.Http
    if (-not (Test-Path -LiteralPath $library)) { throw ('There is no library folder next to this script ({0}): it checks the pictures in library\, see SETUP-ComfyUI.md step 7.' -f $library) }
    $recordsFile = Join-Path $library 'records.xml'
    if (-not (Test-Path -LiteralPath $recordsFile)) { throw 'The library has no records yet: run Update-PortraitLibrary.ps1 (or Copy-ToLibrary.cmd) first, then this again.' }
    $records = Read-Records $recordsFile

    # The pictures: library\<batch>\*.png.
    $pictures = New-Object System.Collections.ArrayList
    foreach ($dir in @(Get-ChildItem -LiteralPath $library -Directory | Sort-Object Name)) {
        foreach ($f in @(Get-ChildItem -LiteralPath $dir.FullName -Filter '*.png' -File | Sort-Object Name)) {
            $stem = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
            $row = ''
            $m = [regex]::Match($stem, '^(?<name>.+?)_(?<style>style[^_]*)_')
            if ($m.Success) { $row = $m.Groups['name'].Value }
            [void]$pictures.Add([pscustomobject]@{
                key = ($dir.Name + '/' + $f.Name); show = ($dir.Name + '\' + $f.Name); tex = ($dir.Name + '/' + $stem).ToLowerInvariant()
                path = $f.FullName; size = [string]$f.Length; row = $row; cut = ($stem -match '_cut(_|$)')
            })
        }
    }
    if ($pictures.Count -eq 0) { throw ('No pictures in the library ({0}) yet.' -f $library) }

    $db = Read-Checked
    $todo = @($pictures | Where-Object {
        $old = $db.map[$_.key.ToLowerInvariant()]
        $Again -or $old -eq $null -or $old.size -ne $_.size -or $old.question -ne $QuestionVersion
    })
    $unread = New-Object System.Collections.ArrayList
    $asked = 0

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
        $fill = Fill-Workflow ($script:WfText | ConvertFrom-Json) 'test.png'
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
        Write-Host ''
        Write-Host ('Asking the model about {0} pictures ({1} were checked before). The first takes longer while the model loads.' -f $todo.Count, ($pictures.Count - $todo.Count))
        $n = 0
        foreach ($pic in $todo) {
            $n++
            $image = Upload-Picture $pic.path
            $texts = @(Ask-Comfy $image ($n -eq 1))
            if ($texts.Count -eq 0) {
                if ($asked -eq 0) { throw 'ComfyUI answered with no text. Check that the Generate Text box''s text is wired into a Preview as Text box, and export the workflow again (step 8).' }
                [void]$unread.Add([pscustomobject]@{ pic = $pic; text = '(no text)' })
                continue
            }
            $best = $null; $bestText = ''
            foreach ($tx in $texts) {
                $parsed = Read-Answer $tx
                if ($best -eq $null -or $parsed.Count -gt $best.Count) { $best = $parsed; $bestText = $tx }
            }
            if ($best.Count -lt 10) {
                [void]$unread.Add([pscustomobject]@{ pic = $pic; text = $bestText })
                Write-Host ('[{0}/{1}] {2}: the answer could not be read' -f $n, $todo.Count, $pic.show) -ForegroundColor Yellow
                continue
            }
            $values = New-Object System.Collections.ArrayList
            foreach ($v in @($pic.key, $pic.size, $QuestionVersion, (Get-Date -Format 'yyyy-MM-dd HH:mm'), $model)) { [void]$values.Add([string]$v) }
            foreach ($k in $FieldKeys) { $v = ''; if ($best.ContainsKey($k)) { $v = [string]$best[$k] }; [void]$values.Add($v) }
            [void]$values.Add(((($bestText -replace "`r", '') -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }) -join ' / '))
            [System.IO.File]::AppendAllText($checkedFile, (Csv-Line ([string[]]$values.ToArray())) + "`r`n", (New-Object System.Text.UTF8Encoding($false)))
            $asked++
            $seen = [pscustomobject]$best
            Write-Host ('[{0}/{1}] {2}: {3}' -f $n, $todo.Count, $pic.show, (Short (Seen-Says $seen)))
        }
        # Pictures asked about again (changed, or a newer question) keep only their newest answer.
        $db = Read-Checked
        if ($db.rows -gt $db.map.Count) {
            $keep = @($db.map.Keys | Sort-Object | ForEach-Object { $r = $db.map[$_]; Csv-Line ([string[]]@($CheckedColumns | ForEach-Object { Field $r $_ })) })
            Write-CsvFile $checkedFile $CheckedColumns $keep
        }
    }

    # The report: every checked picture in the library against its record now.
    $out = New-Object System.Collections.ArrayList
    $compared = 0
    foreach ($pic in $pictures) {
        $row = $db.map[$pic.key.ToLowerInvariant()]
        if ($row -eq $null -or $row.size -ne $pic.size) { continue }
        $seen = Seen-From-Row $row
        $rec = $records[$pic.tex]
        if ($rec -eq $null) {
            [void]$out.Add([pscustomobject]@{ level = 4; count = 1; picture = $pic.show; row = $pic.row; what = 'no record: not in records.xml'; seen = (Seen-Says $seen); says = ''; fix = 'run Update-PortraitLibrary.ps1 (if it still has no record, its batch list has no row for this name)' })
            continue
        }
        $compared++
        $issues = @(Compare-Picture $rec $seen $pic.cut)
        if ($issues.Count -eq 0) { continue }
        $issues = @($issues | Sort-Object level)
        $fixes = New-Object System.Collections.ArrayList
        foreach ($i in $issues) { if (-not $fixes.Contains($i.fix)) { [void]$fixes.Add($i.fix) } }
        [void]$out.Add([pscustomobject]@{
            level = $issues[0].level; count = $issues.Count; picture = $pic.show; row = $pic.row
            what = (($issues | ForEach-Object { $_.what }) -join ' | '); seen = (Seen-Says $seen); says = (Row-Says $rec); fix = ($fixes -join ' | ')
        })
    }
    foreach ($u in $unread) {
        [void]$out.Add([pscustomobject]@{ level = 4; count = 1; picture = $u.pic.show; row = $u.pic.row; what = 'the model''s answer could not be read; it is asked again next time'; seen = (Short $u.text); says = ''; fix = 'run this again; if it keeps happening, send check-report.csv to the lead' })
    }
    $LevelNames = @{ 1 = '1 wrong'; 2 = '2 shows more'; 3 = '3 shows less'; 4 = '4 not compared' }
    $sorted = @($out | Sort-Object @{ Expression = { $_.level } }, @{ Expression = { $_.count }; Descending = $true }, @{ Expression = { $_.picture } })
    $lines = @($sorted | ForEach-Object { Csv-Line ([string[]]@($_.picture, $_.row, $LevelNames[$_.level], $_.what, $_.seen, $_.says, $_.fix)) })
    Write-CsvFile $reportFile @('picture', 'row', 'check', 'what is wrong', 'what the model saw', 'its row says', 'suggested fix') $lines

    $count = @{ 1 = 0; 2 = 0; 3 = 0; 4 = 0 }
    foreach ($o in $out) { $count[$o.level]++ }
    Write-Host ''
    if ($todo.Count -gt 0) { Write-Host ('Asked about {0} new pictures; {1} were checked before.' -f $asked, ($pictures.Count - $todo.Count)) }
    else { Write-Host ('No new pictures; compared the {0} checked pictures with their records again.' -f $compared) }
    if ($out.Count -eq 0) {
        Write-Host ('All {0} checked pictures match their records.' -f $compared) -ForegroundColor Green
    }
    else {
        Write-Host ('{0} pictures to look at: {1} wrong, {2} show more than their rows say, {3} show less, {4} not compared.' -f $out.Count, $count[1], $count[2], $count[3], $count[4])
        Write-Host 'The worst first:'
        $colours = @{ 1 = 'Red'; 2 = 'Yellow'; 3 = 'Cyan'; 4 = 'Gray' }
        foreach ($o in @($sorted | Select-Object -First 15)) {
            Write-Host ('  {0,-14} {1}: {2}' -f $LevelNames[$o.level], $o.picture, $o.what) -ForegroundColor $colours[$o.level]
        }
        if ($sorted.Count -gt 15) { Write-Host ('  ... and {0} more.' -f ($sorted.Count - 15)) }
        Write-Host ''
        Write-Host ('The whole list, with what the model saw and a suggested fix for each: {0}' -f $reportFile)
    }
    Write-Host 'Nothing was deleted, moved or changed. To throw a picture out, delete it from the library; to fix a row, change it in'
    Write-Host 'batches\<batch>.csv; then run Update-PortraitLibrary.ps1, and this again to see the list without them.'
}
catch {
    Write-Host ''
    Write-Host ('Something went wrong: ' + $_.Exception.Message) -ForegroundColor Red
}
Write-Host ''
if (-not $NoPause) { Read-Host 'Press Enter to close' }
