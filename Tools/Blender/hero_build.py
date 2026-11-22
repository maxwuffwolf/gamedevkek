# Сборка модели героя (Странника) из CC0-базы Quaternius + собственных лоуполи-деталей.
#
# Что делает:
#   * берёт Superhero_Male из Universal Base Characters [Standard] (CC0, Humanoid-риг);
#   * генерирует свои детали и крепит их к костям:
#       Hero_Hair        — растрёпанные золотые пряди средней длины (к кости Head);
#       Hero_Loincloth   — набедренная повязка из мешковины с обгоревшим краем (скиннинг к тазу/бёдрам);
#       Hero_Bracer      — иномирный наруч на левом предплечье, грани, цвета побежалости на краях;
#       Hero_BracerCrystal — двухцветный кристалл в гнезде наруча (половины помечены цветом вершин);
#       Wpn_Sword_Rusty  — ржавый меч в правой руке (к кости hand_r), модель из lowpoly_kit.py;
#   * помечает цветом вершин правый/левый глаз (для гетерохромии в шейдере);
#   * готовит текстуры: кожа, глаза (с маской радужки в альфе), брови;
#   * экспортирует Hero.fbx (+ отдельно Wpn_Sword_Rusty.fbx для лута/скелета) и картинку-превью.
#   (Поменять только меч, не пересобирая героя: ravine_props.py --hero hero.blend.)
#
# Запуск (Blender 4.x/5.x или pip-пакет bpy):
#   blender -b -P hero_build.py -- --ubc "<папка Universal Base Characters[Standard]>" --out <папка> [--preview hero.png]
#   python hero_build.py --ubc ... --out ... (если стоит pip install bpy)
# Результат кладётся в --out; в Unity это Game/Assets/_Project/Art/Models/Characters/Hero/.

import math
import os
import random
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402  bpy первым: в режиме модуля bmesh доступен только после него
import bmesh  # noqa: E402
from mathutils import Matrix, Vector, noise  # noqa: E402

from lowpoly_kit import build_rusty_sword  # noqa: E402

SEED = 11

# ---------------------------------------------------------------- цвета (sRGB 0..1)

HAIR_ROOT = (0.55, 0.36, 0.10)
HAIR_MID = (0.86, 0.62, 0.22)
HAIR_TIP = (1.00, 0.86, 0.48)

BURLAP = (0.46, 0.37, 0.25)
BURLAP_DARK = (0.30, 0.23, 0.15)
CHAR = (0.10, 0.08, 0.07)

METAL = (0.075, 0.08, 0.095)
METAL_EDGE = (0.17, 0.18, 0.21)
HEAT_BRONZE = (0.32, 0.18, 0.08)
HEAT_PURPLE = (0.20, 0.12, 0.28)
HEAT_BLUE = (0.10, 0.16, 0.30)

STEEL = (0.46, 0.46, 0.48)
RUST = (0.52, 0.24, 0.09)
RUST_DARK = (0.24, 0.11, 0.05)
IRON = (0.17, 0.15, 0.14)
LEATHER = (0.27, 0.16, 0.09)
WOOD = (0.36, 0.26, 0.16)


def args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    def get(name, default=None):
        return argv[argv.index(name) + 1] if name in argv else default
    ubc = get("--ubc")
    if not ubc:
        raise SystemExit("Укажи --ubc <папка Universal Base Characters[Standard]>")
    return ubc, os.path.abspath(get("--out", "hero_out")), get("--preview")


# ---------------------------------------------------------------- утилиты

def lerp3(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def smooth(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def new_object(name, bm, material_name):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    mat = bpy.data.materials.get(material_name) or bpy.data.materials.new(material_name)
    mesh.materials.append(mat)
    for p in mesh.polygons:
        p.use_smooth = False
    return obj


def face_colors(obj, color_fn):
    """Цвет на каждую грань (по центру грани) → гранёный лоуполи-вид. color_fn(center, normal, face) -> rgb."""
    mesh = obj.data
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    for p in mesh.polygons:
        c = color_fn(Vector(p.center), Vector(p.normal), p)
        for li in p.loop_indices:
            attr.data[li].color_srgb = (c[0], c[1], c[2], 1.0)
    mesh.color_attributes.active_color = attr
    mesh.color_attributes.render_color_index = mesh.color_attributes.active_color_index


def bone_parent(obj, arm, bone):
    """Жёстко прикрепить объект к кости, не сдвигая его в мире (в FBX станет дочерним узлом кости)."""
    mw = obj.matrix_world.copy()
    obj.parent = arm
    obj.parent_type = "BONE"
    obj.parent_bone = bone
    bpy.context.view_layer.update()
    obj.matrix_world = mw


def bone_world(arm, name, tail=False):
    b = arm.data.bones[name]
    return arm.matrix_world @ (b.tail_local if tail else b.head_local)


# ---------------------------------------------------------------- база

def import_base(ubc):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ubc, "Base Characters", "Unity", "Superhero_Male_FullBody.fbx"))
    arm = bpy.data.objects["Armature"]
    body = bpy.data.objects["SuperHero_Male"]
    body.name = "Hero_Body"
    bpy.data.objects["Eyes"].name = "Hero_Eyes"
    bpy.data.objects["Eyebrows"].name = "Hero_Brows"
    # имена материалов → по ним Unity назначит свои материалы
    rename = {"MI_Superhero_Male": "M_Hero_Skin", "MI_Eyes": "M_Hero_Eyes", "MI_Hair_1": "M_Hero_Brows"}
    for m in bpy.data.materials:
        if m.name in rename:
            m.name = rename[m.name]
    return arm, body


def mark_eyes(eyes):
    """Цвет вершин глаз: правый глаз героя белый (золотой в игре), левый чёрный (фиолетовый)."""
    mesh = eyes.data
    for a in list(mesh.color_attributes):
        mesh.color_attributes.remove(a)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    mw = eyes.matrix_world
    for p in mesh.polygons:
        x = (mw @ Vector(p.center)).x
        v = 1.0 if x > 0 else 0.0  # герой смотрит в +Y, его правая сторона +X
        for li in p.loop_indices:
            attr.data[li].color_srgb = (v, v, v, 1.0)
    mesh.color_attributes.active_color = attr
    mesh.color_attributes.render_color_index = 0


def body_points(body):
    return [body.matrix_world @ v.co for v in body.data.vertices]


# ---------------------------------------------------------------- волосы

def build_hair(rng, pts):
    head = [p for p in pts if p.z > 1.62 and abs(p.x) < 0.12]
    top = max(p.z for p in head)
    cy = sum(p.y for p in head) / len(head)
    center = Vector((0.0, cy, top - 0.09))
    # радиус головы по направлениям: подгоняем «шапку» под череп
    def head_radius(d):
        best = 0.0
        for p in head:
            v = p - center
            if v.length > 1e-4 and v.normalized().dot(d) > 0.97:
                best = max(best, v.length)
        return best if best > 0 else 0.095

    def hairline(y):  # спереди выше (открытый лоб), сзади ниже (до основания шеи)
        return 1.70 + 0.70 * (y - cy) + 0.004

    bm = bmesh.new()
    # шапка: сфера, срезанная по линии роста волос
    cap = bmesh.new()
    bmesh.ops.create_uvsphere(cap, u_segments=14, v_segments=9, radius=1.0)
    for v in cap.verts:
        d = v.co.normalized()
        v.co = center + d * (head_radius(d) + 0.012)
    kill = [v for v in cap.verts if v.co.z < hairline(v.co.y) - 0.01]
    bmesh.ops.delete(cap, geom=kill, context="VERTS")
    tmp = bpy.data.meshes.new("tmp_cap")
    cap.to_mesh(tmp)
    cap.free()
    bm.from_mesh(tmp)
    bpy.data.meshes.remove(tmp)

    crown = center + Vector((0.0, -0.035, 0.08))
    # пряди: сплюснутые изогнутые «перья» от макушки вниз и наружу
    n = 46
    golden = math.pi * (3 - math.sqrt(5))
    for i in range(n):
        z = 1 - (i + 0.5) / n * 1.25  # верхняя часть сферы и чуть ниже экватора
        r = math.sqrt(max(0.0, 1 - z * z))
        a = i * golden + rng.uniform(-0.2, 0.2)
        d = Vector((math.cos(a) * r, math.sin(a) * r, z)).normalized()
        root = center + d * (head_radius(d) + 0.006)
        if root.z < hairline(root.y) + 0.01:
            continue
        front = d.y > 0.55  # прядь на лоб
        out = (root - crown)
        out.z = 0
        out = out.normalized() if out.length > 1e-4 else d
        down = Vector((0, 0, -1))
        if front:
            length = rng.uniform(0.07, 0.10)
            dirv = (out * 0.8 + down * 0.6 + Vector((rng.uniform(-0.3, 0.3), 0, 0))).normalized()
        else:
            length = rng.uniform(0.09, 0.15) * (1.15 if d.y < -0.2 else 1.0)
            dirv = (out * 0.55 + down * (0.65 + 0.4 * max(0.0, -d.z + 0.3)) + d * 0.25).normalized()
        width = rng.uniform(0.022, 0.034)
        strand(bm, rng, root, dirv, d, length, width, center, head_radius, hairline, front)

    obj = new_object("Hero_Hair", bm, "M_Hero_Hair")

    def col(c, nrm, p):
        dist = (c - center).length
        t = smooth(0.09, 0.16, dist + (top - c.z) * 0.25)
        base = lerp3(HAIR_ROOT, HAIR_MID, smooth(0.0, 0.5, t + 0.35))
        base = lerp3(base, HAIR_TIP, smooth(0.45, 1.0, t))
        k = 0.9 + 0.2 * rng.random()
        return tuple(min(1.0, x * k) for x in base)
    face_colors(obj, col)
    return obj


def strand(bm, rng, root, dirv, normal, length, width, center, head_radius, hairline, front):
    segs = 3
    side = dirv.cross(normal)
    if side.length < 1e-4:
        side = Vector((1, 0, 0))
    side.normalize()
    rings = []
    p = root.copy()
    d = dirv.copy()
    for s in range(segs + 1):
        t = s / segs
        w = width * (1 - t) ** 0.8 + 0.002
        h = w * 0.45
        up = side.cross(d).normalized()
        if s == segs:
            rings.append([bm.verts.new(p)])
            break
        ring = [bm.verts.new(p + side * w), bm.verts.new(p + up * h), bm.verts.new(p - side * w), bm.verts.new(p - up * h * 0.4)]
        rings.append(ring)
        # шаг вперёд, прядь изгибается вниз и не входит в голову
        d = (d + Vector((0, 0, -0.35)) + Vector((rng.uniform(-0.15, 0.15), rng.uniform(-0.15, 0.15), 0))).normalized()
        p = p + d * (length / segs)
        v = p - center
        min_r = head_radius(v.normalized()) + 0.012
        if v.length < min_r:
            p = center + v.normalized() * min_r
        if front:
            p.z = max(p.z, 1.735)
    for a, b in zip(rings[:-1], rings[1:]):
        if len(b) == 1:
            for i in range(4):
                bm.faces.new((a[i], a[(i + 1) % 4], b[0]))
        else:
            for i in range(4):
                bm.faces.new((a[i], a[(i + 1) % 4], b[(i + 1) % 4], b[i]))
    bm.faces.new(list(reversed(rings[0])))


# ---------------------------------------------------------------- набедренная повязка

def build_loincloth(rng, arm, body, pts):
    band = [p for p in pts if 0.96 < p.z < 1.02 and abs(p.x) < 0.3]
    rx = max(abs(p.x) for p in band) + 0.014
    y0, y1 = min(p.y for p in band), max(p.y for p in band)
    cy = (y0 + y1) / 2
    ry = (y1 - y0) / 2 + 0.014
    bm = bmesh.new()

    # пояс: скрученная тряпка, спереди ниже, сзади выше
    segs = 18
    def belt_z(a):
        return 0.985 + 0.02 * (-math.sin(a))  # sin>0 → перед (+Y) → ниже
    top, bot = [], []
    for i in range(segs):
        a = 2 * math.pi * i / segs
        x, y = math.cos(a) * rx, cy + math.sin(a) * ry
        z = belt_z(a)
        wob = rng.uniform(-0.006, 0.006)
        top.append(bm.verts.new((x, y, z + 0.028 + wob)))
        bot.append(bm.verts.new((x, y, z - 0.02 + wob)))
    for i in range(segs):
        j = (i + 1) % segs
        bm.faces.new((bot[i], bot[j], top[j], top[i]))

    def flap(front):
        sgn = 1 if front else -1
        ytop = cy + sgn * (ry - 0.004)
        ztop = belt_z(sgn * math.pi / 2) - 0.012
        zbot = 0.64 if front else 0.70
        rows, cols = 6, 4
        grid = []
        for r in range(rows + 1):
            t = r / rows
            z = ztop + (zbot - ztop) * t
            half = (0.085 if front else 0.10) * (1 - 0.25 * t)
            # спереди ткань отходит от тела, сзади огибает ягодицы
            bulge = 0.018 * math.sin(t * math.pi) if front else 0.05 * math.sin(min(1.0, t * 1.6) * math.pi * 0.75)
            row = []
            for c in range(cols + 1):
                u = c / cols * 2 - 1
                x = u * half
                y = ytop + sgn * (bulge + 0.01 * t) - sgn * 0.012 * u * u
                zz = z
                if r == rows:  # рваный край
                    zz += rng.uniform(-0.02, 0.05)
                row.append(bm.verts.new((x, y, zz)))
            grid.append(row)
        for r in range(rows):
            for c in range(cols):
                f = (grid[r][c], grid[r + 1][c], grid[r + 1][c + 1], grid[r][c + 1])
                bm.faces.new(f if front else tuple(reversed(f)))

    flap(True)
    flap(False)
    # пара коротких обрывков по бокам
    for sx in (-1, 1):
        a = math.atan2(0, sx)
        x = sx * (rx - 0.004)
        z = belt_z(a) - 0.015
        w = 0.035
        v = [bm.verts.new((x, cy - w, z)), bm.verts.new((x, cy + w, z)),
             bm.verts.new((x + sx * 0.012, cy + w * 0.6, z - 0.12 - rng.uniform(0, 0.04))),
             bm.verts.new((x + sx * 0.012, cy - w * 0.7, z - 0.09 - rng.uniform(0, 0.03)))]
        bm.faces.new(v if sx > 0 else list(reversed(v)))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = new_object("Hero_Loincloth", bm, "M_Hero_Cloth")
    mod = obj.modifiers.new("Solid", "SOLIDIFY")
    mod.thickness = 0.008
    mod.offset = 1.0
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier="Solid")

    def col(c, nrm, p):
        n = noise.noise(c * 22.0) * 0.5 + 0.5
        base = lerp3(BURLAP_DARK, BURLAP, n)
        grime = smooth(0.85, 0.66, c.z)  # низ грязнее
        base = lerp3(base, BURLAP_DARK, grime * 0.6)
        burnt = smooth(0.73, 0.64, c.z + noise.noise(c * 30.0) * 0.03)
        if c.z < 0.76:
            base = lerp3(base, CHAR, burnt * 0.85)
        return base
    face_colors(obj, col)

    # скиннинг: пояс берёт веса с тела, полотнища в основном за тазом, низ немного за бёдрами
    obj.parent = arm
    obj.matrix_parent_inverse = arm.matrix_world.inverted()
    am = obj.modifiers.new("Armature", "ARMATURE")
    am.object = arm
    for bone in arm.data.bones:
        obj.vertex_groups.new(name=bone.name)
    dt = obj.modifiers.new("Weights", "DATA_TRANSFER")
    dt.object = body
    dt.use_vert_data = True
    dt.data_types_verts = {"VGROUP_WEIGHTS"}
    dt.vert_mapping = "POLYINTERP_NEAREST"
    dt.layers_vgroup_select_src = "ALL"
    dt.layers_vgroup_select_dst = "NAME"
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_move_to_index(modifier="Weights", index=0)
    bpy.ops.object.modifier_apply(modifier="Weights")

    groups = {g.name: g for g in obj.vertex_groups}
    for v in obj.data.vertices:
        z = v.co.z
        if z > 0.95:
            continue
        for g in obj.vertex_groups:
            g.remove([v.index])
        f = smooth(0.95, 0.65, z) * 0.55
        left = smooth(0.05, -0.05, v.co.x)  # левая сторона героя: -X
        groups["pelvis"].add([v.index], 1 - f, "REPLACE")
        if left > 0:
            groups["thigh_l"].add([v.index], f * left, "REPLACE")
        if left < 1:
            groups["thigh_r"].add([v.index], f * (1 - left), "REPLACE")
    return obj


# ---------------------------------------------------------------- наруч

def build_bracer(rng, arm, pts):
    a = bone_world(arm, "lowerarm_l")
    b = bone_world(arm, "hand_l")
    axis = (b - a).normalized()
    up = Vector((0, 0, 1))
    side = axis.cross(up).normalized()
    up = side.cross(axis).normalized()
    n_dir = 8

    def dir_k(k):
        ang = 2 * math.pi * k / n_dir + math.pi / n_dir  # плоская грань сверху
        return (up * math.cos(ang) + side * math.sin(ang)).normalized()

    stations = [0.10, 0.14, 0.22, 0.55, 0.84, 0.92, 0.97, 1.0]
    length = (b - a).length
    radii = []
    for t in stations:
        c = a + axis * (length * min(t, 0.98))
        near = [p - c for p in pts if abs((p - c).dot(axis)) < 0.03]
        row = []
        for k in range(n_dir):
            d = dir_k(k)
            best = 0.03
            for w in near:
                rad = w - axis * w.dot(axis)
                if rad.length > 1e-4 and rad.normalized().dot(d) > 0.9:
                    best = max(best, rad.length)
            row.append(best + 0.009)
        radii.append(row)
    # сглаживание по окружности, чтобы форма была аккуратной
    for row in radii:
        sm = [(row[k - 1] + 2 * row[k] + row[(k + 1) % n_dir]) / 4 for k in range(n_dir)]
        row[:] = sm
    # наруч чуть шире к локтю и с отбортовкой на концах
    flare = [0.006, 0.0, 0.0, 0.0, 0.0, 0.0, 0.004, 0.0]

    bm = bmesh.new()
    thick = 0.011
    outer, inner = [], []
    for si, t in enumerate(stations):
        c = a + axis * (length * t)
        o, i_ = [], []
        for k in range(n_dir):
            d = dir_k(k)
            r = max(radii[si][k], radii[min(si + 1, len(stations) - 1)][k]) if si < 2 else radii[si][k]
            o.append(bm.verts.new(c + d * (r + thick + flare[si])))
            i_.append(bm.verts.new(c + d * r))
        outer.append(o)
        inner.append(i_)
    for s in range(len(stations) - 1):
        for k in range(n_dir):
            j = (k + 1) % n_dir
            bm.faces.new((outer[s][k], outer[s][j], outer[s + 1][j], outer[s + 1][k]))
            bm.faces.new((inner[s][j], inner[s][k], inner[s + 1][k], inner[s + 1][j]))
    for s, flip in ((0, True), (len(stations) - 1, False)):
        for k in range(n_dir):
            j = (k + 1) % n_dir
            f = (outer[s][k], inner[s][k], inner[s][j], outer[s][j])
            bm.faces.new(tuple(reversed(f)) if flip else f)

    # гнездо кристалла сверху (ромбовидная рамка)
    t_sock = 0.55
    c = a + axis * (length * t_sock)
    r_top = radii[3][0] + thick
    sock_c = c + up * (r_top - 0.002)
    ring_o, ring_i = [], []
    for k in range(8):
        ang = 2 * math.pi * k / 8
        d = axis * math.cos(ang) * 1.35 + side * math.sin(ang)
        ring_o.append(bm.verts.new(sock_c + d * 0.036 + up * 0.002))
        ring_i.append(bm.verts.new(sock_c + d * 0.026 + up * 0.012))
    base_c = bm.verts.new(sock_c + up * 0.003)
    for k in range(8):
        j = (k + 1) % 8
        bm.faces.new((ring_o[k], ring_o[j], ring_i[j], ring_i[k]))
        bm.faces.new((ring_i[k], ring_i[j], base_c))
    # продольные рёбра-накладки по бокам от гнезда
    for sgn in (-1, 1):
        d = dir_k(0 if sgn > 0 else n_dir - 1)
        for (t0, t1) in ((0.24, 0.42), (0.68, 0.82)):
            p0 = a + axis * (length * t0)
            p1 = a + axis * (length * t1)
            r0 = radii[3][0] + thick
            q = [p0 + d * r0, p1 + d * r0, p1 + d * (r0 + 0.006), p0 + d * (r0 + 0.006)]
            off = side * sgn * 0.004
            vs = [bm.verts.new(v + off) for v in q] + [bm.verts.new(v - off) for v in q]
            for f in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
                bm.faces.new([vs[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = new_object("Hero_Bracer", bm, "M_Hero_Bracer")

    def col(cpos, nrm, p):
        t = (cpos - a).dot(axis) / length
        edge = min(abs(t - stations[0]), abs(t - stations[-1]))
        n = noise.noise(cpos * 40.0) * 0.5 + 0.5
        base = METAL
        if abs(nrm.dot(axis)) > 0.6 or t < 0.16 or t > 0.93:
            base = METAL_EDGE  # отбортовки и торцы светлее
        if (cpos - sock_c).length < 0.045 and nrm.dot(up) > 0.2:
            base = METAL_EDGE
        # цвета побежалости у краёв: бронза → фиолет → синий, рваной полосой
        heat = smooth(0.12, 0.0, edge + (n - 0.5) * 0.05)
        if heat > 0:
            band = lerp3(HEAT_BLUE, HEAT_PURPLE, smooth(0.3, 0.6, heat))
            band = lerp3(band, HEAT_BRONZE, smooth(0.6, 0.95, heat))
            base = lerp3(base, band, min(1.0, heat * 1.4))
        k = 0.9 + 0.2 * n
        return tuple(x * k for x in base)
    face_colors(obj, col)

    # кристалл: вытянутый ромб, половины помечены цветом (R=1 золото, R=0 тьма)
    cb = bmesh.new()
    top_v = cb.verts.new(sock_c + up * 0.032)
    bot_v = cb.verts.new(sock_c + up * 0.006)
    ring = []
    for k in range(6):
        ang = 2 * math.pi * k / 6
        d = axis * math.cos(ang) * 1.5 + side * math.sin(ang)
        ring.append(cb.verts.new(sock_c + d * 0.021 + up * 0.017))
    for k in range(6):
        j = (k + 1) % 6
        cb.faces.new((ring[k], ring[j], top_v))
        cb.faces.new((ring[j], ring[k], bot_v))
    bmesh.ops.recalc_face_normals(cb, faces=cb.faces)
    crystal = new_object("Hero_BracerCrystal", cb, "M_Hero_Crystal")

    def ccol(cpos, nrm, p):
        light = 1.0 if (cpos - sock_c).dot(axis) > 0 else 0.0  # к запястью — Свет
        return (light, rng.random(), 1.0)
    face_colors(crystal, ccol)

    bone_parent(obj, arm, "lowerarm_l")
    bone_parent(crystal, arm, "lowerarm_l")
    return obj, crystal


# ---------------------------------------------------------------- меч

def build_sword(rng, name="Wpn_Sword_Rusty"):
    """Меч вдоль +Z, точка опоры в центре рукояти. Модель общая с мечом в овраге (ravine_props.py):
    lowpoly_kit.build_rusty_sword — клинок с долом и сколами, изогнутая гарда, обмотка, дисковое навершие."""
    return build_rusty_sword(name, "M_Sword_Rusty")


def place_sword_in_hand(arm, sword):
    hand = bone_world(arm, "hand_r")
    tip = bone_world(arm, "hand_r", tail=True)
    along = (tip - hand).normalized()
    grip = hand + along * 0.085 + Vector((0, 0.0, -0.035))
    # клинок вперёд (+Y, сторона большого пальца), кромки вверх/вниз
    rot = Matrix.Rotation(-math.pi / 2, 4, "X") @ Matrix.Rotation(math.pi / 2, 4, "Z")
    sword.matrix_world = Matrix.Translation(grip) @ rot
    bone_parent(sword, arm, "hand_r")


# ---------------------------------------------------------------- текстуры

def prepare_textures(ubc, out):
    tex = os.path.join(ubc, "Base Characters", "Textures")
    os.makedirs(out, exist_ok=True)
    # кожа: серые «трусы» из базы перекрашиваем в тёмную ткань-подмотку под повязкой
    skin = bpy.data.images.load(os.path.join(tex, "T_Superhero_Male_Ligh.png"))
    sp = list(skin.pixels)
    for i in range(0, len(sp), 4):
        r, g, b = sp[i], sp[i + 1], sp[i + 2]
        mx, mn = max(r, g, b), min(r, g, b)
        sat = (mx - mn) / mx if mx > 1e-4 else 0.0
        k = smooth(0.14, 0.07, sat) * smooth(0.12, 0.25, mx)
        if k > 0:
            lum = 0.3 * r + 0.59 * g + 0.11 * b
            cloth = (0.16 * lum / 0.6 + 0.04, 0.115 * lum / 0.6 + 0.03, 0.075 * lum / 0.6 + 0.02)
            sp[i], sp[i + 1], sp[i + 2] = (r * (1 - k) + cloth[0] * k, g * (1 - k) + cloth[1] * k, b * (1 - k) + cloth[2] * k)
    skin_out = bpy.data.images.new("T_Hero_Skin", skin.size[0], skin.size[1])
    skin_out.pixels = sp
    skin_out.filepath_raw = os.path.join(out, "T_Hero_Skin.png")
    skin_out.file_format = "PNG"
    skin_out.save()
    shutil.copy(os.path.join(tex, "Normals Unity - Godot", "T_Superhero_Male_Normal.png"), os.path.join(out, "T_Hero_Skin_Normal.png"))
    shutil.copy(os.path.join(tex, "T_Hair_1_BaseColor.png"), os.path.join(out, "T_Hero_Brows.png"))
    # глаза: альфа = маска радужки (насыщенные пиксели) — шейдер перекрашивает её в свой цвет для каждого глаза
    img = bpy.data.images.load(os.path.join(tex, "T_Eye_Brown.png"))
    w, h = img.size
    px = list(img.pixels)
    for i in range(0, len(px), 4):
        r, g, b = px[i], px[i + 1], px[i + 2]
        mx, mn = max(r, g, b), min(r, g, b)
        sat = (mx - mn) / mx if mx > 1e-4 else 0.0
        iris = smooth(0.18, 0.4, sat) * smooth(0.04, 0.12, mx)
        lum = 0.3 * r + 0.59 * g + 0.11 * b
        # RGB: склера/зрачок как были, радужка → яркость (оттенок даст шейдер)
        px[i] = px[i] * (1 - iris) + lum * iris
        px[i + 1] = px[i + 1] * (1 - iris) + lum * iris
        px[i + 2] = px[i + 2] * (1 - iris) + lum * iris
        px[i + 3] = iris
    out_img = bpy.data.images.new("T_Hero_Eyes", w, h, alpha=True)
    out_img.pixels = px
    out_img.filepath_raw = os.path.join(out, "T_Hero_Eyes.png")
    out_img.file_format = "PNG"
    out_img.save()


# ---------------------------------------------------------------- экспорт

def export_fbx(path, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH", "EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", bake_space_transform=False,
        mesh_smooth_type="FACE", use_mesh_modifiers=True, colors_type="SRGB",
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        bake_anim=False, path_mode="STRIP")


# ---------------------------------------------------------------- превью

def preview_materials(out):
    def mat(name):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        o = nt.nodes.new("ShaderNodeOutputMaterial")
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.inputs["Roughness"].default_value = 0.75
        nt.links.new(bsdf.outputs[0], o.inputs[0])
        return nt, bsdf

    def tex(nt, bsdf, file, tint=None):
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(os.path.join(out, file))
        if tint:
            mix = nt.nodes.new("ShaderNodeMixRGB")
            mix.blend_type = "MULTIPLY"
            mix.inputs[0].default_value = 1
            mix.inputs[2].default_value = (*tint, 1)
            nt.links.new(t.outputs[0], mix.inputs[1])
            nt.links.new(mix.outputs[0], bsdf.inputs["Base Color"])
        else:
            nt.links.new(t.outputs[0], bsdf.inputs["Base Color"])

    def vcol(nt, bsdf):
        a = nt.nodes.new("ShaderNodeVertexColor")
        a.layer_name = "Col"
        nt.links.new(a.outputs[0], bsdf.inputs["Base Color"])

    nt, b = mat("M_Hero_Skin"); tex(nt, b, "T_Hero_Skin.png")
    nt, b = mat("M_Hero_Brows"); tex(nt, b, "T_Hero_Brows.png", (0.9, 0.6, 0.2))
    nt, b = mat("M_Hero_Eyes"); tex(nt, b, "T_Hero_Eyes.png")
    for name in ("M_Hero_Hair", "M_Hero_Cloth", "M_Sword_Rusty"):
        nt, b = mat(name); vcol(nt, b)
    nt, b = mat("M_Hero_Bracer"); vcol(nt, b); b.inputs["Metallic"].default_value = 0.6; b.inputs["Roughness"].default_value = 0.45
    nt, b = mat("M_Hero_Crystal")
    b.inputs["Base Color"].default_value = (1.0, 0.7, 0.2, 1)
    b.inputs["Emission Color"].default_value = (1.0, 0.6, 0.15, 1)
    b.inputs["Emission Strength"].default_value = 3.0


def render_preview(path, arm):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 40
    sc.cycles.device = "CPU"
    sc.render.resolution_x, sc.render.resolution_y = 1800, 1100
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("w")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.08, 0.09, 0.13, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 1.2
    # три ракурса: копии героя через инстансы коллекции
    coll = bpy.data.collections.new("HeroColl")
    for o in list(sc.collection.objects):
        if o.type in {"MESH", "ARMATURE"}:
            coll.objects.link(o)
            sc.collection.objects.unlink(o)
    sc.collection.children.link(coll)
    for i, (dx, rz) in enumerate(((-2.0, 180.0), (0.0, 215.0), (2.0, 0.0))):
        inst = bpy.data.objects.new(f"inst{i}", None)
        inst.instance_type = "COLLECTION"
        inst.instance_collection = coll
        inst.location = (dx, 0, 0)
        inst.rotation_euler = (0, 0, math.radians(rz))
        sc.collection.objects.link(inst)
    coll.hide_render = False
    bpy.context.view_layer.layer_collection.children["HeroColl"].exclude = True
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.location = (0.0, 6.4, 1.1)
    cam.rotation_euler = (math.radians(90), 0, math.radians(180))
    cam.data.lens = 30
    key = bpy.data.objects.new("key", bpy.data.lights.new("key", "SUN"))
    key.data.energy = 3.5
    key.rotation_euler = (math.radians(55), 0, math.radians(160))
    sc.collection.objects.link(key)
    rim = bpy.data.objects.new("rim", bpy.data.lights.new("rim", "SUN"))
    rim.data.energy = 2.0
    rim.data.color = (0.6, 0.7, 1.0)
    rim.rotation_euler = (math.radians(60), 0, math.radians(-20))
    sc.collection.objects.link(rim)
    sc.render.filepath = os.path.abspath(path)
    bpy.ops.render.render(write_still=True)


# ---------------------------------------------------------------- main

def main():
    ubc, out, preview = args()
    rng = random.Random(SEED)
    os.makedirs(out, exist_ok=True)
    arm, body = import_base(ubc)
    pts = body_points(body)
    mark_eyes(bpy.data.objects["Hero_Eyes"])

    hair = build_hair(rng, pts)
    bone_parent(hair, arm, "Head")
    cloth = build_loincloth(rng, arm, body, pts)
    bracer, crystal = build_bracer(rng, arm, pts)
    sword_hand = build_sword(random.Random(SEED + 1))
    place_sword_in_hand(arm, sword_hand)

    # герой в исходнике смотрит в +Y; разворачиваем к «переду» Blender (-Y) → в Unity он смотрит в +Z
    arm.rotation_euler = (0, 0, 0)
    bpy.context.view_layer.update()

    prepare_textures(ubc, out)
    hero_objs = [arm] + [o for o in bpy.data.objects if o.type == "MESH"]
    export_fbx(os.path.join(out, "Hero.fbx"), hero_objs)

    # отдельный меч (лут на земле, скелет в овраге)
    loose = build_sword(random.Random(SEED + 1), "Wpn_Sword_Rusty_Prop")
    export_fbx(os.path.join(out, "Wpn_Sword_Rusty.fbx"), [loose])
    loose.name = "Wpn_Sword_Rusty_Prop"
    bpy.data.objects.remove(loose)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "hero.blend"), compress=True)
    if preview:
        preview_materials(out)
        render_preview(preview, arm)
    print("[hero_build] готово:", out)


if __name__ == "__main__":
    main()
