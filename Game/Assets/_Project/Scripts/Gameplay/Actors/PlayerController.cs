using Game.Core;
using Game.Data;
using Game.Gameplay.Combat;
using Game.Gameplay.Progression;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay.Actors
{
    /// <summary>
    /// Управление: WASD (относительно камеры), прицел мышью, ЛКМ = базовая атака (или клик по NPC/луту),
    /// 1..0,-,= = слоты панели навыков.
    /// </summary>
    [RequireComponent(typeof(Actor), typeof(Mover), typeof(SkillCaster))]
    public sealed class PlayerController : MonoBehaviour
    {
        private static readonly GameKey[] SlotKeys =
        {
            GameKey.Digit1, GameKey.Digit2, GameKey.Digit3, GameKey.Digit4, GameKey.Digit5, GameKey.Digit6,
            GameKey.Digit7, GameKey.Digit8, GameKey.Digit9, GameKey.Digit0, GameKey.Minus, GameKey.Equals,
        };

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        private Actor _actor;
        private Mover _mover;
        private SkillCaster _caster;
        private SkillBar _skillBar;
        private PlayerProfile _profile;
        private IInteractable _pendingInteract;
        private float _lastWarnTime;

        /// <summary>Объект под курсором (NPC, лут) — для подсказок в UI.</summary>
        public IInteractable Hovered { get; private set; }
        /// <summary>Враг под курсором.</summary>
        public Actor HoveredEnemy { get; private set; }
        public Vector3 AimPoint { get; private set; }

        private void Awake()
        {
            _actor = GetComponent<Actor>();
            _mover = GetComponent<Mover>();
            _caster = GetComponent<SkillCaster>();
            _skillBar = Services.Get<SkillBar>();
            _profile = Services.Get<PlayerProfile>();
        }

        private void Update()
        {
            if (_actor.IsDead)
            {
                _mover.Stop();
                _pendingInteract = null;
                return;
            }

            UpdateHover();
            bool blocked = GameInput.GameplayBlocked;
            bool pointerOverUi = GameInput.IsPointerOverUI;

            // --- движение
            Vector2 input = GameInput.Move;
            Vector3 moveDir = CameraRelative(input);
            if (moveDir.sqrMagnitude > 0.01f) _pendingInteract = null;

            // --- клик
            if (!blocked && !pointerOverUi && GameInput.LeftDown && Hovered != null && Hovered.CanInteract)
                _pendingInteract = Hovered;

            if (_pendingInteract != null)
            {
                if (!_pendingInteract.CanInteract)
                {
                    _pendingInteract = null;
                }
                else
                {
                    Vector3 to = _pendingInteract.InteractPosition - transform.position;
                    to.y = 0f;
                    if (to.magnitude <= _pendingInteract.InteractRange)
                    {
                        var target = _pendingInteract;
                        _pendingInteract = null;
                        _mover.FaceTowards(target.InteractPosition, 2000f);
                        target.Interact(_actor);
                    }
                    else
                    {
                        moveDir = to.normalized;
                    }
                }
            }
            else if (!blocked && !pointerOverUi && GameInput.LeftHeld && Hovered == null)
            {
                var basic = _profile.Character?.BasicAttack;
                if (!string.IsNullOrEmpty(basic) && !_caster.IsCasting) _caster.TryCast(basic, AimPoint);
            }

            // --- навыки
            if (!blocked)
            {
                for (int i = 0; i < SlotKeys.Length; i++)
                {
                    if (!GameInput.Down(SlotKeys[i])) continue;
                    string skill = _skillBar.Get(i);
                    if (skill == null) continue;
                    var result = _caster.TryCast(skill, AimPoint);
                    if (result == CastResult.NoMana) Warn("Не хватает маны");
                    else if (result == CastResult.Cooldown) Warn("Навык ещё не готов");
                }
            }

            // --- поворот и шаг
            float speed = _actor.Stats.Get(StatId.MoveSpeed) * _caster.MoveMultiplier;
            if (_caster.IsCasting) _mover.FaceDirection(_caster.CastDirection, 1500f);
            else if (moveDir.sqrMagnitude > 0.01f) _mover.FaceDirection(moveDir, 900f);
            _mover.Move(moveDir, speed);
        }

        private void Warn(string text)
        {
            if (Time.time - _lastWarnTime < 0.6f) return;
            _lastWarnTime = Time.time;
            EventBus.Publish(new UiMessageEvent(text, new Color(1f, 0.5f, 0.4f)));
        }

        private static Vector3 CameraRelative(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f) return Vector3.zero;
            var cam = CameraRig.Instance != null ? CameraRig.Instance.transform : null;
            Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 dir = fwd * input.y + right * input.x;
            return dir.sqrMagnitude > 1f ? dir.normalized : dir;
        }

        private void UpdateHover()
        {
            Hovered = null;
            HoveredEnemy = null;
            var cam = CameraRig.Instance != null ? CameraRig.Instance.Camera : Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(GameInput.MousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            AimPoint = plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : transform.position + transform.forward;

            if (GameInput.IsPointerOverUI) return;

            int n = Physics.RaycastNonAlloc(ray, _hits, 200f, ~0, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.distance >= best) continue;
                var interactable = h.collider.GetComponentInParent<IInteractable>();
                if (interactable != null && interactable.CanInteract)
                {
                    best = h.distance;
                    Hovered = interactable;
                    HoveredEnemy = null;
                    continue;
                }
                var actor = h.collider.GetComponentInParent<Actor>();
                if (actor != null && !actor.IsDead && _actor.IsHostileTo(actor))
                {
                    best = h.distance;
                    HoveredEnemy = actor;
                    Hovered = null;
                }
            }
            if (HoveredEnemy != null) AimPoint = HoveredEnemy.transform.position;
        }
    }
}
