# Мелкая живая детализация леса/оврага: грибы, ягоды, корни, ветки, кусты, папоротники, трава,
# цветы-колокольчики, кристаллы, камешки, пень, бревно — и живность (птичка, мотылёк, жук).
#
# Стиль как у ravine_props.py: гранёный лоуполи, цвет на каждую грань в атрибуте "Col".
# Цвета подобраны под ночь: альбедо насыщенное и средней яркости (тёмным ночь делает свет, а не краска),
# у каждой модели светлый верх и тёмный низ — силуэт читается сверху.
#
# Материалы в FBX (Unity подменяет их в Editor/ForestDetails.cs):
#   M_Detail       — обычный (Game/Stylized Toon + vertex colors);
#   M_Detail_Leaf  — листья/трава: видны с двух сторон, качаются на ветру;
#   M_Detail_Glow  — светится цветом вершин (шляпки, ягоды, кристаллы, пятна на крыльях).
# Живность: у Det_Bird / Det_Moth крылья — отдельные объекты WingL/WingR (ось вращения в плече),
# их машет скрипт Critter в Unity.
#
# Запуск (pip-пакет bpy 4.x/5.x или Blender):
#   python forest_details.py --out <папка> [--preview preview.png]
# Результат: Det_*.fbx → Game/Assets/_Project/Art/Models/Props/Forest/, forest_details.blend → Art/Blender/Props/.
# Оси: Z вверх, «перед» -Y (в Unity +Z).

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from lowpoly_kit import (cylinder, get_material, ico, lerp3, look_rot, n01, smooth, sweep)  # noqa: E402

SEED = 31
MAT = "M_Detail"
LEAF = "M_Detail_Leaf"
GLOW = "M_Detail_Glow"

# ---------------------------------------------------------------- палитра (sRGB)
BARK = (0.38, 0.28, 0.22)
BARK_DARK = (0.2, 0.15, 0.13)
BARK_LIGHT = (0.55, 0.43, 0.33)
WOOD_CUT = (0.78, 0.62, 0.42)
WOOD_RING = (0.6, 0.44, 0.28)
MOSS_A = (0.26, 0.5, 0.34)
MOSS_B = (0.42, 0.62, 0.34)
LEAF_TEAL = (0.18, 0.46, 0.44)
LEAF_GREEN = (0.3, 0.55, 0.3)
LEAF_LIME = (0.55, 0.75, 0.38)
LEAF_BLUE = (0.24, 0.38, 0.62)
LEAF_DARK = (0.1, 0.22, 0.22)
STEM_PALE = (0.84, 0.86, 0.9)
GILL = (0.62, 0.66, 0.78)
STONE = (0.5, 0.52, 0.6)
STONE_DARK = (0.3, 0.31, 0.38)
GLOW_CYAN = (0.42, 1.0, 0.92)
GLOW_BLUE = (0.42, 0.66, 1.0)
GLOW_VIOLET = (0.74, 0.46, 1.0)
GLOW_GOLD = (1.0, 0.76, 0.32)
GLOW_PINK = (1.0, 0.5, 0.78)
GLOW_WHITE = (0.86, 0.94, 1.0)
TOAD_RED = (0.8, 0.17, 0.14)
TOAD_SPOT = (0.96, 0.93, 0.84)
BERRY_RED = (0.86, 0.13, 0.2)


def jit(rng, col, a=0.06):
    k = 1 + rng.uniform(-a, a)
    return tuple(max(0.0, min(1.0, c * k)) for c in col)


# ---------------------------------------------------------------- объекты

def make_obj(name, bm, material, up_normals=False, recalc=True):
    if recalc:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if up_normals:  # тонкие листья: нормаль всегда «наружу/вверх», иначе свет на них скачет
        for f in bm.faces:
            f.normal_update()
            if f.normal.z < -0.05:
                f.normal_flip()
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    mesh.materials.append(get_material(material))
    return obj


def paint(obj, fn):
    mesh = obj.data
    attr = mesh.color_attributes.get("Col") or mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    for p in mesh.polygons:
        c = fn(Vector(p.center), Vector(p.normal), p)
        for li in p.loop_indices:
            attr.data[li].color_srgb = (c[0], c[1], c[2], 1.0)
    mesh.color_attributes.active_color = attr
    mesh.color_attributes.render_color_index = mesh.color_attributes.active_color_index


def piece(name, build, color, material, up_normals=False, recalc=True):
    bm = bmesh.new()
    build(bm)
    if len(bm.faces) == 0:
        bm.free()
        return None
    o = make_obj(name, bm, material, up_normals, recalc)
    paint(o, color)
    return o


def merge(objs, name):
    objs = [o for o in objs if o is not None]
    with bpy.context.temp_override(active_object=objs[0], object=objs[0],
                                   selected_objects=objs, selected_editable_objects=objs):
        bpy.ops.object.join()
    o = objs[0]
    o.name = name
    o.data.name = name
    return o


def height_grad(lo, hi, h0, h1):
    def fn(c, nrm, p):
        return lerp3(lo, hi, smooth(h0, h1, c.z))
    return fn


# ---------------------------------------------------------------- примитивы растений

def leaf(bm, base, dirv, L, W, droop=0.25, seg=4, fold=0.22, curl=0.0):
    """Лист с центральной жилкой (V-сгиб), сужается к черешку и к кончику. dirv — направление от черешка."""
    d = Vector(dirv).normalized()
    side = d.cross(Vector((0, 0, 1)))
    if side.length < 1e-3:
        side = Vector((1, 0, 0))
    side.normalize()
    up = side.cross(d).normalized()
    if up.z < 0:
        up = -up
    rings = []
    for i in range(seg):
        t = i / seg
        p = Vector(base) + d * L * t + Vector((0, 0, -droop * L * t * t))
        w = W * (0.12 + 0.88 * math.sin(math.pi * min(0.999, t * 0.9 + 0.08)) ** 0.8)
        lift = curl * w * t
        rings.append((bm.verts.new(p - side * w + up * lift), bm.verts.new(p + up * fold * w),
                      bm.verts.new(p + side * w + up * lift)))
    tip = bm.verts.new(Vector(base) + d * L + Vector((0, 0, -droop * L)))
    for a, b in zip(rings[:-1], rings[1:]):
        bm.faces.new((a[0], a[1], b[1], b[0]))
        bm.faces.new((a[1], a[2], b[2], b[1]))
    last = rings[-1]
    bm.faces.new((last[0], last[1], tip))
    bm.faces.new((last[1], last[2], tip))


def blade(bm, base, dirv, L, W, droop=0.35, seg=3):
    """Травинка: сужающаяся лента, гнётся под своим весом."""
    d = Vector(dirv).normalized()
    side = d.cross(Vector((0, 0, 1)))
    if side.length < 1e-3:
        side = Vector((1, 0, 0))
    side.normalize()
    prev = None
    for i in range(seg + 1):
        t = i / seg
        p = Vector(base) + d * L * t + Vector((0, 0, -droop * L * t * t))
        w = W * (1 - t) ** 0.9
        if i == seg:
            tip = bm.verts.new(p)
            bm.faces.new((prev[0], prev[1], tip))
        else:
            cur = (bm.verts.new(p - side * w), bm.verts.new(p + side * w))
            if prev:
                bm.faces.new((prev[0], prev[1], cur[1], cur[0]))
            prev = cur


def bezier(p0, p1, p2, n):
    p0, p1, p2 = Vector(p0), Vector(p1), Vector(p2)
    return [p0 * (1 - t) ** 2 + p1 * 2 * (1 - t) * t + p2 * t * t for t in (i / (n - 1) for i in range(n))]


def blob(bm, center, r, scale=(1, 1, 1), rng=None, amount=0.18, subdiv=1, seed=0.0):
    def deform(co):
        k = 1 + amount * (n01(co * 2.3, 1.0, (seed, seed * 0.7, 1.3)) - 0.5) * 2
        return co * k
    return ico(bm, center, r, scale=scale, subdiv=subdiv, deform=deform)


def ring_disc(bm, center, r, seg, rot):
    """Плоский диск (для пятнышек на шляпке)."""
    m = Matrix.Translation(Vector(center)) @ rot.to_4x4()
    vs = [bm.verts.new(m @ Vector((math.cos(2 * math.pi * k / seg) * r, math.sin(2 * math.pi * k / seg) * r, 0))) for k in range(seg)]
    bm.faces.new(vs)


# ---------------------------------------------------------------- грибы

def mushroom(rng, at, height, cap_r, cap_h, tilt, cap_col, glow, spots=0, spot_col=TOAD_SPOT, seg=10,
             stem_col=STEM_PALE, lip=1.75, cone=0.0, tag="m"):
    """Гриб: изогнутая ножка с юбочкой, шляпка-купол с загнутым краем, пластинки снизу, пятнышки."""
    at = Vector(at)
    lean = Vector((math.sin(tilt[0]) * math.cos(tilt[1]), math.sin(tilt[0]) * math.sin(tilt[1]), 0))
    top = at + Vector((0, 0, height)) + lean * height
    mid = at + Vector((0, 0, height * 0.5)) + lean * height * 0.2
    pts = bezier(at - Vector((0, 0, 0.02)), mid, top, 5)
    sr = max(0.012, cap_r * 0.2)
    up = (top - pts[-2]).normalized()
    rot = look_rot(up)
    parts = []

    def stem_build(bm):
        sweep(bm, pts, [sr * 1.35, sr * 1.05, sr, sr * 0.92, sr * 0.9], seg=7, rng=rng, jitter=0.06, cap1=False)
        if height > 0.12:  # юбочка-кольцо на ножке
            c = pts[3]
            cylinder(bm, c, sr * 1.9, sr * 1.0, sr * 0.8, seg=7, rot=look_rot((pts[4] - pts[2]).normalized()), caps=False)

    def stem_col_fn(c, nrm, p):
        return lerp3(lerp3(stem_col, (0.5, 0.48, 0.5), 0.35), stem_col, smooth(at.z, top.z, c.z))
    parts.append(piece(f"{tag}_stem", stem_build, stem_col_fn, MAT))

    # шляпка: профиль от макушки (theta=0) до загнутого края (theta=lip)
    rings_n = 5
    wob = [1 + rng.uniform(-0.07, 0.07) for _ in range(seg)]

    def prof(j):
        th = lip * j / rings_n
        r = cap_r * math.sin(min(th, math.pi / 2)) * (1 + cone * 0)
        if th > math.pi / 2:
            r = cap_r * (1 - (th - math.pi / 2) * 0.25)
        z = cap_h * (math.cos(th) if th <= math.pi / 2 else -0.18 * (th - math.pi / 2))
        if cone:
            z += cone * cap_h * (1 - j / rings_n) ** 2
        return r, z

    m = Matrix.Translation(top - up * cap_h * 0.15) @ rot.to_4x4()
    top_faces = []

    def cap_build(bm):
        apex = bm.verts.new(m @ Vector((0, 0, prof(0)[1])))
        prev = None
        for j in range(1, rings_n + 1):
            r, z = prof(j)
            ring = [bm.verts.new(m @ Vector((math.cos(2 * math.pi * k / seg) * r * wob[k],
                                             math.sin(2 * math.pi * k / seg) * r * wob[k], z))) for k in range(seg)]
            if prev is None:
                for k in range(seg):
                    bm.faces.new((apex, ring[k], ring[(k + 1) % seg]))
            else:
                for k in range(seg):
                    bm.faces.new((prev[k], ring[k], ring[(k + 1) % seg], prev[(k + 1) % seg]))
            prev = ring
        # низ шляпки до ножки
        r_in = sr * 1.1
        z_in = prof(rings_n)[1] + cap_h * 0.18
        inner = [bm.verts.new(m @ Vector((math.cos(2 * math.pi * k / seg) * r_in, math.sin(2 * math.pi * k / seg) * r_in, z_in))) for k in range(seg)]
        for k in range(seg):
            bm.faces.new((prev[k], inner[k], inner[(k + 1) % seg], prev[(k + 1) % seg]))
        bm.faces.new(list(reversed(inner)))

    lip_z = (m @ Vector((0, 0, prof(rings_n)[1] + cap_h * 0.05))).z
    shade_c = lerp3(cap_col, (0.08, 0.08, 0.16), 0.45)

    def cap_col_fn(c, nrm, p):
        # низ (пластинки) — полосками, верх — светлеет к макушке
        local = rot.transposed() @ (c - top)
        if local.z < (prof(rings_n)[1] - cap_h * 0.1) + cap_h * 0.12 and nrm.dot(up) < 0.2:
            k = int((math.atan2(local.y, local.x) + math.pi) / (2 * math.pi) * seg * 2)
            g = GILL if not glow else lerp3(cap_col, GILL, 0.5)
            return lerp3(g, (0.3, 0.3, 0.42), 0.25 if k % 2 else 0.0)
        t = smooth(lip_z, top.z + cap_h * 0.8, c.z)
        col = lerp3(shade_c, cap_col, 0.35 + 0.65 * t)
        return lerp3(col, (1, 1, 1), 0.18 * smooth(0.85, 1.0, nrm.dot(up)))
    parts.append(piece(f"{tag}_cap", cap_build, cap_col_fn, GLOW if glow else MAT))

    if spots:
        def spots_build(bm):
            for _ in range(spots):
                th = rng.uniform(0.25, 1.15)
                ph = rng.uniform(0, 2 * math.pi)
                r = cap_r * math.sin(th) * 1.02
                z = cap_h * math.cos(th) * 1.04 + 0.002
                loc = m @ Vector((math.cos(ph) * r, math.sin(ph) * r, z))
                nrm = (loc - (m @ Vector((0, 0, -cap_h * 0.6)))).normalized()
                ring_disc(bm, loc + nrm * 0.003, cap_r * rng.uniform(0.09, 0.16), 6, look_rot(nrm))
        parts.append(piece(f"{tag}_spots", spots_build, lambda c, n, p: jit(rng, spot_col, 0.05), GLOW if glow else MAT,
                           up_normals=False))
    return parts


def build_mushroom_glow_cluster(rng, name, col=GLOW_CYAN, n=6, spread=0.28):
    parts = []
    for i in range(n):
        a = rng.uniform(0, 2 * math.pi)
        d = spread * math.sqrt(rng.random())
        big = 1.0 if i == 0 else rng.uniform(0.45, 0.85)
        at = (math.cos(a) * d, math.sin(a) * d, 0)
        parts += mushroom(rng, at, 0.3 * big + 0.07, 0.085 * big + 0.025, 0.042 * big + 0.012,
                          (rng.uniform(0.05, 0.35), a), jit(rng, col, 0.05), glow=True,
                          spots=rng.randint(3, 6), spot_col=GLOW_WHITE, tag=f"{name}_{i}")
    parts.append(moss_patch(rng, (0, 0, 0), spread + 0.12, f"{name}_moss"))
    return merge(parts, name)


def build_mushroom_tall(rng, name):
    """Тонкие высокие фиолетовые колокольчики-грибы."""
    parts = []
    for i in range(rng.randint(4, 6)):
        a = rng.uniform(0, 2 * math.pi)
        d = rng.uniform(0, 0.16)
        h = rng.uniform(0.25, 0.5)
        r = rng.uniform(0.045, 0.07)
        parts += mushroom(rng, (math.cos(a) * d, math.sin(a) * d, 0), h, r, r * 1.4, (rng.uniform(0.05, 0.3), a),
                          jit(rng, GLOW_VIOLET), glow=True, lip=1.6, cone=0.6, seg=8, tag=f"{name}_{i}")
    parts.append(moss_patch(rng, (0, 0, 0), 0.28, f"{name}_moss"))
    return merge(parts, name)


def build_toadstool(rng, name):
    """Мухоморы: красная шляпка с белыми пятнами, не светятся — тёплый акцент."""
    parts = []
    specs = [((0, 0, 0), 0.3, 0.16, 0.09), ((0.17, 0.08, 0), 0.17, 0.09, 0.06), ((-0.1, 0.13, 0), 0.1, 0.06, 0.05)]
    for i, (at, h, r, ch) in enumerate(specs):
        parts += mushroom(rng, at, h, r, ch, (rng.uniform(0.05, 0.2), rng.uniform(0, 6.28)), TOAD_RED, glow=False,
                          spots=9 if r > 0.1 else 5, tag=f"{name}_{i}")
    parts.append(moss_patch(rng, (0.04, 0.05, 0), 0.3, f"{name}_moss"))
    parts.append(grass_ring(rng, (0, 0, 0), 0.28, 12, f"{name}_grass", h=(0.16, 0.3), col_lo=LEAF_GREEN))
    return merge(parts, name)


def build_shelf_fungus(rng, name):
    """Трутовики ярусами (вешаются на пни, брёвна, стены). Край светится золотом."""
    def build(bm):
        for i in range(rng.randint(3, 5)):
            z = 0.08 + i * rng.uniform(0.07, 0.1)
            x = rng.uniform(-0.12, 0.12)
            r = rng.uniform(0.08, 0.15) * (1.15 - i * 0.12)
            seg = 9
            m = Matrix.Translation((x, 0, z))
            rings = []
            for j, (rr, zz, yy) in enumerate([(0.15, 0.03, 0), (0.6, 0.035, -0.5), (0.95, 0.0, -0.9), (1.0, -0.025, -0.95)]):
                ring = []
                for k in range(seg):
                    a = math.pi * k / (seg - 1)
                    ring.append(bm.verts.new(m @ Vector((math.cos(a) * r * rr * 1.25, -math.sin(a) * r * rr + 0.01, zz * (1 + r * 4)))))
                rings.append(ring)
            for a_, b_ in zip(rings[:-1], rings[1:]):
                for k in range(seg - 1):
                    bm.faces.new((a_[k], b_[k], b_[k + 1], a_[k + 1]))
            # низ
            back = [bm.verts.new(m @ Vector((math.cos(math.pi * k / (seg - 1)) * r * 0.2, 0.005, -0.03))) for k in range(seg)]
            for k in range(seg - 1):
                bm.faces.new((rings[-1][k], back[k], back[k + 1], rings[-1][k + 1]))

    def col(c, nrm, p):
        dist = Vector((c.x, c.y * 1.25, 0)).length
        rim = smooth(0.08, 0.14, dist)
        base = lerp3((0.45, 0.3, 0.22), (0.7, 0.52, 0.34), smooth(-0.5, 1, nrm.z))
        return lerp3(base, GLOW_GOLD, rim * 0.9)
    return piece(name, build, col, MAT)


def moss_patch(rng, center, r, name):
    def build(bm):
        for k in range(rng.randint(3, 5)):
            a = rng.uniform(0, 6.28)
            d = rng.uniform(0, r * 0.6)
            blob(bm, (center[0] + math.cos(a) * d, center[1] + math.sin(a) * d, -0.01), rng.uniform(0.06, 0.12) * (r / 0.3 + 0.4),
                 scale=(1.3, 1.1, 0.32), amount=0.25, seed=rng.random() * 10)

    def col(c, nrm, p):
        return lerp3(MOSS_A, MOSS_B, n01(c, 18, (3, 1, 0)) * smooth(0, 0.03, c.z) + 0.15)
    return piece(name, build, col, MAT)


def grass_ring(rng, center, r, n, name, h=(0.12, 0.25), col_lo=LEAF_DARK, col_hi=LEAF_LIME):
    def build(bm):
        for _ in range(n):
            a = rng.uniform(0, 6.28)
            d = rng.uniform(r * 0.6, r)
            base = Vector((center[0] + math.cos(a) * d, center[1] + math.sin(a) * d, -0.01))
            out = Vector((math.cos(a), math.sin(a), 0)) * rng.uniform(0.2, 0.6) + Vector((0, 0, 1))
            blade(bm, base, out, rng.uniform(*h), rng.uniform(0.012, 0.02))
    return piece(name, build, height_grad(col_lo, col_hi, 0.0, h[1] * 0.8), LEAF, up_normals=True, recalc=False)


# ---------------------------------------------------------------- кусты, травы, цветы

def build_fern(rng, name, fronds=9, size=0.6):
    """Папоротник: дугообразные вайи с парами листочков, к кончику мельче."""
    def build(bm):
        for i in range(fronds):
            a = 2 * math.pi * i / fronds + rng.uniform(-0.2, 0.2)
            L = size * rng.uniform(0.75, 1.1)
            out = Vector((math.cos(a), math.sin(a), 0))
            rise = rng.uniform(0.55, 0.9)
            p0 = Vector((0, 0, 0.0))
            p1 = out * L * 0.35 + Vector((0, 0, L * rise))
            p2 = out * L + Vector((0, 0, L * 0.15))
            spine = bezier(p0, p1, p2, 10)
            sweep(bm, spine, [0.008 * (1 - i / 10) + 0.002 for i in range(10)], seg=3, cap0=False, cap1=False)
            for k in range(1, 9):
                t = k / 9
                pa, pb = spine[k], spine[k + 1]
                d = (pb - pa).normalized()
                side = d.cross(Vector((0, 0, 1))).normalized()
                ll = L * 0.2 * (1 - t * 0.75)
                for s in (-1, 1):
                    leaf(bm, pa, (side * s + d * 0.55).normalized() + Vector((0, 0, 0.1)), ll, ll * 0.28, droop=0.25, seg=3)
            leaf(bm, spine[-1], (spine[-1] - spine[-2]).normalized(), L * 0.08, L * 0.025, seg=2)

    def col(c, nrm, p):
        t = smooth(0.0, size * 0.75, c.z + Vector((c.x, c.y, 0)).length * 0.3)
        return lerp3(LEAF_DARK, lerp3(LEAF_TEAL, LEAF_LIME, 0.45), t)
    return piece(name, build, col, LEAF, up_normals=True, recalc=False)


def build_bush(rng, name, r=0.55, leafy=70, palette=(LEAF_TEAL, LEAF_GREEN, LEAF_LIME), flowers=0):
    """Круглый куст: тёмные комья листвы внутри + сотни листиков на поверхности, светлеет к макушке."""
    parts = []
    centers = []
    for i in range(7):
        a = rng.uniform(0, 6.28)
        d = rng.uniform(0, r * 0.45)
        centers.append(Vector((math.cos(a) * d, math.sin(a) * d, r * rng.uniform(0.45, 0.85))))

    def core(bm):
        for c in centers:
            blob(bm, c, r * rng.uniform(0.38, 0.5), scale=(1, 1, 0.8), amount=0.2, seed=rng.random() * 9)

    def core_col(c, nrm, p):
        t = smooth(0.0, r * 1.3, c.z) * 0.6 + 0.4 * smooth(-0.2, 1.0, nrm.z)
        return lerp3(LEAF_DARK, palette[0], t)
    parts.append(piece(f"{name}_core", core, core_col, MAT))

    def leaves(bm):
        for _ in range(leafy):
            c = centers[rng.randrange(len(centers))]
            d = Vector((rng.gauss(0, 1), rng.gauss(0, 1), abs(rng.gauss(0, 1)) * 0.9 + 0.2)).normalized()
            base = c + d * r * 0.42
            L = r * rng.uniform(0.28, 0.4)
            leaf(bm, base, (d + Vector((0, 0, 0.25))).normalized(), L, L * 0.36, droop=0.3, seg=3, curl=0.3)

    def leaves_col(c, nrm, p):
        t = smooth(r * 0.2, r * 1.4, c.z)
        n = n01(c, 7, (2, 5, 1))
        return lerp3(lerp3(palette[0], palette[1], n), palette[2], t * 0.75)
    parts.append(piece(f"{name}_leaves", leaves, leaves_col, LEAF, up_normals=True, recalc=False))

    if flowers:
        def fl(bm):
            for _ in range(flowers):
                c = centers[rng.randrange(len(centers))]
                d = Vector((rng.gauss(0, 1), rng.gauss(0, 1), abs(rng.gauss(0, 1)) + 0.5)).normalized()
                p = c + d * r * 0.55
                for k in range(5):
                    a = 2 * math.pi * k / 5
                    leaf(bm, p, Vector((math.cos(a), math.sin(a), 0.4)), 0.035, 0.016, droop=0.1, seg=2)
                blob(bm, p + Vector((0, 0, 0.008)), 0.01, subdiv=1, amount=0.0)
        parts.append(piece(f"{name}_flowers", fl, lambda c, n, p: jit(rng, GLOW_PINK, 0.08), GLOW, up_normals=True, recalc=False))
    return merge(parts, name)


def build_berry_bush(rng, name, berry=GLOW_BLUE, glow=True, r=0.38):
    """Низкий кустик-черника: ветвистые прутики, мелкие листья, кисти ягод."""
    parts = []
    twigs = []

    def tw(bm):
        for i in range(13):
            a = rng.uniform(0, 6.28)
            out = Vector((math.cos(a), math.sin(a), 0))
            L = r * rng.uniform(0.7, 1.1)
            pts = bezier((0, 0, 0), out * L * 0.2 + Vector((0, 0, L * 0.95)), out * L * 0.6 + Vector((0, 0, L * 0.8)), 5)
            sweep(bm, pts, [0.014, 0.011, 0.009, 0.007, 0.004], seg=4, cap0=False, rng=rng, jitter=0.1)
            twigs.append(pts)
    parts.append(piece(f"{name}_twigs", tw, lambda c, n, p: lerp3(BARK_DARK, BARK, smooth(0, r, c.z)), MAT))

    def lv(bm):
        for pts in twigs:
            for k in range(1, len(pts)):
                for _ in range(3):
                    d = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.uniform(0.2, 1.0))).normalized()
                    leaf(bm, pts[k], d, rng.uniform(0.06, 0.09), 0.026, droop=0.1, seg=2, curl=0.4)
    parts.append(piece(f"{name}_leaves", lv, lambda c, n, p: lerp3(LEAF_GREEN, LEAF_LIME, smooth(0, r, c.z) * n01(c, 9, (1, 1, 1))),
                       LEAF, up_normals=True, recalc=False))

    def br(bm):
        for pts in twigs:
            if rng.random() < 0.25:
                continue
            p = pts[rng.randint(2, len(pts) - 1)]
            for _ in range(rng.randint(3, 6)):
                ico(bm, p + Vector((rng.uniform(-0.035, 0.035), rng.uniform(-0.035, 0.035), rng.uniform(-0.04, 0.01))),
                    rng.uniform(0.016, 0.024), subdiv=1)

    def br_col(c, nrm, p):
        return lerp3(lerp3(berry, (0.05, 0.05, 0.15), 0.35), berry, smooth(-0.3, 0.9, nrm.z))
    parts.append(piece(f"{name}_berries", br, br_col, GLOW if glow else MAT))
    return merge(parts, name)


def build_grass_tuft(rng, name, n=24, h=(0.25, 0.5), r=0.12, palette=(LEAF_DARK, LEAF_LIME)):
    def build(bm):
        for _ in range(n):
            a = rng.uniform(0, 6.28)
            d = rng.uniform(0, r)
            base = Vector((math.cos(a) * d, math.sin(a) * d, -0.01))
            out = Vector((math.cos(a), math.sin(a), 0)) * rng.uniform(0.15, 0.55) + Vector((0, 0, 1))
            blade(bm, base, out, rng.uniform(*h), rng.uniform(0.014, 0.024), droop=rng.uniform(0.2, 0.5))
    return piece(name, build, height_grad(palette[0], palette[1], 0.0, h[1] * 0.85), LEAF, up_normals=True, recalc=False)


def build_bellflowers(rng, name, col=GLOW_WHITE):
    """Ночные колокольчики: дугой изогнутые стебли, светящиеся колокольчики вниз, листья у земли."""
    parts = []
    bells = []

    def stems(bm):
        for i in range(rng.randint(4, 6)):
            a = rng.uniform(0, 6.28)
            d = rng.uniform(0, 0.08)
            base = Vector((math.cos(a) * d, math.sin(a) * d, 0))
            out = Vector((math.cos(a), math.sin(a), 0))
            h = rng.uniform(0.22, 0.4)
            pts = bezier(base, base + Vector((0, 0, h * 1.1)), base + out * h * 0.45 + Vector((0, 0, h * 0.85)), 6)
            sweep(bm, pts, 0.006, seg=3, cap0=False)
            bells.append((pts[-1], (pts[-1] - pts[-2]).normalized()))
            # вторая чашечка на ответвлении
            if rng.random() < 0.6:
                q = pts[3]
                tip = q + out.cross(Vector((0, 0, 1))) * 0.06 * rng.choice((-1, 1)) + Vector((0, 0, -0.01))
                sweep(bm, [q, (q + tip) / 2 + Vector((0, 0, 0.02)), tip], 0.004, seg=3, cap0=False)
                bells.append((tip, (tip - q).normalized()))
    parts.append(piece(f"{name}_stems", stems, lambda c, n, p: lerp3(LEAF_DARK, LEAF_GREEN, smooth(0, 0.3, c.z)), MAT))

    def bl(bm):
        for p, d in bells:
            down = (d * 0.3 + Vector((0, 0, -1))).normalized()
            rot = look_rot(down)
            seg = 6
            r0, r1, L = 0.012, 0.035, 0.05
            ring0 = [bm.verts.new(p + rot @ Vector((math.cos(2 * math.pi * k / seg) * r0, math.sin(2 * math.pi * k / seg) * r0, 0))) for k in range(seg)]
            ring1 = [bm.verts.new(p + rot @ Vector((math.cos(2 * math.pi * (k + 0.5) / seg) * r1 * 0.85,
                                                    math.sin(2 * math.pi * (k + 0.5) / seg) * r1 * 0.85, L * 0.75))) for k in range(seg)]
            ring2 = [bm.verts.new(p + rot @ Vector((math.cos(2 * math.pi * k / seg) * r1 * 1.1, math.sin(2 * math.pi * k / seg) * r1 * 1.1, L))) for k in range(seg)]
            bm.faces.new(ring0)
            for a_, b_ in ((ring0, ring1), (ring1, ring2)):
                for k in range(seg):
                    bm.faces.new((a_[k], a_[(k + 1) % seg], b_[(k + 1) % seg], b_[k]))
    parts.append(piece(f"{name}_bells", bl, lambda c, n, p: jit(rng, col, 0.05), GLOW, recalc=False))

    def lv(bm):
        for i in range(7):
            a = 2 * math.pi * i / 7 + rng.uniform(-0.3, 0.3)
            leaf(bm, (0, 0, 0.005), Vector((math.cos(a), math.sin(a), 0.5)), rng.uniform(0.1, 0.15), 0.03, droop=0.5, seg=3)
    parts.append(piece(f"{name}_leaves", lv, height_grad(LEAF_DARK, LEAF_TEAL, 0, 0.06), LEAF, up_normals=True, recalc=False))
    return merge(parts, name)


# ---------------------------------------------------------------- дерево: корни, ветки, пень, бревно

def bark_col(seed, moss=0.55):
    def fn(c, nrm, p):
        n = n01(c, 6.0, (seed, 0, 0))
        stripe = n01(Vector((c.x * 2, c.y * 2, c.z * 14)), 3.0, (0, seed, 2))
        col = lerp3(BARK_DARK, BARK, n)
        col = lerp3(col, BARK_LIGHT, smooth(0.6, 0.85, stripe) * 0.6)
        m = smooth(0.3, 0.85, nrm.z) * smooth(0.35, 0.75, n01(c, 3.5, (seed, 3, 1))) * moss
        return lerp3(col, lerp3(MOSS_A, MOSS_B, n), m)
    return fn


def root_curve(rng, start, out, length, height):
    a = Vector(start)
    o = Vector(out).normalized()
    pts = []
    n = 8
    for i in range(n):
        t = i / (n - 1)
        h = math.sin(math.pi * min(1, t * 1.05)) * height - 0.06 * t
        side = o.cross(Vector((0, 0, 1))) * math.sin(t * 5 + rng.random() * 3) * 0.06 * length
        pts.append(a + o * length * t + Vector((0, 0, h)) + side)
    return pts


def build_roots(rng, name, n=5, length=1.4, thick=0.11, arch=0.25):
    """Корни, выползающие из земли дугами (как будто тут стояло огромное дерево)."""
    def build(bm):
        for i in range(n):
            a = 2 * math.pi * i / n + rng.uniform(-0.4, 0.4)
            out = Vector((math.cos(a), math.sin(a), 0))
            L = length * rng.uniform(0.6, 1.0)
            pts = root_curve(rng, out * 0.1 + Vector((0, 0, 0.04)), out, L, arch * rng.uniform(0.6, 1.3))
            rr = [thick * (1 - t / 8) ** 1.3 + 0.01 for t in range(8)]
            sweep(bm, pts, rr, seg=7, rng=rng, jitter=0.12, cap0=True, cap1=True)
            # отросток
            k = rng.randint(2, 4)
            side = out.cross(Vector((0, 0, 1))) * rng.choice((-1, 1))
            sp = root_curve(rng, pts[k], (out + side).normalized(), L * 0.45, arch * 0.4)
            sweep(bm, sp, [rr[k] * 0.55 * (1 - j / 8) + 0.006 for j in range(8)], seg=5, rng=rng, jitter=0.1)
    return piece(name, build, bark_col(rng.random() * 10), MAT)


def build_stick(rng, name, L=0.9, r=0.03, twigs=3):
    def build(bm):
        pts = [Vector((-L / 2 + L * i / 5, rng.uniform(-0.03, 0.03), r * 0.9 + 0.01 * math.sin(i))) for i in range(6)]
        sweep(bm, pts, [r * (1 - i * 0.09) for i in range(6)], seg=6, rng=rng, jitter=0.1)
        for _ in range(twigs):
            k = rng.randint(1, 4)
            d = Vector((rng.uniform(0.2, 0.8), rng.choice((-1, 1)) * rng.uniform(0.5, 1), rng.uniform(0.0, 0.25))).normalized()
            ll = L * rng.uniform(0.15, 0.3)
            sweep(bm, [pts[k], pts[k] + d * ll * 0.6, pts[k] + d * ll + Vector((0, 0, -0.01))], [r * 0.5, r * 0.35, r * 0.15], seg=4)
    return piece(name, build, bark_col(rng.random() * 10, moss=0.3), MAT)


def build_stick_pile(rng, name):
    parts = []
    for i in range(6):
        s = build_stick(rng, f"{name}_{i}", L=rng.uniform(0.4, 0.8), r=rng.uniform(0.015, 0.03), twigs=rng.randint(0, 2))
        s.matrix_world = Matrix.Translation((rng.uniform(-0.15, 0.15), rng.uniform(-0.15, 0.15), i * 0.012)) @ \
            Matrix.Rotation(rng.uniform(0, 6.28), 4, "Z") @ Matrix.Rotation(rng.uniform(-0.1, 0.1), 4, "X")
        parts.append(s)
    parts.append(leaf_litter(rng, f"{name}_leaves", 0.35, 14))
    o = merge(parts, name)
    apply_transform(o)
    return o


def leaf_litter(rng, name, r, n):
    def build(bm):
        for _ in range(n):
            a = rng.uniform(0, 6.28)
            d = rng.uniform(0, r)
            base = Vector((math.cos(a) * d, math.sin(a) * d, 0.006 + rng.uniform(0, 0.01)))
            leaf(bm, base, Vector((math.cos(rng.uniform(0, 6.28)), math.sin(rng.uniform(0, 6.28)), 0.05)), rng.uniform(0.05, 0.08), 0.022,
                 droop=-0.08, seg=2, curl=0.3)
    cols = [(0.72, 0.38, 0.18), (0.8, 0.56, 0.22), (0.55, 0.24, 0.16), (0.62, 0.48, 0.3)]
    return piece(name, build, lambda c, n, p: cols[int(n01(c, 20, (5, 5, 5)) * 3.99)], LEAF, up_normals=True, recalc=False)


def build_stump(rng, name):
    """Сломанный пень: рваный верх, кольца, корни-лапы, мох и трутовики."""
    parts = []
    R, H, seg = 0.42, 0.55, 12
    top_tag = {}

    def build(bm):
        rings = []
        for j, (z, rr) in enumerate([(-0.05, 1.25), (0.08, 1.08), (H * 0.5, 1.0), (H, 0.98)]):
            ring = []
            for k in range(seg):
                a = 2 * math.pi * k / seg
                wob = 1 + 0.08 * math.sin(a * 3 + 1.3) + rng.uniform(-0.04, 0.04)
                zz = z
                if j == 3:  # рваный слом
                    zz = H + (0.18 if k % 3 == 0 else 0.06 if k % 3 == 1 else -0.02) * rng.uniform(0.5, 1.4)
                ring.append(bm.verts.new((math.cos(a) * R * rr * wob, math.sin(a) * R * rr * wob, zz)))
            rings.append(ring)
        for a_, b_ in zip(rings[:-1], rings[1:]):
            for k in range(seg):
                bm.faces.new((a_[k], a_[(k + 1) % seg], b_[(k + 1) % seg], b_[k]))
        # верх: внутреннее кольцо и центр
        inner = [bm.verts.new((v.co.x * 0.55, v.co.y * 0.55, H - 0.02)) for v in rings[-1]]
        cen = bm.verts.new((0, 0, H - 0.05))
        for k in range(seg):
            bm.faces.new((rings[-1][k], rings[-1][(k + 1) % seg], inner[(k + 1) % seg], inner[k]))
            bm.faces.new((inner[k], inner[(k + 1) % seg], cen))
        top_tag["z"] = H - 0.1
        # корни-лапы
        for i in range(5):
            a = 2 * math.pi * i / 5 + rng.uniform(-0.3, 0.3)
            out = Vector((math.cos(a), math.sin(a), 0))
            pts = [out * R * 0.8 + Vector((0, 0, 0.25)), out * R * 1.2 + Vector((0, 0, 0.08)), out * R * 1.8 + Vector((0, 0, 0.0)),
                   out * R * 2.3 + Vector((0, 0, -0.06))]
            sweep(bm, pts, [0.12, 0.09, 0.06, 0.03], seg=6, rng=rng, jitter=0.1)

    base_col = bark_col(rng.random() * 10, moss=0.7)

    def col(c, nrm, p):
        if nrm.z > 0.6 and c.z > top_tag["z"]:
            d = Vector((c.x, c.y)).length
            return WOOD_RING if int(d / 0.06) % 2 else WOOD_CUT
        return base_col(c, nrm, p)
    parts.append(piece(f"{name}_body", build, col, MAT))
    sf = build_shelf_fungus(rng, f"{name}_shelf")
    sf.matrix_world = Matrix.Translation((0, -R * 0.95, 0.1)) @ Matrix.Rotation(0, 4, "Z")
    parts.append(sf)
    parts += mushroom(rng, (R * 0.9, R * 0.6, 0.02), 0.14, 0.06, 0.04, (0.2, 0.6), GLOW_CYAN, glow=True, spots=3, spot_col=GLOW_WHITE, tag=f"{name}_m1")
    parts += mushroom(rng, (R * 1.05, R * 0.35, 0.0), 0.09, 0.04, 0.03, (0.3, 0.2), GLOW_CYAN, glow=True, tag=f"{name}_m2")
    parts.append(moss_patch(rng, (-R * 0.6, R * 0.5, 0), 0.3, f"{name}_moss"))
    o = merge(parts, name)
    apply_transform(o)
    return o


def build_log(rng, name, L=2.4, R=0.26):
    """Поваленное мшистое бревно с дуплом на торце, ветками-обломками и трутовиками."""
    parts = []
    seg = 10

    def build(bm):
        pts = [Vector((-L / 2 + L * i / 12, 0.03 * math.sin(i * 0.85), R * 0.85)) for i in range(13)]
        rings = sweep(bm, pts, [R * (1.08 - 0.025 * i) * (1 + 0.04 * math.sin(i * 2.1)) for i in range(13)], seg=seg, rng=rng, jitter=0.07, cap0=False, cap1=False)
        # торцы: кольцо коры → внутреннее кольцо → дупло внутрь
        for ring, sgn in ((rings[0], -1), (rings[-1], 1)):
            c = sum((v.co for v in ring), Vector()) / len(ring)
            inner = [bm.verts.new(c + (v.co - c) * 0.72) for v in ring]
            deep = [bm.verts.new(c + (v.co - c) * 0.5 + Vector((sgn * -0.25, 0, 0))) for v in ring]
            for k in range(seg):
                j = (k + 1) % seg
                bm.faces.new((ring[k], ring[j], inner[j], inner[k]))
                bm.faces.new((inner[k], inner[j], deep[j], deep[k]))
            bm.faces.new(deep)
        # обломки веток
        for _ in range(3):
            k = rng.randint(2, 10)
            d = Vector((rng.uniform(-0.3, 0.3), rng.choice((-1, 1)) * rng.uniform(0.3, 0.7), rng.uniform(0.6, 1))).normalized()
            p = pts[k] + d * R * 0.8
            sweep(bm, [p - d * 0.1, p + d * 0.18, p + d * 0.32], [0.055, 0.04, 0.02], seg=5)

    base_col = bark_col(rng.random() * 10, moss=0.85)

    def col(c, nrm, p):
        if abs(c.x) > L / 2 - 0.02 and abs(nrm.x) > 0.6:
            d = Vector((c.y, c.z - R * 0.85)).length
            if d < R * 0.55:
                return (0.12, 0.08, 0.07)
            return WOOD_RING if int(d / 0.05) % 2 else WOOD_CUT
        return base_col(c, nrm, p)
    parts.append(piece(f"{name}_body", build, col, MAT))
    for i, x in enumerate((-0.5, 0.35)):
        sf = build_shelf_fungus(rng, f"{name}_shelf{i}")
        sf.matrix_world = Matrix.Translation((x, -R * 0.95, R * 0.45)) @ Matrix.Scale(0.8, 4)
        parts.append(sf)
    parts += mushroom(rng, (0.75, 0.08, R * 1.7), 0.1, 0.05, 0.035, (0.15, 1.0), GLOW_CYAN, glow=True, spots=3, spot_col=GLOW_WHITE, tag=f"{name}_m1")
    parts += mushroom(rng, (0.82, -0.04, R * 1.7), 0.07, 0.035, 0.025, (0.25, 4.0), GLOW_CYAN, glow=True, tag=f"{name}_m2")
    parts.append(grass_ring(rng, (0.2, 0.2, 0), 0.5, 18, f"{name}_grass", h=(0.1, 0.22)))
    o = merge(parts, name)
    apply_transform(o)
    return o


def apply_transform(o):
    o.data.transform(o.matrix_world)
    o.matrix_world = Matrix()


# ---------------------------------------------------------------- камень и кристаллы

def build_pebbles(rng, name, n=7, r=0.35):
    def build(bm):
        for i in range(n):
            a = rng.uniform(0, 6.28)
            d = r * math.sqrt(rng.random())
            s = rng.uniform(0.04, 0.11) * (1.6 if i == 0 else 1)
            blob(bm, (math.cos(a) * d, math.sin(a) * d, s * 0.25), s, scale=(1.2, 1.0, 0.6), amount=0.3, subdiv=1, seed=rng.random() * 9)

    def col(c, nrm, p):
        col = lerp3(STONE_DARK, STONE, smooth(-0.3, 0.9, nrm.z) * 0.8 + n01(c, 14, (1, 2, 3)) * 0.2)
        return lerp3(col, MOSS_A, smooth(0.8, 0.95, nrm.z) * smooth(0.55, 0.7, n01(c, 12, (4, 4, 4))))
    return piece(name, build, col, MAT)


def crystal(bm, base, dirv, L, r, seg=6):
    """Кристалл: шестигранная призма, сужается, острая вершина со скосом."""
    rot = look_rot(dirv)
    rings = []
    for z, rr in ((-0.05, 0.9), (L * 0.15, 1.0), (L * 0.72, 0.88)):
        rings.append([bm.verts.new(Vector(base) + rot @ Vector((math.cos(2 * math.pi * k / seg) * r * rr, math.sin(2 * math.pi * k / seg) * r * rr, z)))
                      for k in range(seg)])
    tip = bm.verts.new(Vector(base) + rot @ Vector((r * 0.15, 0, L)))
    for a_, b_ in zip(rings[:-1], rings[1:]):
        for k in range(seg):
            bm.faces.new((a_[k], a_[(k + 1) % seg], b_[(k + 1) % seg], b_[k]))
    for k in range(seg):
        bm.faces.new((rings[-1][k], rings[-1][(k + 1) % seg], tip))
    bm.faces.new(list(reversed(rings[0])))


def build_crystal_cluster(rng, name, col, n=7, size=1.0):
    parts = []

    def rock(bm):
        blob(bm, (0, 0, 0.02), 0.28 * size, scale=(1.3, 1.1, 0.55), amount=0.3, subdiv=1, seed=rng.random() * 9)
        for _ in range(3):
            a = rng.uniform(0, 6.28)
            blob(bm, (math.cos(a) * 0.3 * size, math.sin(a) * 0.3 * size, 0), rng.uniform(0.07, 0.12) * size, scale=(1.2, 1, 0.7), amount=0.3, seed=rng.random() * 9)

    def rock_col(c, nrm, p):
        return lerp3(lerp3(STONE_DARK, STONE, smooth(-0.4, 0.9, nrm.z)), col, 0.12)
    parts.append(piece(f"{name}_rock", rock, rock_col, MAT))
    crys = []

    def cr(bm):
        for i in range(n):
            a = rng.uniform(0, 6.28)
            d = rng.uniform(0, 0.18) * size if i else 0
            lean = rng.uniform(0.1, 0.65) if i else rng.uniform(0, 0.15)
            dirv = Vector((math.cos(a) * math.sin(lean), math.sin(a) * math.sin(lean), math.cos(lean)))
            L = (rng.uniform(0.35, 0.7) if i else 0.95) * size
            base = Vector((math.cos(a) * d, math.sin(a) * d, 0.05 * size))
            crystal(bm, base, dirv, L, L * rng.uniform(0.13, 0.18))
            crys.append(base)
        for _ in range(5):  # мелкие осколки вокруг
            a = rng.uniform(0, 6.28)
            d = rng.uniform(0.3, 0.5) * size
            crystal(bm, (math.cos(a) * d, math.sin(a) * d, -0.02), Vector((math.cos(a) * 0.6, math.sin(a) * 0.6, 1)), rng.uniform(0.08, 0.16) * size, 0.025 * size)

    def cr_col(c, nrm, p):
        side = abs(nrm.x * 0.7 + nrm.y * 0.3)
        t = smooth(0, 0.9 * size, c.z)
        base = lerp3(lerp3(col, (0.1, 0.05, 0.2), 0.45), col, t)
        return lerp3(base, (1, 1, 1), 0.35 * smooth(0.6, 0.95, side) + 0.25 * smooth(0.7, 1.0, nrm.z))
    parts.append(piece(f"{name}_crystals", cr, cr_col, GLOW))
    return merge(parts, name)


# ---------------------------------------------------------------- живность

def wing_shape(bm, root, span, chord, feathers=5, side=1, droop=0.0):
    """Крыло птицы: перья веером. Ось крыла — +X*side от плеча (root)."""
    root = Vector(root)
    for i in range(feathers):
        t = i / (feathers - 1)
        a = math.radians(10 - 70 * t)
        d = Vector((math.cos(a) * side, -math.sin(-a) * 0.0 - 0.95 * t, -droop))
        L = span * (1.0 - 0.45 * t)
        base = root + Vector((side * span * 0.25 * (1 - t), 0.05 * t * chord, 0))
        leaf(bm, base, d.normalized(), L, chord * 0.45, droop=0.03, seg=3, fold=0.05)


def build_bird(rng, name):
    """Ночная птичка-зарянка: круглая тушка, рыжая грудка, светлые бровки. Крылья — WingL/WingR."""
    parts = []
    BODY = (0.24, 0.27, 0.44)
    BODY_L = (0.4, 0.44, 0.62)
    BREAST = (0.96, 0.52, 0.24)
    BELLY = (0.92, 0.88, 0.8)

    def body(bm):
        blob(bm, (0, 0, 0.075), 0.06, scale=(0.9, 1.25, 0.85), amount=0.05, subdiv=2)
        blob(bm, (0, -0.07, 0.125), 0.04, scale=(1, 1, 0.95), amount=0.03, subdiv=2)  # голова
        # хвост
        for s in (-1, 0, 1):
            leaf(bm, (0, 0.06, 0.085), Vector((s * 0.2, 1, 0.35)), 0.07, 0.018, droop=-0.05, seg=2, fold=0.05)

    def body_col(c, nrm, p):
        if c.y < -0.02 and nrm.y < -0.3 and c.z < 0.13:
            return lerp3(BREAST, BELLY, smooth(0.08, 0.03, c.z))
        if nrm.z < -0.4:
            return BELLY
        return lerp3(BODY, BODY_L, smooth(0.05, 0.16, c.z) * 0.6 + n01(c, 40, (1, 1, 1)) * 0.3)
    parts.append(piece(f"{name}_body", body, body_col, MAT))

    def beak(bm):
        cylinder(bm, (0, -0.112, 0.122), 0.011, 0.0, 0.03, seg=5, rot=look_rot((0, -1, -0.15)))
    parts.append(piece(f"{name}_beak", beak, lambda c, n, p: (0.95, 0.72, 0.3), MAT))

    def eyes(bm):
        for s in (-1, 1):
            ico(bm, (s * 0.027, -0.093, 0.135), 0.008, subdiv=1)
    parts.append(piece(f"{name}_eyes", eyes, lambda c, n, p: (0.03, 0.03, 0.05) if n.y > -0.5 else (0.95, 0.95, 1.0), MAT))

    def brows(bm):
        for s in (-1, 1):
            leaf(bm, (s * 0.02, -0.095, 0.148), Vector((s, 0.4, 0.15)), 0.025, 0.006, droop=0, seg=2, fold=0)
    parts.append(piece(f"{name}_brows", brows, lambda c, n, p: GLOW_WHITE, GLOW, recalc=False))

    def legs(bm):
        for s in (-1, 1):
            sweep(bm, [(s * 0.018, 0.0, 0.03), (s * 0.02, -0.005, 0.0)], 0.004, seg=4)
            for a in (-0.5, 0, 0.5):
                sweep(bm, [(s * 0.02, -0.005, 0.002), (s * 0.02 + math.sin(a) * 0.018, -0.005 - math.cos(a) * 0.018, 0.0)], 0.0025, seg=3)
    parts.append(piece(f"{name}_legs", legs, lambda c, n, p: (0.55, 0.42, 0.3), MAT))
    body_obj = merge(parts, name)

    wings = []
    for side, wname in ((1, "WingL"), (-1, "WingR")):
        def wb(bm, side=side):
            wing_shape(bm, (0, 0, 0), 0.09, 0.07, feathers=5, side=side)

        def wc(c, nrm, p):
            t = abs(c.x) / 0.1
            return lerp3(BODY, (0.12, 0.13, 0.25), smooth(0.4, 1.0, t))
        w = piece(f"{name}_{wname}", wb, wc, LEAF, up_normals=True, recalc=False)
        w.name = wname
        w.data.name = f"{name}_{wname}"
        w.parent = body_obj
        w.location = (side * 0.045, -0.005, 0.1)  # плечо
        # крылья раскрыты; сложить/махать — скрипт Critter (поворот вокруг оси «вперёд»)
        wings.append(w)
    return body_obj, wings


def build_moth(rng, name):
    """Ночной мотылёк: пушистое тельце, усики-перья, крылья со светящимися «глазками»."""
    parts = []
    FUR = (0.62, 0.6, 0.72)

    def body(bm):
        blob(bm, (0, 0.0, 0), 0.012, scale=(1, 2.6, 1), amount=0.15, subdiv=1)
        blob(bm, (0, -0.03, 0.002), 0.01, amount=0.1, subdiv=1)
        for s in (-1, 1):
            sweep(bm, [(s * 0.004, -0.038, 0.006), (s * 0.018, -0.06, 0.02), (s * 0.03, -0.07, 0.022)], [0.0018, 0.0016, 0.001], seg=3)
    parts.append(piece(f"{name}_body", body, lambda c, n, p: lerp3((0.3, 0.28, 0.4), FUR, smooth(-0.6, 1, n.z)), MAT))
    body_obj = merge(parts, name)
    wings = []
    for side, wname in ((1, "WingL"), (-1, "WingR")):
        def wb(bm, side=side):
            leaf(bm, (0, -0.01, 0), Vector((side, -0.45, 0)), 0.055, 0.024, droop=0.0, seg=4, fold=0.02)  # переднее
            leaf(bm, (0, 0.0, -0.001), Vector((side, 0.55, 0)), 0.04, 0.02, droop=0.0, seg=3, fold=0.02)    # заднее

        def wc(c, nrm, p):
            d = Vector((c.x - side * 0.03, c.y + 0.015, 0)).length
            if d < 0.009:
                return GLOW_CYAN
            return lerp3((0.55, 0.52, 0.75), (0.85, 0.82, 0.95), smooth(0.0, 0.05, abs(c.x)))
        w = piece(f"{name}_{wname}", wb, wc, GLOW, up_normals=True, recalc=False)
        w.name = wname
        w.data.name = f"{name}_{wname}"
        w.parent = body_obj
        w.location = (side * 0.004, -0.012, 0.004)
        wings.append(w)
    return body_obj, wings


def build_beetle(rng, name):
    """Жук-светлячок: тёмно-синие надкрылья с переливом, светящееся брюшко, лапки и усики."""
    parts = []
    SHELL = (0.12, 0.22, 0.42)
    SHELL_L = (0.3, 0.6, 0.75)

    def shell(bm):
        blob(bm, (0, 0.005, 0.017), 0.024, scale=(0.9, 1.25, 0.62), amount=0.02, subdiv=2)
        blob(bm, (0, -0.028, 0.012), 0.011, scale=(1.1, 0.8, 0.7), amount=0.02, subdiv=1)  # голова

    def shell_col(c, nrm, p):
        if nrm.z < 0.1:
            return (0.08, 0.08, 0.12)
        seam = smooth(0.004, 0.0, abs(c.x)) if c.y > -0.012 else 0
        col = lerp3(SHELL, SHELL_L, smooth(0.4, 1.0, nrm.z) * 0.7 + smooth(0.0, 0.02, c.y) * 0.2)
        return lerp3(col, (0.05, 0.05, 0.1), seam)
    parts.append(piece(f"{name}_shell", shell, shell_col, MAT))

    def glow(bm):
        blob(bm, (0, 0.03, 0.01), 0.011, scale=(0.9, 0.9, 0.6), amount=0.0, subdiv=1)
        for s in (-1, 1):
            ico(bm, (s * 0.01, 0.0, 0.029), 0.0045, subdiv=1)
    parts.append(piece(f"{name}_glow", glow, lambda c, n, p: GLOW_GOLD, GLOW))

    def legs(bm):
        for s in (-1, 1):
            for k, y in enumerate((-0.012, 0.0, 0.013)):
                a = Vector((s * 0.012, y, 0.01))
                b = Vector((s * 0.024, y + (k - 1) * 0.008, 0.014))
                c = Vector((s * 0.03, y + (k - 1) * 0.014, 0.0))
                sweep(bm, [a, b, c], [0.0022, 0.0018, 0.0012], seg=3)
            sweep(bm, [(s * 0.005, -0.036, 0.016), (s * 0.014, -0.05, 0.026), (s * 0.02, -0.058, 0.03)], 0.0012, seg=3)
    parts.append(piece(f"{name}_legs", legs, lambda c, n, p: (0.08, 0.08, 0.12), MAT))
    return merge(parts, name)


# ---------------------------------------------------------------- экспорт

def export_fbx(path, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", bake_space_transform=False,
        mesh_smooth_type="FACE", use_mesh_modifiers=True, colors_type="SRGB",
        add_leaf_bones=False, bake_anim=False, path_mode="STRIP")


def args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]

    def get(n, d=None):
        return argv[argv.index(n) + 1] if n in argv else d
    return os.path.abspath(get("--out", "forest_details_out")), get("--preview")


def build_all(rng):
    """Возвращает [(имя, [объекты для FBX])]."""
    items = []

    def add(o, extra=()):
        items.append((o.name, [o] + list(extra)))

    add(build_mushroom_glow_cluster(rng, "Det_Shroom_Glow_A", GLOW_CYAN, n=6))
    add(build_mushroom_glow_cluster(rng, "Det_Shroom_Glow_B", GLOW_BLUE, n=4, spread=0.15))
    add(build_mushroom_tall(rng, "Det_Shroom_Tall"))
    add(build_toadstool(rng, "Det_Shroom_Toadstool"))
    add(build_berry_bush(rng, "Det_Berry_Blue", GLOW_BLUE, glow=True))
    add(build_berry_bush(rng, "Det_Berry_Red", BERRY_RED, glow=False, r=0.32))
    add(build_fern(rng, "Det_Fern_A"))
    add(build_fern(rng, "Det_Fern_B", fronds=7, size=0.42))
    add(build_bush(rng, "Det_Bush_Round"))
    add(build_bush(rng, "Det_Bush_Blue", r=0.45, leafy=60, palette=(LEAF_BLUE, LEAF_TEAL, (0.5, 0.7, 0.85))))
    add(build_bush(rng, "Det_Bush_Flower", r=0.4, leafy=50, palette=(LEAF_GREEN, LEAF_TEAL, LEAF_LIME), flowers=9))
    add(build_grass_tuft(rng, "Det_Grass_Tall"))
    add(build_grass_tuft(rng, "Det_Grass_Blue", n=16, h=(0.15, 0.3), r=0.1, palette=(LEAF_DARK, (0.45, 0.65, 0.85))))
    add(build_bellflowers(rng, "Det_Flower_Bells"))
    add(build_bellflowers(rng, "Det_Flower_Bells_Pink", GLOW_PINK))
    add(build_roots(rng, "Det_Roots_Big"))
    add(build_roots(rng, "Det_Roots_Small", n=3, length=0.8, thick=0.06, arch=0.15))
    add(build_stick(rng, "Det_Stick_A"))
    add(build_stick(rng, "Det_Stick_B", L=0.6, r=0.022, twigs=2))
    add(build_stick_pile(rng, "Det_Stick_Pile"))
    add(build_stump(rng, "Det_Stump"))
    add(build_log(rng, "Det_Log_Mossy"))
    add(build_shelf_fungus(rng, "Det_Shroom_Shelf"))
    add(build_pebbles(rng, "Det_Pebbles"))
    add(leaf_litter(rng, "Det_Leaves", 0.4, 22))
    add(build_crystal_cluster(rng, "Det_Crystal_Violet", GLOW_VIOLET))
    add(build_crystal_cluster(rng, "Det_Crystal_Gold", GLOW_GOLD, n=5, size=0.8))
    add(build_crystal_cluster(rng, "Det_Crystal_Small", GLOW_VIOLET, n=4, size=0.5))
    b, w = build_bird(rng, "Det_Bird")
    add(b, w)
    m, w = build_moth(rng, "Det_Moth")
    add(m, w)
    add(build_beetle(rng, "Det_Beetle"))
    return items


# ---------------------------------------------------------------- превью

def render_preview(path, items):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 40
    sc.cycles.device = "CPU"
    sc.render.resolution_x, sc.render.resolution_y = 2000, 1250
    sc.view_settings.view_transform = "AgX"
    w = bpy.data.worlds.new("w")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.07, 0.16, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 1.6

    for m in list(bpy.data.materials):
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        o = nt.nodes.new("ShaderNodeOutputMaterial")
        b = nt.nodes.new("ShaderNodeBsdfPrincipled")
        b.inputs["Roughness"].default_value = 0.85
        a = nt.nodes.new("ShaderNodeVertexColor")
        a.layer_name = "Col"
        nt.links.new(a.outputs[0], b.inputs["Base Color"])
        if m.name == GLOW:
            nt.links.new(a.outputs[0], b.inputs["Emission Color"])
            b.inputs["Emission Strength"].default_value = 2.5
        nt.links.new(b.outputs[0], o.inputs[0])

    # раскладка сеткой
    cols = 8
    sizes = {"Det_Log_Mossy": 2.6, "Det_Roots_Big": 2.6, "Det_Stump": 1.8}
    x, y, row_h = 0.0, 0.0, 0.0
    i = 0
    for name, objs in items:
        o = objs[0]
        s = sizes.get(name, 1.1)
        if i and i % cols == 0:
            x = 0.0
            y += 1.6
        o.location = (x + s / 2, y, 0)
        if name in ("Det_Bird", "Det_Moth", "Det_Beetle"):
            o.scale = (4, 4, 4) if name != "Det_Bird" else (3, 3, 3)
        x += s + 0.2
        i += 1

    bpy.ops.mesh.primitive_plane_add(size=40, location=(5, 2.5, 0))
    g = bpy.context.active_object
    gm = bpy.data.materials.new("ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.05, 0.06, 0.12, 1)
    g.data.materials.append(gm)

    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.location = (5.3, -5.2, 7.2)
    cam.rotation_euler = (math.radians(42), 0, 0)
    cam.data.lens = 30
    key = bpy.data.objects.new("key", bpy.data.lights.new("key", "SUN"))
    key.data.energy = 2.0
    key.data.color = (0.7, 0.8, 1.0)
    key.rotation_euler = (math.radians(40), 0, math.radians(-25))
    sc.collection.objects.link(key)
    sc.render.filepath = os.path.abspath(path)
    bpy.ops.render.render(write_still=True)


def main():
    out, preview = args()
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rng = random.Random(SEED)
    items = build_all(rng)
    total = 0
    for name, objs in items:
        tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs)
        total += tris
        print(f"[forest_details] {name}: ~{tris} tris")
        export_fbx(os.path.join(out, name + ".fbx"), objs)
    print(f"[forest_details] {len(items)} моделей, ~{total} треугольников всего")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "forest_details.blend"), compress=True)
    if preview:
        render_preview(preview, items)
    print("[forest_details] готово:", out)


if __name__ == "__main__":
    main()
