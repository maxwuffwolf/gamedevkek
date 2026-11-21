# Генератор стартового набора лесных моделей в стиле проекта.
#
# Делает камни, деревья, ёлки, кусты и траву, раскрашенные палитрой T_Palette_Forest.png:
# цвет по высоте (низ тёмный, верх светлый), мох на гранях, смотрящих вверх.
# У каждой модели точка опоры внизу по центру, масштаб применён, имена Env_*.
#
# Запуск в Blender: вкладка Scripting → Open → этот файл → Run Script (▶).
# Скрипт удалит всё в текущей сцене и построит набор заново.
# Палитру скрипт ищет сам в Game/Assets/_Project/Art/Textures/Environment/ (вверх от папки скрипта).
#
# Можно и без окна: blender -b -P forest_kit.py -- --out forest_kit.blend --preview preview.png

import math
import os
import random
import sys

import bpy  # bpy первым: в режиме модуля bmesh доступен только после него
import bmesh
from mathutils import Matrix, Vector, noise

# ---------------------------------------------------------------- настройки

PALETTE_REL = os.path.join("Game", "Assets", "_Project", "Art", "Textures", "Environment", "T_Palette_Forest.png")
SEED = 7


def script_dir():
    """Папка этого скрипта: и при запуске из вкладки Scripting, и из командной строки."""
    try:
        sd = bpy.context.space_data
        if sd and getattr(sd, "text", None) and sd.text.filepath:
            return os.path.dirname(bpy.path.abspath(sd.text.filepath))
    except Exception:
        pass
    f = globals().get("__file__")
    if f and os.path.isabs(f):
        return os.path.dirname(f)
    return os.getcwd()


def find_palette():
    here = script_dir()
    candidates = []
    d = here
    for _ in range(6):  # ищем корень репозитория вверх по папкам
        candidates.append(os.path.join(d, PALETTE_REL))
        d = os.path.dirname(d)
    if bpy.data.filepath:
        candidates.append(os.path.join(os.path.dirname(bpy.data.filepath), "T_Palette_Forest.png"))
    candidates.append(os.path.join(here, "T_Palette_Forest.png"))
    for c in candidates:
        if os.path.exists(c):
            return c
    raise FileNotFoundError("Не нашёл T_Palette_Forest.png. Проверь пути: " + "; ".join(candidates))

# Палитра 8x4 полос. (ряд сверху, колонка слева), как на превью с подписями.
SW = {
    "leaf_dark": (0, 0), "leaf": (0, 1), "leaf_light": (0, 2), "leaf_teal": (0, 3),
    "autumn_orange": (0, 4), "autumn_red": (0, 5), "blossom": (0, 6), "lilac": (0, 7),
    "grass": (1, 0), "moss": (1, 1), "dry_grass": (1, 2), "dirt": (1, 3),
    "dirt_dark": (1, 4), "sand": (1, 5), "path": (1, 6), "mud": (1, 7),
    "bark_dark": (2, 0), "bark": (2, 1), "bark_light": (2, 2), "birch": (2, 3),
    "deadwood": (2, 4), "planks": (2, 5), "pine": (2, 6), "pine_light": (2, 7),
    "stone_cold": (3, 0), "stone_warm": (3, 1), "stone_blue": (3, 2), "stone_moss": (3, 3),
    "stone_dark": (3, 4), "stone_light": (3, 5), "flower_yellow": (3, 6), "flower_blue": (3, 7),
}
COLS, ROWS = 8, 4


def swatch_uv(name, t, s=0.5):
    """UV точки в полосе: t=0 низ (тёмный), t=1 верх (светлый); s — положение поперёк полосы."""
    r, c = SW[name]
    w, h = 1.0 / COLS, 1.0 / ROWS
    u0, v0 = c * w, 1.0 - (r + 1) * h
    mu, mv = w * 0.2, h * 0.04
    t = min(1.0, max(0.0, t))
    s = min(1.0, max(0.0, s))
    return Vector((u0 + mu + s * (w - 2 * mu), v0 + mv + t * (h - 2 * mv)))


# ---------------------------------------------------------------- утилиты bmesh

class Part:
    """Кусок модели: bmesh + какой полосой его красить."""

    def __init__(self, bm, swatch, t_range=(0.0, 1.0), smooth=False, top_swatch=None, top_dot=0.55):
        self.bm, self.swatch, self.t_range, self.smooth = bm, swatch, t_range, smooth
        self.top_swatch, self.top_dot = top_swatch, top_dot


def ico(radius, subdiv=2):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    return bm


def cone(r1, r2, depth, segs=8):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=depth)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, depth / 2))
    return bm


def displace(bm, amount, scale, seed):
    off = Vector((seed * 13.1, seed * 7.7, seed * 3.3))
    for v in bm.verts:
        n = noise.noise_vector(v.co * scale + off)
        v.co += n * amount


def tube(points, radii, segs=8):
    """Изогнутая труба по точкам (ствол/ветка) с разным радиусом."""
    bm = bmesh.new()
    rings = []
    for i, p in enumerate(points):
        d = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
        q = d.to_track_quat("Z", "Y")
        ring = []
        for k in range(segs):
            a = 2 * math.pi * k / segs
            local = Vector((math.cos(a) * radii[i], math.sin(a) * radii[i], 0))
            ring.append(bm.verts.new(p + q @ local))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for k in range(segs):
            bm.faces.new((a[k], a[(k + 1) % segs], b[(k + 1) % segs], b[k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def transform(bm, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    m = Matrix.Translation(loc) @ Matrix.Rotation(rot[2], 4, "Z") @ Matrix.Rotation(rot[1], 4, "Y") \
        @ Matrix.Rotation(rot[0], 4, "X") @ Matrix.Diagonal((*scale, 1))
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return bm


def flatten_bottom(bm, frac=0.12):
    zs = [v.co.z for v in bm.verts]
    zmin, zmax = min(zs), max(zs)
    cut = zmin + (zmax - zmin) * frac
    for v in bm.verts:
        if v.co.z < cut:
            v.co.z = cut


# ---------------------------------------------------------------- сборка объекта

def build_object(name, parts, material):
    mesh = bpy.data.meshes.new(name)
    out = bmesh.new()
    uv = out.loops.layers.uv.new("UVMap")
    for part in parts:
        bm = part.bm
        bm.normal_update()
        zs = [v.co.z for v in bm.verts]
        zmin, zmax = min(zs), max(zs)
        span = max(zmax - zmin, 1e-4)
        t0, t1 = part.t_range
        tmp = bpy.data.meshes.new("tmp")
        lay = bm.loops.layers.uv.verify()
        for f in bm.faces:
            top = part.top_swatch and f.normal.z > part.top_dot
            sw = part.top_swatch if top else part.swatch
            s = 0.5 + 0.35 * math.sin(f.calc_center_median().x * 3.1 + f.calc_center_median().y * 1.7)
            for l in f.loops:
                t = t0 + (t1 - t0) * (l.vert.co.z - zmin) / span
                if top:
                    t = 0.15 + 0.5 * t
                l[lay].uv = swatch_uv(sw, t, s)
            f.smooth = part.smooth
        bm.to_mesh(tmp)
        out.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
        bm.free()
    # точка опоры: низ по центру
    zs = [v.co.z for v in out.verts]
    xs = [v.co.x for v in out.verts]
    ys = [v.co.y for v in out.verts]
    shift = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs)))
    bmesh.ops.translate(out, verts=out.verts, vec=-shift)
    out.to_mesh(mesh)
    out.free()
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


# ---------------------------------------------------------------- ассеты

def rock(name, size, seed, swatch="stone_cold", moss=True, squash=0.7):
    rnd = random.Random(seed)
    bm = ico(1.0, 2)
    displace(bm, 0.28, 1.1, seed)
    transform(bm, scale=(1.0 + rnd.uniform(0, 0.35), 1.0, squash))
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(14), verts=bm.verts, edges=bm.edges)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    flatten_bottom(bm, 0.15)
    transform(bm, rot=(0, 0, rnd.uniform(0, math.pi)), scale=(size, size, size))
    return Part(bm, swatch, (0.0, 1.0), smooth=False, top_swatch="moss" if moss else None, top_dot=0.72)


def rock_cluster(name, seed, swatch="stone_warm"):
    rnd = random.Random(seed)
    parts = [rock(name, 0.9, seed, swatch, moss=True)]
    for i in range(3):
        p = rock(name, rnd.uniform(0.25, 0.45), seed + 10 + i, swatch, moss=False, squash=0.8)
        a = rnd.uniform(0, 2 * math.pi)
        transform(p.bm, loc=(math.cos(a) * 1.05, math.sin(a) * 0.9, -0.05))
        parts.append(p)
    return parts


def crown(center, radius, seed, swatch, t_range):
    bm = ico(radius, 2)
    displace(bm, radius * 0.22, 1.6 / radius, seed)
    transform(bm, loc=center, scale=(1.0, 1.0, 0.85))
    return Part(bm, swatch, t_range, smooth=True)


def oak(name, seed, leaf="leaf", height=3.2):
    rnd = random.Random(seed)
    lean = Vector((rnd.uniform(-0.25, 0.25), rnd.uniform(-0.25, 0.25), 0))
    pts, radii = [], []
    for i in range(6):
        k = i / 5
        pts.append(Vector((0, 0, height * k)) + lean * (k * k) * 2 + Vector((math.sin(k * 3 + seed) * 0.12, 0, 0)))
        radii.append(0.32 * (1 - 0.6 * k) * (1.5 if i == 0 else 1.0))
    parts = [Part(tube(pts, radii), "bark", (0.0, 0.8))]
    top = pts[-1]
    tips = [top]
    for b in range(3):
        a = 2 * math.pi * b / 3 + rnd.uniform(-0.4, 0.4)
        start = pts[3]
        dirv = Vector((math.cos(a), math.sin(a), 1.1)).normalized()
        end = start + dirv * rnd.uniform(1.0, 1.4)
        mid = (start + end) / 2 + Vector((0, 0, 0.15))
        parts.append(Part(tube([start, mid, end], [0.14, 0.1, 0.06], 6), "bark", (0.4, 0.9)))
        tips.append(end)
    # крона: крупный ком сверху и комки на концах веток
    blobs = [(top + Vector((0, 0, 0.6)), 1.5)] + [(t + Vector((0, 0, 0.35)), rnd.uniform(1.0, 1.25)) for t in tips[1:]]
    zs = [c.z for c, _ in blobs]
    lo, hi = min(zs) - 1.3, max(zs) + 1.5
    for i, (c, r) in enumerate(blobs):
        tr = ((c.z - r - lo) / (hi - lo), (c.z + r - lo) / (hi - lo))
        parts.append(crown(c, r, seed * 10 + i, leaf, tr))
    return parts


def pine(name, seed, height=4.5):
    rnd = random.Random(seed)
    parts = [Part(tube([Vector((0, 0, 0)), Vector((0, 0, 1.0))], [0.22, 0.16], 7), "bark_dark", (0.0, 0.7))]
    tiers = 4
    z = 0.7
    for i in range(tiers):
        k = i / (tiers - 1)
        r = 1.6 * (1 - 0.55 * k)
        d = 1.6 * (1 - 0.25 * k)
        bm = cone(r, 0.0, d, 8)
        displace(bm, 0.08, 2.0, seed + i)
        transform(bm, loc=(0, 0, z), rot=(rnd.uniform(-0.06, 0.06), rnd.uniform(-0.06, 0.06), rnd.uniform(0, 1)))
        parts.append(Part(bm, "pine" if i < 2 else "pine_light", (0.15 + 0.1 * i, 1.0), smooth=False))
        z += d * 0.55
    return parts


def bush(name, seed, leaf="leaf", flowers=None):
    rnd = random.Random(seed)
    parts = []
    for i in range(3):
        r = rnd.uniform(0.45, 0.7)
        a = 2 * math.pi * i / 3 + rnd.uniform(-0.3, 0.3)
        c = Vector((math.cos(a) * 0.35, math.sin(a) * 0.35, r * 0.45))
        p = crown(c, r, seed * 3 + i, leaf, (0.0, 1.0))
        flatten_bottom(p.bm, 0.25)
        parts.append(p)
    if flowers:
        for i in range(8):
            a = rnd.uniform(0, 2 * math.pi)
            el = rnd.uniform(0.3, 1.1)
            c = Vector((math.cos(a) * 0.75 * math.cos(el), math.sin(a) * 0.75 * math.cos(el), 0.35 + 0.55 * math.sin(el)))
            bm = ico(0.08, 1)
            transform(bm, loc=c)
            parts.append(Part(bm, flowers, (0.7, 1.0)))
    return parts


def grass(name, seed, blades=7):
    rnd = random.Random(seed)
    parts = []
    for i in range(blades):
        h = rnd.uniform(0.45, 0.8)
        bm = cone(0.08, 0.0, h, 3)
        a = rnd.uniform(0, 2 * math.pi)
        transform(bm, loc=(math.cos(a) * rnd.uniform(0, 0.12), math.sin(a) * rnd.uniform(0, 0.12), 0),
                  rot=(rnd.uniform(-0.35, 0.35), rnd.uniform(-0.35, 0.35), rnd.uniform(0, 3)))
        parts.append(Part(bm, "grass", (0.0, 1.0)))
    return parts


# ---------------------------------------------------------------- сцена

def make_material(palette_path):
    mat = bpy.data.materials.get("M_Env_Forest") or bpy.data.materials.new("M_Env_Forest")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.location = (-400, 200)
    img = bpy.data.images.load(palette_path, check_existing=True)
    img.pack()
    tex.image = img
    tex.interpolation = "Linear"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 1.0
    bsdf.inputs["IOR"].default_value = 1.5
    bsdf.inputs["Specular IOR Level"].default_value = 0.2
    return mat


def build_kit():
    if bpy.app.background:
        bpy.ops.wm.read_factory_settings(use_empty=True)
    else:
        for o in list(bpy.context.scene.objects):
            bpy.data.objects.remove(o, do_unlink=True)
    mat = make_material(find_palette())
    random.seed(SEED)

    assets = [
        ("Env_Rock_Big_01", [rock("", 1.3, 1, "stone_cold")]),
        ("Env_Rock_Big_02", [rock("", 1.1, 4, "stone_warm", squash=0.85)]),
        ("Env_Rock_Med_01", [rock("", 0.6, 9, "stone_blue")]),
        ("Env_Rock_Cluster_01", rock_cluster("", 21, "stone_warm")),
        ("Env_Tree_Oak_01", oak("", 3, "leaf")),
        ("Env_Tree_Oak_02", oak("", 8, "leaf_light", height=2.8)),
        ("Env_Tree_Autumn_01", oak("", 13, "autumn_orange", height=3.0)),
        ("Env_Tree_Pine_01", pine("", 5)),
        ("Env_Tree_Pine_02", pine("", 11)),
        ("Env_Bush_01", bush("", 2, "leaf")),
        ("Env_Bush_Flower_01", bush("", 6, "leaf_dark", flowers="flower_yellow")),
        ("Env_Bush_Flower_02", bush("", 12, "leaf_teal", flowers="blossom")),
        ("Env_Grass_01", grass("", 1)),
        ("Env_Grass_02", grass("", 2, blades=10)),
    ]
    objs = [build_object(n, parts, mat) for n, parts in assets]
    return objs


def layout(objs):
    """Раскладка: камни и трава спереди, кусты в середине, деревья сзади."""
    groups = {k: [o for o in objs if f"_{k}_" in o.name] for k in ("Rock", "Grass", "Bush", "Tree")}
    front = groups["Rock"] + groups["Grass"]
    def place(items, y, step):
        total = step * (len(items) - 1)
        for i, o in enumerate(items):
            o.location = (-total / 2 + i * step, y, 0)
    place(front, 0.0, 2.6)
    place(groups["Bush"], 3.8, 2.6)
    place(groups["Tree"], 8.0, 4.6)


def setup_preview():
    scene = bpy.context.scene
    # земля
    bpy.ops.mesh.primitive_plane_add(size=80, location=(0, 5, 0))
    ground = bpy.context.active_object
    ground.name = "Preview_Ground"
    gm = bpy.data.materials.new("Preview_Ground")
    gm.use_nodes = True
    gb = gm.node_tree.nodes.get("Principled BSDF")
    gb.inputs["Base Color"].default_value = (0.075, 0.13, 0.045, 1)
    gb.inputs["Roughness"].default_value = 1.0
    ground.data.materials.append(gm)
    # солнце + небо
    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3.2
    sun_data.color = (1.0, 0.93, 0.82)
    sun_data.angle = math.radians(8)
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    scene.collection.objects.link(sun)
    world = bpy.data.worlds.new("Preview_World")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.45, 0.55, 0.75, 1)
    bg.inputs["Strength"].default_value = 0.9
    scene.world = world
    # камера под игровым углом
    cam_data = bpy.data.cameras.new("Camera")
    cam_data.lens = 32
    cam = bpy.data.objects.new("Camera", cam_data)
    cam.location = Vector((0, -11.5, 14.5))
    target = Vector((0, 4.2, 1.6))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(cam)
    scene.camera = cam
    for o in (ground, sun, cam):
        o.hide_select = False


def render(path, samples=48, res=(1600, 900)):
    scene = bpy.context.scene
    import addon_utils
    addon_utils.enable("cycles", default_set=True)
    scene.render.engine = "CYCLES"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.cycles.device = "CPU"
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[argv.index("--out") + 1] if "--out" in argv else None
    preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    objs = build_kit()
    layout(objs)
    setup_preview()
    if out:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(out), compress=True)
    if preview:
        render(os.path.abspath(preview))


if __name__ == "__main__":
    main()
