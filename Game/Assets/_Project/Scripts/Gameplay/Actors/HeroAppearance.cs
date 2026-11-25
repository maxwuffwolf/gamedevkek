using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Gameplay.Items;
using Game.Gameplay.Progression;
using UnityEngine;

namespace Game.Gameplay.Actors
{
    /// <summary>
    /// Живой внешний вид героя на настоящей модели (Hero.prefab):
    ///   * цвета волос, бровей, глаз (гетерохромия) из characters.json → "appearance" (hot reload F5);
    ///   * слой грязи на коже: Dirt 0..1, Wash() плавно смывает;
    ///   * кристалл наруча: Balance (0 Тьма .. 1 Свет), вспышка и «ручейки» энергии из убитых врагов;
    ///   * оружие в руке: показывает модель того, что надето в MainHand.
    /// Ищет части модели по именам из Hero.fbx (Hero_Body, Hero_Hair, Hero_BracerCrystal, Wpn_*).
    /// Работает через MaterialPropertyBlock — общие материалы не меняются.
    /// </summary>
    public sealed class HeroAppearance : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int DirtId = Shader.PropertyToID("_DirtAmount");
        private static readonly int DirtColorId = Shader.PropertyToID("_DirtColor");
        private static readonly int EyeRightId = Shader.PropertyToID("_EyeRight");
        private static readonly int EyeLeftId = Shader.PropertyToID("_EyeLeft");
        private static readonly int EyeGlowId = Shader.PropertyToID("_EyeGlow");
        private static readonly int BalanceId = Shader.PropertyToID("_Balance");
        private static readonly int PulseId = Shader.PropertyToID("_Pulse");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int LightColorId = Shader.PropertyToID("_LightColor");
        private static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");

        private Renderer _body, _eyes, _brows, _hair, _crystal;
        private Light _crystalLight;
        private readonly Dictionary<string, GameObject> _weapons = new();
        private readonly Dictionary<Transform, Quaternion> _weaponBaseRot = new();
        private Vector3 _gripOverride;
        private bool _hasGripOverride;
        private MaterialPropertyBlock _mpb; // создаём в Awake: Unity не разрешает это в инициализаторе поля

        private AppearanceDef _def = new();
        private Inventory _inventory;
        private float _dirt;
        private float _washFrom, _washStart = -1f;
        private float _pulse;

        /// <summary>Грязь на коже 0..1.</summary>
        public float Dirt
        {
            get => _dirt;
            set { _dirt = Mathf.Clamp01(value); _washStart = -1f; ApplySkin(); }
        }

        /// <summary>Баланс наруча: 0 — Тьма, 0.5 — поровну, 1 — Свет.</summary>
        public float Balance { get; set; } = 0.5f;

        /// <summary>Мировая точка кристалла (куда летит энергия).</summary>
        public Vector3 CrystalPosition => _crystal != null ? _crystal.bounds.center : transform.position + Vector3.up * 1.2f;

        public static HeroAppearance Current { get; private set; }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                if (n == "Hero_Body") _body = r;
                else if (n == "Hero_Eyes") _eyes = r;
                else if (n == "Hero_Brows") _brows = r;
                else if (n == "Hero_Hair") _hair = r;
                else if (n == "Hero_BracerCrystal") _crystal = r;
                else if (n.StartsWith("Wpn_"))
                {
                    _weapons[n] = r.gameObject;
                    _weaponBaseRot[r.transform] = r.transform.localRotation;
                }
            }
            if (_crystal != null)
            {
                var go = new GameObject("CrystalLight");
                go.transform.SetParent(_crystal.transform, false);
                go.transform.position = _crystal.bounds.center;
                _crystalLight = go.AddComponent<Light>();
                _crystalLight.type = LightType.Point;
                _crystalLight.range = 2.2f;
                _crystalLight.shadows = LightShadows.None;
            }
        }

        private void Start()
        {
            Current = this;
            _inventory = Services.TryGet<Inventory>(out var inv) ? inv : null;
            if (_inventory != null) _inventory.Changed += RefreshWeapon;
            EventBus.Subscribe<ContentReloadedEvent>(OnReloaded);
            EventBus.Subscribe<MonsterKilledEvent>(OnMonsterKilled);
            LoadConfig(resetState: true);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (_inventory != null) _inventory.Changed -= RefreshWeapon;
            EventBus.Unsubscribe<ContentReloadedEvent>(OnReloaded);
            EventBus.Unsubscribe<MonsterKilledEvent>(OnMonsterKilled);
        }

        private void OnReloaded(ContentReloadedEvent e) => LoadConfig(resetState: false);

        private void LoadConfig(bool resetState)
        {
            var character = Services.TryGet<PlayerProfile>(out var profile) ? profile.Character : null;
            _def = character?.Appearance ?? new AppearanceDef();
            if (resetState)
            {
                _dirt = Mathf.Clamp01(_def.DirtStart);
                Balance = Mathf.Clamp01(_def.Balance);
            }
            ApplyColors();
            ApplySkin();
            RefreshWeapon();
        }

        // ------------------------------------------------------------ цвета

        private void ApplyColors()
        {
            SetColor(_hair, BaseColorId, ColorUtil.Parse(_def.HairColor, Color.white));
            if (_eyes != null)
            {
                _eyes.GetPropertyBlock(_mpb, 0);
                _mpb.SetColor(EyeRightId, ColorUtil.Parse(_def.EyeRight, Color.yellow));
                _mpb.SetColor(EyeLeftId, ColorUtil.Parse(_def.EyeLeft, Color.magenta));
                _mpb.SetFloat(EyeGlowId, _def.EyeGlow);
                _eyes.SetPropertyBlock(_mpb, 0);
            }
            // брови: второй материал у Hero_Brows и Hero_Eyes (ресницы)
            var brow = ColorUtil.Parse(_def.BrowColor, Color.white);
            foreach (var r in new[] { _brows, _eyes })
            {
                if (r == null || r.sharedMaterials.Length < 2) continue;
                r.GetPropertyBlock(_mpb, 1);
                _mpb.SetColor(BaseColorId, brow);
                r.SetPropertyBlock(_mpb, 1);
            }
        }

        private void SetColor(Renderer r, int id, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(id, c);
            r.SetPropertyBlock(_mpb);
        }

        private void ApplySkin()
        {
            if (_body == null) return;
            _body.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, ColorUtil.Parse(_def.SkinTint, Color.white));
            _mpb.SetFloat(DirtId, _dirt);
            _mpb.SetColor(DirtColorId, ColorUtil.Parse(_def.DirtColor, Color.gray));
            _body.SetPropertyBlock(_mpb);
        }

        /// <summary>Умыться: грязь плавно сходит за WashSeconds.</summary>
        public void Wash()
        {
            _washFrom = _dirt;
            _washStart = Time.time;
        }

        // ------------------------------------------------------------ оружие

        private void RefreshWeapon()
        {
            if (_weapons.Count == 0) return;
            string show = null;
            var item = _inventory?.GetEquipped(EquipSlot.MainHand);
            if (item != null)
            {
                if (_def.Weapons == null || !_def.Weapons.TryGetValue(item.BaseId, out show)) show = _def.DefaultWeapon;
            }
            foreach (var kv in _weapons) kv.Value.SetActive(kv.Key == show);
            ApplyGrip();
        }

        /// <summary>Текущий поворот оружия в кисти (из characters.json → appearance.weaponGrip или из консоли).</summary>
        public Vector3 WeaponGrip
        {
            get
            {
                if (_hasGripOverride) return _gripOverride;
                var g = _def.WeaponGrip;
                return g != null && g.Length >= 3 ? new Vector3(g[0], g[1], g[2]) : Vector3.zero;
            }
            set
            {
                _gripOverride = value;
                _hasGripOverride = true;
                ApplyGrip();
            }
        }

        private void ApplyGrip()
        {
            var rot = Quaternion.Euler(WeaponGrip);
            foreach (var kv in _weaponBaseRot)
            {
                if (kv.Key == null) continue;
                var baseRot = kv.Value;
                if (_def.WeaponAutoGrip && TryAutoGrip(kv.Key, out var auto)) baseRot = auto;
                kv.Key.localRotation = baseRot * rot;
            }
        }

        /// <summary>
        /// Хват по костям кисти (Humanoid): ось клинка — вдоль линии костяшек от мизинца к указательному,
        /// ширина клинка (кромки) — вдоль пальцев. Считается в локальных координатах родителя оружия,
        /// поэтому не зависит от текущего кадра анимации.
        /// </summary>
        private bool TryAutoGrip(Transform weapon, out Quaternion local)
        {
            local = Quaternion.identity;
            var anim = GetComponentInChildren<Animator>();
            if (anim == null || !anim.isHuman || weapon.parent == null) return false;
            var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var index = anim.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            var little = anim.GetBoneTransform(HumanBodyBones.RightLittleProximal) ?? anim.GetBoneTransform(HumanBodyBones.RightRingProximal);
            var middle = anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            if (hand == null || index == null || little == null || middle == null) return false;
            var mf = weapon.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            if (!BladeAxes(mf.sharedMesh.bounds, out var blade, out var width)) return false;

            var parent = weapon.parent;
            Vector3 P(Transform t) => parent.InverseTransformPoint(t.position);
            var knuckles = (P(index) - P(little)).normalized;   // клинок выходит со стороны указательного
            var fingers = P(middle) - P(hand);                  // от запястья к пальцам
            fingers = Vector3.ProjectOnPlane(fingers, knuckles).normalized;
            if (knuckles.sqrMagnitude < 1e-6f || fingers.sqrMagnitude < 1e-6f) return false;
            local = Quaternion.LookRotation(knuckles, fingers) * Quaternion.Inverse(Quaternion.LookRotation(blade, width));
            return true;
        }

        /// <summary>Ось клинка (самая длинная ось меша, к острию — в сторону от точки опоры) и ось ширины (гарда).</summary>
        private static bool BladeAxes(Bounds b, out Vector3 blade, out Vector3 width)
        {
            var e = b.extents;
            var axes = new[] { (e.x, Vector3.right), (e.y, Vector3.up), (e.z, Vector3.forward) };
            System.Array.Sort(axes, (p, q) => q.Item1.CompareTo(p.Item1));
            blade = axes[0].Item2;
            width = axes[1].Item2;
            if (Vector3.Dot(b.center, blade) < 0f) blade = -blade;
            return axes[0].Item1 > axes[1].Item1 * 1.5f;
        }

        /// <summary>
        /// Куда смотрит клинок видимого оружия относительно героя: (вперёд, вправо, вверх), каждое −1..1.
        /// Ось клинка — самая длинная ось меша, в сторону от рукояти (точки опоры) к острию.
        /// </summary>
        public bool TryGetBladeDirection(out Vector3 relative)
        {
            relative = Vector3.zero;
            foreach (var kv in _weapons)
            {
                if (!kv.Value.activeInHierarchy) continue;
                var mf = kv.Value.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                BladeAxes(mf.sharedMesh.bounds, out var axis, out _);
                var world = kv.Value.transform.TransformDirection(axis).normalized;
                var root = transform;
                relative = new Vector3(Vector3.Dot(world, root.forward), Vector3.Dot(world, root.right), Vector3.Dot(world, Vector3.up));
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------ энергия

        private void OnMonsterKilled(MonsterKilledEvent e)
        {
            if (_def.AbsorbRadius <= 0f || _crystal == null) return;
            if ((e.Position - transform.position).sqrMagnitude > _def.AbsorbRadius * _def.AbsorbRadius) return;
            Absorb(e.Position + Vector3.up * 0.6f, Mathf.Clamp(e.Level, 1, 6));
        }

        /// <summary>Энергия летит из точки в кристалл, на прилёте кристалл вспыхивает.</summary>
        public void Absorb(Vector3 from, int motes = 3)
        {
            var color = Color.Lerp(ColorUtil.Parse(_def.CrystalDark, Color.magenta), ColorUtil.Parse(_def.CrystalLight, Color.yellow), Balance);
            for (int i = 0; i < motes; i++)
                EnergyMote.Spawn(from + Random.insideUnitSphere * 0.4f, this, color * 4f, 0.45f + i * 0.08f);
        }

        public void OnMoteArrived() => _pulse = Mathf.Min(_pulse + 0.6f, 2f);

        private void Update()
        {
            if (_washStart >= 0f)
            {
                float t = (Time.time - _washStart) / Mathf.Max(0.05f, _def.WashSeconds);
                _dirt = Mathf.Lerp(_washFrom, 0f, t);
                if (t >= 1f) _washStart = -1f;
                ApplySkin();
            }

            _pulse = Mathf.MoveTowards(_pulse, 0f, Time.deltaTime * 1.8f);
            if (_crystal != null)
            {
                var light = ColorUtil.Parse(_def.CrystalLight, Color.yellow);
                var dark = ColorUtil.Parse(_def.CrystalDark, Color.magenta);
                _crystal.GetPropertyBlock(_mpb);
                _mpb.SetFloat(BalanceId, Balance);
                _mpb.SetFloat(PulseId, _pulse);
                _mpb.SetFloat(GlowId, _def.CrystalGlow);
                _mpb.SetColor(LightColorId, light);
                _mpb.SetColor(DarkColorId, dark);
                _crystal.SetPropertyBlock(_mpb);
                if (_crystalLight != null)
                {
                    _crystalLight.color = Color.Lerp(dark, light, Balance);
                    _crystalLight.intensity = 0.6f + _pulse * 2.5f;
                }
            }
        }
    }

    /// <summary>Светящаяся искра энергии: летит по дуге к кристаллу наруча.</summary>
    public sealed class EnergyMote : MonoBehaviour
    {
        private HeroAppearance _target;
        private Vector3 _from, _bend;
        private float _start, _duration;

        public static void Spawn(Vector3 from, HeroAppearance target, Color hdrColor, float duration)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "EnergyMote";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.09f;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = MaterialFactory.CreateUnlit(hdrColor);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = 0.08f;
            trail.endWidth = 0f;
            trail.sharedMaterial = r.sharedMaterial;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var m = go.AddComponent<EnergyMote>();
            m._target = target;
            m._from = from;
            m._bend = Vector3.up * Random.Range(0.8f, 1.6f) + Random.insideUnitSphere * 0.8f;
            m._start = Time.time;
            m._duration = duration;
        }

        private void Update()
        {
            if (_target == null) { Destroy(gameObject); return; }
            float t = Mathf.Clamp01((Time.time - _start) / _duration);
            float e = t * t * (3f - 2f * t);
            Vector3 to = _target.CrystalPosition;
            Vector3 mid = (_from + to) * 0.5f + _bend;
            // квадратичная кривая Безье
            transform.position = Vector3.Lerp(Vector3.Lerp(_from, mid, e), Vector3.Lerp(mid, to, e), e);
            transform.localScale = Vector3.one * Mathf.Lerp(0.09f, 0.04f, e);
            if (t >= 1f)
            {
                _target.OnMoteArrived();
                Destroy(GetComponent<Renderer>().sharedMaterial, 0.5f);
                Destroy(gameObject);
            }
        }
    }
}
