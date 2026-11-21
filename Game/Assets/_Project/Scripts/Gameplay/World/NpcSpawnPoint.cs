using System.Collections;
using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>Точка, где появится NPC из npcs.json. Поставь на сцену и впиши id.</summary>
    public sealed class NpcSpawnPoint : MonoBehaviour
    {
        public string NpcId = "npc_elder";

        private IEnumerator Start()
        {
            while (!GameState.IsReady) yield return null;
            var def = Services.Get<ContentDatabase>().Npc(NpcId);
            if (def == null)
            {
                Log.Warn("World", $"NpcSpawnPoint: нет NPC \"{NpcId}\" в данных");
                yield break;
            }
            ActorFactory.SpawnNpc(def, GameWorld.SnapToGround(transform.position), transform.rotation);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(0.8f, 2f, 0.8f));
        }
    }
}
