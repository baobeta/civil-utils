"""Port of the arithmetic in YTC.lsp (c:YTC), used to generate golden CSVs.

Replace the generated CSVs with real output of the LISP (run YTC on the same
polylines in AutoCAD) whenever a Windows machine is available; the tests do
not care which produced them.

Usage: python3 generate_from_lisp.py
"""
import math, os

def n(x):                                  # ytc:n — rtos 2 2 then trim zeros
    s = "%.2f" % (math.floor(abs(x) * 100 + 0.5) / 100 * (1 if x >= 0 else -1))
    if "." in s:
        s = s.rstrip("0").rstrip(".")
    return s if s not in ("-0", "") else "0"

def dms(a, dsym="d"):                      # ytc:dms — radians -> DD°MM'SS"
    ts = int(a * 180 / math.pi * 3600 + 0.5)
    return "%d%s%02d'%02d\"" % (ts // 3600, dsym, (ts % 3600) // 60, ts % 60)

def sta(s):                                # ytc:sta — 131.09 -> 0+131.09
    s = int(s * 100 + 0.5) / 100
    km = int(s / 1000); m = s - km * 1000
    st = "%.2f" % m
    if m < 10: st = "00" + st
    elif m < 100: st = "0" + st
    return "%d+%s" % (km, st)

def unit(v):
    l = math.hypot(*v); return (v[0] / l, v[1] / l)

def run(pts, sta0, inputs):
    """inputs: list of (R, L, Wb, Wl) per interior PI (used only when the PI deflects)."""
    rows = []; stp = sta0; prevT = 0.0; idx = 0; k = 0
    for i in range(1, len(pts) - 1):
        a, b, c = pts[i - 1], pts[i], pts[i + 1]
        v1 = unit((b[0] - a[0], b[1] - a[1])); v2 = unit((c[0] - b[0], c[1] - b[1]))
        cr = v1[0] * v2[1] - v1[1] * v2[0]; dt = v1[0] * v2[0] + v1[1] * v2[1]
        alf = abs(math.atan2(cr, dt))
        dab = math.dist(a, b)
        if alf < 1e-6:
            stp += dab - prevT; prevT = 0.0; k += 1; continue
        idx += 1
        rr, ll, wb, wl = inputs[k]; k += 1
        beta = ll / (2 * rr)
        if ll > 0 and 2 * beta >= alf:
            ll = 0.0; beta = 0.0
        if ll > 0:
            sp = ll * ll / (24 * rr) - ll ** 4 / (2688 * rr ** 3)
            mm = ll / 2 - ll ** 3 / (240 * rr * rr)
            kk = rr * (alf - 2 * beta) + 2 * ll
        else:
            sp = mm = 0.0; kk = rr * alf
        tt = (rr + sp) * math.tan(alf / 2) + mm
        pc = (rr + sp) / math.cos(alf / 2) - rr
        tlen = dab - prevT - tt
        sta_ts = stp + tlen; sta_st = sta_ts + kk; sta_mp = sta_ts + kk / 2
        sta_sc = sta_ts + ll; sta_cs = sta_st - ll
        rows.append("Đ%d,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s" % (
            idx, dms(alf).rstrip('"'), n(rr), n(ll), n(tt), n(pc), n(kk), n(wb), n(wl),
            sta(sta_ts), sta(sta_sc), sta(sta_mp), sta(sta_cs), sta(sta_st)))
        stp = sta_st; prevT = tt
    end = stp + math.dist(pts[-2], pts[-1]) - prevT
    return rows, end

HEADER = "Dinh,A,R,L,T,P,K,Wb,Wl,Ly trinh ND,Ly trinh TD,Ly trinh P,Ly trinh TC,Ly trinh NC"
FIXTURES = {
    # simple circular curve, right angle, left turn
    "simple_arc": ([(0, 0), (100, 0), (100, 100)], 0.0, [(50, 0, 0, 0)]),
    # symmetric spiral-curve-spiral, 60° deflection
    "symmetric_scs": ([(0, 0), (300, 0), (300 + 300 * math.cos(math.radians(60)), 300 * math.sin(math.radians(60)))],
                      1000.0, [(200, 50, 0.6, 0.3)]),
    # 5 PIs: two curves, one collinear PI in between, start station 250
    "route_with_collinear": ([(0, 0), (200, 0), (200, 150), (200, 300), (350, 400)], 250.0,
                             [(120, 40, 0.5, 0.2), (0, 0, 0, 0), (250, 0, 0, 0)]),
}

if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    for name, (pts, s0, inputs) in FIXTURES.items():
        rows, end = run(pts, s0, inputs)
        with open(os.path.join(here, name + ".csv"), "w", encoding="utf-8") as f:
            f.write(HEADER + "\n" + "\n".join(rows) + "\n")
        print(name, "end station", n(end))
        for r in rows: print("  ", r)
