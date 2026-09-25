from PIL import Image, ImageDraw
from pathlib import Path
import math
import random

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[1]
OUT_DIR = PROJECT / "Assets" / "Models" / "Targets" / "CanTargetStand"
OUT_DIR.mkdir(parents=True, exist_ok=True)
OUT = OUT_DIR / "WoodStand_BaseColor.png"
W = H = 1024
im = Image.new("RGB", (W, H))
px = im.load()

# Warm walnut-brown base with long grain aligned to the plank direction.
for y in range(H):
    cross = y / H
    broad = 7.0 * math.sin(cross * math.tau * 2.0) + 3.5 * math.sin(cross * math.tau * 7.0)
    for x in range(W):
        along = x / W
        wave = (8.0 * math.sin(y * .075 + 2.0 * math.sin(x * .010))
                + 4.0 * math.sin(y * .29 + math.sin(x * .027) * 1.8)
                + 2.1 * math.sin(y * .81 + math.sin(x * .063))
                + 1.2 * math.sin(x * .019 + y * .018))
        tone = broad + wave
        px[x, y] = (max(0,min(255,int(145+tone))),
                    max(0,min(255,int(91+tone*.78))),
                    max(0,min(255,int(53+tone*.53))))

d = ImageDraw.Draw(im)
random.seed(24)
# Fine flowing grain lines with low contrast, continuous along the board length.
for _ in range(95):
    y0 = random.randint(-24, H+24)
    amp = random.uniform(1.0, 7.0)
    phase = random.uniform(0, math.tau)
    frequency = random.uniform(.004, .022)
    shade = random.choice((-1, 1))
    base = (145,91,53)
    color = tuple(max(0,min(255,c+shade*random.randint(4,12))) for c in base)
    pts=[]
    for x in range(-8,W+16,8):
        y = y0 + amp*math.sin(x*frequency+phase) + .45*amp*math.sin(x*frequency*.31+phase*.7)
        pts.append((x,y))
    d.line(pts,fill=color,width=random.choice((1,1,2)))

# A few soft elongated knots, typical of a finished pine plank.
for cx,cy in ((265,335),(745,700),(825,235)):
    for i in range(1,8):
        rx=4+i*8
        ry=2+i*2
        col=(112+i*3,68+i*2,40+i)
        d.ellipse((cx-rx,cy-ry,cx+rx,cy+ry),outline=col,width=1)
    d.ellipse((cx-5,cy-2,cx+5,cy+2),fill=(102,61,36))

im.save(OUT,format="PNG",optimize=True)
print(f"Wrote {OUT} ({OUT.stat().st_size} bytes)")
