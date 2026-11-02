using System;
using System.IO;
using TheForest.Utils;
using UnityEngine;

// РџСЂРёРјРµСЂ РјРѕРґР° РґР»СЏ СЃРІРѕРµРіРѕ Р·Р°РіСЂСѓР·С‡РёРєР°: СЃС‚Р°С‚РёС‡РµСЃРєРёР№ Init() РІС‹Р·С‹РІР°РµС‚СЃСЏ Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРё.
public static class SampleCheats
{
    private static readonly object LogLock = new object();

    public static void Init()
    {
        GameObject go = new GameObject("SampleCheatsHost");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<SampleHost>();
        Log("SampleCheats.Init done");
    }

    public static void Log(string msg)
    {
        try
        {
            lock (LogLock)
            {
                File.AppendAllText(Path.Combine(Directory.GetCurrentDirectory(), "doorstop_loader.log"),
                    "[" + DateTime.Now.ToString("HH:mm:ss") + "] [SampleCheats] " + msg + "\r\n");
            }
        }
        catch
        {
        }
    }
}

public class SampleHost : MonoBehaviour
{
    private bool _god;
    private bool _fly;

    private void Update()
    {
        if (UnityEngine.Input.GetKeyDown(KeyCode.F8)) Heal();
        if (UnityEngine.Input.GetKeyDown(KeyCode.F9)) ToggleGod();
        if (UnityEngine.Input.GetKeyDown(KeyCode.F10)) ToggleFly();

        if (_fly) Fly();
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
        _god = !_god;
        Cheats.GodMode = _god;
        SampleCheats.Log("godmode = " + _god);
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

