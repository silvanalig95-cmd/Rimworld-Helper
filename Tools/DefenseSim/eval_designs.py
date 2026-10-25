import sys, math; sys.path.insert(0, sys.argv[1])
from kbsim import Layout
def z(rows, r): return len(rows)-1-r
def analyse(name, rows, tunnel_rc, door_rc, sym='f', show_map=True, box_rows=None):
    for r in rows: assert len(r)==len(rows[0]), (name, r)
    L=Layout(rows); sh=L.cells(sym)
    print(f"\n=== {name}: {len(sh)} shooters")
    bad=[]
    for (c,r) in tunnel_rc:
        cell=(c,z(rows,r)); vis=[s for s in sh if L.can_hit(cell,s)]
        if vis:
            cv=[L.cover(cell,s) for s in vis]; d=[math.hypot(s[0]-cell[0],s[1]-cell[1]) for s in vis]
            bad.append(f"({c},{r}) sees {len(vis)} [cover {min(cv)*100:.0f}-{max(cv)*100:.0f}%, nearest {min(d):.1f}]")
    print("   tunnel/door cells that can shoot at a shooter:", "; ".join(bad) if bad else "none")
    door=(door_rc[0],z(rows,door_rc[1]))
    cv=[L.cover(s,door) for s in sh]; d=[math.hypot(s[0]-door[0],s[1]-door[1]) for s in sh]
    print(f"   shooters vs raider in doorway: their cover {min(cv)*100:.0f}-{max(cv)*100:.0f}%, distance {min(d):.1f}-{max(d):.1f} (grenade 12.9)")
    if show_map and box_rows:
        print("   shooters able to hit each cell:")
        for r in range(*box_rows):
            line=""
            for x in range(len(rows[0])):
                ch=rows[r][x]
                if ch=='W': line+='#'; continue
                if ch=='_': line+=' '; continue
                if ch in 'fUuS': line+=ch; continue
                line+=str(sum(1 for s in sh if L.can_hit(s,(x,z(rows,r)))))
            print("     "+line)
    return L
# ---------- K2 compact killbox (tribal / early), 15 wide
K2=["WWWW.WWWWWWWWWW",
    "W__W....W_____W",
    "W__WWWW.W_____W",
    "W_____W.W_____W",
    "W_____W.W_____W",
    "WWWWWWW.WWWWWWW",
    "W.............W","W.............W","W.............W","W.............W",
    "W.............W","W.............W","W.............W","W.............W",
    "W.SSSSS.SSSSS.W",
    "W.f.f.....f.f.W",
    "W.............W"]
analyse("K2 compact (straight 4, plain sandbags, shooters at offsets 3 and 5, 10-11 tiles)", K2,
        [(4,0),(4,1),(5,1),(6,1),(7,1),(7,2),(7,3),(7,4),(7,5)], (7,5), box_rows=(5,17))
# ---------- K4 big-raid box: 21 wide, long box, two shooter groups in pockets, turret bait on axis
BOX="W...................W"
K4=["WWWWWW.WWWWWWWWWWWWWW",
    "W____W.WWWWWWWWW____W",
    "W____W.........W____W",
    "W____WWWWWWWWW.W____W",
    "W_____________W.W___W",   # fixed below
   ]
K4=["WWWWWW.WWWWWWWWWWWWWW",
    "W____W.WWWWWWWWW____W",
    "W____W.........W____W",
    "W____WWWWWWWWW.W____W",
    "W____________W.W____W",
    "W____________W.W____W",
    "W____________W.W____W",
    "W____________W.W____W",
    "WWWWWWWWWWWWWW.WWWWWW"] 
BOX="W...................W"
K4 += [BOX]*20 + ["WSWSWSWS.....SWSWSWSW".replace('WSWSWSWS','WSWSWSWS',1),
                  "WSfSfSfS.....SfSfSfSW", BOX]
# fix line rows: want cols1..19 = S W S W S W S . . . . . S W S W S W S
K4[-3] = "W" + "SWSWSWS" + "....." + "SWSWSWS" + "W"
K4[-2] = "W" + "SfSfSfS" + "....." + "SfSfSfS" + "W"
# mini-turret bait on the axis, 8 and 12 tiles in front of the line
r_line = len(K4)-3
for rr in (r_line-8, r_line-12):
    K4[rr] = K4[rr][:10] + "U" + K4[rr][11:]
analyse("K4 large-raid box (straight 6, 22 tiles, 6 shooters in pockets at offsets 4-8, axis turrets)", K4,
        [(6,0),(6,1),(6,2),(9,2),(10,2),(10,3),(10,5),(10,7),(10,8)], (10,8), box_rows=(8,len(K4)))
L=Layout(K4)
sh=L.cells('f'); tur=L.cells('U')
print("   turret-to-nearest-shooter distance:", [round(min(math.hypot(t[0]-s[0],t[1]-s[1]) for s in sh),1) for t in tur])
# ---------- K3 mountain tunnel killbox: rock = W; slowing sandbags in outer tunnel; chamber 13 wide
BOX3="W.............W"
K3=["WWWWW.WWWWWWWWW",
    "WWWWWSWWWWWWWWW",
    "WWWWW.WWWWWWWWW",
    "WWWWWSWWWWWWWWW",
    "WWWWW.S.WWWWWWW",
    "WWWWWWWW.WWWWWW",   # placeholder
   ]
K3=["WWWWW.WWWWWWWWW",
    "WWWWWSWWWWWWWWW",
    "WWWWW.WWWWWWWWW",
    "WWWWWSWWWWWWWWW",
    "WWWWW..WWWWWWWW",
    "WWWWWW.S.WWWWWW",
    "WWWWWWWW.WWWWWW",
    "WWWWWWWW.WWWWWW",
    "WWWWWWW.WWWWWWW",
   ]
# simpler explicit K3: tunnel down col 5 (sandbags every other cell), east along row 4 to col 7, straight down col 7 rows 5-8 (doorway row 8)
K3=["WWWWW.WWWWWWWWW",
    "WWWWWSWWWWWWWWW",
    "WWWWW.WWWWWWWWW",
    "WWWWWSWWWWWWWWW",
    "WWWWW...WWWWWWW",
    "WWWWWWW.WWWWWWW",
    "WWWWWWW.WWWWWWW",
    "WWWWWWW.WWWWWWW",
    "WWWWWWW.WWWWWWW"] + [BOX3]*13 + ["W.SWSWS.SWSWS.W","W.SfSfS.SfSfS.W",BOX3]
analyse("K3 mountain tunnel (sandbag-slowed outer tunnel, straight 4, chamber 13x13, 4 shooters offsets 3,5)", K3,
        [(5,0),(5,2),(5,4),(6,4),(7,4),(7,5),(7,6),(7,7),(7,8)], (7,8), box_rows=(8,len(K3)))
# ---------- G1 baffled gate (perimeter gate that is a mini killbox): 2-thick wall, L passage, yard, line
G1=[".................",
    "WWWWWW.WWWWWWWWWW",
    "WWWWWW....WWWWWWW",
    "WWWWWWWWW.WWWWWWW",
    "_______WW.WW_____",
    "_______WW.WW_____",
    "_______W.....W___"]
G1=[".................",
    "WWWWWW.WWWWWWWWWW",   # wall row 1 with gate cell col 6 (held open)
    "WWWWWW...WWWWWWWW",   # wall row 2: passage turns east cols 6-8
    "WWWWWWWW.WWWWWWWW",   # straight col 8 rows 3-6
    "_______W.W_______",
    "_______W.W_______",
    "_______W.W_______",
    "_W.............W_",
    "_W.............W_","_W.............W_","_W.............W_","_W.............W_",
    "_W.............W_","_W.............W_","_W.............W_","_W.............W_",
    "_W.SSSS...SSSS.W_",
    "_W.f.f.....f.f.W_",
    "_W.............W_"]
analyse("G1 baffled gate (L passage, 3-cell straight, yard 13x9, shooters at offsets 3 and 5, 11 tiles)", G1,
        [(6,0),(5,0),(7,0),(6,1),(6,2),(7,2),(8,2),(8,3),(8,4),(8,5),(8,6)], (8,6), box_rows=(6,len(G1)))
# ---------- P1 palisade killing yard: outer gate offset from inner gate; embrasures in yard walls face inward
P1=["...............",
    "WWWWWWWGWWWWWWW",   # outer palisade, gate col 7 (held open)
    "W.....WGW.....W",   # hmm
   ]
BOX3="W...............W"
K3=["WWWWWW.WWWWWWWWWW",
    "WWWWWWSWWWWWWWWWW",
    "WWWWWW.WWWWWWWWWW",
    "WWWWWWSWWWWWWWWWW",
    "WWWWWW...WWWWWWWW",
    "WWWWWWWW.WWWWWWWW",
    "WWWWWWWW.WWWWWWWW",
    "WWWWWWWW.WWWWWWWW",
    "WWWWWWWW.WWWWWWWW"] + [BOX3]*13 + ["W.SWSWS...SWSWS.W","W.SfSfS...SfSfS.W",BOX3]
analyse("K3b mountain tunnel, chamber 15 wide, shooters at offsets 3 and 5", K3,
        [(6,0),(6,2),(6,4),(7,4),(8,4),(8,5),(8,6),(8,7),(8,8)], (8,8), box_rows=(8,len(K3)))
# P1 palisade killing yard (E = embrasure 0.75 see-over, impassable), outer gate (G open) offset, inner gate in a corner
P1=["...............",
    "WWWW.WWWWWWWWWW",   # outer palisade; gate col 4 held open (drawn '.')
    "W__W....W_____W",
    "W__WWWW.W_____W",
    "W_____W.W_____W",
    "WWWWWWW.WWWWWWW",   # yard entry
    "W.............W","W.............W","W.............W","W.............W",
    "W.............W","W.............W","W.............W",
    "WWEWEWWWWWEWEW.",   # back wall: embrasures for archers, inner gate at col 14 (open)
    "_.f.f.....f.f._"]
analyse("P1 palisade killing yard (archers behind embrasures at offsets 3,5; 8-9 tiles)", P1,
        [(4,0),(3,0),(5,0),(4,1),(4,2),(7,2),(7,3),(7,4),(7,5)], (7,5), box_rows=(5,len(P1)))
K3c = K3[:5] + ["WWWWWWWW.WWWWWWWW"] + K3[5:]
analyse("K3c mountain tunnel, straight 5 (incl. doorway), shooters at offsets 3 and 5", K3c,
        [(6,0),(6,2),(6,4),(7,4),(8,4),(8,5),(8,6),(8,7),(8,8),(8,9)], (8,9), show_map=False)
P1b=["...............",
    "WWWW.WWWWWWWWWW",
    "W__W....W_____W",
    "W__WWWW.W_____W",
    "W_____W.W_____W",
    "W_____W.W_____W",
    "WWWWWWW.WWWWWWW",
    "W.............W","W.............W","W.............W","W.............W",
    "W.............W","W.............W","W.............W",
    "WEEEEEWWWEEEEE.",
    "_.f.f.....f.f._"]
analyse("P1b palisade killing yard, straight 4, continuous embrasure runs, archers offsets 3,5", P1b,
        [(4,0),(3,0),(5,0),(4,1),(4,2),(5,2),(6,2),(7,2),(7,3),(7,4),(7,5),(7,6)], (7,6), box_rows=(6,len(P1b)))
