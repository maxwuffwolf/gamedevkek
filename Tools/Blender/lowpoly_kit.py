# Общие кирпичики для генераторов лоуполи-моделей (ravine_props.py, hero_build.py)
# и сам ржавый меч героя (build_rusty_sword) — один и тот же меч в руке, в земле и на иконке.
#
# Стиль: гранёный лоуполи, цвет на каждую грань в атрибуте вершин "Col" (в Unity шейдер
# Game/Character Toon с галкой Vertex Colors). Без текстур — модели весят десятки КБ.
#
# Единицы: метры, Z вверх, «перед» модели смотрит в -Y (после экспорта в Unity это +Z).

import math

import bpy  # bpy первым: в режиме модуля bmesh доступен только после него
import bmesh
from mathutils import Matrix, Vector, noise

# ---------------------------------------------------------------- палитра (sRGB 0..1)

STEEL = (0.46, 0.46, 0.48)
STEEL_LIGHT = (0.62, 0.62, 0.64)
RUST = (0.52, 0.24, 0.09)
RUST_DARK = (0.24, 0.11, 0.05)
RUST_ORANGE = (0.66, 0.33, 0.11)
IRON = (0.17, 0.15, 0.14)
LEATHER = (0.27, 0.16, 0.09)
LEATHER_DARK = (0.16, 0.10, 0.06)
WOOD = (0.36, 0.26, 0.16)
WOOD_DARK = (0.22, 0.15, 0.09)
BONE = (0.80, 0.75, 0.62)
BONE_DARK = (0.52, 0.47, 0.37)
CHAIN = (0.30, 0.30, 0.31)
CLOTH_RED = (0.40, 0.11, 0.09)
CLOTH_RED_DARK = (0.22, 0.06, 0.05)
CLOTH_GOLD = (0.62, 0.50, 0.24)
CLOAK = (0.17, 0.19, 0.23)
CLOAK_DARK = (0.09, 0.10, 0.12)
MOSS = (0.24, 0.36, 0.19)
SOOT = (0.05, 0.045, 0.04)


# ---------------------------------------------------------------- утилиты

def lerp3(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def smooth(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def n01(p, scale=1.0, offset=(0.0, 0.0, 0.0)):
    """Шум Перлина 0..1 в точке."""
    return noise.noise(Vector(p) * scale + Vector(offset)) * 0.5 + 0.5


def get_material(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)


def new_object(name, bm, material_name):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    mesh.materials.append(get_material(material_name))
    for p in mesh.polygons:
        p.use_smooth = False
    return obj


def face_colors(obj, color_fn):
    """Цвет на каждую грань (по центру грани) → гранёный лоуполи-вид. color_fn(center, normal, face) -> rgb."""
    mesh = obj.data
    attr = mesh.color_attributes.get("Col") or mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    for p in mesh.polygons:
        c = color_fn(Vector(p.center), Vector(p.normal), p)
        for li in p.loop_indices:
            attr.data[li].color_srgb = (c[0], c[1], c[2], 1.0)
    mesh.color_attributes.active_color = attr
    mesh.color_attributes.render_color_index = mesh.color_attributes.active_color_index


def part(name, build_fn, color_fn, material):
    """Собрать кусок модели: build_fn(bm) строит геометрию, color_fn красит грани."""
    bm = bmesh.new()
    build_fn(bm)
    obj = new_object(name, bm, material)
    face_colors(obj, color_fn)
    return obj


def join(objs, name):
    """Склеить объекты в один меш (материалы и цвета граней сохраняются)."""
    objs = [o for o in objs if o is not None]
    with bpy.context.temp_override(active_object=objs[0], object=objs[0],
                                   selected_objects=objs, selected_editable_objects=objs):
        bpy.ops.object.join()
    obj = objs[0]
    obj.name = name
    obj.data.name = name
    return obj


# ---------------------------------------------------------------- примитивы (сразу в мировых координатах)

def sweep(bm, pts, radii, seg=6, sx=1.0, sy=1.0, cap0=True, cap1=True, ref=None, twist=0.0, jitter=0.0, rng=None):
    """Труба вдоль ломаной: кость, рукоять, рукав. radii — число или список на каждую точку.
    sx/sy — сплюснутость сечения (по «нормали» и «бинормали» рамки), twist — поворот сечения по длине (рад)."""
    pts = [Vector(p) for p in pts]
    n = len(pts)
    tans = []
    for i in range(n):
        if i == 0:
            t = pts[1] - pts[0]
        elif i == n - 1:
            t = pts[-1] - pts[-2]
        else:
            t = (pts[i + 1] - pts[i]).normalized() + (pts[i] - pts[i - 1]).normalized()
        tans.append(t.normalized())
    r0 = Vector(ref) if ref is not None else Vector((0, 0, 1))
    if abs(tans[0].dot(r0)) > 0.95:
        r0 = Vector((1, 0, 0)) if abs(tans[0].x) < 0.9 else Vector((0, 1, 0))
    nrm = (r0 - tans[0] * r0.dot(tans[0])).normalized()
    rings = []
    for i in range(n):
        if i > 0:
            nrm = tans[i - 1].rotation_difference(tans[i]) @ nrm
            nrm = (nrm - tans[i] * nrm.dot(tans[i])).normalized()
        bin_ = tans[i].cross(nrm)
        r = radii[i] if isinstance(radii, (list, tuple)) else radii
        ring = []
        for k in range(seg):
            a = 2 * math.pi * k / seg + twist * i / max(1, n - 1)
            rr = r * (1 + (rng.uniform(-jitter, jitter) if (jitter and rng) else 0))
            ring.append(bm.verts.new(pts[i] + nrm * math.cos(a) * rr * sx + bin_ * math.sin(a) * rr * sy))
        rings.append(ring)
    for a_, b_ in zip(rings[:-1], rings[1:]):
        for k in range(seg):
            j = (k + 1) % seg
            bm.faces.new((a_[k], a_[j], b_[j], b_[k]))
    if cap0:
        bm.faces.new(list(reversed(rings[0])))
    if cap1:
        bm.faces.new(rings[-1])
    return rings


def ico(bm, center, radius, scale=(1, 1, 1), rot=None, subdiv=1, deform=None):
    """Икосфера. deform(local_co) -> local_co (до масштаба/поворота) — вмятины, глазницы."""
    res = bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    m = Matrix.Translation(Vector(center)) @ (rot.to_4x4() if rot is not None else Matrix())
    for v in res["verts"]:
        co = v.co.copy()
        if deform:
            co = deform(co)
        co = Vector((co.x * scale[0], co.y * scale[1], co.z * scale[2]))
        v.co = m @ co
    return res["verts"]


def box(bm, center, half, rot=None):
    m = Matrix.Translation(Vector(center)) @ (rot.to_4x4() if rot is not None else Matrix())
    res = bmesh.ops.create_cube(bm, size=2.0)
    for v in res["verts"]:
        v.co = m @ Vector((v.co.x * half[0], v.co.y * half[1], v.co.z * half[2]))
    return res["verts"]


def cylinder(bm, center, r1, r2, depth, seg=8, rot=None, caps=True):
    """Цилиндр/конус вдоль локальной Z."""
    m = Matrix.Translation(Vector(center)) @ (rot.to_4x4() if rot is not None else Matrix())
    res = bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=depth)
    for v in res["verts"]:
        v.co = m @ v.co
    return res["verts"]


def look_rot(forward, up=(0, 0, 1)):
    """Матрица поворота: локальная Z смотрит вдоль forward."""
    f = Vector(forward).normalized()
    u = Vector(up)
    if abs(f.dot(u.normalized())) > 0.98:
        u = Vector((0, 1, 0))
    x = u.cross(f).normalized()
    y = f.cross(x)
    return Matrix((x, y, f)).transposed()


def rust_metal(c, base=STEEL, amount=0.5, scale=14.0, seed=0.0):
    """Цвет ржавого металла: пятна ржавчины по шуму, тёмные каверны."""
    n1 = n01(c, scale, (seed, 1.7, 0))
    n2 = n01(c, scale * 3.1, (0, seed, 4.2))
    t0 = 0.78 - amount * 0.42  # amount ≈ доля поверхности в ржавчине
    rust = smooth(t0, t0 + 0.14, n1 * 0.7 + n2 * 0.3)
    col = lerp3(base, RUST, rust)
    col = lerp3(col, RUST_ORANGE, smooth(0.55, 0.8, n2) * rust * 0.6)
    col = lerp3(col, RUST_DARK, smooth(0.72, 0.95, n2) * rust)
    return col


def mossy(col, c, nrm, amount=0.5, seed=0.0):
    """Мох на гранях, смотрящих вверх."""
    up = smooth(0.35, 0.85, nrm.z)
    m = smooth(0.55, 0.75, n01(c, 9.0, (seed, 3.3, 0.5))) * up * amount
    return lerp3(col, MOSS, m)


# ---------------------------------------------------------------- ржавый меч

SWORD_SEED = 24


def build_rusty_sword(name="Wpn_Sword_Rusty", material="M_Sword_Rusty", seed=SWORD_SEED):
    """Ржавый одноручный меч. Лежит вдоль +Z, точка опоры — центр рукояти (как держит рука).
    Клинок с долом и фасками, сколы на кромках, изогнутая гарда с шишечками, рукоять в полусгнившей
    кожаной обмотке с железными кольцами, дисковое навершие с заклёпкой.
    seed фиксирован: меч в руке героя (hero_build.py) и в земле (ravine_props.py) — один и тот же."""
    import random
    rng = random.Random(seed)
    guard_z = 0.096
    blade_z0 = guard_z + 0.012
    blade_len = 0.76
    tip_z = blade_z0 + blade_len + 0.035

    def blade(bm):
        rows = 22
        prev = None
        for r in range(rows + 1):
            t = r / rows
            z = blade_z0 + blade_len * t
            if r == 0:
                w = 0.028  # рикассо — чуть уже клинка
            elif t < 0.86:
                w = 0.035 * (1 - 0.24 * t)
            else:
                w = 0.035 * 0.79 * (1 - t) / 0.14
            w = max(w, 0.003)
            th = 0.0075 * (1 - 0.55 * t)
            fd = 0.5 * (1 - smooth(0.55, 0.7, t)) if r > 1 else 0.0  # глубина дола
            wr, wl = w, w
            if 0 < r < rows - 1 and rng.random() < 0.4:  # сколы и зазубрины
                bite = rng.uniform(0.004, 0.011) * (1 - 0.4 * t)
                if rng.random() < 0.5:
                    wr -= bite
                else:
                    wl -= bite
            if r == rows:
                ring = [bm.verts.new((0, 0, tip_z))]
            else:
                prof = [(wr, 0), (0.55 * wr, th), (0.22 * w, th), (0.12 * w, th * (1 - fd)), (-0.12 * w, th * (1 - fd)),
                        (-0.22 * w, th), (-0.55 * wl, th), (-wl, 0), (-0.55 * wl, -th), (-0.22 * w, -th),
                        (-0.12 * w, -th * (1 - fd)), (0.12 * w, -th * (1 - fd)), (0.22 * w, -th), (0.55 * wr, -th)]
                ring = [bm.verts.new((x, y, z)) for x, y in prof]
            if prev:
                m = len(prev)
                if len(ring) == 1:
                    for i in range(m):
                        bm.faces.new((prev[i], prev[(i + 1) % m], ring[0]))
                else:
                    for i in range(m):
                        bm.faces.new((prev[i], prev[(i + 1) % m], ring[(i + 1) % m], ring[i]))
            else:
                bm.faces.new(list(reversed(ring)))
            prev = ring

    def blade_col(c, nrm, p):
        t = (c.z - blade_z0) / blade_len
        col = rust_metal(c, STEEL, amount=0.5 + 0.25 * (1 - t), scale=16.0, seed=2.0)
        if abs(c.x) > 0.022 and n01(c, 30.0) < 0.55:
            col = lerp3(col, STEEL_LIGHT, 0.45)  # кромку недавно правили камнем — светлее
        if abs(c.x) < 0.005 and t < 0.6:
            col = lerp3(col, RUST_DARK, 0.5)  # в доле грязь
        if t < 0.04:
            col = lerp3(col, RUST_DARK, 0.4)
        return col

    def guard(bm):
        # крестовина: концы загнуты к клинку
        pts = []
        for i in range(9):
            x = -0.108 + 0.216 * i / 8
            k = abs(x) / 0.108
            pts.append((x, 0, guard_z - 0.004 + 0.02 * k * k))
        radii = [0.0085 + 0.0035 * (1 - abs(i - 4) / 4) for i in range(9)]
        sweep(bm, pts, radii, seg=6, sx=1.0, sy=1.35)
        for sgn in (-1, 1):
            ico(bm, (sgn * 0.112, 0, guard_z + 0.018), 0.0115, scale=(1, 1, 1.15), subdiv=1)
        # центральный блок с язычками на клинок
        box(bm, (0, 0, guard_z), (0.021, 0.016, 0.016))
        for sgn in (-1, 1):
            cylinder(bm, (0, sgn * 0.0165, guard_z), 0.006, 0.005, 0.004, seg=6, rot=Matrix.Rotation(math.pi / 2, 3, "X"))
        box(bm, (0, 0, guard_z + 0.024), (0.01, 0.0095, 0.01))

    def guard_col(c, nrm, p):
        col = rust_metal(c, IRON, amount=0.6, scale=40.0, seed=5.0)
        if abs(c.y) > 0.015 and abs(c.x) < 0.008:
            col = lerp3(col, STEEL, 0.4)  # заклёпки
        return col

    def grip(bm):
        seg = 8
        n = 15
        z0, z1 = -0.082, 0.083
        pts = [(0, 0, z0 + (z1 - z0) * i / (n - 1)) for i in range(n)]
        radii = []
        for i in range(n):
            if 3 <= i <= 5:
                radii.append(0.0128)  # обмотка сгнила — видно дерево
            else:
                radii.append(0.0148 + (0.0022 if i % 2 else 0.0))
        sweep(bm, pts, radii, seg=seg, twist=math.pi * 0.9, ref=(1, 0, 0))
        # железные кольца у гарды и навершия
        cylinder(bm, (0, 0, z1 + 0.002), 0.0168, 0.0168, 0.01, seg=10)
        cylinder(bm, (0, 0, z0 - 0.001), 0.0165, 0.0165, 0.009, seg=10)
        # обрывок обмотки болтается
        sweep(bm, [(0.014, 0.0, -0.015), (0.024, -0.004, -0.035), (0.028, -0.002, -0.06), (0.026, 0.003, -0.075)],
              [0.0045, 0.0042, 0.0038, 0.0032], seg=4, sx=1.0, sy=0.3, ref=(0, 1, 0))

    def grip_col(c, nrm, p):
        r = math.hypot(c.x, c.y)
        if abs(c.z - 0.085) < 0.007 or abs(c.z + 0.083) < 0.007:
            return rust_metal(c, IRON, amount=0.7, scale=60.0)
        if r < 0.0135 and -0.06 < c.z < -0.02:
            return lerp3(WOOD, WOOD_DARK, n01(c, 80.0))
        if c.x > 0.019:
            return LEATHER_DARK
        stripe = (math.atan2(c.y, c.x) / (2 * math.pi) + c.z * 9.0) % 1.0
        return lerp3(LEATHER, LEATHER_DARK, 0.25 + 0.6 * smooth(0.4, 0.6, stripe) * n01(c, 40.0))

    def pommel(bm):
        rx = Matrix.Rotation(math.pi / 2, 3, "X")
        cylinder(bm, (0, 0, -0.113), 0.027, 0.027, 0.018, seg=10, rot=rx)
        cylinder(bm, (0, 0, -0.113), 0.0145, 0.0145, 0.026, seg=8, rot=rx)
        cylinder(bm, (0, 0, -0.142), 0.008, 0.0065, 0.012, seg=6)

    def pommel_col(c, nrm, p):
        col = rust_metal(c, IRON, amount=0.55, scale=50.0, seed=9.0)
        if abs(nrm.y) > 0.8 and math.hypot(c.x, c.z + 0.113) < 0.015:
            col = lerp3(col, STEEL, 0.35)
        return col

    objs = [
        part(name + "_blade", blade, blade_col, material),
        part(name + "_guard", guard, guard_col, material),
        part(name + "_grip", grip, grip_col, material),
        part(name + "_pommel", pommel, pommel_col, material),
    ]
    return join(objs, name)


# длина меча от центра рукояти до острия (для «воткнуть в землю»)
SWORD_TIP = 0.096 + 0.012 + 0.76 + 0.035
