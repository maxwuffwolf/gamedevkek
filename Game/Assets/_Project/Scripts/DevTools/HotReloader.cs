using System;
using System.IO;
using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// Горячая перезагрузка JSON: F5 вручную или автоматически при сохранении файла в StreamingAssets/Data.
    /// </summary>
    public sealed class HotReloader : MonoBehaviour
    {
        public static bool AutoReload = true;

        private FileSystemWatcher _watcher;
        private volatile bool _changed;
        private DateTime _lastChange;

        private void Start()
        {
            if (!Services.TryGet<ContentDatabase>(out var db) || string.IsNullOrEmpty(db.RootPath) || !Directory.Exists(db.RootPath)) return;
            try
            {
                _watcher = new FileSystemWatcher(db.RootPath, "*.json")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                };
                _watcher.Changed += OnFsEvent;
                _watcher.Created += OnFsEvent;
                _watcher.Renamed += OnFsEvent;
                _watcher.Deleted += OnFsEvent;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception e)
            {
                Log.Warn("HotReload", $"Слежение за файлами недоступно ({e.Message}). Используй F5.");
            }
        }

        // вызывается из фонового потока
        private void OnFsEvent(object sender, FileSystemEventArgs e)
        {
            _lastChange = DateTime.UtcNow;
            _changed = true;
        }

        private void Update()
        {
            if (GameInput.Down(GameKey.F5) && !GameInput.TextInputActive) Reload("F5");
            if (_changed && AutoReload && (DateTime.UtcNow - _lastChange).TotalMilliseconds > 250)
            {
                _changed = false;
                Reload("файл изменён");
            }
        }

        public static void Reload(string reason)
        {
            if (!Services.TryGet<ContentDatabase>(out var db)) return;
            Services.Get<VisualRegistry>().Reload();
            db.Reload();
            var color = db.Errors.Count == 0 ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
            EventBus.Publish(new UiMessageEvent(db.Errors.Count == 0
                ? $"Данные перезагружены ({reason})"
                : $"Ошибок в данных: {db.Errors.Count} — подробности в консоли (~)", color));
        }

        private void OnDestroy()
        {
            if (_watcher == null) return;
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => AutoReload = true;
    }
}
