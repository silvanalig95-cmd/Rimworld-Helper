# One release, the text side: run from the repo root after the merge, before the checks.
# python3 Tools/release.py VERSION "DATE" "TITLE" "DESIGN heading substring" points.txt
# It turns the first "- Not released: ..." entry of docs/HANDOFF.md (it must sit above the newest "- 0." line) into the
# version, marks the DESIGN.md heading "(...; released VERSION)" (its "not released yet" replaced), adds the What's
# new round (2 to 8 points, one per line, no em dashes or exclamation marks, a title not used before), sets About.xml modVersion and rewrites CHANGELOG.md (then checks it). "SKIP" skips DESIGN.md.
import sys, re, subprocess, html
ver, date, title, heading, pts = sys.argv[1:6]
points = [l.strip() for l in open(pts) if l.strip()]
assert 2 <= len(points) <= 8, len(points)
for t in [title] + points:
    assert '—' not in t and '!' not in t, t
# HANDOFF
p = 'docs/HANDOFF.md'; s = open(p).read()
mark = "- Not released: no version, no What's new round, About.xml and CHANGELOG untouched. "
i = s.find(mark); assert i >= 0 and i < s.find('\n- 0.'), 'no unreleased entry at top'
s = s[:i] + f"- {ver}, " + s[i+len(mark):]
open(p, 'w').write(s)
# DESIGN
p = 'DESIGN.md'; s = open(p).read().split('\n')
if heading != 'SKIP':
    hits = [k for k, l in enumerate(s) if l.startswith('#') and heading in l]
    assert len(hits) == 1, hits
    l = s[hits[0]]
    if '; not released yet)' in l: l = l.replace('; not released yet)', f'; released {ver})')
    elif '(not released yet)' in l: l = l.replace('(not released yet)', f'(released {ver})')
    elif l.endswith(')'): l = l[:-1] + f'; released {ver})'
    else: l = l + f' (released {ver})'
    s[hits[0]] = l
    open(p, 'w').write('\n'.join(s))
# What's new
p = 'Defs/AlmanacArticleDefs/Almanac_WhatsNew.xml'; s = open(p).read()
assert f"<title>{html.escape(title, quote=False)}</title>" not in s, 'title used'
rnd = (f"      <li>\n        <version>{ver}</version>\n        <date>{date}</date>\n        <title>{html.escape(title, quote=False)}</title>\n        <points>\n"
       + ''.join(f"          <li>{html.escape(x, quote=False)}</li>\n" for x in points)
       + "        </points>\n      </li>\n\n")
k = s.index('<rounds>\n\n') + len('<rounds>\n\n')
s = s[:k] + rnd + s[k:]
open(p, 'w').write(s)
# About
p = 'About/About.xml'; s = open(p).read()
s = re.sub(r'<modVersion>[^<]*</modVersion>', f'<modVersion>{ver}</modVersion>', s, count=1)
open(p, 'w').write(s)
subprocess.run(['python3', 'Tools/changelog.py'], check=True)
subprocess.run(['python3', 'Tools/changelog.py', '--check'], check=True)
print('release text done', ver)
