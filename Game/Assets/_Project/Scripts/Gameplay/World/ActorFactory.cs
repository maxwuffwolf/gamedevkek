using Game.Core;
using Game.Data;
using Game.Gameplay.Actors;
using Game.Gameplay.AI;
using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Сборка акторов из данных: капсула-коллайдер + компоненты + модель (префаб из ActorVisual или заглушка).
    /// Новый моб в JSON → спавнится этим же кодом, ничего писать не нужно.
    /// </summary>
    public static class ActorFactory
    {
        public static Actor SpawnPlayer(CharacterDef def, Vector3 position, Quaternion rotation)
        {
            var go = CreateBody("Player", position, rotation, 0.4f, 1.8f);
            var actor = go.AddComponent<Actor>();
            actor.Team = Team.Player;
            actor.Radius = 0.4f;
            actor.HeadHeight = 2.1f;
            go.AddComponent<Mover>();
            go.AddComponent<SkillCaster>();
            BuildModel(go.transform, def.Prefab, def.Placeholder);
            go.AddComponent<AnimatorBridge>();

            GameWorld.Player = actor;
            var character = go.AddComponent<PlayerCharacter>();
            character.Init();
            GameWorld.PlayerController = go.AddComponent<PlayerController>();
            EventBus.Publish(new PlayerSpawnedEvent(actor));
            EventBus.Publish(new ActorSpawnedEvent(actor));
            return actor;
        }

        public static Actor SpawnMonster(MonsterDef def, int level, Vector3 position, Quaternion rotation)
        {
            if (def == null) return null;
            float scale = Mathf.Max(0.3f, def.Placeholder?.Scale ?? 1f);
            var go = CreateBody(def.Id, position, rotation, 0.45f * scale, 1.1f * scale);
            var actor = go.AddComponent<Actor>();
            actor.Team = Team.Monster;
            actor.Radius = 0.5f * scale;
            actor.HeadHeight = 1.5f * scale;
            go.AddComponent<Mover>();
            go.AddComponent<SkillCaster>();
            var model = BuildModel(go.transform, def.Prefab, def.Placeholder);
            var monster = go.AddComponent<MonsterActor>();
            monster.Init(def.Id, level, model);
            go.AddComponent<MonsterBrain>();
            go.AddComponent<AnimatorBridge>();
            EventBus.Publish(new ActorSpawnedEvent(actor));
            return actor;
        }

        public static NpcActor SpawnNpc(NpcDef def, Vector3 position, Quaternion rotation)
        {
            if (def == null) return null;
            var go = new GameObject($"NPC_{def.Id}");
            go.transform.SetPositionAndRotation(position, rotation);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.5f;
            col.height = 2f;
            col.center = new Vector3(0f, 1f, 0f);
            BuildModel(go.transform, def.Prefab, def.Placeholder);
            var npc = go.AddComponent<NpcActor>();
            npc.Init(def.Id, 2f * (def.Placeholder?.Scale ?? 1f));
            return npc;
        }

        private static GameObject CreateBody(string name, Vector3 position, Quaternion rotation, float radius, float height)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(position, rotation);
            var cc = go.AddComponent<CharacterController>();
            cc.radius = radius;
            cc.height = Mathf.Max(height, radius * 2f);
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            cc.skinWidth = 0.05f;
            cc.stepOffset = Mathf.Min(0.3f, cc.height * 0.3f);
            cc.slopeLimit = 50f;
            return go;
        }

        /// <summary>Префаб из ActorVisual (Resources/Visuals) или процедурная заглушка.</summary>
        public static Transform BuildModel(Transform parent, string visualId, PlaceholderDef placeholder)
        {
            var visual = Services.Get<VisualRegistry>().Actor(visualId);
            if (visual != null && visual.Prefab != null)
            {
                var inst = Object.Instantiate(visual.Prefab, parent);
                inst.name = "Model";
                inst.transform.localPosition = visual.Offset;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one * visual.Scale;
                foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                return inst.transform;
            }
            return PlaceholderModels.Build(placeholder, parent).transform;
        }
    }
}
