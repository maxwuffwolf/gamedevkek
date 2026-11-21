using Game.Core;
using Game.Data;
using Game.Gameplay.Combat;
using Game.Gameplay.Progression;
using Game.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Нижняя панель как в Diablo: столбик HP слева, 12 слотов навыков по центру, столбик маны справа, полоска опыта.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private const float SlotSize = 58f;
        private const float SlotGap = 6f;

        private sealed class Slot
        {
            public Image Icon;
            public Text IconText;
            public Image Cooldown;
            public Text CooldownText;
            public Image NoMana;
            public HoverTarget Hover;
        }

        private RectTransform _hpFill, _manaFill, _xpFill;
        private Text _hpText, _manaText, _levelText;
        private readonly Slot[] _slots = new Slot[SkillBar.SlotCount];
        private ContentDatabase _db;
        private SkillBar _skillBar;
        private PlayerProfile _profile;
        private int _contentVersion = -1;

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

            float barWidth = SkillBar.SlotCount * SlotSize + (SkillBar.SlotCount - 1) * SlotGap;
            const float columnW = 46f, columnH = 170f, bottom = 18f;

            // фон панели
            var panel = UiKit.Image(root, "BottomPanel", UiKit.PanelColor, true).rectTransform;
            panel.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, bottom - 8f), new Vector2(barWidth + 2 * (columnW + 34f) + 20f, SlotSize + 40f));
            UiKit.Frame(panel, UiKit.FrameColor);

            // слоты навыков
            for (int i = 0; i < SkillBar.SlotCount; i++)
            {
                float x = -barWidth / 2f + i * (SlotSize + SlotGap) + SlotSize / 2f;
                _slots[i] = BuildSlot(root, i, new Vector2(x, bottom + 12f));
            }

            // XP
            var xpBg = UiKit.Image(root, "XpBg", new Color(0.05f, 0.05f, 0.05f, 0.9f)).rectTransform;
            xpBg.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, bottom + SlotSize + 18f), new Vector2(barWidth, 8f));
            _xpFill = UiKit.Image(xpBg, "Fill", new Color(0.75f, 0.55f, 1f)).rectTransform;
            _xpFill.anchorMin = Vector2.zero;
            _xpFill.anchorMax = new Vector2(0f, 1f);
            _xpFill.offsetMin = _xpFill.offsetMax = Vector2.zero;
            var xpHover = xpBg.gameObject.AddComponent<HoverTarget>();
            xpBg.GetComponent<Image>().raycastTarget = true;
            xpHover.GetText = () => $"Опыт: {_profile.Xp} / {_profile.XpToNext(_profile.Level)}";

            _levelText = UiKit.Text(root, "Level", "", 18, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            _levelText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, bottom + SlotSize + 30f), new Vector2(240f, 24f));

            // столбики HP и маны
            float columnX = barWidth / 2f + 26f + columnW / 2f;
            BuildColumn(root, "Health", new Vector2(-columnX, bottom - 2f), new Vector2(columnW, columnH),
                new Color(0.75f, 0.1f, 0.1f), out _hpFill, out _hpText);
            BuildColumn(root, "Mana", new Vector2(columnX, bottom - 2f), new Vector2(columnW, columnH),
                new Color(0.15f, 0.35f, 0.9f), out _manaFill, out _manaText);

            // подсказка по управлению
            var hint = UiKit.Text(root, "Hint", "ЛКМ — атака / взаимодействие   1-0,-,= — навыки   B — инвентарь   ~ — консоль   F1 — читы",
                14, new Color(1f, 1f, 1f, 0.45f)).Shadowed();
            hint.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(1000f, 18f));
        }

        private void BuildColumn(RectTransform root, string name, Vector2 pos, Vector2 size, Color color, out RectTransform fill, out Text text)
        {
            var frame = UiKit.Image(root, name, UiKit.FrameColor, true).rectTransform;
            frame.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), pos, size + new Vector2(6f, 6f));
            var bg = UiKit.Image(frame, "Bg", new Color(0.04f, 0.03f, 0.03f, 1f)).rectTransform;
            bg.Stretch(3, 3, 3, 3);
            fill = UiKit.Image(bg, "Fill", color).rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            // блик
            var shine = UiKit.Image(fill, "Shine", new Color(1f, 1f, 1f, 0.18f)).rectTransform;
            shine.anchorMin = new Vector2(0.15f, 0f);
            shine.anchorMax = new Vector2(0.35f, 1f);
            shine.offsetMin = shine.offsetMax = Vector2.zero;

            text = UiKit.Text(root, name + "Text", "", 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            text.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), pos + new Vector2(0f, size.y + 10f), new Vector2(110f, 20f));
        }

        private Slot BuildSlot(RectTransform root, int index, Vector2 pos)
        {
            var s = new Slot();
            var frame = UiKit.Image(root, $"Slot{index + 1}", new Color(0.35f, 0.3f, 0.22f), true).rectTransform;
            frame.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), pos, new Vector2(SlotSize, SlotSize));
            var bg = UiKit.Image(frame, "Bg", UiKit.SlotColor).rectTransform;
            bg.Stretch(2, 2, 2, 2);
            s.Icon = UiKit.Image(bg, "Icon", Color.clear);
            s.Icon.rectTransform.Stretch(3, 3, 3, 3);
            s.IconText = UiKit.Text(s.Icon.transform, "Abbr", "", 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            s.IconText.rectTransform.Stretch();
            s.NoMana = UiKit.Image(bg, "NoMana", new Color(0.1f, 0.2f, 0.7f, 0.45f));
            s.NoMana.rectTransform.Stretch();
            s.Cooldown = UiKit.Image(bg, "Cooldown", new Color(0f, 0f, 0f, 0.65f));
            var cd = s.Cooldown.rectTransform;
            cd.anchorMin = Vector2.zero;
            cd.anchorMax = new Vector2(1f, 0f);
            cd.offsetMin = cd.offsetMax = Vector2.zero;
            s.CooldownText = UiKit.Text(bg, "CdText", "", 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            s.CooldownText.rectTransform.Stretch();
            var key = UiKit.Text(frame, "Key", SkillBar.KeyLabels[index], 13, UiKit.TextColor, TextAnchor.UpperLeft, FontStyle.Bold).Shadowed();
            key.rectTransform.Stretch(5, 2, 2, 2);

            s.Hover = frame.gameObject.AddComponent<HoverTarget>();
            int i = index;
            s.Hover.GetText = () =>
            {
                var def = _db.Skill(_skillBar.Get(i));
                return def != null ? TooltipView.SkillText(def) : $"Слот {SkillBar.KeyLabels[i]}: пусто\n<color=#999999>Навыки добавляются в characters.json → skillBar</color>";
            };
            return s;
        }

        private void RefreshIcons()
        {
            _contentVersion = _db.Version;
            for (int i = 0; i < _slots.Length; i++)
            {
                var def = _db.Skill(_skillBar.Get(i));
                var s = _slots[i];
                if (def == null)
                {
                    s.Icon.color = Color.clear;
                    s.IconText.text = "";
                    continue;
                }
                s.Icon.color = ColorUtil.Parse(def.Icon?.Color, Color.gray);
                s.IconText.text = def.Icon?.Text ?? "?";
            }
        }

        private void Update()
        {
            if (_contentVersion != _db.Version) RefreshIcons();
            var player = GameWorld.Player;
            if (player == null) return;

            SetFill(_hpFill, player.Health01);
            SetFill(_manaFill, player.Mana01);
            _hpText.text = $"{Mathf.CeilToInt(player.Health)}/{Mathf.CeilToInt(player.MaxHealth)}";
            _manaText.text = $"{Mathf.FloorToInt(player.Mana)}/{Mathf.CeilToInt(player.MaxMana)}";
            _xpFill.anchorMax = new Vector2(_profile.Xp01, 1f);
            _levelText.text = $"Уровень {_profile.Level}";

            var caster = player.GetComponent<SkillCaster>();
            for (int i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                var def = _db.Skill(_skillBar.Get(i));
                if (def == null || caster == null)
                {
                    s.Cooldown.rectTransform.anchorMax = new Vector2(1f, 0f);
                    s.CooldownText.text = "";
                    s.NoMana.enabled = false;
                    continue;
                }
                float frac = caster.CooldownFraction(def.Id);
                s.Cooldown.rectTransform.anchorMax = new Vector2(1f, frac);
                float rem = caster.CooldownRemaining(def.Id);
                s.CooldownText.text = rem > 0f ? (rem >= 1f ? rem.ToString("0") : rem.ToString("0.0")) : "";
                s.NoMana.enabled = player.Mana < def.ManaCost;
            }
        }

        private static void SetFill(RectTransform fill, float value01) => fill.anchorMax = new Vector2(1f, Mathf.Clamp01(value01));
    }
}
