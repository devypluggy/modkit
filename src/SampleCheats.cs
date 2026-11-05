using System;
using System.IO;
using System.Reflection;
using TheForest.Utils;
using UnityEngine;

// Пример мода для своего загрузчика: статический Init() вызывается автоматически.
public static class SampleCheats
{
    private static readonly object LogLock = new object();
    private static MethodInfo _loaderLog;
    private static bool _loaderLogResolved;

    public static void Init()
    {
        GameObject go = new GameObject("SampleCheatsHost");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<SampleHost>();
        Log("SampleCheats.Init done");
    }

    public static void Log(string msg)
    {
        string line = "[SampleCheats] " + msg;

        // Если лоадер рядом — пишем в его лог, чтобы у мода и загрузчика
        // был один файл и один путь (включая fallback в %LOCALAPPDATA%).
        if (TryLoaderLog(line)) return;

        try
        {
            lock (LogLock)
            {
                string text = Stamp(line);
                string dir = FindGameDir();
                if (dir != null)
                {
                    try
                    {
                        File.AppendAllText(Path.Combine(dir, "doorstop_loader.log"), text);
                        return;
                    }
                    catch
                    {
                    }
                }

                string alt = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TheForestModLoader");
                Directory.CreateDirectory(alt);
                File.AppendAllText(Path.Combine(alt, "doorstop_loader.log"), text);
            }
        }
        catch
        {
        }
    }

    private static bool TryLoaderLog(string msg)
    {
        try
        {
            if (!_loaderLogResolved)
            {
                _loaderLogResolved = true;

                Type t = Type.GetType("Doorstop.Entrypoint, Doorstop");
                if (t == null)
                {
                    Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
                    for (int i = 0; i < asms.Length; i++)
                    {
                        if (string.Equals(asms[i].GetName().Name, "Doorstop", StringComparison.OrdinalIgnoreCase))
                        {
                            t = asms[i].GetType("Doorstop.Entrypoint");
                            break;
                        }
                    }
                }

                if (t != null)
                {
                    _loaderLog = t.GetMethod("Log", BindingFlags.Public | BindingFlags.Static,
                        null, new Type[] { typeof(string) }, null);
                }
            }

            if (_loaderLog != null)
            {
                _loaderLog.Invoke(null, new object[] { msg });
                return true;
            }
        }
        catch
        {
            _loaderLog = null;
        }

        return false;
    }

    private static string Stamp(string line)
    {
        return "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + line + "\r\n";
    }

    private static string FindGameDir()
    {
        try
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            for (int i = 0; i < 5 && !string.IsNullOrEmpty(dir); i++)
            {
                if (File.Exists(Path.Combine(dir, "TheForest.exe")) ||
                    File.Exists(Path.Combine(dir, "TheForest32.exe")) ||
                    File.Exists(Path.Combine(dir, "TheForestVR.exe")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
        }
        catch
        {
        }

        return null;
    }
}

public class SampleHost : MonoBehaviour
{
    private bool _fly;

    private void Update()
    {
        bool blocked = InputBlocked();

        if (!blocked)
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F8)) Heal();
            if (UnityEngine.Input.GetKeyDown(KeyCode.F9)) ToggleGod();
            if (UnityEngine.Input.GetKeyDown(KeyCode.F10)) ToggleFly();

            if (_fly) Fly();
        }
    }

    // Пока открыта консоль (F1), чат, инвентарь, книга или меню — горячие
    // клавиши не должны срабатывать. Консоль выставляет InputState.Chat.
    private static bool InputBlocked()
    {
        try
        {
            if (TheForest.Utils.Input.GetState(InputState.Chat)) return true;
            if (TheForest.Utils.Input.GetState(InputState.Menu)) return true;
            if (TheForest.Utils.Input.GetState(InputState.Inventory)) return true;
            if (TheForest.Utils.Input.GetState(InputState.Book)) return true;
            if (TheForest.Utils.Input.GetState(InputState.RadialWorld)) return true;
            if (TheForest.Utils.Input.GetState(InputState.Locked)) return true;
            if (TheForest.Utils.Input.GetState(InputState.SavingMaps)) return true;
        }
        catch
        {
        }

        return false;
    }

    private void Heal()
    {
        try
        {
            // Health/Energy/Stamina — шкалы 0..100, а Fullness/Thirst — 0..1.
            // Если поставить Fullness = 100, игра клампит его до 1.0 в следующем
            // кадре; пишем правильное значение сразу.
            LocalPlayer.Stats.Health = 100f;
            LocalPlayer.Stats.HealthTarget = 100f; // иначе HUD-полоска и реген тянутся к старому значению
            LocalPlayer.Stats.Energy = 100f;
            LocalPlayer.Stats.Stamina = 100f;
            LocalPlayer.Stats.Fullness = 1f;
            LocalPlayer.Stats.Thirst = 0f;
            LocalPlayer.Stats.Starvation = 0f;
            SampleCheats.Log("healed");
        }
        catch (Exception e)
        {
            SampleCheats.Log("heal error: " + e.Message);
        }
    }

    private void ToggleGod()
    {
        // Читаем фактическое состояние игры, а не свою копию: godmode могли
        // включить консолью, тогда локальный флаг врал бы.
        bool enabled = !Cheats.GodMode;
        Cheats.GodMode = enabled;
        SampleCheats.Log("godmode = " + enabled);
    }

    private void ToggleFly()
    {
        _fly = !_fly;
        SampleCheats.Log("fly = " + _fly);
    }

    private void Fly()
    {
        try
        {
            if (LocalPlayer.Transform == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 dir = Vector3.zero;
            if (UnityEngine.Input.GetKey(KeyCode.W)) dir += cam.transform.forward;
            if (UnityEngine.Input.GetKey(KeyCode.S)) dir -= cam.transform.forward;
            if (UnityEngine.Input.GetKey(KeyCode.D)) dir += cam.transform.right;
            if (UnityEngine.Input.GetKey(KeyCode.A)) dir -= cam.transform.right;
            if (UnityEngine.Input.GetKey(KeyCode.Space)) dir += Vector3.up;
            if (UnityEngine.Input.GetKey(KeyCode.LeftControl)) dir += Vector3.down;

            if (dir != Vector3.zero)
            {
                LocalPlayer.Transform.position += dir.normalized * 20f * Time.deltaTime;
            }
        }
        catch
        {
        }
    }
}
