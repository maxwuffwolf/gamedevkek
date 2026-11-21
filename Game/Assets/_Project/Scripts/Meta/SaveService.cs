using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.Gameplay.Items;
using Game.Gameplay.Progression;
using Game.Gameplay.Quests;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace Game.Meta
{
    /// <summary>Формат сейва. При изменении структуры: Version++ и миграция в SaveService.Migrate.</summary>
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public string SavedAt;
        public string CharacterId = "hero";
        public int Level = 1;
        public long Xp;
        public int Gold;
        public int BackpackSize;
        public int BackpackUnlocked;
        public List<ItemInstance> Backpack = new();
        public Dictionary<EquipSlot, ItemInstance> Equipped = new();
        public List<QuestProgress> Quests = new();
    }

    /// <summary>
    /// Сохранения в persistentDataPath/saves/&lt;slot&gt;.json (+ .bak предыдущей версии).
    /// Автосейв: через пару секунд после любого изменения инвентаря/квестов/прогресса и при выходе.
    /// </summary>
    public sealed class SaveService
    {
        private readonly ContentDatabase _db;
        private readonly JsonSerializerSettings _json = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include,
            Converters = { new StringEnumConverter() },
        };

        private bool _dirty;
        private float _dirtySince;
        private bool _suspended;

        public string Slot { get; private set; } = "main";
        public string Directory => Path.Combine(Application.persistentDataPath, "saves");
        public string FilePath => Path.Combine(Directory, Slot + ".json");
        public bool AutoSaveEnabled = true;

        public SaveService(ContentDatabase db)
        {
            _db = db;
            EventBus.Subscribe<InventoryChangedEvent>(_ => MarkDirty());
            EventBus.Subscribe<QuestChangedEvent>(_ => MarkDirty());
            EventBus.Subscribe<ProfileChangedEvent>(_ => MarkDirty());
        }

        public void SetSlot(string slot) => Slot = string.IsNullOrWhiteSpace(slot) ? "main" : slot.Trim();

        private void MarkDirty()
        {
            if (_suspended) return;
            if (!_dirty) _dirtySince = Time.unscaledTime;
            _dirty = true;
        }

        /// <summary>Вызывается каждый кадр (из GameRoot).</summary>
        public void Tick()
        {
            if (_dirty && AutoSaveEnabled && Time.unscaledTime - _dirtySince > 2f) Save();
        }

        public bool Exists => File.Exists(FilePath);

        public void Save()
        {
            _dirty = false;
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                var data = Capture();
                string json = JsonConvert.SerializeObject(data, _json);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", true);
                File.Copy(tmp, FilePath, true);
                File.Delete(tmp);
            }
            catch (Exception e)
            {
                Log.Error("Save", $"Не удалось сохранить: {e.Message}");
            }
        }

        public bool TryLoad(out SaveData data)
        {
            data = null;
            if (!Exists) return false;
            try
            {
                data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(FilePath), _json);
                if (data == null) return false;
                Migrate(data);
                return true;
            }
            catch (Exception e)
            {
                Log.Error("Save", $"Сейв повреждён ({e.Message}). Будет создан новый, старый лежит рядом: {FilePath}");
                try { File.Copy(FilePath, FilePath + ".broken", true); } catch { /* ignore */ }
                data = null;
                return false;
            }
        }

        private static void Migrate(SaveData data)
        {
            // Пример на будущее:
            // if (data.Version < 2) { ...; data.Version = 2; }
            data.Version = SaveData.CurrentVersion;
        }

        public SaveData Capture()
        {
            var inv = Services.Get<Inventory>();
            var profile = Services.Get<PlayerProfile>();
            var quests = Services.Get<QuestService>();
            return new SaveData
            {
                SavedAt = DateTime.Now.ToString("s"),
                CharacterId = profile.CharacterId,
                Level = profile.Level,
                Xp = profile.Xp,
                Gold = profile.Gold,
                BackpackSize = inv.Capacity,
                BackpackUnlocked = inv.Unlocked,
                Backpack = inv.SnapshotBackpack().ToList(),
                Equipped = inv.Equipped.ToDictionary(kv => kv.Key, kv => kv.Value),
                Quests = quests.All.ToList(),
            };
        }

        public void Apply(SaveData data)
        {
            _suspended = true;
            try
            {
                var profile = Services.Get<PlayerProfile>();
                if (_db.Character(data.CharacterId) != null) profile.CharacterId = data.CharacterId;
                profile.SetState(data.Level, data.Xp, data.Gold);
                var charDef = profile.Character;
                int size = Math.Max(data.BackpackSize, charDef?.Backpack.Size ?? 40);
                Services.Get<Inventory>().SetState(size, data.BackpackUnlocked, data.Backpack, data.Equipped);
                Services.Get<QuestService>().SetState(data.Quests);
            }
            finally
            {
                _suspended = false;
                _dirty = false;
            }
        }

        /// <summary>Новая игра: стартовые предметы из characters.json.</summary>
        public void ApplyNewGame()
        {
            _suspended = true;
            try
            {
                var profile = Services.Get<PlayerProfile>();
                var inv = Services.Get<Inventory>();
                var gen = Services.Get<ItemGenerator>();
                var def = profile.Character;
                profile.SetState(1, 0, 0);
                Services.Get<QuestService>().ResetAll();
                inv.SetState(def?.Backpack.Size ?? 40, def?.Backpack.Unlocked ?? 12, null, null);
                if (def != null)
                {
                    foreach (var s in def.StartingItems)
                    {
                        var item = gen.Create(s.Id, s.Rarity, 1);
                        if (item == null) continue;
                        var baseDef = _db.Item(s.Id);
                        if (s.Equip && baseDef != null) inv.ForceEquip(inv.DefaultSlotFor(baseDef.Slot), item);
                        else inv.TryAdd(item);
                    }
                }
            }
            finally
            {
                _suspended = false;
            }
            Save();
        }

        public void Wipe()
        {
            foreach (var f in new[] { FilePath, FilePath + ".bak" })
                if (File.Exists(f)) File.Delete(f);
        }
    }
}
