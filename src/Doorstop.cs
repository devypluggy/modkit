using System;
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
        private static bool _sceneHooked;
        private static readonly object LogLock = new object();

        public static void Start()
        {
            try
            {
                _logPath = Path.Combine(Directory.GetCurrentDirectory(), "doorstop_loader.log");
                Log("Doorstop.Start, cwd=" + Directory.GetCurrentDirectory());

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
            try
            {
                if (_logPath == null)
                    _logPath = Path.Combine(Directory.GetCurrentDirectory(), "doorstop_loader.log");
                lock (LogLock)
                {
                    File.AppendAllText(_logPath, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\r\n");
                }
            }
            catch
            {
            }
        }
    }

    public class ModHost : MonoBehaviour
    {
        private bool _loaded;

        private void Update()
        {
            if (_loaded) return;
            _loaded = true;
            LoadMods();
        }

        private void LoadMods()
        {
            try
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "DllMods");
                Entrypoint.Log("LoadMods: " + dir + " exists=" + Directory.Exists(dir));
                if (!Directory.Exists(dir)) return;

                string[] files = Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase); // стабильный порядок загрузки
                Entrypoint.Log("found mods: " + files.Length);

                for (int i = 0; i < files.Length; i++) LoadOne(files[i]);
            }
            catch (Exception e)
            {
                // Раньше исключение здесь оставляло _loaded = true и моды
                // больше не пытались загрузиться никогда.
                Entrypoint.Log("LoadMods failed: " + e);
            }
        }

        private static void LoadOne(string file)
        {
            try
            {
                Assembly asm = Assembly.LoadFrom(file);
                Entrypoint.Log("loaded assembly: " + asm.GetName().Name);

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
                    Entrypoint.Log("partial type load in " + asm.GetName().Name + ": " + ex.Message);
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
