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
#   wrong, what the model saw, what the row says, and a suggested fix, the worst first.
# - And library\corrections.csv: one line per change to one picture's record (age, sex, looks add:/remove:, role or
#   clear, marks add:/remove:, headwear, out), marked yes where it is safe or plainly right (an age two bands or more
#   off, a role for the weapon it holds, the wings look for its wings, out for two people in a cut-out) and ask where
#   you decide (fire or magic, anything taken away, a body its kind doesn't have). Update-PortraitLibrary.ps1 puts the
#   lines marked yes into the records; you change ask to yes or no. Lines already there are never changed here, the
#   yes lines count as done when comparing, and an issue marked no stays out of the report.
# - It never deletes, moves or renames a picture and never changes a batch list or the records: the name ties a
#   picture to its row, and the row's other pictures may be right.
# - A picture deleted for good in the game (listed in library\deleted.csv) is never looked at.
#
# Run it: keep ComfyUI open and double-click Check-Pictures.cmd (or right-click this file, "Run with PowerShell"). Then
# look at corrections.csv's ask lines, run Update-PortraitLibrary.ps1 and "Reload the library" in the game.
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
$correctionsFile = Join-Path $library 'corrections.csv'
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
        foreach ($w in [regex]::Matches($before, '[A-Za-z]\S*')) {
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
        # An answer is one line (on the line after its name, if the model put it there): the next item's number or bullet
        # ("Man\n3.") and any words after the form are not part of it.
        $v = $v.TrimStart()
        $nl = $v.IndexOf("`n")
        if ($nl -ge 0) { $v = $v.Substring(0, $nl) }
        $v = [regex]::Replace($v, '(?<=\S)\s+(?:\d+\s*[.)]|[-*+\u2022])\s*$', '')
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
    $r = @{ weapon = $false; armour = $false; magic = $false; tools = $false; wings = $false; horns = $false; tail = $false; crown = $false; glowEyes = $false; bionicEye = $false }
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
    if ((Years-Of $seen.age) -ge 0) { $who = ($who + ', looks ' + (Short $seen.age)).Trim(' ', ',') }
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

function Add-Issue($list, [int] $level, [string] $what, [string] $fix, [string] $field = '', [string] $value = '', [string] $apply = '') {
    # One thing that doesn't fit, and the correction for library\corrections.csv that would fix it (field, value, and
    # yes for a change that is safe or plainly right, ask for one you decide), or none.
    [void]$list.Add([pscustomobject]@{ level = $level; what = $what; fix = $fix; field = $field; value = $value; apply = $apply })
}

function Sure-Sex([string] $v) {
    # The model said the sex plainly ("woman", "a man"), with no doubt in it.
    return (Low $v) -match '^(a |an )?(man|woman|male|female)[.]?$'
}

function Compare-Picture($rec, $seen, [bool] $cut, [string] $asked) {
    # What doesn't fit between a picture's record and what the model saw, each with a level: 1 wrong (throw it out), 2
    # shows more than its row says (add a look or role to the row, or throw it out), 3 shows less (look at it).
    # $asked is the row's own prompt (its subject and pose; '' when the batch list isn't there): a thing the prompt asked
    # for belongs to the row's look or role ("a long rifle resting on one shoulder" of sniperClass, "small dark horns" of
    # succubus), so a row with a look or role may show it, and should.
    # Corrections marked yes only narrow who gets the picture or set plainly what it shows (an age band two or more
    # away, a role for the weapon it holds, the wings look for its wings, out for two people in a cut-out); anything that
    # would give it to more people, or can't be told from the answer, is marked ask.
    $issues = New-Object System.Collections.ArrayList
    $people = People-Of $seen.people
    if ($rec.kind -eq 'backdrop') {
        if ($people -ge 1) { Add-Issue $issues 1 'a person in the backdrop' 'throw it out (a backdrop shows no people)' 'out' '' 'ask' }
        return $issues
    }
    $looks = $rec.looks
    $role = $rec.role
    $pinned = $rec.pawn -ne ''
    $tie = ($looks.Count -gt 0) -or ($role -ne '')
    $ask = Asks-Of $asked
    $outAsk = 'look at it: if it is wrong, mark out yes in corrections.csv (or throw it out)'

    # Wrong: more than one person, the other sex, two age bands or more apart.
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
        if ($pinned) { Add-Issue $issues 1 $what 'its colonist is a {0}: look at it, and mark out yes in corrections.csv if it is wrong' 'out' '' 'ask' }
        else {
            $apply = 'ask'; if (Sure-Sex $seen.sex) { $apply = 'yes' }
            Add-Issue $issues 1 $what ('its record gets the sex {0}; or throw it out' -f $sex) 'sex' $sex $apply
        }
    }
    $years = Years-Of $seen.age
    $bandRec = Band-Of-Word $rec.age
    if ($years -ge 0 -and $bandRec -ge 0) {
        $bandSeen = Band-Of-Years $years
        if ([Math]::Abs($bandSeen - $bandRec) -ge 2) {
            Add-Issue $issues 1 ('looks about {0}, {1}; its row says {2}' -f [Math]::Round($years), $BandLabels[$bandSeen], $BandLabels[$bandRec]) ('its record gets the age {0}; or throw it out' -f $BandWords[$bandSeen]) 'age' $BandWords[$bandSeen] 'yes'
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
    # library\corrections.csv: picture, field, value, apply (yes, ask, no), why.
    $list = New-Object System.Collections.ArrayList
    if (-not (Test-Path -LiteralPath $path)) { return $list }
    foreach ($c in (Read-Csv-Any $path)) {
        $pic = (Field $c 'picture').Trim().Replace('/', '\')
        if ($pic -eq '') { continue }
        [void]$list.Add([pscustomobject]@{
            picture = $pic; field = (Field $c 'field').Trim().ToLowerInvariant(); value = (Field $c 'value').Trim()
            apply = (Field $c 'apply').Trim().ToLowerInvariant(); why = (Field $c 'why').Trim()
        })
    }
    return $list
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
    # The record with this picture's corrections marked yes, as Update-PortraitLibrary.ps1 writes it.
    $r = $rec.PSObject.Copy()
    $lw = New-Object System.Collections.ArrayList
    foreach ($w in $rec.lookWords) { [void]$lw.Add($w) }
    $mk = New-Object System.Collections.ArrayList
    foreach ($w in $rec.marks) { [void]$mk.Add($w) }
    foreach ($c in @($lines)) {
        if ($c.apply -ne 'yes') { continue }
        $v = [string]$c.value
        switch ($c.field) {
            'age' { if ($v -ne '') { $r.age = $v.ToLowerInvariant() } }
            'sex' { $sx = Sex-Word $v; if ($sx -ne '') { $r.sex = $sx } }
            'role' { if ($v -match '^(clear|none|-)$') { $r.role = '' } elseif ($v -ne '') { $r.role = $v.ToLowerInvariant() } }
            'headwear' { $r.headwear = ($v -match '^(true|yes|1)$') }
            'looks' { Change-List $lw $v $false }
            'marks' { Change-List $mk $v $true }
        }
    }
    $r.lookWords = @($lw)
    $r.looks = @($lw | ForEach-Object { Look-Words $_ })
    $r.marks = @($mk)
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

# ---------------- The run ----------------

try {
    Add-Type -AssemblyName System.Net.Http
    if (-not (Test-Path -LiteralPath $library)) { throw ('There is no library folder next to this script ({0}): it checks the pictures in library\, see SETUP-ComfyUI.md step 7.' -f $library) }
    $recordsFile = Join-Path $library 'records.xml'
    if (-not (Test-Path -LiteralPath $recordsFile)) { throw 'The library has no records yet: run Update-PortraitLibrary.ps1 (or Copy-ToLibrary.cmd) first, then this again.' }
    $records = Read-Records $recordsFile

    # The pictures: library\<batch>\*.png. A backdrop's depth map (<picture>_depth.png) and a person's expressions
    # (<picture>_v-blink.png ...) belong to their picture, which is checked; they are not looked at.
    $pictures = New-Object System.Collections.ArrayList
    $skippedBackdrops = 0
    # Pictures deleted for good in the game (deleted.csv) are not looked at, even where their file is still here.
    $deleted = Read-Deleted $library
    $skippedDeleted = 0
    foreach ($dir in @(Get-ChildItem -LiteralPath $library -Directory | Sort-Object Name)) {
        foreach ($f in @(Get-ChildItem -LiteralPath $dir.FullName -Filter '*.png' -File | Sort-Object Name)) {
            $stem = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
            if ($stem -match '_(depth|v-[A-Za-z]+)$') { continue }
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
    $todo = @($pictures | Where-Object {
        $old = $db.map[$_.key.ToLowerInvariant()]
        $Again -or $old -eq $null -or $old.size -ne $_.size -or $old.question -ne $QuestionVersion
    })
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
        Write-Host ('Asking the model about {0} ({1} checked before). The first takes longer while the model loads.' -f (Count-Of $todo.Count 'picture' 'pictures'), ($pictures.Count - $todo.Count))
        $n = 0
        foreach ($pic in $todo) {
            $n++
            $image = Upload-Picture $pic.path
            $texts = @(Ask-Comfy $image ($n -eq 1))
            if ($texts.Count -eq 0) {
                if ($described -eq 0) { throw 'ComfyUI answered with no text. Check that the Generate Text box''s text is wired into a Preview as Text box, and export the workflow again (step 8).' }
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
            $described++
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

    # The rows' own prompts (subject and pose, from batches\<batch>.csv), which say what a look or role asked to show.
    $askedOf = @{}
    foreach ($b in @($pictures | ForEach-Object { $_.batch } | Select-Object -Unique)) {
        $csv = Join-Path (Join-Path $Folder 'batches') ($b + '.csv')
        if (-not (Test-Path -LiteralPath $csv)) { continue }
        foreach ($r in @(Import-Csv -LiteralPath $csv -Encoding UTF8)) {
            if ([string]::IsNullOrWhiteSpace($r.name)) { continue }
            $askedOf[$b + '|' + ($r.name -replace '[^A-Za-z0-9_\-]', '_')] = ((Field $r 'subject') + ' ' + (Field $r 'pose')).Trim()
        }
    }

    # The corrections (library\corrections.csv, kept from run to run; you may change apply there). Lines marked yes are
    # applied here as Update-PortraitLibrary.ps1 applies them, so a corrected picture leaves the report; a line marked
    # no keeps its issue out of the report (you decided to keep it so); ask waits for you. New lines are added, and the
    # lines already there are never changed.
    $corrections = @(Read-Corrections $correctionsFile)
    $byPic = @{}
    foreach ($c in $corrections) {
        $k = $c.picture.ToLowerInvariant()
        if (-not $byPic.ContainsKey($k)) { $byPic[$k] = New-Object System.Collections.ArrayList }
        [void]$byPic[$k].Add($c)
    }
    $newLines = New-Object System.Collections.ArrayList
    $leftOut = 0

    # The report: every checked picture in the library against its record now.
    $out = New-Object System.Collections.ArrayList
    $compared = 0
    foreach ($pic in $pictures) {
        $row = $db.map[$pic.key.ToLowerInvariant()]
        if ($row -eq $null -or $row.size -ne $pic.size) { continue }
        $seen = Seen-From-Row $row
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
        $found = @(Compare-Picture $rec $seen $pic.cut $asked)
        $issues = @(foreach ($lv in 1..4) { $found | Where-Object { $_.level -eq $lv } })
        $kept = New-Object System.Collections.ArrayList
        $notes = New-Object System.Collections.ArrayList
        foreach ($i in $issues) {
            if ($i.field -eq '') { [void]$kept.Add($i); continue }
            $same = $null
            $key = Why-Key $i.what
            foreach ($c in $mine) { if ($c.field -eq $i.field -and (Why-Key $c.why) -eq $key) { $same = $c; break } }
            if ($same -ne $null) {
                if ($same.apply -eq 'no') { continue }
                [void]$notes.Add((Corr-Text $same '')); [void]$kept.Add($i); continue
            }
            $other = $null
            if (@('age', 'sex', 'role', 'headwear', 'out') -contains $i.field) {
                foreach ($c in $mine) { if ($c.field -eq $i.field -and $c.apply -ne 'no') { $other = $c; break } }
            }
            if ($other -ne $null) { [void]$notes.Add((Corr-Text $other 'already there')); [void]$kept.Add($i); continue }
            $c = [pscustomobject]@{ picture = $pic.show; field = $i.field; value = $i.value; apply = $i.apply; why = $i.what.Trim() }
            [void]$mine.Add($c); [void]$newLines.Add($c)
            [void]$notes.Add((Corr-Text $c 'new')); [void]$kept.Add($i)
        }
        if ($kept.Count -eq 0) { continue }
        $fixes = New-Object System.Collections.ArrayList
        foreach ($i in $kept) { if (-not $fixes.Contains($i.fix)) { [void]$fixes.Add($i.fix) } }
        [void]$out.Add([pscustomobject]@{
            level = $kept[0].level; count = $kept.Count; picture = $pic.show; row = $pic.row
            what = (($kept | ForEach-Object { $_.what }) -join ' | '); seen = (Seen-Says $seen); says = (Row-Says $rec); fix = ($fixes -join ' | '); corr = ($notes -join ' | ')
        })
    }
    foreach ($u in $unread) {
        [void]$out.Add([pscustomobject]@{ level = 4; count = 1; picture = $u.pic.show; row = $u.pic.row; what = 'the model''s answer could not be read; it is asked again next time'; seen = (Short $u.text); says = ''; fix = 'run this again; if it keeps happening, send check-report.csv to the lead'; corr = '' })
    }

    # corrections.csv first (the lines already there as they were, then the new ones), then the report.
    if ($newLines.Count -gt 0) {
        $all = @($corrections) + @($newLines)
        Write-CsvFile $correctionsFile @('picture', 'field', 'value', 'apply', 'why') @($all | ForEach-Object { Csv-Line ([string[]]@($_.picture, $_.field, $_.value, $_.apply, $_.why)) })
    }
    $LevelNames = @{ 1 = '1 wrong'; 2 = '2 shows more'; 3 = '3 shows less'; 4 = '4 not compared' }
    $sorted = @($out | Sort-Object @{ Expression = { $_.level } }, @{ Expression = { $_.count }; Descending = $true }, @{ Expression = { $_.picture } })
    $lines = @($sorted | ForEach-Object { Csv-Line ([string[]]@($_.picture, $_.row, $LevelNames[$_.level], $_.what, $_.seen, $_.says, $_.fix, $_.corr)) })
    Write-CsvFile $reportFile @('picture', 'row', 'check', 'what is wrong', 'what the model saw', 'its row says', 'suggested fix', 'in corrections.csv') $lines

    $count = @{ 1 = 0; 2 = 0; 3 = 0; 4 = 0 }
    foreach ($o in $out) { $count[$o.level]++ }
    Write-Host ''
    if ($todo.Count -gt 0) {
        $line = 'The model described {0} of {1}.' -f $described, (Count-Of $todo.Count 'picture' 'pictures')
        if ($unread.Count -eq 1) { $line += ' 1 answer could not be read; that picture is asked about again next time.' }
        elseif ($unread.Count -gt 1) { $line += (' {0} answers could not be read; those pictures are asked about again next time.' -f $unread.Count) }
        Write-Host $line
    }
    else { Write-Host ('No new pictures; compared the {0} with the records again.' -f (Count-Of $compared 'checked picture' 'checked pictures')) }
    if ($out.Count -eq 0) {
        Write-Host ('All {0} match their records.' -f (Count-Of $compared 'checked picture' 'checked pictures')) -ForegroundColor Green
    }
    else {
        $parts = New-Object System.Collections.ArrayList
        if ($count[1] -gt 0) { [void]$parts.Add(('{0} wrong' -f $count[1])) }
        if ($count[2] -eq 1) { [void]$parts.Add('1 shows more than its row says') }
        elseif ($count[2] -gt 1) { [void]$parts.Add(('{0} show more than their rows say' -f $count[2])) }
        if ($count[3] -gt 0) { [void]$parts.Add(('{0} less' -f (Count-Of $count[3] 'shows' 'show'))) }
        if ($count[4] -gt 0) { [void]$parts.Add(('{0} not compared' -f $count[4])) }
        Write-Host ('{0} to look at: {1}.' -f (Count-Of $out.Count 'picture' 'pictures'), ($parts -join ', '))
        Write-Host 'The worst first:'
        $colours = @{ 1 = 'Red'; 2 = 'Yellow'; 3 = 'Cyan'; 4 = 'Gray' }
        foreach ($o in @($sorted | Select-Object -First 15)) {
            Write-Host ('  {0,-14} {1}: {2}' -f $LevelNames[$o.level], $o.picture, $o.what) -ForegroundColor $colours[$o.level]
        }
        if ($sorted.Count -gt 15) { Write-Host ('  ... and {0} more.' -f ($sorted.Count - 15)) }
        Write-Host ''
        Write-Host ('The whole list, with what the model saw and a suggested fix for each: {0}' -f $reportFile)
    }
    $yesNew = @($newLines | Where-Object { $_.apply -eq 'yes' }).Count
    $askAll = @(@($corrections) + @($newLines) | Where-Object { $_.apply -eq 'ask' }).Count
    if ($newLines.Count -gt 0) { Write-Host ('{0} written to {1}: {2} marked yes, {3} marked ask.' -f (Count-Of $newLines.Count 'new correction' 'new corrections'), $correctionsFile, $yesNew, ($newLines.Count - $yesNew)) }
    if ($leftOut -gt 0) { Write-Host ('{0} left out by corrections.csv (out, yes).' -f (Count-Of $leftOut 'picture is' 'pictures are')) }
    if ($askAll -gt 0) { Write-Host ('{0} for you in corrections.csv: open it (in Excel) and change ask to yes or no.' -f (Count-Of $askAll 'correction waits' 'corrections wait')) -ForegroundColor Yellow }
    Write-Host 'Run Update-PortraitLibrary.ps1 (Copy-ToLibrary.cmd runs it too) to put the corrections marked yes into the records,'
    Write-Host 'then "Reload the library" in the game. No picture was deleted, moved or renamed, and no batch list was changed.'
}
catch {
    Write-Host ''
    Write-Host ('Something went wrong: ' + $_.Exception.Message) -ForegroundColor Red
}
Write-Host ''
if (-not $NoPause) { Read-Host 'Press Enter to close' }
