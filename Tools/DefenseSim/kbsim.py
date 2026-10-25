"""Mirror of RimWorld 1.6 GenSight.LineOfSight, ShootLeanUtility and CoverUtility (decompiled
Assembly-CSharp), to check killbox geometry. Layout rows: first row = north = threat side.
Symbols: W wall, E embrasure(0.75, see-over, impassable), S sandbag/barricade, F fence, C chunk,
u/U mini-turret footprint (0.40, see-over), G gate (held open = passable, no cover),
f firing position, T trap, . open, _ untouched (treated as open ground for sight)."""
import math
COVER = {'W':0.75,'E':0.75,'S':0.55,'F':0.25,'C':0.50,'u':0.40,'U':0.40}
BLOCKS_SIGHT = set('W')
ADJ = [(0,1),(1,0),(0,-1),(-1,0),(1,-1),(1,1),(-1,1),(-1,-1)]  # N E S W SE NE NW SW
class Layout:
    def __init__(self, rows):
        self.rows = rows; self.h = len(rows); self.w = max(len(r) for r in rows)
    def ch(self, c):
        x,z = c; r = self.h-1-z
        if r<0 or r>=self.h or x<0 or x>=len(self.rows[r]): return '.'
        return self.rows[r][x]
    def see_over(self, c): return self.ch(c) not in BLOCKS_SIGHT
    def cells(self, sym):
        out=[]
        for r,row in enumerate(self.rows):
            for x,ch in enumerate(row):
                if ch==sym: out.append((x,self.h-1-r))
        return out
    def los(self, start, end, skip_first=True):
        x0,z0=start; x1,z1=end
        flag = (x0<x1) if x0!=x1 else (z0<z1)
        dx=abs(x1-x0); dz=abs(z1-z0); n=1+dx+dz
        sx=1 if x1>x0 else -1; sz=1 if z1>z0 else -1
        dx*=4; dz*=4; err=dx//2-dz//2; x,z=x0,z0
        while n>1:
            c=(x,z)
            if not (skip_first and c==start):
                if not self.see_over(c): return False
            if err>0 or (err==0 and flag): x+=sx; err-=dz
            else: z+=sz; err+=dx
            n-=1
        return True
    def lean_sources(self, loc, target):
        dx=target[0]-loc[0]; dz=target[1]-loc[1]
        ang = math.degrees(math.atan2(dx,dz))%360 if (dx or dz) else 0
        f = ang>270 or ang<90; f2 = 90<ang<270; f3 = ang>180; f4 = ang<180
        b=[not self.see_over((loc[0]+a,loc[1]+c)) for a,c in ADJ]
        out=[]
        E=(loc[0]+1,loc[1]); Wc=(loc[0]-1,loc[1]); S=(loc[0],loc[1]-1); N=(loc[0],loc[1]+1)
        if not b[1] and ((b[0] and not b[5] and f) or (b[2] and not b[4] and f2)): out.append(E)
        if not b[3] and ((b[0] and not b[6] and f) or (b[2] and not b[7] and f2)): out.append(Wc)
        if not b[2] and ((b[3] and not b[7] and f3) or (b[1] and not b[4] and f4)): out.append(S)
        if not b[0] and ((b[3] and not b[6] and f3) or (b[1] and not b[5] and f4)): out.append(N)
        if self.see_over(loc): out.append(loc)
        for j,ok in ((0,f),(1,f4),(2,f2),(3,f3)):
            if b[j] or not ok: continue
            c=(loc[0]+ADJ[j][0],loc[1]+ADJ[j][1])
            if self.ch(c) in COVER: out.append(c)
        return out
    def can_hit(self, shooter, target):
        """Pawn shooter -> pawn target (Verb.TryFindShootLineFromTo, ignoring range)."""
        dests = self.lean_sources(target, shooter)
        for src in [shooter]+self.lean_sources(shooter, target):
            for d in dests:
                if self.los(src, d, True): return True
        return False
    def cover(self, target, shooter):
        tx,tz=target; sx,sz=shooter; total=0.0
        for a,c in ADJ:
            cc=(tx+a,tz+c)
            if cc==shooter: continue
            ch=self.ch(cc)
            if ch not in COVER: continue
            d=abs((math.degrees(math.atan2(a,c))-math.degrees(math.atan2(sx-tx,sz-tz))+180)%360-180)
            if a!=0 and c!=0: d*=1.75
            bl=COVER[ch]
            if d<15: f=1
            elif d<27: f=0.8
            elif d<40: f=0.6
            elif d<52: f=0.4
            elif d<65: f=0.2
            else: continue
            bl*=f
            dist=math.hypot(sx-cc[0],sz-cc[1])
            if dist<1.9: bl*=0.3333
            elif dist<2.9: bl*=0.66666
            total+=(1-total)*bl
        return total
def report(name, rows, raider_cells, shooter_sym='f', grenade=12.9):
    L=Layout(rows); shooters=L.cells(shooter_sym)
    print(f"\n### {name}  (shooters: {len(shooters)})")
    for rc in raider_cells:
        vis=[]; 
        for s in shooters:
            if L.can_hit(rc,s):
                d=math.hypot(s[0]-rc[0],s[1]-rc[1])
                vis.append((s, L.cover(rc,s), d))
        if not vis:
            print(f"  raider at {rc}: sees no shooter"); continue
        covs=[v[1] for v in vis]; ds=[v[2] for v in vis]
        ingren=sum(1 for v in vis if v[2]<=grenade)
        print(f"  raider at {rc}: sees {len(vis)}/{len(shooters)} shooters; his cover vs them {min(covs)*100:.0f}-{max(covs)*100:.0f}%; dist {min(ds):.1f}-{max(ds):.1f}; within grenade range of {ingren}")
    return L
