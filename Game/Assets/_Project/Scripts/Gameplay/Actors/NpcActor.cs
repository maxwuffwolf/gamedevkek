using Game.Core;
using Game.Data;
using Game.Gameplay.Dialogue;
using Game.Gameplay.Quests;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay.Actors
{
    /// <summary>NPC: имя над головой, значок квеста («!» есть задание, «?» можно сдать), клик → диалог.</summary>
    public sealed class NpcActor : MonoBehaviour, IInteractable
    {
        public string NpcId;

        private WorldLabel _marker;
        private QuestService _quests;
        private Quaternion _baseRotation;

        public NpcDef Def => Services.Get<ContentDatabase>().Npc(NpcId);

        public void Init(string npcId, float height)
        {
            NpcId = npcId;
            _quests = Services.Get<QuestService>();
            _baseRotation = transform.rotation;
            var def = Def;
            string title = def != null && !string.IsNullOrEmpty(def.Title) ? $"\n«{def.Title}»" : "";
            WorldLabel.Create(transform, new Vector3(0f, height + 0.35f, 0f), (def?.Name ?? npcId) + title, new Color(0.6f, 1f, 0.6f), 0.35f);
            _marker = WorldLabel.Create(transform, new Vector3(0f, height + 1.25f, 0f), "!", new Color(1f, 0.85f, 0.1f), 1.4f);
            _marker.BobAmplitude = 0.12f;
        }

        private void Update()
        {
            if (_marker == null || _quests == null) return;
            switch (_quests.MarkerFor(NpcId))
            {
                case NpcMarker.Available:
                    _marker.gameObject.SetActive(true);
                    _marker.Text.text = "!";
                    _marker.Text.color = new Color(1f, 0.85f, 0.1f);
                    break;
                case NpcMarker.Ready:
                    _marker.gameObject.SetActive(true);
                    _marker.Text.text = "?";
                    _marker.Text.color = new Color(1f, 0.85f, 0.1f);
                    break;
                case NpcMarker.InProgress:
                    _marker.gameObject.SetActive(true);
                    _marker.Text.text = "?";
                    _marker.Text.color = new Color(0.6f, 0.6f, 0.6f);
                    break;
                default:
                    _marker.gameObject.SetActive(false);
                    break;
            }

            // поворачиваемся к игроку, пока он рядом
            if (GameWorld.Player != null)
            {
                Vector3 to = GameWorld.Player.transform.position - transform.position;
                to.y = 0f;
                var target = to.magnitude < 6f && to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to) : _baseRotation;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 240f * Time.deltaTime);
            }
        }

        public string HoverText => Def?.Name ?? NpcId;
        public string ActionText => "Поговорить";
        public Vector3 InteractPosition => transform.position;
        public float InteractRange => 2.8f;
        public bool CanInteract => this != null;

        public void Interact(Actor player) => Services.Get<DialogueService>().Open(NpcId, transform);
    }
}
