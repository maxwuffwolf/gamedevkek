using Game.Core;
using Game.Gameplay.Actors;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Top-down камера: следует за игроком с лёгким опережением к курсору, зум колесом, тряска от тяжёлых ударов.
    /// (Позже можно заменить на Cinemachine — он уже есть в редакторе 6.6 как встроенный пакет.)
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        public float Pitch = 55f;
        public float Yaw;
        public float Distance = 17f;
        public float MinDistance = 9f;
        public float MaxDistance = 28f;
        public float FollowSharpness = 10f;
        public float CursorLead = 1.5f;

        public Camera Camera { get; private set; }
        public Transform Target { get; set; }

        private Vector3 _focus;
        private float _shake;

        public static CameraRig Create(Transform target)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            var rig = cam.gameObject.GetComponent<CameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
            rig.Camera = cam;
            rig.Target = target;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            rig.SnapToTarget();
            return rig;
        }

        private void Awake()
        {
            Instance = this;
            Camera = GetComponent<Camera>();
        }

        private void OnEnable() => EventBus.Subscribe<DamageDealtEvent>(OnDamage);
        private void OnDisable() => EventBus.Unsubscribe<DamageDealtEvent>(OnDamage);

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnDamage(DamageDealtEvent e)
        {
            if (e.ScreenShake > 0f && e.Source != null && e.Source.Team == Team.Player) Shake(e.ScreenShake);
            else if (e.Target != null && e.Target.Team == Team.Player && e.Amount > 0) Shake(0.08f);
        }

        public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        public void SnapToTarget()
        {
            if (Target == null) return;
            _focus = Target.position;
            transform.SetPositionAndRotation(DesiredPosition(_focus), Quaternion.Euler(Pitch, Yaw, 0f));
        }

        private Vector3 DesiredPosition(Vector3 focus) => focus + Quaternion.Euler(Pitch, Yaw, 0f) * (Vector3.back * Distance);

        private void LateUpdate()
        {
            if (Target == null) return;

            if (!GameInput.GameplayBlocked && !GameInput.IsPointerOverUI)
                Distance = Mathf.Clamp(Distance - GameInput.Scroll * 1.5f, MinDistance, MaxDistance);

            Vector3 lead = Vector3.zero;
            var pc = GameWorld.PlayerController;
            if (pc != null)
            {
                lead = pc.AimPoint - Target.position;
                lead.y = 0f;
                lead = Vector3.ClampMagnitude(lead * 0.15f, CursorLead);
            }

            _focus = Vector3.Lerp(_focus, Target.position + lead, 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime));
            Vector3 pos = DesiredPosition(_focus);
            if (_shake > 0f)
            {
                pos += Random.insideUnitSphere * _shake;
                _shake = Mathf.MoveTowards(_shake, 0f, Time.deltaTime * 1.5f);
            }
            transform.SetPositionAndRotation(pos, Quaternion.Euler(Pitch, Yaw, 0f));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}
