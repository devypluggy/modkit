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
                if (args.LoadedAssembly.GetName().Name == "Assembly-CSharp" && !_sceneHooked)
                {
                    Log("Assembly-CSharp loaded, hooking SceneManager");
                    TryHookSceneManager();
                }
            }
            catch (Exception e)
            {
                Log("OnAssemblyLoad failed: " + e);
            }
        }

        private static void TryHookSceneManager()
        {
            if (_sceneHooked) return;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneHooked = true;
            Log("subscribed to SceneManager.sceneLoaded");
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
                File.AppendAllText(_logPath, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\r\n");
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
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "DllMods");
            Entrypoint.Log("LoadMods: " + dir + " exists=" + Directory.Exists(dir));
            if (!Directory.Exists(dir)) return;

            string[] files = Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories);
            Entrypoint.Log("found mods: " + files.Length);
            foreach (string file in files)
            {
                try
                {
                    Assembly asm = Assembly.LoadFrom(file);
                    Entrypoint.Log("loaded assembly: " + asm.GetName().Name);
                    Type[] types = asm.GetExportedTypes();
                    foreach (Type t in types)
                    {
                        MethodInfo m = t.GetMethod("Init", BindingFlags.Static | BindingFlags.Public);
                        if (m != null && m.GetParameters().Length == 0)
                        {
                            m.Invoke(null, null);
                            Entrypoint.Log("Init called: " + t.FullName);
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
}
