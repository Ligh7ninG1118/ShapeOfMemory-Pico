"""The hand as one smooth signed-distance shape, meshed by surface nets (numpy only; build_hand.py imports it inside
Blender). 2026-11-15, the user: the capsule hand "still looks pretty chubby and generated with primitive shapes", make a
more realistic one in the look of the SDK sample hands (built from our own numbers, not theirs).

What makes it read as a hand rather than tubes:
  - fingers wider than they are deep (an ellipse, depth/width Q), each phalanx waisted between its joints, the joints a
    little wider, soft pads on the palm side of every phalanx and a fuller fingertip pad;
  - knuckles on the back of the hand over each finger's base;
  - the palm a rounded slab with a real outline (narrow at the heel, the knuckle line slanting down to the little finger),
    slightly arched across its back, thinner toward the fingers, with the thumb's (thenar) and the little finger's
    (hypothenar) pads on its palm side;
  - everything joined by smooth minimums, so fingers grow out of the palm, neighbouring fingers web together near their
    bases and the thumb's web closes the gap to the index;
  - a wrist wider than deep, running into the heel of the hand.
Distances in metres. Coordinates as build_hand.py: right hand, wrist joint at the origin, fingers +y, palm toward -z,
thumb toward +x.
"""
import numpy as np

H = .0008                       # grid step (m)
Q = .84                         # finger depth / width
QT = .82                        # thumb depth / width
# Width radii at the knuckle, the middle joint, the last joint and the tip (fingers); the thumb at its base (CMC),
# its knuckle (MCP), its last joint (IP) and its tip.
WIDTH = dict(Index=(.0088, .0079, .0070, .0061), Middle=(.0091, .0082, .0072, .0063),
             Ring=(.0086, .0077, .0068, .0059), Little=(.0075, .0067, .0060, .0053))
THUMB_W = (.0124, .0103, .0092, .0082)
WAIST = 1.0                     # a phalanx's middle against its ends (1: smooth, as the SDK sample hands)
K_FINGER, K_PART, K_THUMB, K_WEB, K_PALM = .0065, .0060, .0100, .0070, .0100
# The palm's outline (x, y), anticlockwise from the heel on the thumb side; the knuckle line slants down to the little
# finger (its knuckle sits 1.5 cm nearer the wrist than the middle finger's).
OUTLINE = [(.021, -.012), (.022, .006), (.025, .030), (.029, .056), (.031, .074), (.029, .085), (.019, .090), (.005, .093),
           (-.010, .090), (-.022, .085), (-.033, .076), (-.039, .064), (-.041, .044), (-.038, .022), (-.031, .006),
           (-.024, -.012), (-.002, -.016)]
PALM_ROUND = .0085
DOME = .0055                    # how far the back of the hand rises over its flat slab (m)


def smin(a, b, k):
    if k <= 0:
        return np.minimum(a, b)
    h = np.maximum(k - np.abs(a - b), 0) / k
    return np.minimum(a, b) - h * h * k * .25


def smooth01(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def norm(v):
    v = np.asarray(v, float)
    return v / max(np.linalg.norm(v), 1e-12)


def round_cone(P, a, b, ra, rb):
    """Inigo Quilez's exact round cone (a capsule whose radius goes ra -> rb)."""
    ba = b - a
    l2 = float(ba @ ba)
    rr = ra - rb
    a2 = l2 - rr * rr
    il2 = 1 / l2
    pa = P - a
    y = pa @ ba
    z = y - l2
    x2 = np.einsum('ij,ij->i', pa * l2 - y[:, None] * ba, pa * l2 - y[:, None] * ba)
    y2 = y * y * l2
    z2 = z * z * l2
    k = np.sign(rr) * rr * rr * x2
    d3 = (np.sqrt(np.maximum(x2 * a2 * il2, 0)) + y * rr) * il2 - ra
    d1 = np.sqrt(x2 + z2) * il2 - rb
    d2 = np.sqrt(x2 + y2) * il2 - ra
    return np.where(np.sign(z) * a2 * z2 > k, d1, np.where(np.sign(y) * a2 * y2 < k, d2, d3))


def flat_cone(P, a, b, ra, rb, n, q):
    """A round cone whose section is an ellipse: depth q of its width along n (squashed by stretching space along n)."""
    n = norm(n)
    s = 1 / q - 1
    Ps = P + s * ((P - a) @ n)[:, None] * n
    bs = b + s * float((b - a) @ n) * n
    return round_cone(Ps, a, bs, ra, rb) * (1 + q) * .5


def ellipsoid(P, c, axes, radii):
    """Quilez's ellipsoid bound: c centre, axes 3x3 rows (unit), radii along them."""
    L = (P - c) @ np.asarray(axes).T
    r = np.asarray(radii, float)
    k0 = np.linalg.norm(L / r, axis=1)
    k1 = np.linalg.norm(L / (r * r), axis=1)
    return k0 * (k0 - 1) / np.maximum(k1, 1e-9)


def frame(along, up):
    """Rows: x (across), y (along), z (up, square to along)."""
    y = norm(along)
    z = np.asarray(up, float) - y * float(np.dot(up, y))
    z = norm(z)
    x = np.cross(y, z)
    return np.array([x, y, z])


def dense_outline(points, per=6):
    """Catmull-Rom through a closed outline."""
    p = [np.asarray(q, float) for q in points]
    out = []
    n = len(p)
    for i in range(n):
        p0, p1, p2, p3 = p[(i - 1) % n], p[i], p[(i + 1) % n], p[(i + 2) % n]
        for s in range(per):
            t = s / per
            out.append(.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    return np.array(out)


def polygon2d(xy, poly):
    """Signed distance to a closed polygon (negative inside), Quilez's winding version, vectorised over points."""
    d = np.sum((xy - poly[0]) ** 2, axis=1)
    s = np.ones(len(xy))
    n = len(poly)
    for i in range(n):
        vi, vj = poly[i], poly[i - 1]
        e = vj - vi
        w = xy - vi
        t = np.clip((w @ e) / float(e @ e), 0, 1)
        b = w - t[:, None] * e
        d = np.minimum(d, np.sum(b * b, axis=1))
        c1 = xy[:, 1] >= vi[1]
        c2 = xy[:, 1] < vj[1]
        c3 = e[0] * w[:, 1] > e[1] * w[:, 0]
        flip = (c1 & c2 & c3) | (~c1 & ~c2 & ~c3)
        s = np.where(flip, -s, s)
    return s * np.sqrt(d)


class Field:
    """The distance field on a regular grid, built up primitive by primitive, each only inside its own box."""

    def __init__(self, lo, hi, h=H):
        self.h = h
        self.lo = np.asarray(lo, float)
        self.n = np.ceil((np.asarray(hi, float) - self.lo) / h).astype(int) + 1
        self.f = np.full(self.n, 1.0, np.float32)

    def box(self, lo, hi):
        i0 = np.clip(np.floor((np.asarray(lo) - self.lo) / self.h).astype(int), 0, self.n - 1)
        i1 = np.clip(np.ceil((np.asarray(hi) - self.lo) / self.h).astype(int) + 1, 1, self.n)
        sl = tuple(slice(a, b) for a, b in zip(i0, i1))
        axes = [self.lo[k] + np.arange(i0[k], i1[k]) * self.h for k in range(3)]
        X, Y, Z = np.meshgrid(*axes, indexing='ij')
        return sl, np.stack([X.ravel(), Y.ravel(), Z.ravel()], 1), tuple(i1 - i0)

    def add(self, lo, hi, fn, k):
        # the box grows by the blend's reach: a primitive cut off inside its blend leaves a crease along the cut
        pad = k + .004
        sl, P, shape = self.box(np.asarray(lo, float) - pad, np.asarray(hi, float) + pad)
        d = fn(P).reshape(shape).astype(np.float32)
        self.f[sl] = smin(self.f[sl], d, k)


def sweep(P, pts, radii, normal_of, q, per=8):
    """One smooth tube through pts (a Catmull-Rom centre line), its width radius going smoothly through radii and its
    depth q of its width along normal_of(tangent): a chain of short round cones, so no joint between bones shows (the
    phalanges built one by one ringed every joint)."""
    pts = [np.asarray(p, float) for p in pts]
    n = len(pts)
    ext = [2 * pts[0] - pts[1]] + pts + [2 * pts[-1] - pts[-2]]
    rex = [radii[0]] + list(radii) + [radii[-1]]
    samples, rs = [], []
    for i in range(n - 1):
        p0, p1, p2, p3 = ext[i], ext[i + 1], ext[i + 2], ext[i + 3]
        r1, r2 = rex[i + 1], rex[i + 2]
        for k in range(per):
            t = k / per
            samples.append(.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
            u = t * t * (3 - 2 * t)
            rs.append(r1 + (r2 - r1) * (.5 * t + .5 * u))
    samples.append(pts[-1])
    rs.append(radii[-1])
    d = np.full(len(P), 1.0)
    for i in range(len(samples) - 1):
        a, b = samples[i], samples[i + 1]
        d = np.minimum(d, flat_cone(P, a, b, rs[i], rs[i + 1], normal_of(norm(b - a)), q))
    return d


def pad_box(points, r):
    p = np.asarray(points, float)
    return p.min(0) - r, p.max(0) + r


def build(J):
    """J: joint name -> (x, y, z). Returns the Field."""
    J = {k: np.asarray(v, float) for k, v in J.items()}
    up = np.array([0, 0, 1.])           # the back of the hand
    allp = np.array(list(J.values()))
    F = Field(allp.min(0) - [.05, .095, .04], allp.max(0) + [.04, .02, .04])

    # ---- palm
    poly = dense_outline(OUTLINE)
    def palm(P):
        x, y, z = P[:, 0], P[:, 1], P[:, 2]
        d2 = polygon2d(P[:, :2], poly)
        mid = .0022 * (1 - (x / .04) ** 2) - .0010                    # arched across its back
        half = (.0112 - .0022 * smooth01(.055, .092, y) + .0024 * (1 - smooth01(-.012, .030, y))) * (1 - .14 * np.clip((x / .04) ** 2, 0, 1))
        # A full back (2026-11-15): the palm's upper face itself rises into a dome over the metacarpals, highest across
        # the middle and easing out to the sides, up from the heel and down toward the knuckles. Built into the slab
        # rather than a blob on top, which showed its own rim like a pillow.
        across = np.clip(1 - ((x + .003) / .042) ** 2, 0, 1) ** .75
        along = smooth01(-.014, .032, y) * (1 - smooth01(.060, .096, y))
        top = mid + half + DOME * across * along
        dz = np.maximum(z - top, (mid - half) - z)
        r = PALM_ROUND
        a, b = d2 + r, dz + r
        return np.minimum(np.maximum(a, b), 0) + np.sqrt(np.maximum(a, 0) ** 2 + np.maximum(b, 0) ** 2) - r
    F.add((-.050, -.030, -.024), (.040, .100, .024), palm, 0)
    # pads on the palm side: thumb (thenar) and little finger (hypothenar)
    th_axes = frame((.50, .86, -.12), up)
    F.add((-.005, .005, -.030), (.050, .072, .010), lambda P: ellipsoid(P, np.array([.021, .038, -.0098]), th_axes, (.0145, .028, .0122)), .013)
    hy_axes = frame((-.10, 1, 0), up)
    F.add((-.050, .008, -.022), (-.012, .082, .010), lambda P: ellipsoid(P, np.array([-.0285, .045, -.0042]), hy_axes, (.0112, .031, .0100)), .013)
    # wrist: wider than deep, running into the heel
    F.add((-.040, -.090, -.030), (.040, .030, .030), lambda P: flat_cone(P, np.array([0, -.054, -.0012]), np.array([0, .004, -.0016]), .0250, .0228, up, .58), .020)
    # A full back of the hand (2026-11-15, the user: it looked sunken, "not full like a real hand"; measured, the back
    # was flat across, 7 mm under the wrist, and fell away to a hollow between the index and the thumb):
    # - the metacarpals' dome: in the palm's own top face (DOME, see palm());
    # - the muscle between the thumb and the index on the back (first dorsal interosseous), full rather than a hollow.
    di_axes = frame((-.30, 1, 0), up)
    F.add((-.000, .010, -.020), (.050, .090, .025), lambda P: ellipsoid(P, np.array([.0210, .046, .0016]), di_axes, (.0110, .023, .0088)), .017)

    # ---- fingers
    for name in ('Little', 'Ring', 'Middle', 'Index'):
        w = WIDTH[name]
        B, K, I, D, T = (J[name + s] for s in ('Metacarpal', 'Proximal', 'Intermediate', 'Distal', 'Tip'))
        cap = T - norm(T - D) * w[3]
        base = K - norm(I - K) * .007
        lo, hi = pad_box([base, K, I, D, T], .016)
        def finger(P, base=base, K=K, I=I, D=D, T=T, w=w, cap=cap):
            d = sweep(P, [base, K, I, D, cap], [w[0] * 1.02, w[0], w[1], w[2], w[3]],
                      lambda t: up - t * float(np.dot(up, t)), Q)
            # a fuller fingertip pad
            ax = frame(T - D, up)
            d = smin(d, ellipsoid(P, D + (cap - D) * .62 - ax[2] * w[3] * .28, ax, (w[3] * .84, float(np.linalg.norm(cap - D)) * .5, w[3] * Q * .70)), .004)
            return d
        F.add(lo, hi, finger, K_FINGER)
    # knuckles on the back of the hand
    for name in ('Index', 'Middle', 'Ring', 'Little'):
        K, I = J[name + 'Proximal'], J[name + 'Intermediate']
        ax = frame(I - K, up)
        r = WIDTH[name][0]
        c = K + up * r * .58 - ax[1] * .0025
        F.add(c - .02, c + .02, lambda P, c=c, ax=ax, r=r: ellipsoid(P, c, ax, (r * .72, r * .80, r * .48)), .0035)

    # ---- thumb: its depth runs along the way it bends (the nail faces away from the palm's middle)
    M, Pr, D, T = (J['Thumb' + s] for s in ('Metacarpal', 'Proximal', 'Distal', 'Tip'))
    w = THUMB_W
    cap = T - norm(T - D) * w[3]
    bend = norm(J['IndexProximal'] - M)
    def thumb(P):
        d = sweep(P, [M, Pr, D, cap], [w[0], w[1], w[2], w[3]], lambda t: bend - t * float(np.dot(bend, t)), QT)
        ax = frame(cap - D, norm(bend - norm(cap - D) * float(np.dot(bend, norm(cap - D)))))
        d = smin(d, ellipsoid(P, D + (cap - D) * .6 + ax[2] * w[3] * .28, ax, (w[3] * .86, float(np.linalg.norm(cap - D)) * .5, w[3] * QT * .72)), .004)
        return d
    lo, hi = pad_box([M, Pr, D, T], .02)
    F.add(lo, hi, thumb, K_THUMB)
    # the web between the thumb and the index
    a = M + (Pr - M) * .72
    b = J['IndexMetacarpal'] + (J['IndexProximal'] - J['IndexMetacarpal']) * .45 + np.array([0, 0, -.003])
    lo, hi = pad_box([a, b], .015)
    F.add(lo, hi, lambda P: flat_cone(P, a, b, .0060, .0052, up, .55), K_WEB)
    return F


def surface_nets(F):
    """Vertices (one per cell the surface crosses, at the mean of its edge crossings) and outward quads."""
    f = F.f.astype(np.float64)
    nx, ny, nz = f.shape
    inside = f < 0
    acc = np.zeros((nx - 1, ny - 1, nz - 1, 3))
    num = np.zeros((nx - 1, ny - 1, nz - 1))
    quads = []
    for axis in range(3):
        a = [slice(None)] * 3
        b = [slice(None)] * 3
        a[axis] = slice(0, -1)
        b[axis] = slice(1, None)
        f0, f1 = f[tuple(a)], f[tuple(b)]
        cross = (f0 < 0) != (f1 < 0)
        t = np.where(cross, f0 / np.where(cross, f0 - f1, 1), 0)
        idx = np.argwhere(cross)
        tt = t[cross]
        pos = idx.astype(float)
        pos[:, axis] += tt
        # each crossing edge belongs to the (up to) four cells round it
        o1, o2 = [k for k in range(3) if k != axis]
        for d1 in (-1, 0):
            for d2 in (-1, 0):
                c = idx.copy()
                c[:, o1] += d1
                c[:, o2] += d2
                ok = (c >= 0).all(1) & (c[:, 0] < nx - 1) & (c[:, 1] < ny - 1) & (c[:, 2] < nz - 1)
                np.add.at(acc, (c[ok, 0], c[ok, 1], c[ok, 2]), pos[ok])
                np.add.at(num, (c[ok, 0], c[ok, 1], c[ok, 2]), 1)
        # quads: the four cells round each crossing edge, wound so the normal points out (from inside to outside)
        ok = (idx[:, o1] >= 1) & (idx[:, o1] <= [nx, ny, nz][o1] - 2) & (idx[:, o2] >= 1) & (idx[:, o2] <= [nx, ny, nz][o2] - 2)
        e = idx[ok]
        out_pos = (f0[cross] < 0)[ok]                  # inside at the lower end: the normal points +axis
        def cell(d1, d2):
            c = e.copy()
            c[:, o1] += d1
            c[:, o2] += d2
            return c
        ring = [cell(-1, -1), cell(0, -1), cell(0, 0), cell(-1, 0)]
        # (o1, o2) is right-handed about axis for axis 0 (y, z) and axis 2 (x, y); for axis 1 it is (x, z): flip
        flip = axis == 1
        quads.append((ring, out_pos ^ flip))
    vid = -np.ones((nx - 1, ny - 1, nz - 1), int)
    has = num > 0
    vid[has] = np.arange(int(has.sum()))
    verts = acc[has] / num[has][:, None]
    verts = F.lo + verts * F.h
    faces = []
    for ring, forward in quads:
        ids = np.stack([vid[c[:, 0], c[:, 1], c[:, 2]] for c in ring], 1)
        ids = np.where(forward[:, None], ids, ids[:, ::-1])
        faces.append(ids)
    faces = np.concatenate(faces, 0)
    faces = faces[(faces >= 0).all(1)]
    return verts, faces
