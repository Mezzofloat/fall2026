using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Breathe
{
    /// <summary>
    /// Self-playtest harness (only when the environment variable BREATHE_AUTOPLAY is set). It plays the real build: walks the tour along real paths, takes
    /// real window screenshots, answers pages and writes a log, so layout and flow bugs can be seen without a human at the keyboard.
    /// </summary>
    public sealed class AutoPlay : MonoBehaviour
    {
        BreatheGame game; string folder; StreamWriter log; int shot;
        void Log(string line) { log.WriteLine(Time.realtimeSinceStartup.ToString("0.0") + "  " + line); log.Flush(); }
        IEnumerator Shot(string label) { yield return new WaitForEndOfFrame(); string path = Path.Combine(folder, (++shot).ToString("00") + "-" + label + ".png"); ScreenCapture.CaptureScreenshot(path); yield return null; yield return null; Log("shot " + path + "  act=" + game.Run.Act + " horror=" + game.VisualHorror + " pos=" + game.PlayerPosition); }

        IEnumerator Start()
        {
            game = GetComponent<BreatheGame>();
            folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "Codex", "breathe-runtime", "autoplay"); Directory.CreateDirectory(folder);
            foreach (var f in Directory.GetFiles(folder)) File.Delete(f);
            log = new StreamWriter(Path.Combine(folder, "autoplay.log"));
            Log("autoplay start, screen " + Screen.width + "x" + Screen.height);
            yield return new WaitForSeconds(5); yield return Shot("lobby");
            game.Run.StartSelection(); yield return new WaitForSeconds(3); yield return Shot("folders");
            game.Run.StartTour(); yield return new WaitForSeconds(2); yield return Shot("tour-start");
            // tour: really walk to each colleague
            for (int stop = 0; stop < 4; stop++)
            {
                yield return Walk(game.Map.Tour[stop], "tour" + stop);
                game.DebugInteract(); yield return new WaitForSeconds(.6f);
            }
            yield return new WaitForSeconds(3); yield return Shot("interview-name");
            Log("tour done, act=" + game.Run.Act);
            game.Run.Introduce(); yield return new WaitForSeconds(5); yield return Shot("transformation");
            while (game.Run.Act == Act.Transformation) yield return null;
            yield return new WaitForSeconds(3); yield return Shot("unreadable");
            game.Run.FinishUnreadable(); yield return Settle(); yield return Shot("challenge-intro");
            game.Run.StartQuestion(); yield return Settle(); yield return Shot("chase-start");
            // chase question: Vale far away; collect and answer every page, anywhere
            game.DebugPlaceHunter(game.Map.Exit, 99999f);
            for (int page = 0; page < game.Run.Required; page++)
            {
                yield return Walk(game.Map.Challenge[page], "page" + page);
                game.DebugInteract(); yield return new WaitForSeconds(1.2f); yield return Shot("page" + page + "-taken");
                Log("page " + page + " taken; question: " + game.Run.CurrentPageQuestion + "  prompt: " + game.HudPrompt.Replace("\n", " / "));
                yield return Walk(game.Map.Spawn, "walk-away-" + page, 5f);     // answer from a different place
                game.DebugSpeech(new SpeechReply { intent = "answer", reply = "Acceptable.", tone = "sarcastic", verdict = "good", anger = .3f }); yield return new WaitForSeconds(1.0f);
                Log("answered=" + game.Run.Answered + " of " + game.Run.Required);
            }
            yield return Walk(game.Map.Exit, "exit"); game.DebugInteract(); yield return Settle(); yield return Shot("after-q3");
            Log("RESULT act=" + game.Run.Act + " (expected TeamIntro) retries=" + game.Run.Catches);
            Log("autoplay done"); log.Close();
            Application.Quit();
        }

        IEnumerator Settle() { float end = Time.realtimeSinceStartup + 10; yield return null; while (game.TransitionBusy && Time.realtimeSinceStartup < end) yield return null; yield return new WaitForSeconds(.5f); }

        IEnumerator Walk(Vector2 target, string label, float maxSeconds = 60f)
        {
            var path = game.Map.FindPath(game.PlayerPosition, target); float t = 0, nextShot = 2.5f; int idx = 0;
            while (idx < path.Count && t < maxSeconds)
            {
                var pos = game.PlayerPosition; var goal = path[idx]; var delta = goal - pos;
                if (delta.magnitude < .25f) { idx++; continue; }
                float want = Mathf.Atan2(delta.x, -delta.y) * Mathf.Rad2Deg; game.DebugSetView(Mathf.LerpAngle(game.Yaw, want, .2f), 2f);
                game.DebugSetPlayer(pos + delta.normalized * 3.2f * Time.deltaTime);
                t += Time.deltaTime; nextShot -= Time.deltaTime;
                if (nextShot <= 0) { nextShot = 3.2f; yield return Shot(label); }
                yield return null;
            }
            Log("walked to " + target + " arrived=" + (Vector2.Distance(game.PlayerPosition, target) < 1.2f) + " in " + t.ToString("0.0") + "s");
        }
    }
}
