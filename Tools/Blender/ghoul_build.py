# Падальщик — гуль из сумеречного леса, первый враг игры (стартовый овраг).
#
# Оригинальное существо в нашем лоуполи-стиле (цвет на грань в атрибуте "Col", как у героя и пропсов):
#   * сгорбленный, тощий, на «звериных» (пальцеходящих) ногах, руки длинные — когти до колен;
#   * пепельно-зелёная шкура: тёмная спина в пятнах, светлое брюхо, к кистям и стопам темнеет;
#   * вытянутый череп с надбровной дугой, длинные острые уши назад (одно надорвано),
#     пасть с неровными зубами, глаза — жёлто-зелёные угольки (светятся);
#   * гребень позвонков по хребту, рёбра под кожей, выпирающие лопатки, узловатые суставы;
#   * лес растёт на нём: трутовики на плече и спине, крошечные светящиеся грибы (как в овраге);
#   * трофеи мародёра: набедренник из выцветшего красного табарда павшего солдата на верёвке,
#     ожерелье из зубов с ржавым кольцом, грязные тряпки на правом предплечье, мешочек на поясе.
#
# Риг Generic (свой скелет), анимации ключами прямо в скрипте:
#   Idle (петля 2 с), Walk (петля), Run (петля, ~3.9 м/с), Attack (0.8 с, удар на 55 %),
#   Hit (вздрог 0.33 с), Death (падение ничком, остаётся лежать).
#
# Запуск (Blender 4.x/5.x или pip-пакет bpy):
#   blender -b -P ghoul_build.py -- --out <папка> [--preview ghoul_preview.png] [--poses ghoul_poses.png]
#   python ghoul_build.py --out <папка> ...
# Результат: Mon_Ghoul.fbx → Game/Assets/_Project/Art/Models/Characters/Monsters/Ghoul/,
#            ghoul.blend → Art/Blender/Characters/.
# Оси: модель смотрит в -Y Blender (как герой) → в Unity префаб доворачивает её на 180° (см. GhoulBuilder.cs).

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Matrix, Quaternion, Vector  # noqa: E402

from lowpoly_kit import (BONE, BONE_DARK, CLOTH_RED, CLOTH_RED_DARK, IRON, LEATHER, LEATHER_DARK,  # noqa: E402
                         SOOT, ico, lerp3, look_rot, n01, rust_metal, smooth, sweep, cylinder)

SEED = 31
FPS = 30
MAT = "M_Ghoul"         # шкура, кости, трофеи (цвет в вершинах)
MAT_2S = "M_Ghoul_2S"   # ткань и уши — видно с обеих сторон
MAT_GLOW = "M_Ghoul_Glow"  # глаза и споровые грибы (свечение × цвет вершин)

# ---------------------------------------------------------------- палитра (sRGB)

SKIN = (0.42, 0.49, 0.39)        # пепельно-зелёная шкура
SKIN_LIGHT = (0.62, 0.62, 0.53)  # брюхо, горло
SKIN_DARK = (0.22, 0.25, 0.25)   # спина, пятна
SKIN_DEEP = (0.13, 0.13, 0.15)   # кисти, стопы
MOTTLE = (0.33, 0.28, 0.38)      # лиловые синяки-пятна
VEIN = (0.17, 0.15, 0.24)        # тёмные прожилки под кожей
SCAR = (0.55, 0.36, 0.34)
CLAW = (0.10, 0.08, 0.08)
CLAW_TIP = (0.55, 0.50, 0.40)
MOUTH = (0.26, 0.05, 0.06)
GUM = (0.42, 0.14, 0.15)
TOOTH = (0.86, 0.80, 0.62)
TOOTH_DARK = (0.55, 0.47, 0.30)
EYE = (0.82, 1.0, 0.32)          # жёлто-зелёные угольки
SPORE = (0.45, 1.0, 0.75)        # светящиеся грибочки (как в овраге)
FUNGUS = (0.78, 0.55, 0.30)
FUNGUS_DARK = (0.42, 0.27, 0.15)
ROPE = (0.42, 0.34, 0.22)
ROPE_DARK = (0.25, 0.19, 0.12)
RAG = (0.52, 0.48, 0.39)
RAG_DARK = (0.30, 0.27, 0.21)
HAIR = (0.10, 0.10, 0.11)


def args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]

    def get(name, default=None):
        return argv[argv.index(name) + 1] if name in argv else default
    return os.path.abspath(get("--out", "ghoul_out")), get("--preview"), get("--poses")


# ---------------------------------------------------------------- скелет (позиции суставов в позе покоя)
# Поза покоя = сгорбленная стойка (почти как Idle). Единицы — метры, перед смотрит в -Y, левая сторона — +X.

V = Vector
J = {
    "pelvis": V((0, 0.06, 0.74)),
    "spine": V((0, 0.05, 0.90)),
    "chest": V((0, -0.05, 1.07)),
    "neck": V((0, -0.17, 1.17)),
    "head": V((0, -0.27, 1.21)),
    "head_tip": V((0, -0.50, 1.20)),
    "jaw": V((0, -0.31, 1.165)),
    "chin": V((0, -0.47, 1.105)),
}
for s, sx in (("L", 1), ("R", -1)):
    J["clav_" + s] = V((0.04 * sx, -0.13, 1.13))
    J["shoulder_" + s] = V((0.21 * sx, -0.11, 1.11))
    J["elbow_" + s] = V((0.30 * sx, -0.16, 0.82))
    J["wrist_" + s] = V((0.29 * sx, -0.34, 0.58))
    J["knuckle_" + s] = V((0.295 * sx, -0.405, 0.49))
    J["finger_" + s] = V((0.30 * sx, -0.46, 0.33))
    J["hip_" + s] = V((0.12 * sx, 0.06, 0.72))
    J["knee_" + s] = V((0.17 * sx, -0.12, 0.46))
    J["hock_" + s] = V((0.165 * sx, 0.10, 0.17))
    J["ball_" + s] = V((0.165 * sx, -0.05, 0.035))
    J["toe_" + s] = V((0.165 * sx, -0.17, 0.0))
    J["ear_" + s] = V((0.075 * sx, -0.31, 1.30))
    J["ear_tip_" + s] = V((0.25 * sx, -0.17, 1.40))

# (имя, голова, хвост, родитель)
BONES = [
    ("root", V((0, 0, 0)), V((0, 0, 0.25)), None),
    ("hips", J["pelvis"], J["spine"], "root"),
    ("spine", J["spine"], J["chest"], "hips"),
    ("chest", J["chest"], J["neck"], "spine"),
    ("neck", J["neck"], J["head"], "chest"),
    ("head", J["head"], J["head_tip"], "neck"),
    ("jaw", J["jaw"], J["chin"], "head"),
]
for s in ("L", "R"):
    BONES += [
        ("ear." + s, J["ear_" + s], J["ear_tip_" + s], "head"),
        ("clavicle." + s, J["clav_" + s], J["shoulder_" + s], "chest"),
        ("upperarm." + s, J["shoulder_" + s], J["elbow_" + s], "clavicle." + s),
        ("forearm." + s, J["elbow_" + s], J["wrist_" + s], "upperarm." + s),
        ("hand." + s, J["wrist_" + s], J["knuckle_" + s], "forearm." + s),
        ("fingers." + s, J["knuckle_" + s], J["finger_" + s], "hand." + s),
        ("thigh." + s, J["hip_" + s], J["knee_" + s], "hips"),
        ("shin." + s, J["knee_" + s], J["hock_" + s], "thigh." + s),
        ("foot." + s, J["hock_" + s], J["ball_" + s], "shin." + s),
        ("toes." + s, J["ball_" + s], J["toe_" + s], "foot." + s),
    ]
BONE_SEG = {b[0]: (b[1], b[2]) for b in BONES}

# Короткие задние лапы: модель строится в «длинноногих» координатах выше, а потом ноги сжимаются по высоте,
# а всё, что выше таза, опускается на ту же величину (руки почти достают земли — упырь ползает на них).
LEG_K = 0.72  # доля исходной длины ног
HIP_Z = J["pelvis"].z
LEG_DROP = HIP_Z * (1 - LEG_K)
LEG_BONES = ("thigh.", "shin.", "foot.", "toes.")


def squash(co, legw):
    """legw — насколько точка принадлежит ноге (0 — корпус/руки, 1 — нога)."""
    z = co.z * LEG_K * legw + (co.z - LEG_DROP) * (1 - legw)
    return V((co.x, co.y, z))


def squash_bone(name, co):
    if name == "root":
        return co
    return squash(co, 1.0 if name.startswith(LEG_BONES) else 0.0)


# ---------------------------------------------------------------- сборка кусков с весами

PARTS = []  # (obj, bones|None, rigid_bone|None)
SUBD_LEVEL = 1  # 0 = старый гранёный low-poly
HARD_PARTS = ("Ghoul_Teeth", "Ghoul_Spores", "Ghoul_Necklace")
# куски, которые сливаются в одну цельную «вылепленную» кожу (voxel remesh): без шаров-суставов и стыков
SKIN_PARTS = ("Ghoul_Torso", "Ghoul_Ribs", "Ghoul_Spine", "Ghoul_Neck", "Ghoul_Head", "Ghoul_Arm_", "Ghoul_Leg_")
SKIN_VOXEL = 0.0045
SKIN_TRIS = 16000
PART_INFO = {}  # name -> (color_fn, bones, rigid)


def new_part(name, build, color, material=MAT, bones=None, rigid=None):
    bm = bmesh.new()
    build(bm)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for m in (MAT, MAT_2S, MAT_GLOW):  # одинаковый порядок слотов во всех кусках — join не путает материалы
        mesh.materials.append(bpy.data.materials.get(m) or bpy.data.materials.new(m))
    slot = (MAT, MAT_2S, MAT_GLOW).index(material)
    soft = SUBD_LEVEL > 0 and not name.startswith(HARD_PARTS)
    skin = SUBD_LEVEL > 0 and name.startswith(SKIN_PARTS)
    PART_INFO[name] = (color, bones, rigid)
    for p in mesh.polygons:
        p.material_index = slot
    if soft:
        # сглаженная «альбионная» форма: Катмулл-Кларк + мягкие нормали
        mod = obj.modifiers.new("Subd", "SUBSURF")
        mod.levels = mod.render_levels = 2 if skin else SUBD_LEVEL
        mod.quality = 3
        with bpy.context.temp_override(object=obj, active_object=obj, selected_objects=[obj]):
            bpy.ops.object.modifier_apply(modifier=mod.name)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    verts = mesh.vertices
    for p in mesh.polygons:
        p.use_smooth = soft
        if soft:
            # краска по вершинам — плавные переходы, как у рисованной текстуры
            for li in p.loop_indices:
                v = verts[mesh.loops[li].vertex_index]
                c = color(Vector(v.co), Vector(v.normal), p)
                attr.data[li].color_srgb = (c[0], c[1], c[2], 1.0)
        else:
            c = color(Vector(p.center), Vector(p.normal), p)
            for li in p.loop_indices:
                attr.data[li].color_srgb = (c[0], c[1], c[2], 1.0)
    PARTS.append((obj, bones, rigid))
    return obj


def apply_mod(obj, kind, **kw):
    mod = obj.modifiers.new(kind, kind)
    for k, v in kw.items():
        setattr(mod, k, v)
    with bpy.context.temp_override(object=obj, active_object=obj, selected_objects=[obj]):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def merge_skin():
    """Тело, шея, голова, руки и ноги -> одна цельная кожа: remesh сливает стыки в мягкие переходы,
    цвет и веса каждой вершины берутся у ближайшего исходного куска."""
    from mathutils.bvhtree import BVHTree
    skin = [(o, b, r) for o, b, r in PARTS if o.name.startswith(SKIN_PARTS)]
    if not skin:
        return
    trees = []
    for o, _, _ in skin:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        trees.append((o.name, BVHTree.FromBMesh(bm)))
        bm.free()
    # копия кусков -> одна сетка
    copies = []
    for o, _, _ in skin:
        c = o.copy()
        c.data = o.data.copy()
        c.vertex_groups.clear()
        bpy.context.scene.collection.objects.link(c)
        copies.append(c)
    with bpy.context.temp_override(active_object=copies[0], object=copies[0], selected_objects=copies,
                                   selected_editable_objects=copies):
        bpy.ops.object.join()
    sk = copies[0]
    sk.name = sk.data.name = "Ghoul_Skin"
    apply_mod(sk, "REMESH", mode="VOXEL", voxel_size=SKIN_VOXEL, adaptivity=0.0)
    apply_mod(sk, "SMOOTH", factor=0.6, iterations=8)
    tris = sum(len(p.vertices) - 2 for p in sk.data.polygons)
    if tris > SKIN_TRIS:
        apply_mod(sk, "DECIMATE", ratio=SKIN_TRIS / tris)
    for o, _, _ in skin:
        PARTS.remove(next(p for p in PARTS if p[0] is o))
        bpy.data.objects.remove(o)
    mesh = sk.data
    mesh.materials.clear()
    for m in (MAT, MAT_2S, MAT_GLOW):
        mesh.materials.append(bpy.data.materials[m])
    for a in list(mesh.color_attributes):
        mesh.color_attributes.remove(a)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    # ближайший кусок для каждой вершины
    owner = []
    for v in mesh.vertices:
        best = None
        for nm, t in trees:
            h = t.find_nearest(v.co)
            if h[0] is not None and (best is None or h[3] < best[1]):
                best = (nm, h[3])
        owner.append(best[0])
    for p in mesh.polygons:
        p.use_smooth = True
        p.material_index = 0
        for li in p.loop_indices:
            vi = mesh.loops[li].vertex_index
            v = mesh.vertices[vi]
            c = PART_INFO[owner[vi]][0](Vector(v.co), Vector(v.normal), p)
            attr.data[li].color_srgb = (c[0], c[1], c[2], 1.0)
    # веса: обратное расстояние до костей своего куска, затем сглаживание по соседям
    nv = len(mesh.vertices)
    W = [dict() for _ in range(nv)]
    for v in mesh.vertices:
        _, bones, rigid = PART_INFO[owner[v.index]]
        if rigid:
            W[v.index] = {rigid: 1.0}
            continue
        ws = sorted(((1.0 / (seg_dist(v.co, *BONE_SEG[b]) ** 4 + 1e-7), b) for b in bones), reverse=True)[:3]
        tot = sum(w for w, _ in ws)
        W[v.index] = {b: w / tot for w, b in ws}
    nbr = [[] for _ in range(nv)]
    for e in mesh.edges:
        a, b = e.vertices
        nbr[a].append(b)
        nbr[b].append(a)
    for _ in range(3):
        NW = []
        for i in range(nv):
            acc = dict((k, 0.5 * w) for k, w in W[i].items())
            k = 0.5 / max(1, len(nbr[i]))
            for j in nbr[i]:
                for b, w in W[j].items():
                    acc[b] = acc.get(b, 0.0) + k * w
            top = sorted(acc.items(), key=lambda t: -t[1])[:3]
            tot = sum(w for _, w in top)
            NW.append({b: w / tot for b, w in top})
        W = NW
    groups = {}
    for i, ws in enumerate(W):
        for b, w in ws.items():
            if w < 0.02:
                continue
            g = groups.get(b) or sk.vertex_groups.new(name=b)
            groups[b] = g
            g.add([i], w, "REPLACE")
    PARTS.insert(0, (sk, None, "__done__"))
    print(f"[ghoul] skin: {sum(len(p.vertices) - 2 for p in mesh.polygons)} tris")


# ---------------------------------------------------------------- рисованная доводка (после join)

def paint_pass(body):
    """AO + кривизна + верхний свет поверх базового цвета вершин: объём как у рисованной текстуры."""
    import bmesh as _bm
    mesh = body.data
    bm = _bm.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    n = len(bm.verts)
    # кривизна: выпуклые рёбра светлее, впадины темнее
    curv = [0.0] * n
    for v in bm.verts:
        if not v.link_edges:
            continue
        acc = 0.0
        for e in v.link_edges:
            o = e.other_vert(v)
            d = o.co - v.co
            if d.length > 1e-6:
                acc += -v.normal.dot(d.normalized())
        curv[v.index] = acc / len(v.link_edges)
    # грубый AO: лучи по полусфере через BVH всего тела
    from mathutils.bvhtree import BVHTree
    tree = BVHTree.FromBMesh(bm)
    dirs = []
    rr = random.Random(7)
    while len(dirs) < 24:
        d = Vector((rr.uniform(-1, 1), rr.uniform(-1, 1), rr.uniform(-1, 1)))
        if 0.05 < d.length <= 1.0:
            dirs.append(d.normalized())
    ao = [1.0] * n
    for v in bm.verts:
        nv = v.normal
        if nv.length < 0.5:
            continue
        hits = tot = 0.0
        for d in dirs:
            c = d.dot(nv)
            if c <= 0.05:
                d = -d
                c = -c
            if c <= 0.05:
                continue
            tot += c
            h = tree.ray_cast(v.co + nv * 0.003, d, 0.18)
            if h[0] is not None:
                hits += c * (1.0 - h[3] / 0.18) ** 0.5
        ao[v.index] = 1.0 - (hits / tot if tot else 0.0)
    # мягкое размытие по соседям, чтобы не было рябки
    for _ in range(2):
        ao = [0.5 * ao[v.index] + 0.5 * sum(ao[e.other_vert(v).index] for e in v.link_edges) / max(1, len(v.link_edges))
              for v in bm.verts]
        curv = [0.5 * curv[v.index] + 0.5 * sum(curv[e.other_vert(v).index] for e in v.link_edges) / max(1, len(v.link_edges))
                for v in bm.verts]
    normals = [Vector(v.normal) for v in bm.verts]
    bm.free()
    attr = mesh.color_attributes["Col"]
    glow_slot = 2
    for p in mesh.polygons:
        if p.material_index == glow_slot:
            continue
        for li in p.loop_indices:
            vi = mesh.loops[li].vertex_index
            r, g, b, a = attr.data[li].color_srgb
            k = 0.3 + 0.7 * ao[vi] ** 1.6                       # впадины, подмышки, под брюхом
            cv = max(-1.0, min(1.0, curv[vi] * 30.0))
            k *= 1.0 + 0.3 * max(0.0, cv) - 0.25 * max(0.0, -cv)  # блик на рёбрах / грязь в складках
            up = normals[vi].z
            k *= 1.0 + 0.12 * max(0.0, up) - 0.08 * max(0.0, -up)  # «нарисованный» свет сверху
            # тёплый отсвет в тени, холодный блик сверху — как кистью
            r2, g2, b2 = r * k, g * k, b * k
            if k < 0.8:
                t = (0.8 - k) * 0.35
                r2, g2, b2 = r2 + t * 0.06, g2 - t * 0.01, b2 + t * 0.05
            attr.data[li].color_srgb = (min(1.0, r2), min(1.0, g2), min(1.0, b2), a)


def seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length


def assign_weights(obj, bones, rigid):
    groups = {}

    def g(name):
        if name not in groups:
            groups[name] = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
        return groups[name]
    for v in obj.data.vertices:
        if rigid:
            g(rigid).add([v.index], 1.0, "REPLACE")
            continue
        ws = []
        for b in bones:
            a, t = BONE_SEG[b]
            d = seg_dist(v.co, a, t)
            ws.append((1.0 / (d ** 4 + 1e-7), b))
        ws.sort(reverse=True)
        ws = ws[:3]
        tot = sum(w for w, _ in ws)
        for w, b in ws:
            if w / tot > 0.02:
                g(b).add([v.index], w / tot, "REPLACE")


# ---------------------------------------------------------------- цвет шкуры

def skin_col(seed=0.0, dark_ends=True):
    def fn(c, nrm, p):
        # брюхо светлее, спина темнее (по нормали: вперёд/вниз — светло, назад/вверх — темно)
        belly = smooth(-0.1, 0.7, -nrm.y * 0.7 - nrm.z * 0.5)
        back = smooth(0.0, 0.8, nrm.y * 0.6 + nrm.z * 0.6)
        col = lerp3(SKIN, SKIN_LIGHT, belly * 0.75)
        col = lerp3(col, SKIN_DARK, back * 0.7)
        # пятна-синяки
        m = smooth(0.58, 0.72, n01(c, 7.0, (seed, 2.0, 0.3)))
        col = lerp3(col, MOTTLE, m * 0.55)
        col = lerp3(col, SKIN_DARK, smooth(0.62, 0.8, n01(c, 15.0, (0, seed, 7))) * 0.4)
        # прожилки: тонкие тёмные линии по изолинии шума (заметны только на плавной коже)
        if SUBD_LEVEL > 0:
            vn = abs(n01(c, 9.0, (seed, 5.0, 1.0)) - 0.5)
            col = lerp3(col, VEIN, smooth(0.025, 0.0, vn) * 0.45 * smooth(0.45, 0.55, n01(c, 3.0, (1, seed, 4))))
            # тёплый подтон на сгибах и брюхе — живая кожа, а не пластик
            col = lerp3(col, (0.5, 0.4, 0.36), 0.18 * belly)
        # грязь снизу
        col = lerp3(col, SOOT, 0.35 * smooth(0.45, 0.05, c.z))
        if dark_ends:
            # кисти и стопы — тёмные, «в земле и крови»
            ends = max(smooth(0.62, 0.48, c.z) * smooth(0.22, 0.27, abs(c.x)) * smooth(-0.2, -0.3, c.y),
                       smooth(0.24, 0.08, c.z))
            col = lerp3(col, SKIN_DEEP, ends * 0.8)
        return col
    return fn


# ---------------------------------------------------------------- торс

def torso_frame(t):
    """Центр и оси сечения торса на высоте t (0 — таз, 1 — основание шеи)."""
    pts = [J["pelvis"] + V((0, 0.0, -0.06)), J["pelvis"] + V((0, 0.0, 0.06)), J["spine"], (J["spine"] + J["chest"]) / 2,
           J["chest"], J["chest"] + (J["neck"] - J["chest"]) * 0.55, J["neck"]]
    f = t * (len(pts) - 1)
    i = min(int(f), len(pts) - 2)
    k = f - i
    # Catmull-Rom по точкам хребта
    p0 = pts[max(i - 1, 0)]
    p1, p2 = pts[i], pts[i + 1]
    p3 = pts[min(i + 2, len(pts) - 1)]
    c = 0.5 * ((2 * p1) + (-p0 + p2) * k + (2 * p0 - 5 * p1 + 4 * p2 - p3) * k * k + (-p0 + 3 * p1 - 3 * p2 + p3) * k ** 3)
    tan = (p2 - p1).normalized()
    side = V((1, 0, 0))
    fwd = tan.cross(side).normalized()  # к брюху (вперёд-вниз)
    if fwd.y > 0:
        fwd = -fwd
    return c, tan, side, fwd


def torso_radius(t, a):
    """Полуоси сечения: ширина (x) и глубина; a — угол (0 — левый бок, π/2 — брюхо, -π/2 — спина)."""
    # профиль ширины: таз — узкая талия — широкая грудная клетка — плечи
    w = 0.125 + 0.03 * smooth(0.0, 0.1, t) - 0.035 * math.exp(-((t - 0.32) / 0.12) ** 2) \
        + 0.045 * math.exp(-((t - 0.70) / 0.18) ** 2) - 0.07 * smooth(0.82, 1.0, t)
    d = 0.105 - 0.03 * math.exp(-((t - 0.32) / 0.12) ** 2) + 0.03 * math.exp(-((t - 0.68) / 0.16) ** 2) \
        - 0.045 * smooth(0.85, 1.0, t)
    belly = math.sin(a)  # >0 спереди
    r_side, r_depth = w, d
    # впалый живот
    if belly > 0:
        r_depth *= 1.0 - 0.22 * math.exp(-((t - 0.3) / 0.1) ** 2) * belly
    # рёбра: волны на боках и груди
    rib = 0.0
    if 0.42 < t < 0.8:
        wave = max(0.0, math.sin((t - 0.42) / 0.38 * math.pi * 4.0))
        rib = 0.035 * wave ** 3 * (1 - abs(math.sin(a + 0.4))) ** 0.6 * (1 if belly > -0.4 else 0)
    # лопатки
    blade = 0.0
    if belly < 0:
        for sx in (-1, 1):
            ang = -math.pi / 2 + sx * 0.75
            blade += 0.035 * math.exp(-((a - ang) / 0.35) ** 2 - ((t - 0.74) / 0.08) ** 2)
    return r_side, r_depth, rib + blade


def torso_point(t, a, extra=0.0):
    c, tan, side, fwd = torso_frame(t)
    rs, rd, bump = torso_radius(t, a)
    return c + side * math.cos(a) * (rs + bump + extra) + fwd * math.sin(a) * (rd + bump + extra)


def build_torso(rng):
    rings, seg = 20, 18

    def geo(bm):
        prev = None
        first = None
        for i in range(rings + 1):
            t = i / rings
            ring = []
            for k in range(seg):
                a = 2 * math.pi * k / seg
                ring.append(bm.verts.new(torso_point(t, a)))
            if prev:
                for k in range(seg):
                    j = (k + 1) % seg
                    bm.faces.new((prev[k], prev[j], ring[j], ring[k]))
            else:
                first = ring
            prev = ring
        bm.faces.new(list(reversed(first)))
        bm.faces.new(prev)
        # шрамы — три рваные полосы по боку (только цвет, см. col)

    sk = skin_col(1.0)
    scar_a = V((0.13, -0.05, 0.98))

    def col(c, nrm, p):
        col = sk(c, nrm, p)
        # следы когтей сородичей на левом боку
        if c.x > 0.08:
            for k in range(3):
                d = abs((c.z - scar_a.z) + (c.y - scar_a.y) * 0.9 - k * 0.035)
                if d < 0.008 and abs(c.y - scar_a.y) < 0.07:
                    col = lerp3(col, SCAR, 0.7)
        # гребень рёбер светлее
        r = math.hypot(c.x, 0)  # noqa: F841
        return col
    return new_part("Ghoul_Torso", geo, col, bones=["hips", "spine", "chest", "neck", "clavicle.L", "clavicle.R"])


def build_ribs(rng):
    """Рёбра чуть выступают из кожи — тонкие дуги по бокам."""
    def geo(bm):
        for sx in (-1, 1):
            for k in range(4):
                t = 0.47 + k * 0.085
                pts, radii = [], []
                for i in range(7):
                    u = i / 6
                    a0 = math.pi if sx < 0 else 0.0
                    # от спины-бока к груди
                    a = a0 + sx * (-0.55 + 1.6 * u) * (1 if sx > 0 else -1) * -1
                    a = (math.pi - (-0.55 + 1.55 * u)) if sx < 0 else (-0.55 + 1.55 * u)
                    a = (math.pi + 0.55 - 1.55 * u) if sx < 0 else (-0.55 + 1.55 * u)
                    pts.append(torso_point(t - 0.03 * u, a if sx > 0 else a, extra=-0.004))
                    radii.append(0.009 * (1 - 0.5 * abs(u - 0.4)))
                sweep(bm, pts, radii, seg=4)

    sk = skin_col(1.0)

    def col(c, nrm, p):
        return lerp3(sk(c, nrm, p), SKIN_LIGHT, 0.25)
    return new_part("Ghoul_Ribs", geo, col, bones=["spine", "chest"])


def build_spine_ridge(rng):
    """Гребень позвонков от крестца до затылка."""
    def geo(bm):
        n = 11
        for i in range(n):
            t = 0.08 + 0.88 * i / (n - 1)
            p = torso_point(t, -math.pi / 2, extra=-0.012)
            c, tan, side, fwd = torso_frame(t)
            back = -fwd
            size = 0.022 + 0.014 * math.sin(math.pi * t)
            rot = look_rot(back, up=tan)
            ico(bm, p + back * size * 0.5, size, scale=(0.75, 1.15, 1.25), rot=rot, subdiv=1)
            # шип
            cylinder(bm, p + back * (size * 1.6), size * 0.45, 0.002, size * 1.4, seg=5,
                     rot=look_rot(back + tan * 0.6))

    def col(c, nrm, p):
        col = lerp3(BONE_DARK, SKIN_DARK, 0.45 + 0.4 * n01(c, 30.0))
        return lerp3(col, BONE, 0.35 * smooth(0.3, 0.9, -nrm.y + nrm.z * 0.3))
    return new_part("Ghoul_Spine", geo, col, bones=["hips", "spine", "chest", "neck"])


def build_neck(rng):
    def geo(bm):
        a = J["chest"] + (J["neck"] - J["chest"]) * 0.4
        pts = [a, J["neck"], (J["neck"] + J["head"]) / 2, J["head"] + V((0, -0.02, -0.01))]
        sweep(bm, pts, [0.07, 0.056, 0.05, 0.052], seg=9, sx=1.0, sy=0.85, ref=(1, 0, 0))
        # жилы-тяжи по бокам шеи
        for sx in (-1, 1):
            sweep(bm, [J["neck"] + V((0.035 * sx, -0.03, -0.04)), J["head"] + V((0.03 * sx, -0.03, -0.03))],
                  [0.014, 0.011], seg=4)

    return new_part("Ghoul_Neck", geo, skin_col(2.0, dark_ends=False), bones=["chest", "neck", "head"])


# ---------------------------------------------------------------- голова

HEAD_C = V((0, -0.345, 1.255))


def build_head(rng):
    def cranium_deform(co):
        x, y, z = co
        # плоский лоб и затылок назад, надбровная дуга
        if z < -0.02:
            z *= 0.75
        y += 0.02 * z
        brow = math.exp(-((z - 0.02) / 0.03) ** 2) * smooth(0.0, -0.06, y) * 0.022
        y -= brow
        # впалые виски
        x *= 1.0 - 0.18 * math.exp(-((y + 0.02) / 0.05) ** 2) * smooth(-0.04, 0.03, z)
        return V((x, y, z))

    def geo(bm):
        ico(bm, HEAD_C + V((0, 0.025, 0.02)), 0.1, scale=(0.95, 1.25, 0.88), subdiv=2, deform=cranium_deform)
        # надбровная дуга — тяжёлый козырёк над глазами
        sweep(bm, [V((-0.07, -0.415, 1.285)), V((0, -0.445, 1.28)), V((0.07, -0.415, 1.285))],
              [0.022, 0.018, 0.022], seg=5, sx=1.0, sy=0.6)
        # скулы
        for sx in (-1, 1):
            ico(bm, V((0.068 * sx, -0.405, 1.232)), 0.03, scale=(0.8, 1.4, 0.7), subdiv=1)
        # верхняя челюсть-морда
        sweep(bm, [V((0, -0.36, 1.225)), V((0, -0.45, 1.215)), V((0, -0.525, 1.195))],
              [0.062, 0.05, 0.03], seg=8, sx=1.0, sy=0.7, ref=(1, 0, 0))
        # ноздри-щели (бугорки над ними)
        for sx in (-1, 1):
            ico(bm, V((0.016 * sx, -0.53, 1.215)), 0.012, scale=(1.0, 1.4, 0.8), subdiv=1)
        # волосы — несколько грязных прядей с затылка
        for k, (sx, ln) in enumerate(((-0.03, 0.22), (0.0, 0.26), (0.035, 0.2), (0.05, 0.15))):
            base = V((sx, -0.29 + 0.012 * k, 1.335 - 0.005 * k))
            pts = [base, base + V((sx * 0.6, 0.07, -0.01)), base + V((sx * 1.2, 0.13, -0.07)),
                   base + V((sx * 1.4 + 0.01 * (k - 1.5), 0.08 + ln * 0.35, -ln * 0.7))]
            sweep(bm, pts, [0.014, 0.011, 0.008, 0.002], seg=4, sx=1.0, sy=0.5)

    sk = skin_col(3.0, dark_ends=False)

    def col(c, nrm, p):
        if c.z > 1.33 and c.y > -0.32 and abs(c.x) < 0.09 and (c - HEAD_C).length > 0.095:
            return lerp3(HAIR, SKIN_DARK, 0.25 * n01(c, 40.0))
        if c.y > -0.31 and c.z < 1.27 and (c - HEAD_C).length > 0.1 and c.z > 1.3:
            return HAIR
        col = sk(c, nrm, p)
        # глазницы — глубокая тень под козырьком
        for sx in (-1, 1):
            if (c - V((0.04 * sx, -0.43, 1.26))).length < 0.028:
                col = lerp3(col, SOOT, 0.75)
        # губы-дёсны по краю морды
        if c.z < 1.2 and c.y < -0.4 and nrm.z < -0.2:
            col = lerp3(col, GUM, 0.6)
        return col

    def hair_or_skin(c, nrm, p):
        if (c - HEAD_C).length > 0.125 and c.y > -0.33:
            return lerp3(HAIR, SKIN_DARK, 0.3 * n01(c, 40.0))
        return col(c, nrm, p)
    return new_part("Ghoul_Head", geo, hair_or_skin, rigid="head")


def jaw_line(t, z, sx=1):
    """Дуга зубного ряда: t 0 — задний зуб, 1 — передний."""
    y = -0.375 - 0.15 * t
    x = sx * 0.05 * (1 - t ** 2) + sx * 0.012
    return V((x, y, z))


def build_teeth(rng, lower=False):
    def geo(bm):
        z = 1.163 if lower else 1.195
        for sx in (-1, 1):
            n = 6
            for i in range(n):
                t = (i + rng.uniform(-0.2, 0.2)) / (n - 0.5)
                if lower:
                    t *= 0.93
                p = jaw_line(t, z, sx)
                if lower:
                    p = V((p.x * 0.92, p.y + 0.012, p.z))
                ln = rng.uniform(0.014, 0.024)
                if i == n - 1 and not lower:
                    ln = 0.04  # клыки
                if i == n - 2 and lower:
                    ln = 0.03
                r = 0.0075 if ln > 0.03 else 0.006
                d = V((rng.uniform(-0.15, 0.15), rng.uniform(-0.2, 0.2), 1 if lower else -1)).normalized()
                cylinder(bm, p + d * ln * 0.5, r, 0.0012, ln, seg=4, rot=look_rot(d))

    def col(c, nrm, p):
        return lerp3(TOOTH, TOOTH_DARK, 0.2 + 0.6 * n01(c, 90.0))
    return new_part("Ghoul_Teeth" + ("_Low" if lower else "_Up"), geo, col, rigid="jaw" if lower else "head")


def build_jaw(rng):
    def geo(bm):
        # нижняя челюсть — узкая, вытянутая
        for sx in (-1, 1):
            sweep(bm, [V((0.055 * sx, -0.31, 1.19)), V((0.052 * sx, -0.37, 1.155)), V((0.035 * sx, -0.44, 1.145)),
                       V((0.012 * sx, -0.49, 1.135))],
                  [0.02, 0.018, 0.016, 0.014], seg=5, sx=1.0, sy=0.8)
        ico(bm, V((0, -0.48, 1.132)), 0.022, scale=(1.2, 1.0, 0.8), subdiv=1)
        # тёмный «пол» пасти и кожа под ним (снизу пасть закрыта — не торчит «язык»)
        ico(bm, V((0, -0.405, 1.168)), 0.042, scale=(0.9, 1.5, 0.2), subdiv=1)
        ico(bm, V((0, -0.40, 1.15)), 0.05, scale=(0.95, 1.55, 0.36), subdiv=1)

    sk = skin_col(4.0, dark_ends=False)

    def col(c, nrm, p):
        if abs(c.x) < 0.04 and nrm.z > 0.4 and c.y < -0.33:
            return MOUTH
        col = sk(c, nrm, p)
        if nrm.z > 0.2:
            col = lerp3(col, GUM, 0.55)
        return col
    return new_part("Ghoul_Jaw", geo, col, rigid="jaw")


def build_mouth(rng):
    """Нёбо и глотка — тёмно-красная внутренность пасти, видна между челюстями."""
    def geo(bm):
        ico(bm, V((0, -0.41, 1.195)), 0.045, scale=(0.9, 1.45, 0.28), subdiv=1)
        ico(bm, V((0, -0.34, 1.18)), 0.045, scale=(1.0, 0.6, 0.7), subdiv=1)

    def col(c, nrm, p):
        return lerp3(MOUTH, (0.12, 0.02, 0.03), smooth(-0.45, -0.33, c.y))
    return new_part("Ghoul_Mouth", geo, col, rigid="head")


def build_eyes(rng):
    def geo(bm):
        for sx in (-1, 1):
            ico(bm, V((0.04 * sx, -0.437, 1.258)), 0.0135, scale=(1.25, 0.7, 0.8), subdiv=1)

    def col(c, nrm, p):
        return EYE
    return new_part("Ghoul_Eyes", geo, col, material=MAT_GLOW, rigid="head")


def build_ears(rng):
    objs = []
    for s, sx in (("L", 1), ("R", -1)):
        def geo(bm, s=s, sx=sx):
            a, b = J["ear_" + s], J["ear_tip_" + s]
            n = 7
            pts, radii = [], []
            for i in range(n):
                u = i / (n - 1)
                p = a + (b - a) * u + V((0, 0, 0.03 * math.sin(math.pi * u)))
                pts.append(p)
                # широкое у основания, к кончику сужается в острый лист (без тонкой «антенны»)
                w = 0.05 * smooth(-0.15, 0.25, u) * (1 - u) ** 0.85 + 0.003
                if s == "R" and 0.45 < u < 0.7:
                    w *= 0.45  # надорванное ухо
                radii.append(max(w, 0.002))
            sweep(bm, pts, radii, seg=6, sx=0.22, sy=1.0, ref=(0, 0, 1))

        def col(c, nrm, p, sx=sx):
            # внутренняя сторона уха — розово-серая, внешняя — как шкура
            inner = smooth(0.0, 0.5, -nrm.x * sx * 0.6 - nrm.y * 0.6 + nrm.z * 0.2)
            col = lerp3(SKIN, SKIN_DARK, 0.3 + 0.4 * n01(c, 25.0))
            col = lerp3(col, (0.55, 0.40, 0.40), inner * 0.55)
            tip = (c - J["ear_tip_" + ("L" if sx > 0 else "R")]).length
            return lerp3(col, SKIN_DEEP, smooth(0.08, 0.0, tip) * 0.7)
        objs.append(new_part("Ghoul_Ear_" + s, geo, col, material=MAT_2S, bones=["head", "ear." + s]))
    return objs


# ---------------------------------------------------------------- руки

def build_arms(rng):
    objs = []
    for s, sx in (("L", 1), ("R", -1)):
        sh, el, wr, kn = J["shoulder_" + s], J["elbow_" + s], J["wrist_" + s], J["knuckle_" + s]

        def geo(bm, sh=sh, el=el, wr=wr, kn=kn, sx=sx):
            # плечо — мясистый «бугор» дельты
            ico(bm, sh + V((0.004 * sx, 0.0, 0.0)) + (el - sh) * 0.1, 0.05, scale=(0.95, 1.0, 1.35), subdiv=2)
            # жилистое плечо: дельта -> бицепс -> тонкий перехват над локтем
            up = [sh + (el - sh) * k for k in (0.0, 0.18, 0.4, 0.62, 0.82, 1.0)]
            sweep(bm, up, [0.05, 0.047, 0.042, 0.04, 0.035, 0.034], seg=10, sx=1.0, sy=0.9, ref=(1, 0, 0))
            ico(bm, el + V((0, 0.016, 0)), 0.027, scale=(0.9, 1.3, 0.9), subdiv=2)  # острый локоть
            # предплечье: толстое у локтя, сухое к запястью
            lo = [el + (wr - el) * k for k in (0.0, 0.15, 0.35, 0.6, 0.85, 1.0)]
            sweep(bm, lo, [0.034, 0.04, 0.038, 0.03, 0.025, 0.025], seg=10, sx=1.0, sy=0.8, ref=(1, 0, 0))
            # кисть: ладонь
            sweep(bm, [wr, wr + (kn - wr) * 0.5, kn], [0.028, 0.034, 0.03], seg=6, sx=0.6, sy=1.0, ref=(1, 0, 0))

        objs.append(new_part("Ghoul_Arm_" + s, geo, skin_col(5.0 + sx),
                             bones=["clavicle." + s, "upperarm." + s, "forearm." + s, "hand." + s]))
    return objs


def build_hands(rng):
    """Пальцы с длинными чёрными когтями (три пальца + большой)."""
    objs = []
    for s, sx in (("L", 1), ("R", -1)):
        wr, kn, fi = J["wrist_" + s], J["knuckle_" + s], J["finger_" + s]
        down = (fi - kn).normalized()
        inward = V((-sx, 0, 0))
        spread_axis = down.cross(inward).normalized()

        def finger(bm, base, d, length, r, curl):
            pts = [base]
            dirv = d.copy()
            seglen = length / 3
            for i in range(3):
                dirv = (dirv + inward * curl).normalized()
                pts.append(pts[-1] + dirv * seglen)
            sweep(bm, pts, [r, r * 0.9, r * 0.82, r * 0.72], seg=5)
            # костяшки
            for p in pts[1:3]:
                ico(bm, p, r * 1.05, subdiv=1)
            # коготь — изогнутый конус
            cp = [pts[-1]]
            cd = dirv.copy()
            for i in range(4):
                cd = (cd + inward * 0.45).normalized()
                cp.append(cp[-1] + cd * 0.022)
            sweep(bm, cp, [r * 0.8, r * 0.65, r * 0.45, r * 0.25, 0.0008], seg=4, cap0=False)
            return pts[-1]

        def geo(bm, kn=kn, down=down, spread_axis=spread_axis, inward=inward):
            for k, off in enumerate((-1.0, 0.0, 1.0)):
                base = kn + spread_axis * off * 0.022
                d = (down + spread_axis * off * 0.18).normalized()
                finger(bm, base, d, 0.115 - 0.012 * abs(off) + (0.01 if off == 0 else 0), 0.0105, 0.12)
            # большой палец — внутрь
            tb = kn + inward * 0.02 + (J["wrist_" + s] - kn) * 0.45
            finger(bm, tb, (down + inward * 0.9).normalized(), 0.065, 0.0105, 0.1)

        def col(c, nrm, p, fi=fi):
            # когти: от кончиков пальцев — чёрный рог, самые кончики светлее
            d = (c - J["knuckle_" + ("L" if sx > 0 else "R")]).length
            if d > 0.12:
                return lerp3(CLAW, CLAW_TIP, smooth(0.15, 0.2, d))
            return lerp3(SKIN_DEEP, SKIN_DARK, 0.3 * n01(c, 40.0))
        objs.append(new_part("Ghoul_Hand_" + s, geo, col, bones=["hand." + s, "fingers." + s]))
    return objs


# ---------------------------------------------------------------- ноги

def build_legs(rng):
    objs = []
    for s, sx in (("L", 1), ("R", -1)):
        hp, kn, hk, bl, to = (J[k + "_" + s] for k in ("hip", "knee", "hock", "ball", "toe"))

        def geo(bm, hp=hp, kn=kn, hk=hk, bl=bl, to=to, sx=sx):
            thigh = [hp + V((0, 0.01, 0.04)), hp + (kn - hp) * 0.3 + V((0, 0.012, 0)),
                     hp + (kn - hp) * 0.62 + V((0, 0.008, 0)), hp + (kn - hp) * 0.86, kn]
            sweep(bm, thigh, [0.075, 0.07, 0.06, 0.048, 0.043], seg=10, sx=1.0, sy=1.1, ref=(1, 0, 0))
            ico(bm, kn + V((0, -0.016, 0.004)), 0.034, scale=(1.0, 1.1, 1.0), subdiv=2)  # колено
            shin = [kn, kn + (hk - kn) * 0.2 + V((0, 0.004, 0)), kn + (hk - kn) * 0.45 + V((0, -0.006, 0)),
                    kn + (hk - kn) * 0.75, hk]
            sweep(bm, shin, [0.042, 0.044, 0.036, 0.028, 0.026], seg=10, sx=1.0, sy=1.1, ref=(1, 0, 0))
            ico(bm, hk + V((0, 0.014, 0)), 0.024, scale=(1.0, 1.3, 1.0), subdiv=2)  # пятка-скакательный сустав
            sweep(bm, [hk, (hk + bl) / 2, bl], [0.027, 0.024, 0.026], seg=7, sx=1.0, sy=0.85, ref=(1, 0, 0))
            # три пальца с когтями
            for off in (-1, 0, 1):
                base = bl + V((off * 0.026, -0.005, 0))
                tip = to + V((off * 0.045, 0.02 * abs(off), 0.0))
                mid = (base + tip) / 2 + V((0, 0, 0.018))
                sweep(bm, [base, mid, tip], [0.016, 0.013, 0.01], seg=5)
                cd = (tip - mid).normalized()
                cp = [tip, tip + cd * 0.02 + V((0, 0, -0.004)), tip + cd * 0.038 + V((0, 0, -0.016))]
                sweep(bm, cp, [0.009, 0.006, 0.0008], seg=4, cap0=False)
            # шпора сзади
            sweep(bm, [bl + V((0, 0.03, 0.01)), bl + V((0, 0.06, 0.0)), bl + V((0, 0.075, -0.02))],
                  [0.01, 0.006, 0.001], seg=4)

        sk = skin_col(8.0 + sx)

        def col(c, nrm, p, to=to):
            if c.z < 0.03 and (c.y < to.y + 0.03 or c.y > J["ball_L"].y + 0.05):
                return CLAW
            return sk(c, nrm, p)
        objs.append(new_part("Ghoul_Leg_" + s, geo, col,
                             bones=["hips", "thigh." + s, "shin." + s, "foot." + s, "toes." + s]))
    return objs


# ---------------------------------------------------------------- трофеи и одежда

def belt_point(a, z=0.76, extra=0.0):
    t = (z - (J["pelvis"].z - 0.06)) / ((J["neck"].z) - (J["pelvis"].z - 0.06)) * 0.9
    return torso_point(max(0.02, t), a, extra=0.012 + extra)


def build_belt(rng):
    def geo(bm):
        seg = 20
        pts = [belt_point(2 * math.pi * k / seg) for k in range(seg + 1)]
        sweep(bm, pts, 0.011, seg=5, twist=math.pi * 6)
        # узел и свисающие концы спереди-слева
        knot = belt_point(math.pi / 2 - 0.5, extra=0.008)
        ico(bm, knot, 0.02, subdiv=1)
        for k, ln in ((0, 0.15), (1, 0.11)):
            sweep(bm, [knot, knot + V((0.01 + 0.015 * k, -0.01, -ln * 0.5)), knot + V((0.008 + 0.025 * k, 0.0, -ln))],
                  [0.008, 0.007, 0.004], seg=4)
        # мешочек на правом бедре
        pc = belt_point(math.pi + 0.35, z=0.72, extra=0.03)
        ico(bm, pc + V((0, 0, -0.04)), 0.045, scale=(0.75, 0.65, 1.0), subdiv=1)
        cylinder(bm, pc + V((0, 0, 0.0)), 0.022, 0.03, 0.025, seg=6)
        # ржавая пряжка-трофей и косточка на левом бедре
        bp = belt_point(0.25, z=0.73, extra=0.02)
        cylinder(bm, bp + V((0, 0, -0.04)), 0.028, 0.028, 0.008, seg=8, rot=Matrix.Rotation(math.pi / 2, 3, "Y"))
        sweep(bm, [bp + V((0.01, -0.01, -0.06)), bp + V((0.012, -0.03, -0.14))], [0.008, 0.007], seg=5)
        for e in (bp + V((0.01, -0.01, -0.06)), bp + V((0.012, -0.03, -0.14))):
            ico(bm, e, 0.012, subdiv=1)

    def col(c, nrm, p):
        pc = belt_point(math.pi + 0.35, z=0.72, extra=0.03)
        bp = belt_point(0.25, z=0.73, extra=0.02)
        if (c - (pc + V((0, 0, -0.03)))).length < 0.06:
            return lerp3(LEATHER, LEATHER_DARK, 0.3 + 0.5 * n01(c, 50.0))
        if (c - (bp + V((0, 0, -0.04)))).length < 0.032:
            return rust_metal(c, IRON, amount=0.75, scale=80.0)
        if (c - bp).length < 0.2 and c.z < bp.z - 0.05:
            return lerp3(BONE, BONE_DARK, 0.4 * n01(c, 60.0))
        stripe = (c.z * 120.0 + math.atan2(c.y, c.x) * 3) % 1.0
        return lerp3(ROPE, ROPE_DARK, 0.3 + 0.5 * smooth(0.4, 0.6, stripe))
    return new_part("Ghoul_Belt", geo, col, bones=["hips", "spine"])


def build_loincloth(rng):
    """Набедренник из обрывка красного табарда павшего солдата — спереди и сзади, рваный низ."""
    def flap(bm, a_center, width_ang, length, front=True):
        cols, rows = 7, 6
        grid = []
        for r in range(rows + 1):
            v = r / rows
            row = []
            for k in range(cols + 1):
                u = k / cols - 0.5
                a = a_center + u * width_ang
                top = belt_point(a, extra=0.004)
                c, tan, side, fwd = torso_frame(0.05)
                outdir = (side * math.cos(a) + fwd * math.sin(a)).normalized()
                # рваный низ — неровная длина по столбцам
                ln = length * (1.0 - 0.25 * abs(u) * 2) * (0.8 + 0.35 * n01((k * 0.7, 0, a_center), 1.0))
                p = top + V((0, 0, -ln * v)) + outdir * (0.025 * v + 0.012 * math.sin(v * 3 + k))
                row.append(bm.verts.new(p))
            grid.append(row)
        for r in range(rows):
            for k in range(cols):
                bm.faces.new((grid[r][k], grid[r][k + 1], grid[r + 1][k + 1], grid[r + 1][k]))

    def geo(bm):
        flap(bm, math.pi / 2, 1.5, 0.27)       # перед
        flap(bm, -math.pi / 2, 1.9, 0.22)      # зад
        flap(bm, math.pi / 2 + 1.3, 0.7, 0.16)  # обрывок сбоку

    def col(c, nrm, p):
        col = lerp3(CLOTH_RED, CLOTH_RED_DARK, 0.25 + 0.45 * n01(c, 14.0, (2, 0, 0)))
        col = lerp3(col, RAG_DARK, 0.35)  # выцвел и в грязи
        col = lerp3(col, SOOT, 0.45 * smooth(0.6, 0.48, c.z))  # грязный подол
        # золотая полоска каймы табарда — едва угадывается
        if 0.66 < c.z < 0.675:
            col = lerp3(col, (0.5, 0.42, 0.22), 0.6)
        return col
    return new_part("Ghoul_Loincloth", geo, col, material=MAT_2S, bones=["hips", "thigh.L", "thigh.R"])


def build_necklace(rng):
    def geo(bm):
        seg = 14
        c0 = J["neck"] + V((0, -0.005, -0.035))
        pts = []
        for k in range(seg + 1):
            a = 2 * math.pi * k / seg
            sag = 0.035 * max(0.0, math.sin(a))  # спереди провисает
            pts.append(c0 + V((math.cos(a) * 0.085, -math.sin(a) * 0.08, -sag - 0.0)))
        sweep(bm, pts, 0.005, seg=4)
        # подвески: зубы-клыки и ржавое кольцо по центру
        for k in range(-2, 3):
            a = math.pi / 2 + k * 0.32
            p = c0 + V((math.cos(a) * 0.085, -math.sin(a) * 0.08, -0.035 * math.sin(a)))
            if k == 0:
                # ржавое кольцо-обруч (дырка насквозь — читается как кольцо, а не диск)
                rc = p + V((0, -0.012, -0.032))
                hoop = [rc + V((math.cos(2 * math.pi * i / 10) * 0.02, 0, math.sin(2 * math.pi * i / 10) * 0.02))
                        for i in range(11)]
                sweep(bm, hoop, 0.0045, seg=5, cap0=False, cap1=False)
            else:
                ln = 0.04 + 0.01 * (2 - abs(k))
                d = V((0.1 * k, -0.25, -1)).normalized()
                cylinder(bm, p + d * ln * 0.5, 0.008, 0.001, ln, seg=4, rot=look_rot(d))

    def col(c, nrm, p):
        cc = J["neck"] + V((0, -0.095, -0.1))
        if (c - cc).length < 0.03:
            return rust_metal(c, IRON, amount=0.7, scale=90.0)
        if c.z < J["neck"].z - 0.055:
            return lerp3(TOOTH, TOOTH_DARK, 0.5 * n01(c, 70.0))
        return LEATHER_DARK
    return new_part("Ghoul_Necklace", geo, col, bones=["chest", "neck"])


def build_wraps(rng):
    """Грязные тряпки на правом предплечье и болтающийся конец."""
    def geo(bm):
        el, wr = J["elbow_R"], J["wrist_R"]
        axis = (wr - el).normalized()
        for k in range(3):
            c = el + (wr - el) * (0.4 + 0.2 * k)
            r = 0.041 - 0.004 * k
            cylinder(bm, c, r, r * 0.97, 0.045, seg=8, rot=look_rot(axis))
        end = el + (wr - el) * 0.55 + V((-0.04, 0.0, 0))
        sweep(bm, [end, end + V((-0.02, 0.02, -0.06)), end + V((-0.025, 0.03, -0.12))],
              [0.012, 0.011, 0.006], seg=4, sx=1.0, sy=0.3)

    def col(c, nrm, p):
        return lerp3(RAG, RAG_DARK, 0.25 + 0.6 * n01(c, 35.0))
    return new_part("Ghoul_Wraps", geo, col, bones=["forearm.R", "upperarm.R"])


def build_fungus(rng):
    """Трутовики на левом плече и лопатке + светящиеся споровые грибочки."""
    shelves = [
        (torso_point(0.78, -math.pi / 2 + 0.85, 0.0), 0.05),
        (torso_point(0.70, -math.pi / 2 + 0.95, 0.0), 0.038),
        (J["shoulder_L"] + V((0.03, 0.035, 0.035)), 0.04),
        (torso_point(0.55, -math.pi / 2 + 0.55, 0.0), 0.03),
    ]

    def geo(bm):
        for p, r in shelves:
            c, tan, side, fwd = torso_frame(0.7)
            out = (p - c).normalized()
            out.z = 0
            out = out.normalized() if out.length > 0.01 else V((1, 0, 0))
            # полудиск, торчащий из кожи
            ico(bm, p + out * r * 0.4, r, scale=(1.0, 1.0, 0.32), rot=look_rot(V((0, 0, 1)), up=out),
                subdiv=1)

    def col(c, nrm, p):
        if nrm.z > 0.5:
            return lerp3(FUNGUS, (0.9, 0.75, 0.5), 0.3 * n01(c, 60.0))
        return lerp3(FUNGUS_DARK, FUNGUS, 0.3)
    fung = new_part("Ghoul_Fungus", geo, col, bones=["chest", "spine", "clavicle.L"])

    spores = [torso_point(0.83, -math.pi / 2 + 0.55, 0.01), torso_point(0.62, -math.pi / 2 + 1.05, 0.01),
              torso_point(0.66, -math.pi / 2 + 0.75, 0.012), J["shoulder_L"] + V((0.055, 0.02, 0.05))]

    def geo2(bm):
        for i, p in enumerate(spores):
            for k in range(3):
                q = p + V((rng.uniform(-0.02, 0.02), rng.uniform(0, 0.02), rng.uniform(-0.015, 0.015)))
                h = rng.uniform(0.018, 0.03)
                cylinder(bm, q + V((0, 0, h / 2)), 0.003, 0.0025, h, seg=4)
                ico(bm, q + V((0, 0, h)), rng.uniform(0.007, 0.011), scale=(1, 1, 0.6), subdiv=1)

    def col2(c, nrm, p):
        return SPORE
    glow = new_part("Ghoul_Spores", geo2, col2, material=MAT_GLOW, bones=["chest", "spine", "clavicle.L"])
    return [fung, glow]


# ---------------------------------------------------------------- арматура

def build_armature():
    arm_data = bpy.data.armatures.new("Ghoul_Rig")
    arm = bpy.data.objects.new("Ghoul_Rig", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones
    for name, h, t, parent in BONES:
        b = eb.new(name)
        h, t = squash_bone(name, h), squash_bone(name, t)
        b.head, b.tail = h, t
        # единая ориентация осей: Z кости — вперёд (-Y) для вертикальных, вверх для горизонтальных
        axis = (t - h).normalized()
        ref = V((0, -1, 0)) if abs(axis.y) < 0.7 else V((0, 0, 1))
        b.align_roll(ref)
        if parent:
            b.parent = eb[parent]
            b.use_connect = False
        b.use_deform = name != "root"
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def skin_to(arm, body):
    body.parent = arm
    mod = body.modifiers.new("Armature", "ARMATURE")
    mod.object = arm


# ---------------------------------------------------------------- анимации

def wq(pitch=0.0, roll=0.0, yaw=0.0):
    """Поворот в осях модели (градусы): pitch — вокруг X (+ = верх кости наклоняется вперёд, низ — назад),
    roll — вокруг Y, yaw — вокруг Z."""
    return (Matrix.Rotation(math.radians(yaw), 3, "Z") @ Matrix.Rotation(math.radians(roll), 3, "Y")
            @ Matrix.Rotation(math.radians(pitch), 3, "X")).to_quaternion()


def apply_pose(arm, pose, frame):
    """pose: {bone: (pitch, roll, yaw)} + 'loc': (x, y, z) для root. Ставит ключи всем костям."""
    for pb in arm.pose.bones:
        rest = pb.bone.matrix_local.to_3x3()
        rot = pose.get(pb.name, (0, 0, 0))
        q_world = wq(*rot)
        q_local = (rest.inverted() @ q_world.to_matrix() @ rest).to_quaternion()
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = q_local
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        if pb.name == "root":
            loc = V(pose.get("loc", (0, 0, 0)))
            pb.location = rest.inverted() @ loc
            pb.keyframe_insert("location", frame=frame)


def add(*poses):
    out = {}
    for p in poses:
        for k, v in p.items():
            o = out.get(k, (0, 0, 0))
            out[k] = tuple(o[i] + v[i] for i in range(3))
    return out


def scale(p, k):
    return {n: tuple(x * k for x in v) for n, v in p.items()}


def lerp_pose(a, b, t):
    keys = set(a) | set(b)
    return {k: tuple(a.get(k, (0, 0, 0))[i] * (1 - t) + b.get(k, (0, 0, 0))[i] * t for i in range(3)) for k in keys}


def keyposes(seq, t):
    """seq: [(t, pose, ease)] → поза в момент t (ease: 'smooth' | 'in' | 'out' | 'lin')."""
    for (t0, p0, _), (t1, p1, ease) in zip(seq[:-1], seq[1:]):
        if t0 <= t <= t1:
            u = (t - t0) / max(t1 - t0, 1e-6)
            if ease == "smooth":
                u = u * u * (3 - 2 * u)
            elif ease == "in":
                u = u * u
            elif ease == "out":
                u = 1 - (1 - u) ** 2
            return lerp_pose(p0, p1, u)
    return dict(seq[-1][1])


S = math.sin
C = math.cos
TAU = 2 * math.pi


def idle_pose(ph):
    b = S(TAU * ph)
    b2 = S(TAU * ph * 2 + 0.6)
    tw = math.exp(-((ph - 0.58) / 0.025) ** 2)   # резкий поворот головы — прислушивается
    tw2 = math.exp(-((ph - 0.3) / 0.02) ** 2)    # дёрнул ухом
    return {
        "loc": (0.0, 0.0, -0.012 * (b * 0.5 + 0.5)),
        "hips": (0, 2.0 * S(TAU * ph + 1.0), 0),
        "spine": (2.0 * b, 0, 0),
        "chest": (2.5 * b, -1.0 * S(TAU * ph + 1.0), 0),
        "neck": (-2.0 * b, 0, 0),
        "head": (-1.5 * b + 4 * tw, 6 * tw, 14 * tw + 3 * S(TAU * ph)),
        "jaw": (-(4 + 4 * max(0.0, b)), 0, 0),
        "ear.L": (0, 0, 8 * tw2),
        "ear.R": (0, 0, -3 * tw),
        "upperarm.L": (3 * S(TAU * ph + 0.8), -2, 0),
        "upperarm.R": (3 * S(TAU * ph + 1.4), 2, 0),
        "forearm.L": (-4 - 3 * b, 0, 0),
        "forearm.R": (-4 - 3 * b, 0, 0),
        "fingers.L": (-8 - 8 * (b2 * 0.5 + 0.5), 0, 0),
        "fingers.R": (-8 - 8 * (S(TAU * ph * 2 + 2.2) * 0.5 + 0.5), 0, 0),
        "thigh.L": (0, 0, 0), "shin.L": (0, 0, 0),
        "shin.R": (0, 0, 0),
    }


LEAN0, LEAN1 = 56.0, 4.0
ARM_BASE, ARM_WIDE, LEG_BASE, LEG_WIDE = 44.0, 50.0, 0.0, 34.0


def limb_cycle(x, stance=0.6):
    """Фаза 0..1 → (вынос вперёд -1..1, подъём 0..1). Опора: лапа на земле едет назад; перенос: дугой вперёд."""
    x %= 1.0
    if x < stance:
        u = x / stance
        return 1.0 - 2.0 * u, 0.0
    u = (x - stance) / (1.0 - stance)
    e = u * u * (3 - 2 * u)
    return -1.0 + 2.0 * e, math.sin(math.pi * u)


def quad_pose(ph, run, lean=None):
    """Ползком, как упырь: корпус низко и горизонтально, лапы широко в стороны.
    Длинные руки гребут попеременно: лапа взлетает, широкой дугой выносится вперёд и вбивается когтями в землю,
    подтягивая тело; короткие задние ноги по-лягушачьи коленями наружу. Корпус перекатывается с боку на бок."""
    reach = 32 + 10 * run   # вынос руки вперёд-назад (град)
    raise_ = 40 + 15 * run  # насколько лапа взлетает в переносе
    stride = 20 + 8 * run   # шаг задних ног
    pose = {}
    fwd = {}
    for s in ("L", "R"):
        out = -1 if s == "L" else 1
        # рука и противоположная нога работают вместе; рука чуть раньше
        f, lift = limb_cycle(ph + (0.0 if s == "L" else 0.5), 0.58)
        fwd[s] = f
        pose["clavicle." + s] = (-6 * f, 12 * out, 4 * f * -out)
        # корпус почти горизонтален, поэтому «в стороны» — это поворот вокруг его оси «вверх» (yaw), а не roll
        pose["upperarm." + s] = (-(LEAN0 + LEAN1 * run) - 26 + ARM_BASE - reach * f - raise_ * lift,
                                 0, -(ARM_WIDE + 12 * lift) * out)
        pose["forearm." + s] = (-55 - 35 * lift + 15 * f, 0, 0)
        pose["hand." + s] = (62 + 25 * lift - 10 * f, 0, 0)
        pose["fingers." + s] = (18 - 35 * lift, 0, 0)  # когти растопырены перед ударом, подгребают на опоре
        g, glift = limb_cycle(ph + (0.5 if s == "L" else 0.0) + 0.08, 0.62)
        pose["thigh." + s] = (-(LEAN0 + LEAN1 * run) - 40 + LEG_BASE - stride * g - 10 * glift, 0, -LEG_WIDE * out)
        pose["shin." + s] = (42 + 18 * glift, 0, 0)
        pose["foot." + s] = (-6 - 10 * glift, 0, 0)
        pose["toes." + s] = (10 * max(0.0, -g), 0, 0)
    # корпус: перекат к опорной руке, поворот плеч за выносимой лапой
    roll = (fwd["R"] - fwd["L"]) * 0.5
    pose["root"] = (LEAN0 + LEAN1 * run, 0, 0)
    pose["loc"] = (0.0, 0.14, 0.0)
    pose["hips"] = (-6, -5 * roll, -7 * roll)
    pose["spine"] = (18, 4 * roll, 6 * roll)
    pose["chest"] = (10, 8 * roll, 12 * roll)
    pose["neck"] = (-40, 0, -6 * roll)
    pose["head"] = (-34, -4 * roll, -8 * roll)
    pose["jaw"] = (-(12 + 10 * run), 0, 0)
    pose["ear.L"] = (0, 0, 14 + 6 * run)
    pose["ear.R"] = (0, 0, -14 - 6 * run)
    return pose


def gait_pose(ph, run):
    """Пальцеходящий бег/шаг. run 0 — крадущийся шаг, 1 — бег."""
    a = TAU * ph
    amp = 20 + 18 * run
    pose = {}
    for s, off in (("L", 0.0), ("R", math.pi)):
        x = a + off
        # отрицательный pitch бедра — нога вперёд
        pose["thigh." + s] = (-amp * S(x) - 6 * run, 0, 0)
        # голень складывается в фазе переноса (нога идёт вперёд и поднята)
        lift = max(0.0, C(x))
        pose["shin." + s] = (-(15 + 30 * run) * lift + 6, 0, 0)
        pose["foot." + s] = ((20 + 25 * run) * lift - 8 * S(x), 0, 0)
        pose["toes." + s] = (-12 * max(0.0, -S(x)) * (0.5 + run), 0, 0)
    bob = abs(C(a))  # два шага за цикл
    pose["loc"] = (0.015 * S(a) * (1 - run * 0.5), 0.0, -0.035 * run - 0.04 * (1 - bob) * (0.5 + run))
    pose["hips"] = (0, 4 * S(a), 6 * S(a))
    pose["spine"] = (6 + 10 * run, 0, -5 * S(a))
    pose["chest"] = (4 + 8 * run + 3 * bob, -3 * S(a), -6 * S(a))
    pose["neck"] = (-8 - 10 * run, 0, 3 * S(a))
    pose["head"] = (-6 - 9 * run + 3 * bob, 2 * S(a), 4 * S(a))
    pose["jaw"] = (-(6 + 10 * run) - 4 * bob * run, 0, 0)
    for s, off in (("L", math.pi), ("R", 0.0)):
        x = a + off
        pose["upperarm." + s] = (-(15 + 20 * run) * S(x) + 12 * run, 0, 0)
        pose["forearm." + s] = (-10 - 25 * run - 10 * max(0.0, S(x)) * run, 0, 0)
        pose["hand." + s] = (-8 * run, 0, 0)
        pose["fingers." + s] = (-10 - 10 * run, 0, 0)
    pose["ear.L"] = (0, 0, 6 * run)
    pose["ear.R"] = (0, 0, -6 * run)
    return pose


REST = {}

def _arms(up_l, up_r, fore, hand, fingers, roll=0.0):
    """Обе лапы одинаково (зеркально по roll)."""
    return {"upperarm.L": (up_l, -roll, 0), "upperarm.R": (up_r, roll, 0),
            "forearm.L": (fore, 0, 0), "forearm.R": (fore, 0, 0),
            "hand.L": (hand, 0, 0), "hand.R": (hand, 0, 0),
            "fingers.L": (fingers, 0, 0), "fingers.R": (fingers, 0, 0)}


# Удар двумя лапами сверху вниз: встаёт на дыбы, лапы над головой когтями назад — и рушит их вниз-вперёд.
ATTACK = [
    (0.00, {}, "lin"),
    # подсел перед прыжком вверх
    (0.14, add({"loc": (0, 0.03, -0.05), "spine": (6, 0, 0), "chest": (4, 0, 0), "head": (6, 0, 0), "jaw": (-10, 0, 0),
                "thigh.L": (-12, 0, 0), "shin.L": (14, 0, 0), "thigh.R": (-12, 0, 0), "shin.R": (14, 0, 0)},
               _arms(-25, -25, -30, -10, 5, 10)), "smooth"),
    # на дыбах: корпус откинут назад, лапы над головой, когти за затылком, пасть раскрыта
    (0.40, add({"loc": (0, 0.07, 0.06), "spine": (-14, 0, 0), "chest": (-16, 0, 0), "neck": (6, 0, 0),
                "head": (-14, 0, 0), "jaw": (-30, 0, 0), "clavicle.L": (0, -10, 0), "clavicle.R": (0, 10, 0),
                "thigh.L": (6, 0, 0), "shin.L": (-6, 0, 0), "thigh.R": (6, 0, 0), "shin.R": (-6, 0, 0),
                "ear.L": (0, 0, 14), "ear.R": (0, 0, -14)},
               _arms(-150, -150, -55, -25, 20, 18)), "out"),
    (0.48, add({"loc": (0, 0.08, 0.07), "spine": (-16, 0, 0), "chest": (-18, 0, 0), "neck": (6, 0, 0),
                "head": (-16, 0, 0), "jaw": (-32, 0, 0), "clavicle.L": (0, -12, 0), "clavicle.R": (0, 12, 0),
                "thigh.L": (6, 0, 0), "shin.L": (-6, 0, 0), "thigh.R": (6, 0, 0), "shin.R": (-6, 0, 0),
                "ear.L": (0, 0, 16), "ear.R": (0, 0, -16)},
               _arms(-158, -158, -62, -28, 22, 18)), "smooth"),
    # удар: обе лапы рушатся вниз перед собой, корпус складывается вперёд, выпад
    (0.58, add({"loc": (0, -0.2, -0.11), "spine": (24, 0, 0), "chest": (22, 0, 0), "neck": (-14, 0, 0),
                "head": (-12, 0, 0), "jaw": (-34, 0, 0), "clavicle.L": (0, 6, 0), "clavicle.R": (0, -6, 0),
                "thigh.L": (-32, 0, 0), "shin.L": (-10, 0, 0), "foot.L": (22, 0, 0),
                "thigh.R": (14, 0, 0), "shin.R": (8, 0, 0), "ear.L": (0, 0, 6), "ear.R": (0, 0, -6)},
               _arms(-38, -38, -6, 14, -38, 6)), "in"),
    # когти скребут по земле, отдача
    (0.70, add({"loc": (0, -0.2, -0.12), "spine": (26, 0, 0), "chest": (22, 0, 0), "neck": (-16, 0, 0),
                "head": (-12, 0, 0), "jaw": (-20, 0, 0),
                "thigh.L": (-32, 0, 0), "shin.L": (-10, 0, 0), "foot.L": (22, 0, 0),
                "thigh.R": (14, 0, 0), "shin.R": (8, 0, 0)},
               _arms(-18, -18, -14, 10, -30, 6)), "out"),
    (1.00, {}, "smooth"),
]

HIT = [
    (0.0, {}, "lin"),
    (0.3, {"loc": (0, 0.07, 0.0), "spine": (-8, 3, 4), "chest": (-14, 4, 6), "neck": (-6, 0, 0), "head": (-20, 8, 10),
           "jaw": (-18, 0, 0), "upperarm.L": (15, -25, 0), "upperarm.R": (15, 25, 0),
           "forearm.L": (-25, 0, 0), "forearm.R": (-25, 0, 0), "ear.L": (0, 0, 15), "ear.R": (0, 0, -15),
           "thigh.L": (6, 0, 0), "thigh.R": (6, 0, 0)}, "out"),
    (1.0, {}, "smooth"),
]

# Смерть: вздрог назад → шатается → колени подкашиваются → падает на колени → медленно валится ничком.
# Позы сглаживаются сплайном (spline_poses), а не по отрезкам — без рывков на стыках.
DEATH = [
    (0.00, {}),
    (0.10, {"loc": (0, 0.08, 0.02), "spine": (-10, 3, 4), "chest": (-16, 5, 6), "neck": (-8, 0, 0),
            "head": (-26, 8, 12), "jaw": (-30, 0, 0), "upperarm.L": (18, -35, 0), "upperarm.R": (18, 35, 0),
            "forearm.L": (-35, 0, 0), "forearm.R": (-35, 0, 0), "ear.L": (0, 0, 20), "ear.R": (0, 0, -20)}),
    (0.26, {"loc": (0.02, 0.11, -0.02), "hips": (0, 5, 4), "spine": (-6, 6, 8), "chest": (-8, 6, 8),
            "neck": (0, 0, 0), "head": (-10, 14, 16), "jaw": (-26, 0, 0),
            "upperarm.L": (10, -25, 0), "upperarm.R": (5, 30, 0), "forearm.L": (-25, 0, 0), "forearm.R": (-30, 0, 0),
            "thigh.L": (-10, 0, 0), "shin.L": (12, 0, 0), "thigh.R": (4, 0, 0), "shin.R": (-2, 0, 0)}),
    (0.44, {"loc": (0, 0.06, -0.2), "hips": (0, 3, 2), "spine": (10, 6, 6), "chest": (12, 6, 4), "neck": (6, 0, 0),
            "head": (8, 14, 12), "jaw": (-20, 0, 0),
            "upperarm.L": (-8, -18, 0), "upperarm.R": (-4, 22, 0), "forearm.L": (-20, 0, 0), "forearm.R": (-22, 0, 0),
            "fingers.L": (-15, 0, 0), "fingers.R": (-15, 0, 0),
            "thigh.L": (-45, 0, 0), "shin.L": (55, 0, 0), "foot.L": (-10, 0, 0),
            "thigh.R": (-40, 0, 0), "shin.R": (52, 0, 0), "foot.R": (-12, 0, 0)}),
    # на коленях, корпус клонится вперёд, лапы обвисли
    (0.60, {"root": (18, 2, 4), "loc": (0, 0.04, -0.3), "spine": (14, 6, 4), "chest": (14, 5, 2), "neck": (6, 0, 0),
            "head": (10, 18, 16), "jaw": (-18, 0, 0),
            "upperarm.L": (-20, -12, 0), "upperarm.R": (-15, 18, 0), "forearm.L": (-15, 0, 0), "forearm.R": (-18, 0, 0),
            "fingers.L": (-20, 0, 0), "fingers.R": (-20, 0, 0),
            "thigh.L": (-60, 0, 0), "shin.L": (75, 0, 0), "foot.L": (15, 0, 0), "toes.L": (25, 0, 0),
            "thigh.R": (-55, 0, 0), "shin.R": (72, 0, 0), "foot.R": (15, 0, 0), "toes.R": (25, 0, 0)}),
    (0.78, {"root": (52, 3, 7), "loc": (0, 0.0, -0.16), "spine": (4, 5, 4), "chest": (2, 4, 2), "neck": (-10, 0, 0),
            "head": (-8, 24, 28), "jaw": (-22, 0, 0),
            "upperarm.L": (-130, -42, 0), "upperarm.R": (20, 35, 5), "forearm.L": (-15, 0, 0), "forearm.R": (8, 0, 0),
            "fingers.R": (10, 0, 0),
            "thigh.L": (-35, 0, 0), "shin.L": (45, 0, 0), "foot.L": (30, 0, 0), "toes.L": (30, 0, 0),
            "thigh.R": (-40, 0, 0), "shin.R": (55, 0, 0), "foot.R": (30, 0, 0), "toes.R": (30, 0, 0)}),
    (0.92, {"root": (80, 4, 9), "loc": (0, -0.03, 0.04), "spine": (-6, 4, 4), "chest": (-7, 4, 0), "neck": (-20, 0, 0),
            "head": (-16, 30, 38), "jaw": (-26, 0, 0),
            "upperarm.L": (-118, -30, 0), "upperarm.R": (25, 30, 0), "forearm.L": (0, 0, 0), "forearm.R": (5, 0, 0),
            "thigh.L": (-6, 0, 0), "shin.L": (8, 0, 0), "foot.L": (25, 0, 0), "toes.L": (20, 0, 0),
            "thigh.R": (-12, 0, 0), "shin.R": (12, 0, 0), "foot.R": (25, 0, 0), "toes.R": (20, 0, 0)}),
    (1.00, {"root": (83, 4, 10), "loc": (0, -0.04, 0.06), "spine": (-8, 4, 4), "chest": (-8, 4, 0), "neck": (-22, 0, 0),
            "head": (-18, 30, 40), "jaw": (-24, 0, 0),
            "upperarm.L": (-124, -32, 0), "upperarm.R": (35, 32, 0), "forearm.L": (6, 0, 0), "forearm.R": (8, 0, 0),
            "fingers.L": (-25, 0, 0), "fingers.R": (10, 0, 0),
            "thigh.L": (-4, 0, 0), "shin.L": (6, 0, 0), "foot.L": (30, 0, 0), "toes.L": (25, 0, 0),
            "thigh.R": (-10, 0, 0), "shin.R": (10, 0, 0), "foot.R": (30, 0, 0), "toes.R": (25, 0, 0)}),
]


def spline_poses(seq, t):
    """Кубический Эрмит по ключевым позам (касательные по соседям, на концах — ноль): плавно, без изломов."""
    ts = [k[0] for k in seq]
    ps = [k[1] for k in seq]
    keys = set()
    for p in ps:
        keys |= set(p)
    n = len(seq)
    i = max(0, min(n - 2, max(j for j in range(n) if ts[j] <= t)))
    t0, t1 = ts[i], ts[i + 1]
    h = t1 - t0
    u = (t - t0) / h

    def val(j, k):
        return V(ps[j].get(k, (0, 0, 0)))

    def tangent(j, k):
        if j == 0 or j == n - 1:
            return V((0, 0, 0))
        return (val(j + 1, k) - val(j - 1, k)) / (ts[j + 1] - ts[j - 1])
    h00 = 2 * u ** 3 - 3 * u ** 2 + 1
    h10 = u ** 3 - 2 * u ** 2 + u
    h01 = -2 * u ** 3 + 3 * u ** 2
    h11 = u ** 3 - u ** 2
    out = {}
    for k in keys:
        v = val(i, k) * h00 + tangent(i, k) * h * h10 + val(i + 1, k) * h01 + tangent(i + 1, k) * h * h11
        out[k] = tuple(v)
    return out


# имя: (кадров, петля, функция фазы → поза)
ANIMS = {
    "Idle": (60, True, lambda ph: idle_pose(ph)),
    "Walk": (40, True, lambda ph: quad_pose(ph, 0.0)),
    "Run": (22, True, lambda ph: quad_pose(ph, 1.0)),
    "Attack": (26, False, lambda ph: add(scale(idle_pose(0), 1 - ph), keyposes(ATTACK, ph))),
    "Hit": (10, False, lambda ph: add(scale(idle_pose(0), 1.0), keyposes(HIT, ph))),
    "Death": (54, False, lambda ph: add(scale(idle_pose(0), max(0.0, 1 - ph * 2.5)), spline_poses(DEATH, ph))),
}

# клипы, где тело ложится на землю: корень приподнимается ровно настолько, чтобы ничто не ушло под землю
# "lift" — только приподнять (смерть, бег с фазой полёта); "contact" — держать нижнюю точку на земле (шаг)
GROUND_FIX = {"Death": "lift", "Walk": "contact", "Run": "contact"}


def lowest_z(body):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = body.evaluated_get(dg)
    me = ev.to_mesh()
    z = min((body.matrix_world @ v.co).z for v in me.vertices)
    ev.to_mesh_clear()
    return z


def build_actions(arm, body):
    arm.animation_data_create()
    actions = {}
    sc = bpy.context.scene
    set_frame = sc.frame_set
    rest_floor = None
    for name, (frames, loop, fn) in ANIMS.items():
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        arm.animation_data.action = act
        last = frames if loop else frames - 1
        poses = []
        for f in range(0, last + 1):
            ph = f / frames if loop else f / max(1, frames - 1)
            pose = fn(ph % 1.0 if loop else ph)
            poses.append(pose)
            apply_pose(arm, pose, f)
        if name == "Idle":
            set_frame(0)
            rest_floor = lowest_z(body)  # кончики когтей ног в стойке (чуть ниже нуля)
        if name in GROUND_FIX:
            mode = GROUND_FIX[name]
            need = []
            for f in range(0, last + 1):
                set_frame(f)
                d = (rest_floor + 0.01) - lowest_z(body)
                need.append(d if mode == "contact" else max(0.0, d))
            n = len(need)

            def at(i):  # петли сглаживаем по кругу, остальное — с краями
                return need[i % n] if loop else need[max(0, min(n - 1, i))]
            win = 1 if loop else 3
            if mode == "lift":
                mx = [max(at(j) for j in range(i - win, i + win + 1)) for i in range(n)]
            else:
                mx = need
            lift = [sum(mx[(j % n) if loop else max(0, min(n - 1, j))] for j in range(i - win, i + win + 1)) / (2 * win + 1)
                    for i in range(n)]
            if loop:
                lift[-1] = lift[0]
            for f, pose in enumerate(poses):
                if lift[f] > 1e-4:
                    pose = dict(pose)
                    lx, ly, lz = pose.get("loc", (0, 0, 0))
                    pose["loc"] = (lx, ly, lz + lift[f])
                    apply_pose(arm, pose, f)
            print(f"[ghoul] {name}: подъём над землёй до {max(lift):.3f} м")
        actions[name] = (act, last)
    # каждое действие — в свою NLA-дорожку (так FBX-экспорт отдаёт их отдельными клипами)
    arm.animation_data.action = None
    for name, (act, last) in actions.items():
        tr = arm.animation_data.nla_tracks.new()
        tr.name = name
        st = tr.strips.new(name, 0, act)
        st.frame_end = last
        tr.mute = True
    return actions
def set_action(arm, name):
    arm.animation_data.action = bpy.data.actions[name]
    try:  # Blender 4.4+: у действия есть слоты
        if arm.animation_data.action_slot is None and len(arm.animation_data.action.slots):
            arm.animation_data.action_slot = arm.animation_data.action.slots[0]
    except AttributeError:
        pass


# ---------------------------------------------------------------- экспорт

def export_fbx(path, arm, body):
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = arm
    arm.animation_data.action = None
    for tr in arm.animation_data.nla_tracks:
        tr.mute = False
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", bake_space_transform=False,
        mesh_smooth_type="FACE", use_mesh_modifiers=False, colors_type="SRGB",
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        use_armature_deform_only=False,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0, bake_anim_step=1.0, path_mode="STRIP")
    for tr in arm.animation_data.nla_tracks:
        tr.mute = True


# ---------------------------------------------------------------- превью

def preview_setup():
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 40
    sc.cycles.device = "CPU"
    sc.view_settings.view_transform = "Standard"
    sc.render.film_transparent = False
    w = bpy.data.worlds.new("w")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.06, 0.07, 0.11, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 1.0
    for name in (MAT, MAT_2S, MAT_GLOW):
        m = bpy.data.materials[name]
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        o = nt.nodes.new("ShaderNodeOutputMaterial")
        a = nt.nodes.new("ShaderNodeVertexColor")
        a.layer_name = "Col"
        if name == MAT_GLOW:
            e = nt.nodes.new("ShaderNodeEmission")
            e.inputs["Strength"].default_value = 6.0
            nt.links.new(a.outputs[0], e.inputs[0])
            nt.links.new(e.outputs[0], o.inputs[0])
        else:
            b = nt.nodes.new("ShaderNodeBsdfPrincipled")
            b.inputs["Roughness"].default_value = 0.85
            nt.links.new(a.outputs[0], b.inputs["Base Color"])
            nt.links.new(b.outputs[0], o.inputs[0])
    bpy.ops.mesh.primitive_plane_add(size=300, location=(0, 0, 0))
    ground = bpy.context.active_object
    gm = bpy.data.materials.new("ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.09, 0.085, 0.08, 1)
    ground.data.materials.append(gm)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    key = bpy.data.objects.new("key", bpy.data.lights.new("key", "SUN"))
    key.data.energy = 3.0
    key.rotation_euler = (math.radians(50), 0, math.radians(-35))
    sc.collection.objects.link(key)
    rim = bpy.data.objects.new("rim", bpy.data.lights.new("rim", "SUN"))
    rim.data.energy = 2.2
    rim.data.color = (0.6, 0.72, 1.0)
    rim.rotation_euler = (math.radians(65), 0, math.radians(165))
    sc.collection.objects.link(rim)
    return cam


def aim(cam, target, yaw_deg, pitch_deg, dist, lens=50):
    yaw, pitch = math.radians(yaw_deg), math.radians(pitch_deg)
    # yaw 0 — камера спереди (со стороны -Y)
    d = V((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    cam.location = V(target) + d * dist
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens


def render(path, w, h):
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def make_previews(arm, out, preview, poses):
    from PIL import Image
    cam = preview_setup()
    tmp = os.path.join(out, "_tmp")
    os.makedirs(tmp, exist_ok=True)
    sc = bpy.context.scene
    if preview:
        set_action(arm, "Idle")
        sc.frame_set(0)
        shots = []
        for i, (yaw, pitch, dist, tgt, lens) in enumerate([
                (-28, 12, 3.0, (0, -0.05, 0.55), 50),    # 3/4 спереди
                (90, 6, 3.0, (0, -0.05, 0.52), 50),      # профиль
                (160, 25, 3.0, (0, 0.0, 0.55), 50),      # со спины
                (-15, 8, 0.95, (0, -0.38, 1.03), 60)]):  # морда крупно
            aim(cam, tgt, yaw, pitch, dist, lens)
            p = os.path.join(tmp, f"v{i}.png")
            render(p, 700, 900)
            shots.append(Image.open(p))
        sheet = Image.new("RGB", (700 * 4, 900))
        for i, im in enumerate(shots):
            sheet.paste(im, (700 * i, 0))
        sheet.save(preview)
    if poses:
        frames = [("Attack", 4), ("Attack", 11), ("Attack", 15), ("Attack", 18), ("Death", 6), ("Death", 24),
                  ("Death", 40), ("Death", 53)]
        shots = []
        for i, (act, f) in enumerate(frames):
            set_action(arm, act)
            sc.frame_set(f)
            aim(cam, (0, -0.2, 0.6), -55, 22, 3.6, 50)
            p = os.path.join(tmp, f"p{i}.png")
            render(p, 520, 640)
            shots.append(Image.open(p))
        sheet = Image.new("RGB", (520 * 4, 640 * 2))
        for i, im in enumerate(shots):
            sheet.paste(im, (520 * (i % 4), 640 * (i // 4)))
        sheet.save(poses)
        set_action(arm, "Idle")
        sc.frame_set(0)


# ---------------------------------------------------------------- main

def main():
    out, preview, poses = args()
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.fps = FPS
    for m in (MAT, MAT_2S, MAT_GLOW):
        bpy.data.materials.new(m)
    rng = random.Random(SEED)

    build_torso(rng)
    build_ribs(rng)
    build_spine_ridge(rng)
    build_neck(rng)
    build_head(rng)
    build_mouth(rng)
    build_jaw(rng)
    build_teeth(rng, lower=False)
    build_teeth(rng, lower=True)
    build_eyes(rng)
    build_ears(rng)
    build_arms(rng)
    build_hands(rng)
    build_legs(rng)
    build_belt(rng)
    build_loincloth(rng)
    build_necklace(rng)
    build_wraps(rng)
    build_fungus(rng)

    if SUBD_LEVEL > 0:
        merge_skin()
    for obj, bones, rigid in PARTS:
        if rigid != "__done__":
            assign_weights(obj, bones, rigid)
    objs = [p[0] for p in PARTS]
    with bpy.context.temp_override(active_object=objs[0], object=objs[0], selected_objects=objs,
                                   selected_editable_objects=objs):
        bpy.ops.object.join()
    body = objs[0]
    body.name = body.data.name = "Ghoul_Body"
    # короткие ноги: сжать вершины по весам ног
    leg_groups = {g.index for g in body.vertex_groups if g.name.startswith(LEG_BONES)}
    for v in body.data.vertices:
        legw = sum(g.weight for g in v.groups if g.group in leg_groups)
        v.co = squash(v.co, min(1.0, legw))
    if SUBD_LEVEL > 0:
        paint_pass(body)
    tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
    print(f"[ghoul] triangles: {tris}, verts: {len(body.data.vertices)}")

    arm = build_armature()
    skin_to(arm, body)
    build_actions(arm, body)
    set_action(arm, "Idle")
    sc.frame_set(0)

    export_fbx(os.path.join(out, "Mon_Ghoul.fbx"), arm, body)
    set_action(arm, "Idle")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "ghoul.blend"))
    if preview or poses:
        make_previews(arm, out, preview and os.path.abspath(preview), poses and os.path.abspath(poses))
    print("[ghoul] done:", out)


if __name__ == "__main__":
    main()
