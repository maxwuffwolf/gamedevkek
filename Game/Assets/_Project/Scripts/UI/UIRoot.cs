using Game.Core;
using Game.Gameplay.Dialogue;
using Game.Gameplay.Items;
using Game.Gameplay.Zones;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if GAME_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Game.UI
{
    /// <summary>
    /// Корень интерфейса: Canvas 1920×1080 (масштабируется), EventSystem, все окна. Горячие клавиши окон тоже тут.
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        public static UIRoot Instance { get; private set; }

        public RectTransform Canvas { get; private set; }
        public HudView Hud { get; private set; }
        public InventoryView Inventory { get; private set; }
        public DialogueView Dialogue { get; private set; }
        public ContainerView Container { get; private set; }

        public static UIRoot Create()
        {
            var go = new GameObject("[UI]");
            DontDestroyOnLoad(go);
            var root = go.AddComponent<UIRoot>();
            Instance = root;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            root.Canvas = (RectTransform)go.transform;

            EnsureEventSystem();

            // порядок = порядок отрисовки (последний поверх)
            WorldBarsView.Create(root.Canvas);
            FloatingTextView.Create(root.Canvas);
            HoverInfoView.Create(root.Canvas);
            QuestTrackerView.Create(root.Canvas);
            root.Hud = HudView.Create(root.Canvas);
            MessageFeedView.Create(root.Canvas);
            root.Dialogue = DialogueView.Create(root.Canvas);
            root.Inventory = InventoryView.Create(root.Canvas);
            root.Container = ContainerView.Create(root.Canvas, root.Inventory);
            DeathView.Create(root.Canvas);
            TooltipView.Create(root.Canvas);
            LoadingScreenView.Create(root.Canvas); // поверх всего

            EventBus.Subscribe<ZoneLeavingEvent>(root.OnZoneLeaving);

            GameInput.RegisterPointerBlocker(IsPointerOverUi);
            return root;
        }

        private static bool IsPointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            DontDestroyOnLoad(es);
            es.AddComponent<EventSystem>();
#if GAME_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        private void Update()
        {
            if (GameInput.GameplayBlocked) return;
            if (GameInput.Down(GameKey.B) || GameInput.Down(GameKey.I)) Inventory.Toggle();
            if (GameInput.Down(GameKey.Escape))
            {
                var dialogue = Services.Get<DialogueService>();
                var containers = Services.Get<ContainerService>();
                if (dialogue.IsOpen) dialogue.Close();
                else if (containers.IsOpen) containers.Close();
                else if (Inventory.IsOpen) Inventory.Toggle(false);
            }
        }

        /// <summary>Перед сменой зоны закрываем окна: NPC из диалога сейчас исчезнет вместе со сценой.</summary>
        private void OnZoneLeaving(ZoneLeavingEvent e)
        {
            var dialogue = Services.Get<DialogueService>();
            if (dialogue.IsOpen) dialogue.Close();
            if (Inventory.IsOpen) Inventory.Toggle(false);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ZoneLeavingEvent>(OnZoneLeaving);
            GameInput.UnregisterPointerBlocker(IsPointerOverUi);
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}
