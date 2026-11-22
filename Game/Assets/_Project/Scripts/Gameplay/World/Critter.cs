using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Мелкая живность для атмосферы (без коллайдеров и геймплея):
    ///  * Bird   — птичка сидит на земле, подпрыгивает и клюёт; если герой подошёл — вспархивает и улетает,
    ///             через время возвращается на своё место, когда героя рядом нет;
    ///  * Moth   — мотылёк порхает вокруг точки (обычно у светящихся грибов/кристаллов);
    ///  * Beetle — жук неспешно ползает по земле в небольшом радиусе, останавливается, поворачивается.
    /// Крылья — дочерние объекты WingL/WingR модели (см. Tools/Blender/forest_details.py), машут вокруг оси «вперёд».
    /// </summary>
    public sealed class Critter : MonoBehaviour
    {
        public enum Kind { Bird, Moth, Beetle }

        public Kind Type = Kind.Bird;
        [Tooltip("Радиус, в котором бродит/летает (м)")]
        public float Radius = 1.2f;
        [Tooltip("Птица: на каком расстоянии от героя вспархивает (м)")]
        public float FleeDistance = 4f;

        private Transform _wingL, _wingR;
        private Quaternion _wingLBase, _wingRBase;
        private float _sideL = 1f, _sideR = -1f;
        private Vector3 _axisL = Vector3.forward, _axisR = Vector3.forward;
        private Vector3 _home;
        private Vector3 _target;
        private float _timer;
        private float _seed;
        private int _state; // Bird: 0 сидит, 1 летит прочь, 2 нет (ждёт); Beetle: 0 стоит, 1 ползёт
        private Vector3 _flyDir;
        private Renderer[] _renderers;

        private void Awake()
        {
            _home = transform.position;
            _seed = Random.value * 100f;
            _wingL = FindDeep(transform, "WingL");
            _wingR = FindDeep(transform, "WingR");
            // FBX из Blender приходит с поворотом осей, поэтому ось взмаха («вперёд» существа) и сторону крыла
            // считаем в мировых координатах и переводим в локальные родителя крыла
            if (_wingL != null) { _wingLBase = _wingL.localRotation; _sideL = WingSide(_wingL, 1f); _axisL = _wingL.parent.InverseTransformDirection(transform.forward); }
            if (_wingR != null) { _wingRBase = _wingR.localRotation; _sideR = WingSide(_wingR, -1f); _axisR = _wingR.parent.InverseTransformDirection(transform.forward); }
            _renderers = GetComponentsInChildren<Renderer>();
            _timer = Random.Range(0.5f, 3f);
            _target = _home;
        }

        private float WingSide(Transform wing, float fallback)
        {
            float d = Vector3.Dot(wing.position - transform.position, transform.right);
            return Mathf.Abs(d) < 1e-5f ? fallback : Mathf.Sign(d);
        }

        private static Transform FindDeep(Transform t, string name)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true))
                if (c.name == name) return c;
            return null;
        }

        /// <summary>Поднять/опустить крылья: angle &gt; 0 — вверх.</summary>
        private void Wings(float angle)
        {
            if (_wingL != null) _wingL.localRotation = Quaternion.AngleAxis(angle * _sideL, _axisL) * _wingLBase;
            if (_wingR != null) _wingR.localRotation = Quaternion.AngleAxis(angle * _sideR, _axisR) * _wingRBase;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            switch (Type)
            {
                case Kind.Bird: UpdateBird(dt); break;
                case Kind.Moth: UpdateMoth(); break;
                case Kind.Beetle: UpdateBeetle(dt); break;
            }
        }

        private float PlayerDistance()
        {
            var p = GameWorld.Player;
            if (p == null) return float.MaxValue;
            var d = p.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        // ------------------------------------------------------------------ птица

        private void UpdateBird(float dt)
        {
            if (_state == 0)
            {
                Wings(-75f); // сложены вдоль тела
                _timer -= dt;
                // клюёт: короткий наклон вперёд
                float peck = Mathf.Max(0f, Mathf.Sin((Time.time + _seed) * 9f)) * (Mathf.Sin((Time.time + _seed) * 0.7f) > 0.6f ? 1f : 0f);
                var yaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                transform.rotation = yaw * Quaternion.Euler(peck * 35f, 0f, 0f);
                if (_timer <= 0f)
                {
                    // прыжок на новое место
                    _target = _home + Random.insideUnitSphere.WithY(0f) * Radius;
                    _timer = Random.Range(1.2f, 4f);
                }
                var flat = _target - transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), 540f * dt);
                    float hop = Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 0.06f;
                    var p = Vector3.MoveTowards(transform.position.WithY(_home.y), _target, 0.9f * dt);
                    transform.position = p + Vector3.up * hop;
                }
                else transform.position = transform.position.WithY(_home.y);

                if (PlayerDistance() < FleeDistance)
                {
                    _state = 1;
                    _timer = 3.5f;
                    var away = transform.position - (GameWorld.Player != null ? GameWorld.Player.transform.position : transform.position - transform.forward);
                    away.y = 0f;
                    _flyDir = (away.normalized + Vector3.up * 0.55f + Random.insideUnitSphere * 0.25f).normalized;
                }
            }
            else if (_state == 1)
            {
                Wings(Mathf.Sin(Time.time * 55f) * 60f + 5f);
                transform.position += _flyDir * (6.5f * dt);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_flyDir.WithY(_flyDir.y * 0.3f)), 8f * dt);
                _timer -= dt;
                if (_timer <= 0f)
                {
                    _state = 2;
                    _timer = Random.Range(15f, 30f);
                    SetVisible(false);
                }
            }
            else
            {
                _timer -= dt;
                if (_timer <= 0f && Vector3.Distance(_home, GameWorld.Player != null ? GameWorld.Player.transform.position : Vector3.one * 1e4f) > 12f)
                {
                    transform.position = _home;
                    _state = 0;
                    _timer = 1f;
                    SetVisible(true);
                }
            }
        }

        private void SetVisible(bool v)
        {
            foreach (var r in _renderers)
                if (r != null) r.enabled = v;
        }

        // ------------------------------------------------------------------ мотылёк

        private void UpdateMoth()
        {
            float t = Time.time * 0.6f + _seed;
            var offset = new Vector3(
                Mathf.Sin(t * 1.3f) + 0.5f * Mathf.Sin(t * 3.1f),
                0f,
                Mathf.Cos(t * 1.1f) + 0.5f * Mathf.Cos(t * 2.7f)) * (Radius * 0.6f);
            offset.y = 0.9f + 0.45f * Mathf.Sin(t * 1.7f) + 0.15f * Mathf.Sin(t * 7.3f);
            var next = _home + offset;
            var vel = next - transform.position;
            if (vel.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(vel.normalized.WithY(0f) + Vector3.up * 0.01f), 0.2f);
            transform.position = next;
            Wings(Mathf.Sin(Time.time * 38f + _seed) * 55f + 10f);
        }

        // ------------------------------------------------------------------ жук

        private void UpdateBeetle(float dt)
        {
            _timer -= dt;
            if (_state == 0)
            {
                if (_timer <= 0f)
                {
                    _target = _home + Random.insideUnitSphere.WithY(0f) * Radius;
                    _state = 1;
                    _timer = 8f;
                }
                return;
            }
            var to = _target - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.03f || _timer <= 0f)
            {
                _state = 0;
                _timer = Random.Range(1.5f, 5f);
                return;
            }
            var want = Quaternion.LookRotation(to);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 120f * dt);
            if (Quaternion.Angle(transform.rotation, want) < 30f)
            {
                transform.position += transform.forward * (0.18f * dt);
                // лёгкое покачивание при шаге
                transform.position = transform.position.WithY(_home.y + Mathf.Abs(Mathf.Sin(Time.time * 22f)) * 0.003f);
            }
        }
    }

    internal static class CritterVectorExt
    {
        public static Vector3 WithY(this Vector3 v, float y) => new(v.x, y, v.z);
    }
}
