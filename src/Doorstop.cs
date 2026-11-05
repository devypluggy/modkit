using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Doorstop
{
    // Точка входа для UnityDoorstop: static void Doorstop.Entrypoint.Start()
    public static class Entrypoint
    {
        private static string _logPath;
        private static string _fallbackLogPath;
        private static bool _usingFallback;
        private static bool _sceneHooked;
        private static readonly object LogLock = new object();

        // Папка игры = папка, из которой загружен Doorstop.dll.
        // Directory.GetCurrentDirectory() здесь ненадёжен: Steam выставляет её,
        // а ярлык/проводник/другой лаунчер — нет.
        public static string BaseDirectory
        {
            get
            {
                try
                {
                    string location = Assembly.GetExecutingAssembly().Location;
                    if (!string.IsNullOrEmpty(location))
                        return Path.GetDirectoryName(location);
                }
                catch
                {
                }

                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        public static string ModDirectory
        {
            get { return Path.Combine(BaseDirectory, "DllMods"); }
        }

        public static void Start()
        {
            try
            {
                _logPath = Path.Combine(BaseDirectory, "doorstop_loader.log");
                RotateLog();
                Log("Doorstop.Start, base=" + BaseDirectory);

                // Страховка: если SceneManager ещё не готов, подпишемся позже,
                // когда Unity загрузит Assembly-CSharp (это происходит на главном потоке).
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

                TryHookSceneManager();
            }
            catch (Exception e)
            {
                Log("Start failed: " + e);
            }
        }

        private static void RotateLog()
        {
            try
            {
                FileInfo fi = new FileInfo(_logPath);
                if (fi.Exists && fi.Length > 2 * 1024 * 1024)
                {
                    string old = _logPath + ".old";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(_logPath, old);
                }
            }
            catch
            {
                // Ротация — не критично, продолжаем писать в текущий файл.
            }
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            try
            {
                // Раньше здесь ждали именно Assembly-CSharp. Если Unity успевал
                // загрузить его до подписки, лоадер больше никогда не пытался
                // подключиться к SceneManager. Теперь пробуем на любой сборке —
                // повтор дешёвый, _sceneHooked отсекает лишнее.
                if (!_sceneHooked) TryHookSceneManager();
            }
            catch (Exception e)
            {
                Log("OnAssemblyLoad failed: " + e);
            }
        }

        private static void TryHookSceneManager()
        {
            if (_sceneHooked) return;
            try
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                _sceneHooked = true;
                Log("subscribed to SceneManager.sceneLoaded");
            }
            catch (Exception e)
            {
                Log("SceneManager not ready yet: " + e.Message);
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                Log("scene loaded: '" + scene.name + "' mode=" + mode);
                if (UnityEngine.Object.FindObjectOfType<ModHost>() == null)
                {
                    GameObject go = new GameObject("DoorstopModHost");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    go.AddComponent<ModHost>();
                    Log("ModHost created");
                }
            }
            catch (Exception e)
            {
                Log("OnSceneLoaded failed: " + e);
            }
        }

        public static void Log(string msg)
        {
            string line = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + msg + "\r\n";

            lock (LogLock)
            {
                if (_logPath == null)
                    _logPath = Path.Combine(BaseDirectory, "doorstop_loader.log");

                if (!_usingFallback && TryAppend(_logPath, line)) return;

                // Program Files защищена от записи для обычного пользователя:
                // пишем в %LOCALAPPDATA%\TheForestModLoader вместо молчаливой потери лога.
                if (_fallbackLogPath == null)
                {
                    string dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "TheForestModLoader");
                    try
                    {
                        Directory.CreateDirectory(dir);
                    }
                    catch
                    {
                    }
                    _fallbackLogPath = Path.Combine(dir, "doorstop_loader.log");
                }

                if (TryAppend(_fallbackLogPath, line))
                    _usingFallback = true;
            }
        }

        private static bool TryAppend(string path, string text)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                File.AppendAllText(path, text);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class ModHost : MonoBehaviour
    {
        private bool _loaded;
        private bool _announcedMissing;
        private float _nextCheck;
        private readonly Dictionary<string, bool> _initialized = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private void Update()
        {
            if (_loaded) return;
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 1f;

            // _loaded ставится только после успешной загрузки. Если DllMods
            // появилась позже (или была пустой на первом кадре) — повторим.
            _loaded = LoadMods();
        }

        private bool LoadMods()
        {
            try
            {
                string dir = Entrypoint.ModDirectory;
                if (!Directory.Exists(dir))
                {
                    if (!_announcedMissing)
                    {
                        Entrypoint.Log("LoadMods: " + dir + " not found yet, will retry");
                        _announcedMissing = true;
                    }
                    return false;
                }

                string[] files = Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase); // стабильный порядок загрузки

                // Один и тот же мод может лежать в DllMods и DllMods\old —
                // берём только первое вхождение, иначе Init вызовется дважды.
                List<string> unique = new List<string>();
                Dictionary<string, bool> fileNames = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < files.Length; i++)
                {
                    string name = Path.GetFileName(files[i]);
                    if (fileNames.ContainsKey(name))
                    {
                        Entrypoint.Log("skipping duplicate mod: " + files[i]);
                        continue;
                    }
                    fileNames[name] = true;
                    unique.Add(files[i]);
                }

                if (unique.Count == 0)
                {
                    if (!_announcedMissing)
                    {
                        Entrypoint.Log("LoadMods: " + dir + " is empty, will retry");
                        _announcedMissing = true;
                    }
                    return false;
                }

                _announcedMissing = false;
                Entrypoint.Log("found mods: " + unique.Count);
                for (int i = 0; i < unique.Count; i++) LoadOne(unique[i]);
                return true;
            }
            catch (Exception e)
            {
                Entrypoint.Log("LoadMods failed (will retry): " + e);
                return false;
            }
        }

        private void LoadOne(string file)
        {
            try
            {
                Assembly asm = Assembly.LoadFrom(file);
                string asmName = asm.GetName().Name;
                if (_initialized.ContainsKey(asmName))
                {
                    Entrypoint.Log("already initialized: " + asmName);
                    return;
                }
                _initialized[asmName] = true;

                Entrypoint.Log("loaded assembly: " + asmName);

                Type[] types;
                try
                {
                    types = asm.GetExportedTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Часть типов может не загрузиться из-за отсутствующей
                    // зависимости — работаем с теми, что уцелели.
                    types = ex.Types;
                    Entrypoint.Log("partial type load in " + asmName + ": " + ex.Message);
                }

                for (int i = 0; i < types.Length; i++)
                {
                    Type t = types[i];
                    if (t == null) continue;

                    try
                    {
                        MethodInfo m = t.GetMethod("Init", BindingFlags.Static | BindingFlags.Public);
                        if (m != null && m.GetParameters().Length == 0)
                        {
                            m.Invoke(null, null);
                            Entrypoint.Log("Init called: " + t.FullName);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Один сломанный мод не должен ронять остальные.
                        Entrypoint.Log("Init failed in " + t.FullName + ": " + ex);
                    }
                }
            }
            catch (Exception e)
            {
                Entrypoint.Log("mod load error '" + file + "': " + e);
            }
        }
    }
}
