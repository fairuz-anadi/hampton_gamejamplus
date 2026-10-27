using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Renders the sky through the day and the story parts from the bridge, for tuning Port/Sky. Owner: Samprity.
///   Unity.exe -batchmode -projectPath . -executeMethod SkyShots.RenderBatch       (exits itself)
/// Writes Logs/Sky_*.png: looking ahead over the bow, and towards the sun.
/// </summary>
public static class SkyShots
{
    public static void RenderBatch()
    {
        EditorSceneManager.OpenScene(ShipDeckBuilder.ScenePath, OpenSceneMode.Single);
        var mood = Object.FindAnyObjectByType<PartMood>();
        var cam = Camera.main;
        cam.GetComponent<AutoFollowCamera>().enabled = false;
        var rt = new RenderTexture(960, 540, 24);
        cam.targetTexture = rt;
        cam.fieldOfView = 60f;
        cam.Render(); // the first headless render is garbage

        var shots = new List<(string name, int part, string time, bool atSun)>
        {
            ("1_dawn_sun", 1, "Dawn", true), ("2_morning", 1, "Morning", false), ("3_noon", 1, "Noon", false),
            ("4_afternoon_off", 2, "Afternoon", false), ("5_dusk_sun", 1, "Dusk", true), ("6_night", 1, "Night", false),
            ("7_night_truth", 3, "Night", false), ("8_noon_sun", 1, "Noon", true),
        };
        foreach (var (name, part, time, atSun) in shots)
        {
            float phase = mood.cycle.PhaseOf(time);
            mood.Preview(part, phase);
            var tod = mood.cycle.Sample(phase);
            float yaw = atSun ? tod.sunYaw + 180f : 0f; // the light points away from the sun
            cam.transform.SetPositionAndRotation(new Vector3(0f, 3.2f, 31f), Quaternion.Euler(-12f, yaw, 0f));
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes($"Logs/Sky_{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[SkyShots] {name}: sun elevation {tod.sunElevation:0.0}, yaw {tod.sunYaw:0}");
        }
        cam.targetTexture = null;
        EditorApplication.Exit(0);
    }
}
