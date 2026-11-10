using UnityEngine;

namespace Zombosland
{
    // Hit feel: hit-stop (a very short time dip) and the melee camera kick. All tuning lives here.
    public partial class Sandbox
    {
        // Hit-stop: duration is counted in unscaled seconds; the time scale dips to HitStopTimeScale (not a full stop).
        const float HitStopMeleeSeconds = 0.05f, HitStopHeadKillSeconds = 0.03f, HitStopTimeScale = 0.05f;
        // Melee camera kick on a zombie hit (pistol shot is 0.12, taking damage 0.55).
        const float MeleeShakeBat = 0.22f, MeleeShakeAxe = 0.30f, MeleeKillShakeBonus = 0.12f;
        const float HeadCrackVolume = 0.6f;

        // Any --qa* / --perf* argument turns hit-stop off, so scripted timing never changes.
        static readonly bool qaOrPerfRun = DetectQaOrPerfRun();

        bool hitStopActive;
        float hitStopEnd;

        static bool DetectQaOrPerfRun()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i].StartsWith("--qa", System.StringComparison.Ordinal) || args[i].StartsWith("--perf", System.StringComparison.Ordinal)) return true;
            return false;
        }

        // Longest remaining time wins; never starts while paused / dead / in perf or QA runs.
        void StartHitStop(float seconds)
        {
            if (qaOrPerfRun || PerfMode || paused || dead || seconds <= 0f) return;
            float end = Time.unscaledTime + seconds;
            if (hitStopActive)
            {
                if (end > hitStopEnd) hitStopEnd = end;
                return;
            }
            hitStopActive = true;
            hitStopEnd = end;
            Time.timeScale = HitStopTimeScale;
        }

        void TickHitStop()
        {
            if (!hitStopActive) return;
            if (paused || dead || Time.unscaledTime >= hitStopEnd) EndHitStop();
        }

        // The pause menu owns timeScale while paused: only restore it when the game is running.
        void EndHitStop()
        {
            hitStopActive = false;
            if (!paused) Time.timeScale = 1f;
        }

        void OnDestroy()
        {
            if (hitStopActive && !paused) Time.timeScale = 1f;
            hitStopActive = false;
        }

        // Melee hit on a living zombie: camera kick (axe > bat, kill adds more) and hit-stop.
        void MeleeHitFeedback(bool killed)
        {
            AddCameraShake((Carrier == HandCarrier.Axe ? MeleeShakeAxe : MeleeShakeBat) + (killed ? MeleeKillShakeBonus : 0f));
            StartHitStop(HitStopMeleeSeconds);
        }

        // Head hit: its own crack replaces the plain hit tick so the two never stack.
        static void PlayHeadCrack(SfxBank sfx)
        {
            sfx.Play(Sfx.HeadCrack, Vector3.zero, HeadCrackVolume, Random.Range(0.95f, 1.08f), false);
        }
    }
}
