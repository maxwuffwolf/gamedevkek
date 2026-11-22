using System.Linq;
using System.Text;
using Game.Core;
using Game.Data;
using Game.Gameplay.Actors;
using Game.Gameplay.Items;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>Команды консоли для контейнеров (сундуки, трупы, меч в земле).</summary>
    public static class ContainerCommands
    {
        [DevCommand("container.list", "Контейнеры на сцене и их состояние")]
        private static string List()
        {
            var svc = Services.Get<ContainerService>();
            var db = Services.Get<ContentDatabase>();
            var sb = new StringBuilder();
            var onScene = WorldContainer.All.Where(c => c != null).ToArray();
            sb.AppendLine($"На сцене: {onScene.Length}");
            foreach (var c in onScene)
            {
                var st = svc.Find(c.Key);
                string status = st == null ? "не открывали" : st.IsEmpty ? "пусто" : $"{st.Slots.Count(i => i != null)} предм.";
                sb.AppendLine($"  {c.Key}  ({db.Container(c.ContainerId)?.Name ?? "<color=#FF7070>нет в JSON</color>"}) — {status}");
            }
            sb.Append($"В сейве: {svc.All.Count()} (шаблонов в JSON: {db.Containers.Count})");
            return sb.ToString();
        }

        [DevCommand("container.reset", "Вернуть содержимое по JSON: container.reset [ключ] (без ключа — все). Пример: container.reset Ravine:ravine_rusty_sword")]
        private static string ResetCmd(string key = null)
        {
            int n = Services.Get<ContainerService>().Reset(key);
            return string.IsNullOrEmpty(key) ? $"Сброшены все контейнеры ({n})" : n > 0 ? $"Сброшен {key}" : $"Нет состояния для {key} (уже как новый)";
        }

        [DevCommand("container.open", "Открыть ближайший контейнер, где бы ты ни стоял")]
        private static string Open()
        {
            var p = GameWorld.Player;
            if (p == null) return "Нет героя";
            var nearest = WorldContainer.All.Where(c => c != null)
                .OrderBy(c => Vector3.Distance(c.transform.position, p.transform.position)).FirstOrDefault();
            if (nearest == null) return "На сцене нет контейнеров";
            Vector3 away = p.transform.position - nearest.InteractPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.back;
            p.GetComponent<Mover>().Teleport(GameWorld.SnapToGround(nearest.InteractPosition + away.normalized * 1.3f));
            nearest.Interact(p);
            return $"Открыт {nearest.Key}";
        }
    }
}
