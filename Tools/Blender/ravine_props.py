# Пропсы стартового оврага: павший солдат (скелет в доспехе) и ржавый меч.
#
# Что делает:
#   * Prop_Skeleton_Soldier — скелет солдата сидит, привалившись спиной к стене оврага:
#       шлем-спангенхельм съехал на глаза, челюсть отвисла; кольчуга с юбкой, ржавый нагрудник
#       с двумя обломками стрел, ремень с пряжкой, выцветший красный табард, левый наплечник
#       (правый валяется рядом), наколенники, сгнившие сапоги, плащ лужей на земле, расколотый щит у стены.
#       Левая рука лежит на поднятом колене, правая тянется по земле к воткнутому мечу.
#       Материалы: M_Prop_Ravine (обычный) и M_Prop_Ravine_2S (ткань, видна с обеих сторон).
#   * Wpn_Sword_Rusty — новый детальный ржавый меч (lowpoly_kit.build_rusty_sword), отдельный FBX для мира.
#   * --hero <hero.blend>: заменяет меч в руке героя на новый и переэкспортирует Hero.fbx (чтобы меч
#     в руке и меч в земле были одинаковые). Сам hero.blend не трогает, кладёт копию рядом с результатом.
#   * --preview <png>: картинка-превью (Cycles CPU).
#
# Запуск (Blender 4.x/5.x или pip-пакет bpy):
#   blender -b -P ravine_props.py -- --out <папка> [--hero hero.blend] [--preview ravine_props.png]
#   python ravine_props.py --out <папка> ...
# Результат: Prop_Skeleton_Soldier.fbx → Game/Assets/_Project/Art/Models/Props/Ravine/,
#            Wpn_Sword_Rusty.fbx и Hero.fbx → Game/Assets/_Project/Art/Models/Characters/Hero/,
#            ravine_props.blend → Art/Blender/Props/.
#
# Оси: модель смотрит в -Y Blender → в Unity +Z; +X Blender → в Unity -X (Unity зеркалит X при импорте FBX).

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
import bmesh  # noqa: E402,F401
from mathutils import Matrix, Vector  # noqa: E402

import lowpoly_kit as K  # noqa: E402
from lowpoly_kit import (BONE, BONE_DARK, CHAIN, CLOAK, CLOAK_DARK, CLOTH_GOLD, CLOTH_RED, CLOTH_RED_DARK,  # noqa: E402
                         IRON, LEATHER, LEATHER_DARK, RUST_DARK, SOOT, STEEL, WOOD, WOOD_DARK,
                         box, cylinder, ico, join, lerp3, look_rot, mossy, n01, part, rust_metal, smooth, sweep)

SEED = 23
PLATE = (0.3, 0.3, 0.32)  # тёмное железо доспеха
MAT = "M_Prop_Ravine"
MAT_2S = "M_Prop_Ravine_2S"

# Где воткнут меч относительно скелета (Blender, метры). RavineSceneBuilder ставит меч туда же:
# в Unity это локальные (-SWORD_POS.x, 0, -SWORD_POS.y).
SWORD_POS = Vector((0.62, -0.12, 0.0))
SWORD_TILT = (3.0, -4.0)  # наклон к стене/вбок, градусы


def args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]

    def get(name, default=None):
        return argv[argv.index(name) + 1] if name in argv else default
    return os.path.abspath(get("--out", "ravine_out")), get("--hero"), get("--preview")


def bone_col(seed):
    def fn(c, nrm, p):
        col = lerp3(BONE, BONE_DARK, 0.15 + 0.6 * smooth(0.45, 0.8, n01(c, 18.0, (seed, 0, 0))))
        col = lerp3(col, SOOT, 0.35 * smooth(0.3, -0.6, nrm.z))  # снизу в тени — грязнее
        return K.mossy(col, c, nrm, 0.35, seed)
    return fn


# ---------------------------------------------------------------- поза (ключевые точки)

PELVIS = Vector((0.0, 0.12, 0.13))
NECK = Vector((0.02, 0.16, 0.66))
UP = (NECK - PELVIS).normalized()

SHOULDER_L = Vector((-0.2, 0.15, 0.6))
ELBOW_L = Vector((-0.27, -0.06, 0.37))
WRIST_L = Vector((-0.13, -0.27, 0.43))

SHOULDER_R = Vector((0.2, 0.17, 0.58))
ELBOW_R = Vector((0.31, 0.1, 0.32))
WRIST_R = Vector((0.39, 0.0, 0.05))

HIP_L, KNEE_L, ANKLE_L = Vector((-0.1, 0.08, 0.12)), Vector((-0.16, -0.22, 0.4)), Vector((-0.18, -0.44, 0.08))
HIP_R, KNEE_R, ANKLE_R = Vector((0.1, 0.08, 0.12)), Vector((0.15, -0.33, 0.12)), Vector((0.18, -0.73, 0.08))

# череп: свесился вперёд и к правому плечу
SKULL = Vector((0.06, 0.08, 0.8))
SKULL_ROT = (Matrix.Rotation(math.radians(-14), 3, "Y") @ Matrix.Rotation(math.radians(-24), 3, "X"))


def torso_frame(h):
    """Точка на оси корпуса на высоте h (0 = таз, 1 = шея)."""
    return PELVIS.lerp(NECK, h)


# ---------------------------------------------------------------- скелет

def build_skull(rng):
    def deform(co):
        # глазницы, носовая впадина, плоский затылок
        for ex in (-0.036, 0.036):
            d = (co - Vector((ex, -0.085, 0.004))).length
            if d < 0.032:
                co = co + Vector((0, 0.022, 0)) * (1 - d / 0.032)
        d = (co - Vector((0, -0.092, -0.035))).length
        if d < 0.022:
            co = co + Vector((0, 0.016, 0)) * (1 - d / 0.022)
        if co.z < -0.04:
            co.z = -0.04 + (co.z + 0.04) * 0.5
        return co

    def geo(bm):
        ico(bm, SKULL, 0.095, scale=(0.84, 1.04, 0.95), rot=SKULL_ROT, subdiv=2, deform=deform)
        # верхняя челюсть и скулы
        loc = lambda v: SKULL + SKULL_ROT @ Vector(v)  # noqa: E731
        box(bm, loc((0, -0.07, -0.05)), (0.034, 0.022, 0.016), rot=SKULL_ROT)
        for sx in (-1, 1):
            box(bm, loc((sx * 0.052, -0.05, -0.022)), (0.012, 0.03, 0.012), rot=SKULL_ROT)
        # зубы
        for i in range(8):
            a = math.radians(-60 + i * 120 / 7)
            box(bm, loc((math.sin(a) * 0.03, -0.083 + (1 - math.cos(a)) * 0.03, -0.069)), (0.0045, 0.004, 0.007), rot=SKULL_ROT)
        # нижняя челюсть отвисла
        hinge = Vector((0, -0.01, -0.045))
        jaw_rot = SKULL_ROT @ Matrix.Rotation(math.radians(24), 3, "X")
        jl = lambda v: SKULL + SKULL_ROT @ hinge + jaw_rot @ (Vector(v) - hinge)  # noqa: E731
        pts = [jl(v) for v in ((-0.048, -0.005, -0.04), (-0.044, -0.05, -0.08), (0, -0.082, -0.09), (0.044, -0.05, -0.08), (0.048, -0.005, -0.04))]
        sweep(bm, pts, [0.01, 0.013, 0.014, 0.013, 0.01], seg=5)
        for i in range(6):
            a = math.radians(-50 + i * 100 / 5)
            box(bm, jl((math.sin(a) * 0.028, -0.078 + (1 - math.cos(a)) * 0.028, -0.072)), (0.004, 0.0035, 0.006), rot=jaw_rot)
        # шейные позвонки к корпусу
        top = SKULL + SKULL_ROT @ Vector((0, 0.02, -0.075))
        for i in range(3):
            c = top.lerp(NECK, (i + 0.5) / 3)
            cylinder(bm, c, 0.019, 0.017, 0.022, seg=6, rot=look_rot(NECK - top))

    inv = SKULL_ROT.inverted()

    def col(c, nrm, p):
        local = inv @ (c - SKULL)
        base = bone_col(1.0)(c, nrm, p)
        for ex in (-0.036, 0.036):
            if (local - Vector((ex, -0.078, 0.004))).length < 0.026:
                return SOOT
        if (local - Vector((0, -0.085, -0.035))).length < 0.018:
            return SOOT
        return base
    return part("skull", geo, col, MAT)


def build_limbs(rng):
    def bone(bm, a, b, r):
        a, b = Vector(a), Vector(b)
        sweep(bm, [a, a.lerp(b, 0.12), a.lerp(b, 0.5), a.lerp(b, 0.88), b], [r * 1.45, r, r * 0.85, r, r * 1.4], seg=6)

    def fingers(bm, wrist, fwd, side, down, curl, spread=0.012, length=0.06):
        fwd, side, down = Vector(fwd).normalized(), Vector(side).normalized(), Vector(down).normalized()
        palm = wrist + fwd * 0.04
        ico(bm, wrist + fwd * 0.018, 0.022, scale=(1.0, 1.0, 0.55), rot=look_rot(down, fwd), subdiv=1)
        for i in range(4):
            base = palm + side * (i - 1.5) * spread
            d1 = (fwd + side * (i - 1.5) * 0.15).normalized()
            p1 = base + d1 * length * 0.4
            d2 = (d1 + down * curl).normalized()
            p2 = p1 + d2 * length * 0.35
            d3 = (d2 + down * curl).normalized()
            p3 = p2 + d3 * length * 0.25
            sweep(bm, [wrist + fwd * 0.02 + side * (i - 1.5) * spread * 0.6, base, p1, p2, p3], [0.0055, 0.006, 0.0055, 0.005, 0.0045], seg=4)
        tb = wrist + fwd * 0.015 - side * 0.022
        td = (fwd - side * 0.8).normalized()
        sweep(bm, [tb, tb + td * 0.03, tb + (td + down * curl * 0.5).normalized() * 0.055], [0.0065, 0.006, 0.005], seg=4)

    def geo(bm):
        # руки: от края рукава до кисти
        for sh, el, wr in ((SHOULDER_L, ELBOW_L, WRIST_L), (SHOULDER_R, ELBOW_R, WRIST_R)):
            bone(bm, sh.lerp(el, 0.6), el, 0.018)
            ico(bm, el, 0.024, subdiv=1)
            side = (wr - el).normalized().cross(Vector((0, 0, 1))).normalized() * 0.011
            bone(bm, el + side, wr + side * 0.6, 0.011)   # лучевая
            bone(bm, el - side, wr - side * 0.6, 0.0105)  # локтевая
        # левая кисть свисает с колена, правая лежит на земле и тянется к мечу
        fingers(bm, WRIST_L, (0.15, -0.55, -0.8), (1, 0, 0), (0, 0.6, -0.4), curl=0.6)
        reach = (Vector((SWORD_POS.x, SWORD_POS.y, 0.02)) - WRIST_R)
        reach.z = 0
        fingers(bm, WRIST_R + Vector((0, 0, -0.025)), reach, reach.cross(Vector((0, 0, 1))), (0, 0, -1), curl=0.25, spread=0.014, length=0.07)
        # ноги: бедро (видно из-под кольчуги), колено, голень до сапога
        for hip, knee, ankle in ((HIP_L, KNEE_L, ANKLE_L), (HIP_R, KNEE_R, ANKLE_R)):
            bone(bm, hip.lerp(knee, 0.35), knee, 0.026)
            ico(bm, knee, 0.031, subdiv=1)
            bone(bm, knee, knee.lerp(ankle, 0.55), 0.022)
            bone(bm, knee + Vector((0.025, 0.0, 0.0)), knee.lerp(ankle, 0.5) + Vector((0.02, 0, 0)), 0.009)  # малоберцовая
        # пара отвалившихся рёбер и фаланг на земле
        for (x, y, yaw) in ((0.27, -0.12, 30), (-0.32, -0.1, -60), (0.05, -0.52, 80)):
            r = Matrix.Rotation(math.radians(yaw), 3, "Z")
            pts = [Vector((x, y, 0.012)) + r @ Vector(v) for v in ((-0.06, 0, 0), (-0.02, -0.02, 0.006), (0.03, -0.025, 0.006), (0.07, -0.01, 0))]
            sweep(bm, pts, 0.0075, seg=4)
        for i in range(4):
            a = rng.uniform(0, math.tau)
            c = WRIST_R + Vector((math.cos(a) * 0.12 + 0.05, math.sin(a) * 0.1 - 0.05, -0.04))
            c.z = 0.008
            box(bm, c, (0.012, 0.005, 0.005), rot=Matrix.Rotation(a, 3, "Z"))
    return part("limbs", geo, bone_col(2.0), MAT)


# ---------------------------------------------------------------- доспех и одежда

def torso_rings(h_list, r_list, sx=1.0, sy=0.72, seg=10):
    pts = [torso_frame(h) for h in h_list]
    return pts, r_list, dict(seg=seg, sx=sx, sy=sy, ref=(1, 0, 0))


def build_mail(rng):
    """Кольчуга: корпус, рукава до локтя, юбка на бёдра."""
    def geo(bm):
        pts, rr, kw = torso_rings([-0.02, 0.2, 0.45, 0.7, 0.88, 1.0], [0.155, 0.15, 0.165, 0.175, 0.15, 0.075])
        sweep(bm, pts, rr, cap0=False, **kw)
        # юбка: от пояса расходится и ложится на бёдра, рваный подол
        top = torso_frame(0.1)
        seg = 14
        ring_top, ring_mid, ring_bot = [], [], []
        for k in range(seg):
            a = 2 * math.pi * k / seg
            d = Vector((math.cos(a), math.sin(a) * 0.75, 0))
            front = max(0.0, -math.sin(a))  # вперёд (-Y) — ложится на колени
            ring_top.append(bm.verts.new(top + d * 0.158))
            mid = top + d * 0.2 + Vector((0, -0.09 * front, -0.04 - 0.02 * (1 - front)))
            ring_mid.append(bm.verts.new(mid))
            hem = top + d * (0.235 + 0.04 * front) + Vector((0, -0.2 * front, -0.06 - 0.03 * (1 - front) + 0.06 * front))
            hem += Vector((0, 0, rng.uniform(-0.02, 0.012)))
            hem.z = max(hem.z, 0.015)
            ring_bot.append(bm.verts.new(hem))
        for ra, rb in ((ring_top, ring_mid), (ring_mid, ring_bot)):
            for k in range(seg):
                j = (k + 1) % seg
                bm.faces.new((ra[k], ra[j], rb[j], rb[k]))
        # рукава
        for sh, el in ((SHOULDER_L, ELBOW_L), (SHOULDER_R, ELBOW_R)):
            sweep(bm, [sh + (sh - NECK).normalized() * -0.02, sh.lerp(el, 0.3), sh.lerp(el, 0.62)], [0.058, 0.052, 0.046], seg=8)

    def col(c, nrm, p):
        n = n01(c, 70.0)
        col = lerp3(CHAIN, IRON, 0.2 + 0.5 * n)
        col = lerp3(col, K.RUST, 0.55 * smooth(0.55, 0.85, n01(c, 9.0, (4, 0, 0))))
        col = lerp3(col, SOOT, 0.35 * smooth(0.25, -0.5, nrm.z))
        return mossy(col, c, nrm, 0.3, 7.0)
    return part("mail", geo, col, MAT_2S)


def build_plate(rng):
    """Нагрудник, наплечники, наколенники, наруч — ржавое железо."""
    def geo(bm):
        # нагрудник: передняя половина «бочки» вокруг корпуса, с килем по центру
        rows = [(0.36, 0.172), (0.48, 0.182), (0.62, 0.19), (0.76, 0.18), (0.86, 0.15)]
        seg = 9
        rings = []
        for h, r in rows:
            c = torso_frame(h)
            ring = []
            for k in range(seg):
                a = math.radians(-100 + 200 * k / (seg - 1))  # 0 = вперёд (-Y)
                keel = 0.012 * (1 - abs(a) / math.radians(30)) if abs(a) < math.radians(30) else 0
                d = Vector((math.sin(a), -math.cos(a) * 0.76, 0))
                v = c + d * (r + keel)
                if h == 0.62 and k == 3:
                    v += Vector((0, 0.018, 0))  # вмятина
                ring.append(bm.verts.new(v))
            rings.append(ring)
        for ra, rb in zip(rings[:-1], rings[1:]):
            for k in range(seg - 1):
                bm.faces.new((ra[k], ra[k + 1], rb[k + 1], rb[k]))
        # ремень с пряжкой
        sweep(bm, [torso_frame(0.18), torso_frame(0.25)], [0.165, 0.165], seg=12, sx=1.0, sy=0.76, ref=(1, 0, 0))
        box(bm, torso_frame(0.215) + Vector((0, -0.128, 0)), (0.024, 0.008, 0.02), rot=Matrix.Rotation(math.radians(-15), 3, "X"))
        # левый наплечник: два ряда пластин
        out = (SHOULDER_L - NECK).normalized()
        for i, (s, off) in enumerate(((1.0, 0.0), (0.82, -0.055))):
            ico(bm, SHOULDER_L + out * (0.02 + i * 0.02) + Vector((0, 0, 0.03 + off)), 0.085 * s,
                scale=(1.05, 1.0, 0.6), rot=look_rot(out + Vector((0, 0, 1.4))), subdiv=1)
        # правый наплечник отвалился и лежит у бедра
        ico(bm, (0.36, 0.2, 0.035), 0.08, scale=(1.05, 1.0, 0.55), rot=Matrix.Rotation(math.radians(160), 3, "X") @ Matrix.Rotation(0.4, 3, "Z"), subdiv=1)
        # наколенники
        for hip, knee, ankle in ((HIP_L, KNEE_L, ANKLE_L), (HIP_R, KNEE_R, ANKLE_R)):
            fwd = ((knee - hip).normalized() + (knee - ankle).normalized()).normalized()
            ico(bm, knee + fwd * 0.022, 0.046, scale=(1.0, 1.0, 0.55), rot=look_rot(fwd), subdiv=1)
        # наруч на левом предплечье
        sweep(bm, [ELBOW_L.lerp(WRIST_L, 0.25), ELBOW_L.lerp(WRIST_L, 0.8)], [0.033, 0.029], seg=8)

    def col(c, nrm, p):
        if abs(c.z - torso_frame(0.215).z) < 0.035 and c.y < 0.2 and abs(nrm.z) < 0.7 and (c - torso_frame(0.215)).length < 0.2:
            return lerp3(LEATHER, LEATHER_DARK, n01(c, 30.0))  # ремень
        col = rust_metal(c, PLATE, amount=0.66, scale=11.0, seed=3.0)
        col = lerp3(col, K.STEEL, 0.25 * smooth(0.75, 0.95, nrm.z) * (1 - smooth(0.5, 0.7, n01(c, 9.0))))
        return mossy(col, c, nrm, 0.55, 11.0)
    return part("plate", geo, col, MAT_2S)


def build_helmet(rng):
    rot = SKULL_ROT @ Matrix.Rotation(math.radians(-14), 3, "X") @ Matrix.Rotation(math.radians(8), 3, "Y")
    center = SKULL + SKULL_ROT @ Vector((0, 0.008, 0.012))
    loc = lambda v: center + rot @ Vector(v)  # noqa: E731

    def geo(bm):
        seg = 10
        profile = [(-0.012, 0.112), (0.03, 0.111), (0.07, 0.096), (0.1, 0.068), (0.122, 0.034), (0.134, 0.008)]
        rings = []
        for z, r in profile:
            ring = []
            for k in range(seg):
                a = 2 * math.pi * k / seg
                dent = 0.012 if (k in (2, 3) and 0.02 < z < 0.08) else 0.0
                ring.append(bm.verts.new(loc((math.cos(a) * (r - dent), math.sin(a) * (r - dent) * 1.1, z))))
            rings.append(ring)
        apex = bm.verts.new(loc((0, 0, 0.142)))
        for ra, rb in zip(rings[:-1], rings[1:]):
            for k in range(seg):
                j = (k + 1) % seg
                bm.faces.new((ra[k], ra[j], rb[j], rb[k]))
        for k in range(seg):
            bm.faces.new((rings[-1][k], rings[-1][(k + 1) % seg], apex))
        # обод и наносник
        cylinder(bm, loc((0, 0, 0.0)), 0.119, 0.117, 0.026, seg=12, rot=rot, caps=False)
        box(bm, loc((0, -0.124, -0.038)), (0.011, 0.006, 0.046), rot=rot)
        # заклёпки на ободе
        for k in range(8):
            a = 2 * math.pi * k / 8 + 0.2
            box(bm, loc((math.cos(a) * 0.121, math.sin(a) * 0.131, 0.002)), (0.006, 0.006, 0.006), rot=rot)

    inv = rot.inverted()

    def col(c, nrm, p):
        local = inv @ (c - center)
        col = rust_metal(c, PLATE, amount=0.7, scale=13.0, seed=6.0)
        ang = math.degrees(math.atan2(local.y, local.x)) % 90
        if (ang < 9 or ang > 81) and local.z > 0.02:
            col = lerp3(col, RUST_DARK, 0.55)  # полосы каркаса «спанген»
        return mossy(col, c, nrm, 0.7, 13.0)
    return part("helmet", geo, col, MAT_2S)


def build_tabard(rng):
    """Выцветший красный табард: свисает с ремня на колени, рваный низ."""
    def geo(bm):
        path = [torso_frame(0.28) + Vector((0, -0.13, 0)), Vector((0.01, -0.1, 0.25)), Vector((0.03, -0.2, 0.22)),
                Vector((0.06, -0.33, 0.2)), Vector((0.08, -0.43, 0.15)), Vector((0.09, -0.5, 0.1))]
        cols = 5
        grid = []
        for i, c in enumerate(path):
            w = 0.1 + 0.015 * i
            row = []
            for j in range(cols):
                t = j / (cols - 1) - 0.5
                v = c + Vector((t * 2 * w, 0, 0)) + Vector((0, 0, 0.012 * math.sin(j * 1.7 + i)))
                if i == len(path) - 1:
                    v += Vector((0, rng.uniform(0, 0.06), rng.uniform(0.0, 0.03)))  # рваный край
                row.append(bm.verts.new(v))
            grid.append(row)
        for ra, rb in zip(grid[:-1], grid[1:]):
            for j in range(cols - 1):
                bm.faces.new((ra[j], ra[j + 1], rb[j + 1], rb[j]))

    def col(c, nrm, p):
        col = lerp3(CLOTH_RED, CLOTH_RED_DARK, 0.3 + 0.5 * n01(c, 14.0))
        if abs(c.x - (0.01 + 0.13 * max(0.0, -c.y - 0.1))) < 0.024 and c.y < -0.12:
            col = lerp3(col, CLOTH_GOLD, 0.7)  # остатки вышитой полосы
        return lerp3(col, SOOT, 0.3 * smooth(-0.35, -0.5, c.y))
    return part("tabard", geo, col, MAT_2S)


def build_boots(rng):
    def geo(bm):
        for knee, ankle, out in ((KNEE_L, ANKLE_L, -0.2), (KNEE_R, ANKLE_R, 0.7)):
            top = knee.lerp(ankle, 0.55)
            sweep(bm, [top, top.lerp(ankle, 0.5), ankle], [0.043, 0.041, 0.044], seg=8, jitter=0.12, rng=rng)
            fwd = Vector((math.sin(out) * 0.6, -1, 0)).normalized()
            side = fwd.cross(Vector((0, 0, 1)))
            tilt = Matrix.Rotation(out * 0.7, 3, fwd)
            sole_c = Vector((ankle.x, ankle.y, 0.035)) + fwd * 0.07
            box(bm, sole_c, (0.042, 0.11, 0.032), rot=tilt @ Matrix((side, fwd, Vector((0, 0, 1)))).transposed())
            box(bm, sole_c + Vector((0, 0, -0.026)), (0.046, 0.118, 0.008), rot=tilt @ Matrix((side, fwd, Vector((0, 0, 1)))).transposed())

    def col(c, nrm, p):
        col = lerp3(LEATHER, LEATHER_DARK, 0.3 + 0.6 * n01(c, 25.0))
        if c.z < 0.02:
            col = lerp3(col, SOOT, 0.6)
        return mossy(col, c, nrm, 0.25, 17.0)
    return part("boots", geo, col, MAT)


def build_cloak(rng):
    """Плащ: со спины стекает на землю и растекается лужей слева от солдата."""
    def geo(bm):
        cols, rows = 6, 6
        grid = []
        for i in range(rows):
            t = i / (rows - 1)
            row = []
            for j in range(cols):
                s = j / (cols - 1)
                x = -0.22 + 0.44 * s
                if t < 0.5:  # по спине вниз
                    k = t / 0.5
                    v = Vector((x * (1 + 0.15 * k), 0.31 + 0.04 * k, 0.6 - 0.58 * k))
                else:  # по земле, уходит влево-вперёд
                    k = (t - 0.5) / 0.5
                    v = Vector((x - 0.25 * k - 0.1 * k * (1 - s), 0.35 - 0.25 * k - 0.15 * k * (1 - s), 0.012 + 0.012 * math.sin(j * 2.1 + i)))
                if i == rows - 1 or (t >= 0.5 and j == 0):
                    v += Vector((rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03), 0))
                row.append(bm.verts.new(v))
            grid.append(row)
        for ra, rb in zip(grid[:-1], grid[1:]):
            for j in range(cols - 1):
                bm.faces.new((ra[j], ra[j + 1], rb[j + 1], rb[j]))

    def col(c, nrm, p):
        col = lerp3(CLOAK, CLOAK_DARK, 0.3 + 0.6 * n01(c, 10.0))
        return mossy(col, c, nrm, 0.35, 19.0)
    return part("cloak", geo, col, MAT_2S)


def build_arrows(rng):
    def geo(bm):
        hits = [(Vector((-0.07, -0.12, 0.52)), Vector((0.35, -1, 0.55)), 0.24, True),
                (Vector((0.08, -0.1, 0.42)), Vector((-0.2, -1, 0.25)), 0.13, False)]
        for hit, d, length, fletched in hits:
            hit = PELVIS.lerp(NECK, 0) + (hit - PELVIS)
            d = d.normalized()
            tail = hit + d * length
            sweep(bm, [hit - d * 0.02, tail], 0.0055, seg=5)
            if fletched:
                for k in range(3):
                    a = k * 2 * math.pi / 3
                    side = d.cross(Vector((0, 0, 1))).normalized()
                    up = side.cross(d)
                    off = (side * math.cos(a) + up * math.sin(a)) * 0.016
                    v = [bm.verts.new(tail - d * 0.01), bm.verts.new(tail - d * 0.065), bm.verts.new(tail - d * 0.07 + off), bm.verts.new(tail - d * 0.02 + off)]
                    bm.faces.new(v)
            else:
                # сломанный конец: щепа
                box(bm, tail + d * 0.008, (0.006, 0.004, 0.01), rot=look_rot(d))

    def col(c, nrm, p):
        return lerp3(WOOD, WOOD_DARK, n01(c, 50.0)) if n01(c, 9.0) > 0.35 else (0.55, 0.52, 0.45)
    return part("arrows", geo, col, MAT_2S)


def build_shield(rng):
    """Круглый щит, прислонён к стене слева, кусок откушен."""
    center = Vector((-0.6, 0.12, 0.27))
    rot = look_rot(Vector((0.3, -1.0, 0.6)), up=(0, 0, 1))  # лицо щита наружу и вверх
    loc = lambda v: center + rot @ Vector(v)  # noqa: E731
    R = 0.26

    def outline():
        pts = []
        for k in range(28):
            a = 2 * math.pi * k / 28
            r = R
            if 0.5 < a < 1.25:  # откол
                r = R * (0.62 + 0.22 * abs(math.sin(a * 9)))
            pts.append((math.cos(a) * r, math.sin(a) * r, a, r < R * 0.99))
        return pts

    def geo(bm):
        pts = outline()
        th = 0.014
        front = [bm.verts.new(loc((x, y, th))) for x, y, _, _ in pts]
        back = [bm.verts.new(loc((x, y, -th))) for x, y, _, _ in pts]
        cf = bm.verts.new(loc((0, 0, th)))
        cb = bm.verts.new(loc((0, 0, -th)))
        n = len(pts)
        for k in range(n):
            j = (k + 1) % n
            bm.faces.new((front[k], front[j], cf))
            bm.faces.new((back[j], back[k], cb))
            bm.faces.new((front[j], front[k], back[k], back[j]))
        # железный обод (кроме отколотого места) и умбон
        rim = [loc((x * 1.01, y * 1.01, 0)) for x, y, a, broken in pts if not broken]
        sweep(bm, rim, 0.013, seg=5, cap0=True, cap1=True, sx=1.0, sy=1.6)
        ico(bm, loc((0, 0, th + 0.012)), 0.06, scale=(1, 1, 0.55), rot=rot, subdiv=1)

    inv = rot.inverted()

    def col(c, nrm, p):
        local = inv @ (c - center)
        r = math.hypot(local.x, local.y)
        if r > R * 0.93 or (r < 0.065 and local.z > 0.01):
            return mossy(rust_metal(c, PLATE, amount=0.65, scale=18.0, seed=8.0), c, nrm, 0.4, 21.0)
        plank = int((local.x + R) / 0.075)
        col = lerp3(WOOD, WOOD_DARK, 0.15 + 0.25 * (plank % 2) + 0.35 * n01(c, 30.0))
        if local.z > 0 and local.y > 0.02:
            col = lerp3(col, CLOTH_RED, 0.75 * smooth(0.35, 0.55, n01(c, 6.0, (2, 2, 2))))  # облезлая краска
        return mossy(col, c, nrm, 0.3, 23.0)
    return part("shield", geo, col, MAT)


STONE = (0.3, 0.3, 0.35)  # темнее костей — фигура читается на фоне камня
STONE_DARK = (0.17, 0.17, 0.21)


def build_boulder(rng):
    """Валун, к которому солдат привалился спиной (+ камень поменьше справа). Часть модели, чтобы спина
    точно лежала на камне, а щит опирался на него, — в любой сцене, без подгонки руками."""
    def rock(bm, center, scale, seed):
        def deform(co):
            d = co.normalized()
            k = 1.0 + 0.16 * (n01(d, 2.3, (seed, 0, 0)) - 0.5) * 2 + 0.06 * (n01(d, 6.0, (0, seed, 0)) - 0.5) * 2
            return co * k
        ico(bm, center, 1.0, scale=scale, subdiv=2, deform=deform)

    def geo(bm):
        # низкий и неглубокий: голова и плечи солдата выше камня, сверху камень не перекрывает фигуру
        rock(bm, (-0.05, 0.6, 0.02), (1.0, 0.42, 0.52), 1.0)
        rock(bm, (-1.0, 0.42, 0.0), (0.26, 0.22, 0.2), 7.0)

    def col(c, nrm, p):
        col = lerp3(STONE, STONE_DARK, 0.2 + 0.55 * n01(c, 4.0, (2, 0, 0)))
        col = lerp3(col, STONE_DARK, 0.5 * smooth(0.25, -0.4, nrm.z))
        col = lerp3(col, SOOT, 0.4 * smooth(0.25, 0.0, c.z))
        return mossy(col, c, nrm, 0.9, 31.0)
    return part("boulder", geo, col, MAT)


def build_soldier(rng):
    objs = [build_skull(rng), build_limbs(rng), build_mail(rng), build_plate(rng), build_helmet(rng),
            build_tabard(rng), build_boots(rng), build_arrows(rng), build_shield(rng), build_boulder(rng)]
    return join(objs, "Prop_Skeleton_Soldier")


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


def sword_in_ground(sword):
    """Поставить меч остриём в землю у руки скелета (для превью; в Unity то же делает RavineSceneBuilder)."""
    tilt = Matrix.Rotation(math.radians(SWORD_TILT[0]), 4, "X") @ Matrix.Rotation(math.radians(SWORD_TILT[1]), 4, "Y")
    flip = Matrix.Rotation(math.pi, 4, "X")  # остриём вниз
    depth = 0.2
    sword.matrix_world = Matrix.Translation(SWORD_POS) @ tilt @ Matrix.Translation((0, 0, K.SWORD_TIP - depth)) @ flip


def swap_hero_sword(hero_blend, out, new_sword_mesh):
    """Подменить меш меча в hero.blend и переэкспортировать Hero.fbx (объекты, кости, имена — как были)."""
    bpy.ops.wm.open_mainfile(filepath=hero_blend)
    old = bpy.data.objects["Wpn_Sword_Rusty"]
    with bpy.data.libraries.load(new_sword_mesh) as (src, dst):
        dst.meshes = ["Wpn_Sword_Rusty"]
    mesh = dst.meshes[0]
    # материал с тем же именем в hero.blend уже есть — берём его, а не копию «.001» (Unity ищет по имени)
    loaded_mat = mesh.materials[0]
    mesh.materials[0] = bpy.data.materials["M_Sword_Rusty"]
    if loaded_mat is not None and loaded_mat.name != "M_Sword_Rusty":
        bpy.data.materials.remove(loaded_mat)
    old_mesh = old.data
    old.data = mesh
    bpy.data.meshes.remove(old_mesh)
    mesh.name = "Wpn_Sword_Rusty"
    arm = bpy.data.objects["Armature"]
    hero_objs = [arm] + [o for o in bpy.data.objects if o.type == "MESH"]
    export_fbx(os.path.join(out, "Hero.fbx"), hero_objs)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "hero.blend"), compress=True)
    print("[ravine_props] Hero.fbx пересобран с новым мечом")


# ---------------------------------------------------------------- превью

def render_preview(path, soldier, sword):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 48
    sc.cycles.device = "CPU"
    sc.render.resolution_x, sc.render.resolution_y = 1800, 1000
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("w")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.07, 0.08, 0.12, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 1.0

    for m in bpy.data.materials:
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        o = nt.nodes.new("ShaderNodeOutputMaterial")
        b = nt.nodes.new("ShaderNodeBsdfPrincipled")
        b.inputs["Roughness"].default_value = 0.8
        a = nt.nodes.new("ShaderNodeVertexColor")
        a.layer_name = "Col"
        nt.links.new(a.outputs[0], b.inputs["Base Color"])
        nt.links.new(b.outputs[0], o.inputs[0])

    # земля и стена оврага
    bpy.ops.mesh.primitive_plane_add(size=6, location=(0, 0, 0))
    ground = bpy.context.active_object
    gm = bpy.data.materials.new("ground")
    gm.diffuse_color = (0.12, 0.11, 0.1, 1)
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.1, 0.09, 0.085, 1)
    ground.data.materials.append(gm)

    # крупный план меча справа
    big = sword.copy()
    big.data = sword.data
    sc.collection.objects.link(big)
    big.matrix_world = Matrix.Translation((1.75, -0.3, 0.62)) @ Matrix.Rotation(math.radians(-90), 4, "Y") @ Matrix.Rotation(math.radians(15), 4, "X") @ Matrix.Scale(1.15, 4)

    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    # как игровая камера: сверху-спереди под 55°
    cam.location = (0.55, -2.55, 3.25)
    cam.rotation_euler = (math.radians(37), 0, math.radians(2))
    cam.data.lens = 34
    key = bpy.data.objects.new("key", bpy.data.lights.new("key", "SUN"))
    key.data.energy = 3.2
    key.rotation_euler = (math.radians(50), 0, math.radians(-30))
    sc.collection.objects.link(key)
    rim = bpy.data.objects.new("rim", bpy.data.lights.new("rim", "SUN"))
    rim.data.energy = 1.4
    rim.data.color = (0.6, 0.7, 1.0)
    rim.rotation_euler = (math.radians(60), 0, math.radians(160))
    sc.collection.objects.link(rim)
    sc.render.filepath = os.path.abspath(path)
    bpy.ops.render.render(write_still=True)


# ---------------------------------------------------------------- main

def main():
    out, hero_blend, preview = args()
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rng = random.Random(SEED)

    soldier = build_soldier(rng)
    sword = K.build_rusty_sword("Wpn_Sword_Rusty_Prop")
    tris = sum(len(p.vertices) - 2 for p in soldier.data.polygons)
    print(f"[ravine_props] скелет: {len(soldier.data.vertices)} вершин, ~{tris} треугольников; меч: {len(sword.data.vertices)} вершин")

    export_fbx(os.path.join(out, "Prop_Skeleton_Soldier.fbx"), [soldier])
    export_fbx(os.path.join(out, "Wpn_Sword_Rusty.fbx"), [sword])

    # меч для героя: тот же меш под именем Wpn_Sword_Rusty
    sword.data.name = "Wpn_Sword_Rusty"
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "ravine_props.blend"), compress=True)

    if preview:
        sword_in_ground(sword)
        render_preview(preview, soldier, sword)
    if hero_blend:
        swap_hero_sword(hero_blend, out, os.path.join(out, "ravine_props.blend"))
    print("[ravine_props] готово:", out)


if __name__ == "__main__":
    main()
