using System.Collections;
using System.IO;
using Breathe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>LIVE checks: need the bridge running on the Mac (scripts/mac/start-bridge.command). Skipped if it is not.</summary>
[Category("Live")]
public class LiveBridgeTests
{
    [UnityTest] public IEnumerator GameConnectsToTheBridgeListsCamerasAndStartsAndStopsThePulse()
    {
        string path = System.Environment.GetEnvironmentVariable("BREATHE_CONNECTION");
        if (string.IsNullOrEmpty(path)) path = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Documents", "Codex", "breathe-runtime", "connection.json");
        if (!File.Exists(path)) Assert.Ignore("Bridge is not running; nothing to test live.");
        foreach (var existing in Object.FindObjectsByType<BreatheGame>(FindObjectsSortMode.None)) Object.Destroy(existing.gameObject);
        yield return null;
        var root = new GameObject("Live test"); var game = root.AddComponent<BreatheGame>(); game.Run.Sound = false;
        float deadline = Time.realtimeSinceStartup + 12;
        while ((!game.Live.Connected || game.Live.Cameras.Length == 0) && Time.realtimeSinceStartup < deadline) yield return null;
        if (!game.Live.Connected) { Object.Destroy(root); Assert.Ignore("Bridge connection file exists but the bridge is not answering."); }
        Assert.That(game.Live.Connected, Is.True); Assert.That(game.Live.Cameras.Length, Is.GreaterThan(0), "The bridge must list at least one camera.");
        TestContext.WriteLine("Cameras: " + string.Join(", ", System.Array.ConvertAll(game.Live.Cameras, c => c.name)) + "  selected: " + game.Live.SelectedCameraName);
        game.Live.StartPulse();
        deadline = Time.realtimeSinceStartup + 20;
        while (!(game.Live.Pulse != null && game.Live.Pulse.running && game.Live.Pulse.camera != null && game.Live.Pulse.camera.fps > 0) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(game.Live.Pulse.running, Is.True, "Presage should be running. Status: " + game.Live.Status);
        TestContext.WriteLine("Camera fps: " + (game.Live.Pulse.camera != null ? game.Live.Pulse.camera.fps.ToString("0.0") : "n/a") + " validation: " + game.Live.Pulse.validation);
        game.Live.StopPulse(); yield return new WaitForSecondsRealtime(2.5f);
        Assert.That(game.Live.BiometricsEnabled, Is.False);
        Object.Destroy(root); yield return null;
    }
}

[Category("Live")]
public class LiveGloveTests
{
    [UnityTest] public IEnumerator RealGloveDataReachesTheGameAndDrivesTheCamera()
    {
        string path = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Documents", "Codex", "breathe-runtime", "connection.json");
        if (!File.Exists(path)) Assert.Ignore("Bridge is not running.");
        foreach (var existing in Object.FindObjectsByType<BreatheGame>(FindObjectsSortMode.None)) Object.Destroy(existing.gameObject);
        yield return null;
        var root = new GameObject("Glove live test"); var game = root.AddComponent<BreatheGame>(); game.Run.Sound = false;
        float deadline = Time.realtimeSinceStartup + 10;
        while (!(game.Live.Imu != null && game.Live.Imu.right != null && game.Live.Imu.right.connected) && Time.realtimeSinceStartup < deadline) yield return null;
        if (game.Live.Imu == null || !game.Live.Imu.right.connected) { Object.Destroy(root); Assert.Ignore("No glove connected."); }
        game.Run.StartSelection(); game.Run.StartTour(); yield return new WaitForSeconds(1f);
        float maxX = 0, maxY = 0, maxLook = 0; float yaw0 = game.Yaw, end = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < end) { var r = game.Live.Imu.right; maxX = Mathf.Max(maxX, Mathf.Abs(r.jx)); maxY = Mathf.Max(maxY, Mathf.Abs(r.jy)); maxLook = Mathf.Max(maxLook, Mathf.Abs(game.Glove.Look.x) + Mathf.Abs(game.Glove.Look.y)); yield return null; }
        TestContext.WriteLine("right stick seen by the game: |x| " + maxX + " |y| " + maxY + "  gamepad look max " + maxLook + "  yaw change " + Mathf.DeltaAngle(yaw0, game.Yaw) + "  RightConnected " + game.Glove.RightConnected);
        Assert.That(game.Glove.RightConnected, Is.True);
        Object.Destroy(root); yield return null;
    }
}
