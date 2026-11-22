using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Gameplay.Actors;
using Game.Gameplay.Combat;
using Game.Gameplay.Progression;
using Game.Gameplay.World;
using Game.Gameplay.Zones;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// HUD в стиле «Тёплое стекло»: уровень и тонкие полосы HP/маны/опыта внизу по центру,
    /// круглые навыки справа снизу (крупная обычная атака + слоты 1..=), зона слева сверху,
    /// баланс наруча Свет/Тьма справа сверху.
    /// Пустые слоты после 4-го скрыты, чтобы не загромождать экран: появятся, когда в них будет навык.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private const float SlotSize = 64f;
        private const float BigSize = 92f;
        private const float SlotGap = 12f;
        private const int AlwaysVisible = 4;
        private const int PerRow = 6;
        private const float Margin = 44f;

        private sealed class Slot
        {
            public RectTransform Root;
            public Image Icon;
            public Text IconText;
            public Image Cooldown;
            public Text CooldownText;
            public Image NoMana;
            public Image Ring;
        }

        private RectTransform _hpFill, _manaFill, _xpFill, _balanceFill, _balanceRoot, _skillsRoot;
        private Image _hpFillImg;
        private Text _hpText, _levelText, _zoneName, _zoneSub, _balanceLight, _balanceDark;
        private readonly Slot[] _slots = new Slot[SkillBar.SlotCount];
        private ContentDatabase _db;
        private SkillBar _skillBar;
        private PlayerProfile _profile;
        private int _contentVersion = -1;
        private string _zoneId;
        private HeroAppearance _appearance;
        private Actor _appearanceOwner;

        public static HudView Create(RectTransform canvas)
        {
            var root = UiKit.Rect("HUD", canvas);
            root.Stretch();
            var view = root.gameObject.AddComponent<HudView>();
            view.Build(root);
            return view;
        }

        private void Build(RectTransform root)
        {
            _db = Services.Get<ContentDatabase>();
            _skillBar = Services.Get<SkillBar>();
            _profile = Services.Get<PlayerProfile>();

            BuildVitals(root);
            BuildSkills(root);
            BuildZone(root);
            BuildBalance(root);

            var hint = UiKit.Text(root, "Hint", "ЛКМ — атака / взаимодействие   1-0,-,= — навыки   B — инвентарь   ~ — консоль   F1 — читы",
                14, new Color(1f, 1f, 1f, 0.4f)).Shadowed();
            hint.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(1000f, 18f));
        }

        // ------------------------------------------------------------ полосы

        private void BuildVitals(RectTransform root)
        {
            const float width = 600f, levelSize = 56f, bottom = 40f;
            var group = UiKit.Rect("Vitals", root);
            group.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, bottom), new Vector2(width + levelSize + 16f, levelSize));

            var lvl = UiKit.Panel(group, "Level", UiTheme.PanelSoft, UiTheme.RadiusPill).rectTransform;
            lvl.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(levelSize, levelSize));
            UiKit.Outline(lvl, UiTheme.Gold, UiTheme.RadiusPill, 2);
            _levelText = UiKit.Text(lvl, "Value", "", 22, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            _levelText.rectTransform.Stretch(0, 0, 0, 2);
            var lvlHover = lvl.gameObject.AddComponent<HoverTarget>();
            lvlHover.GetText = () => $"Уровень {_profile.Level}\nОпыт: {_profile.Xp} / {_profile.XpToNext(_profile.Level)}";

            float x = levelSize + 16f;
            var hp = UiKit.Bar(group, "Health", UiTheme.Hp, out _hpFill);
            hp.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 8f), new Vector2(width, 12f));
            _hpFillImg = _hpFill.GetComponent<Image>();
            hp.GetComponent<Image>().raycastTarget = true;
            hp.gameObject.AddComponent<HoverTarget>().GetText = () =>
                GameWorld.Player != null ? $"Здоровье: {Mathf.CeilToInt(GameWorld.Player.Health)} / {Mathf.CeilToInt(GameWorld.Player.MaxHealth)}" : "";

            _hpText = UiKit.Text(group, "HealthText", "", 15, UiTheme.Text, TextAnchor.LowerRight, FontStyle.Bold).Shadowed();
            _hpText.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0f), new Vector2(0f, 18f), new Vector2(240f, 20f));

            var mp = UiKit.Bar(group, "Mana", UiTheme.Mp, out _manaFill);
            mp.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, -8f), new Vector2(width, 8f));
            mp.GetComponent<Image>().raycastTarget = true;
            mp.gameObject.AddComponent<HoverTarget>().GetText = () =>
                GameWorld.Player != null ? $"Мана: {Mathf.FloorToInt(GameWorld.Player.Mana)} / {Mathf.CeilToInt(GameWorld.Player.MaxMana)}" : "";

            var xp = UiKit.Bar(group, "Xp", UiTheme.Xp, out _xpFill);
            xp.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, -20f), new Vector2(width * 0.5f, 4f));
        }

        // ------------------------------------------------------------ навыки

        private void BuildSkills(RectTransform root)
        {
            _skillsRoot = UiKit.Rect("Skills", root);
            _skillsRoot.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin, Margin - 6f), new Vector2(10f, 10f));

            // обычная атака: всегда есть, без перезарядки — крупный круг справа
            var basic = UiKit.Panel(_skillsRoot, "BasicAttack", UiTheme.PanelSoft, UiTheme.RadiusPill).rectTransform;
            basic.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(BigSize, BigSize));
            UiKit.Outline(basic, UiTheme.Gold, UiTheme.RadiusPill, 2);
            var atk = UiKit.Text(basic, "Label", "Атака", 17, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            atk.rectTransform.Stretch(0, 0, 0, 4);
            var atkKey = UiKit.KeyChip(basic, "ЛКМ", 12);
            atkKey.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), atkKey.rectTransform.sizeDelta);
            basic.gameObject.AddComponent<HoverTarget>().GetText = () => "<size=20><b>Обычная атака</b></size>\nЛКМ по врагу. Урон и скорость зависят от оружия.";

            for (int i = 0; i < SkillBar.SlotCount; i++) _slots[i] = BuildSlot(_skillsRoot, i);
        }

        private Slot BuildSlot(RectTransform parent, int index)
        {
            var s = new Slot();
            var bg = UiKit.Panel(parent, $"Slot{index + 1}", UiTheme.PanelSoft, UiTheme.RadiusPill);
            s.Root = bg.rectTransform;
            s.Root.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(SlotSize, SlotSize));
            s.Ring = UiKit.Outline(s.Root, UiTheme.Line, UiTheme.RadiusPill, 2);

            s.Icon = UiKit.Image(s.Root, "Icon", Color.clear).Round(UiTheme.RadiusPill);
            s.Icon.rectTransform.Stretch(6, 6, 6, 6);
            s.IconText = UiKit.Text(s.Icon.transform, "Abbr", "", 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            s.IconText.rectTransform.Stretch();

            s.NoMana = UiKit.Image(s.Root, "NoMana", new Color(0.1f, 0.25f, 0.75f, 0.45f)).Round(UiTheme.RadiusPill);
            s.NoMana.rectTransform.Stretch();

            // радиальная «шторка» перезарядки: круглый спрайт в режиме Filled
            s.Cooldown = UiKit.Image(s.Root, "Cooldown", new Color(0.03f, 0.04f, 0.08f, 0.68f));
            s.Cooldown.sprite = UiKit.Rounded(UiTheme.RadiusPill);
            s.Cooldown.type = Image.Type.Filled;
            s.Cooldown.fillMethod = Image.FillMethod.Radial360;
            s.Cooldown.fillOrigin = (int)Image.Origin360.Top;
            s.Cooldown.fillClockwise = false;
            s.Cooldown.fillAmount = 0f;
            s.Cooldown.rectTransform.Stretch();
            s.CooldownText = UiKit.Text(s.Root, "CdText", "", 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            s.CooldownText.rectTransform.Stretch();

            var key = UiKit.KeyChip(s.Root, SkillBar.KeyLabels[index], 12);
            key.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), key.rectTransform.sizeDelta);

            var hover = s.Root.gameObject.AddComponent<HoverTarget>();
            int i = index;
            hover.GetText = () =>
            {
                var def = _db.Skill(_skillBar.Get(i));
                return def != null ? TooltipView.SkillText(def) : $"Слот {SkillBar.KeyLabels[i]}: пусто\n<color=#999999>Навыки добавляются в characters.json → skillBar</color>";
            };
            return s;
        }

        private void RefreshIcons()
        {
            _contentVersion = _db.Version;
            var visible = new List<int>();
            for (int i = 0; i < _slots.Length; i++)
            {
                var def = _db.Skill(_skillBar.Get(i));
                var s = _slots[i];
                bool show = def != null || i < AlwaysVisible;
                s.Root.gameObject.SetActive(show);
                if (show) visible.Add(i);
                if (def == null)
                {
                    s.Icon.color = Color.clear;
                    s.IconText.text = "";
                    s.Ring.color = UiTheme.Line;
                    continue;
                }
                var c = ColorUtil.Parse(def.Icon?.Color, Color.gray);
                c.a = 0.85f;
                s.Icon.color = c;
                s.IconText.text = def.Icon?.Text ?? "?";
                s.Ring.color = UiTheme.LineStrong;
            }

            // раскладка: ряды по 6 слева от крупной атаки, клавиши по порядку слева направо, второй ряд выше
            for (int n = 0; n < visible.Count; n++)
            {
                int row = n / PerRow;
                int inRow = Mathf.Min(PerRow, visible.Count - row * PerRow);
                int col = n % PerRow;
                float right = BigSize + SlotGap + (inRow - 1 - col) * (SlotSize + SlotGap);
                float up = row * (SlotSize + SlotGap + 8f);
                _slots[visible[n]].Root.anchoredPosition = new Vector2(-right, up);
            }
        }

        // ------------------------------------------------------------ зона

        private void BuildZone(RectTransform root)
        {
            var g = UiKit.Rect("Zone", root);
            g.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin, -36f), new Vector2(420f, 52f));
            _zoneSub = UiKit.Text(g, "Subtitle", "", 14, UiTheme.Dim, TextAnchor.UpperLeft).Shadowed();
            _zoneSub.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(420f, 20f));
            _zoneName = UiKit.Text(g, "Name", "", 22, UiTheme.Text, TextAnchor.UpperLeft, FontStyle.Bold).Shadowed();
            _zoneName.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -18f), new Vector2(420f, 30f));
        }

        private void RefreshZone()
        {
            ZoneDef zone = Services.TryGet<ZoneService>(out var zones) ? zones.CurrentZone : null;
            _zoneId = zone?.Id ?? "";
            _zoneName.text = zone?.Name ?? "";
            _zoneSub.text = zone?.Subtitle ?? "";
        }

        // ------------------------------------------------------------ баланс наруча

        private void BuildBalance(RectTransform root)
        {
            _balanceRoot = UiKit.Panel(root, "Balance", UiTheme.PanelSoft, UiTheme.RadiusPill, UiTheme.Line).rectTransform;
            _balanceRoot.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Margin, -36f), new Vector2(300f, 38f));
            _balanceLight = UiKit.Text(_balanceRoot, "Light", "", 14, UiTheme.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            _balanceLight.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(80f, 24f));
            _balanceDark = UiKit.Text(_balanceRoot, "Dark", "", 14, UiTheme.Violet, TextAnchor.MiddleRight, FontStyle.Bold);
            _balanceDark.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(80f, 24f));
            var track = UiKit.Image(_balanceRoot, "Track", UiTheme.Violet).Round(UiTheme.RadiusPill).rectTransform;
            track.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 6f));
            _balanceFill = UiKit.Image(track, "Fill", UiTheme.Gold).Round(UiTheme.RadiusPill).rectTransform;
            _balanceFill.anchorMin = Vector2.zero;
            _balanceFill.anchorMax = new Vector2(0.5f, 1f);
            _balanceFill.offsetMin = _balanceFill.offsetMax = Vector2.zero;
            _balanceRoot.gameObject.AddComponent<HoverTarget>().GetText = () =>
                "<size=20><b>Наруч</b></size>\nБаланс Света и Тьмы. Кристалл впитывает энергию убитых врагов.";
            _balanceRoot.gameObject.SetActive(false);
        }

        private void RefreshBalance(Actor player)
        {
            if (_appearanceOwner != player)
            {
                _appearanceOwner = player;
                _appearance = player != null ? player.GetComponentInChildren<HeroAppearance>() : null;
            }
            bool has = _appearance != null;
            if (_balanceRoot.gameObject.activeSelf != has) _balanceRoot.gameObject.SetActive(has);
            if (!has) return;
            float b = Mathf.Clamp01(_appearance.Balance);
            int light = Mathf.RoundToInt(b * 100f);
            _balanceFill.anchorMax = new Vector2(b, 1f);
            _balanceLight.text = $"Свет {light}";
            _balanceDark.text = $"{100 - light} Тьма";
        }

        // ------------------------------------------------------------ обновление

        private void Update()
        {
            if (_contentVersion != _db.Version) RefreshIcons();
            if (Services.TryGet<ZoneService>(out var zones) && (zones.CurrentZoneId ?? "") != _zoneId) RefreshZone();

            var player = GameWorld.Player;
            RefreshBalance(player);
            if (player == null) return;

            float hp01 = Mathf.Clamp01(player.Health01);
            SetFill(_hpFill, hp01);
            _hpFillImg.color = hp01 < 0.3f ? UiTheme.HpLow : UiTheme.Hp;
            SetFill(_manaFill, player.Mana01);
            _hpText.text = $"{Mathf.CeilToInt(player.Health)} / {Mathf.CeilToInt(player.MaxHealth)}";
            SetFill(_xpFill, _profile.Xp01);
            _levelText.text = _profile.Level.ToString();

            var caster = player.GetComponent<SkillCaster>();
            for (int i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                if (!s.Root.gameObject.activeSelf) continue;
                var def = _db.Skill(_skillBar.Get(i));
                if (def == null || caster == null)
                {
                    s.Cooldown.fillAmount = 0f;
                    s.CooldownText.text = "";
                    s.NoMana.enabled = false;
                    continue;
                }
                s.Cooldown.fillAmount = caster.CooldownFraction(def.Id);
                float rem = caster.CooldownRemaining(def.Id);
                s.CooldownText.text = rem > 0f ? (rem >= 1f ? rem.ToString("0") : rem.ToString("0.0")) : "";
                s.NoMana.enabled = player.Mana < def.ManaCost;
            }
        }

        private static void SetFill(RectTransform fill, float value01) => fill.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
    }
}
