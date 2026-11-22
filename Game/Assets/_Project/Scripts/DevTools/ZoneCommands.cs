using System.Linq;
using System.Text;
using Game.Core;
using Game.Data;
using Game.Gameplay.Zones;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>Команды консоли для зон: список, мгновенный переход, входы текущей сцены.</summary>
    public static class ZoneCommands
    {
        [DevCommand("zones", "Список зон и входов текущей сцены")]
        private static string Zones()
        {
            var db = Services.Get<ContentDatabase>();
            var svc = Services.Get<ZoneService>();
            var sb = new StringBuilder();
            sb.AppendLine($"Текущая зона: {svc.CurrentZoneId ?? "(сцена не описана в zones)"}");
            foreach (var z in db.Zones.Values)
            {
                bool ok = Application.CanStreamedLevelBeLoaded(z.Scene);
                sb.AppendLine($"  {z.Id} — {z.Name}, сцена {z.Scene}{(z.Start ? ", старт" : "")}{(ok ? "" : "  <color=#FF7070>НЕТ в Build Settings</color>")}");
            }
            var entrances = ZoneEntrance.All.Where(e => e != null).Select(e => e.Id).ToList();
            sb.Append($"Входы на этой сцене: {(entrances.Count > 0 ? string.Join(", ", entrances) : "нет")}");
            return sb.ToString();
        }

        [DevCommand("zone", "Перейти в зону: zone <id> [вход]. Пример: zone forest from_ravine")]
        private static string Zone(string id, string entrance = null)
        {
            var svc = Services.Get<ZoneService>();
            if (svc.IsLoading) return "Уже идёт загрузка";
            return svc.Travel(id, entrance) ? $"Переход в {id}..." : "Не получилось, см. ошибку выше";
        }
    }
}
