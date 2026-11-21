using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Data;
using Game.Gameplay.Actors;
using Game.Gameplay.Progression;
using UnityEngine;

namespace Game.Gameplay.Items
{
    /// <summary>
    /// Лут на земле: луч света цвета самой редкой вещи в куче. Клик (или подойти и кликнуть) → всё в рюкзак.
    /// </summary>
    public sealed class LootDrop : MonoBehaviour, IInteractable
    {
        public static readonly List<LootDrop> All = new();

        private readonly List<ItemInstance> _items = new();
        private int _gold;
        private Material _beamMat, _glowMat, _discMat;
        private Light _light;
        private Transform _gem;
        private Color _color;
        private float _phase;

        public IReadOnlyList<ItemInstance> Items => _items;
        public int Gold => _gold;

        public static LootDrop Spawn(Vector3 position, IEnumerable<ItemInstance> items, int gold)
        {
            var go = new GameObject("LootDrop");
            go.transform.position = position;
            var drop = go.AddComponent<LootDrop>();
            drop._items.AddRange(items.Where(i => i != null));
            drop._gold = gold;
            drop.BuildVisual();
            return drop;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private ContentDatabase Db => Services.Get<ContentDatabase>();

        public Color BeamColor()
        {
            var best = _items.OrderByDescending(i => Db.RarityOrder(i.Rarity)).FirstOrDefault();
            if (best != null) return Db.RarityColor(best.Rarity);
            return new Color(1f, 0.82f, 0.3f); // только золото
        }

        private void BuildVisual()
        {
            _color = BeamColor();
            _phase = Random.value * 10f;

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = 0.6f;
            col.height = 3f;
            col.center = new Vector3(0f, 1.5f, 0f);

            _beamMat = MaterialFactory.CreateTransparent(WithAlpha(_color, 0.75f));
            _glowMat = MaterialFactory.CreateTransparent(WithAlpha(_color, 0.18f));
            _discMat = MaterialFactory.CreateTransparent(WithAlpha(_color, 0.35f));

            MakePart(PrimitiveType.Cylinder, new Vector3(0f, 3f, 0f), new Vector3(0.09f, 3f, 0.09f), _beamMat);
            MakePart(PrimitiveType.Cylinder, new Vector3(0f, 2.5f, 0f), new Vector3(0.35f, 2.5f, 0.35f), _glowMat);
            MakePart(PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(1.1f, 0.01f, 1.1f), _discMat);
            _gem = MakePart(PrimitiveType.Cube, new Vector3(0f, 0.35f, 0f), Vector3.one * 0.22f, MaterialFactory.Lit(_color)).transform;
            _gem.localRotation = Quaternion.Euler(45f, 0f, 45f);

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = _color;
            _light.range = 4f;
            _light.intensity = 2.5f;
            _light.shadows = LightShadows.None;
        }

        private GameObject MakePart(PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
        {
            var p = GameObject.CreatePrimitive(type);
            DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(transform, false);
            p.transform.localPosition = localPos;
            p.transform.localScale = scale;
            var r = p.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return p;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private void Update()
        {
            float t = Time.time + _phase;
            float pulse = 0.8f + 0.2f * Mathf.Sin(t * 3f);
            if (_beamMat != null) _beamMat.color = WithAlpha(_color, 0.75f * pulse);
            if (_glowMat != null) _glowMat.color = WithAlpha(_color, 0.18f * pulse);
            if (_light != null) _light.intensity = 2.5f * pulse;
            if (_gem != null)
            {
                _gem.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
                _gem.localPosition = new Vector3(0f, 0.35f + 0.08f * Mathf.Sin(t * 2f), 0f);
            }
        }

        // ---------------------------------------------------------------- IInteractable

        public string HoverText
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var it in _items)
                {
                    var def = Db.Item(it.BaseId);
                    sb.AppendLine(ColorUtil.Colorize(def?.Name ?? it.BaseId, Db.RarityColor(it.Rarity)));
                }
                if (_gold > 0) sb.AppendLine(ColorUtil.Colorize($"{_gold} золота", new Color(1f, 0.82f, 0.3f)));
                return sb.ToString().TrimEnd();
            }
        }

        public string ActionText => "Подобрать";
        public Vector3 InteractPosition => transform.position;
        public float InteractRange => 2.2f;
        public bool CanInteract => this != null && (_items.Count > 0 || _gold > 0);

        public void Interact(Actor player)
        {
            var inv = Services.Get<Inventory>();
            if (_gold > 0)
            {
                Services.Get<PlayerProfile>().AddGold(_gold);
                EventBus.Publish(new UiMessageEvent($"+{_gold} золота", new Color(1f, 0.82f, 0.3f)));
                _gold = 0;
            }
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (!inv.TryAdd(_items[i])) continue;
                _items.RemoveAt(i);
            }
            if (_items.Count > 0)
            {
                EventBus.Publish(new UiMessageEvent("Рюкзак полон", new Color(1f, 0.4f, 0.4f)));
                _color = BeamColor();
                return;
            }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_beamMat != null) Destroy(_beamMat);
            if (_glowMat != null) Destroy(_glowMat);
            if (_discMat != null) Destroy(_discMat);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();
    }
}
