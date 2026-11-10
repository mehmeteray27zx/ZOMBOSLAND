using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace Zombosland
{
    // --qa-screens: scripted real-time run that saves 10 screenshots (QaScreens/ next to the exe), logs QA_SCREENS lines, then quits.
    // The shots go through the normal Fire() / BeginReload() paths; only the magazine content and zombie placement are forced.
    public class QaScreens : MonoBehaviour
    {
        Sandbox player;
        string dir;
        float t0;

        IEnumerator Start()
        {
            Application.runInBackground = true;
            Sandbox.InstantHands = true; // hand actions apply at once in scripted runs
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            player = GetComponent<Sandbox>();
            for (int i = 0; i < 300 && (player.Motor == null || ZombieDirector.Instance == null || player.AimPivot == null); i++) yield return null;
            yield return new WaitForSeconds(0.5f);
            ZombieBrain[] brains = FindObjectsByType<ZombieBrain>();
            if (brains.Length < 2) { Debug.LogError("QA_SCREENS FAIL zombies missing"); GameQuit.Request(); yield break; }
            ViewmodelPresenter presenter = GetComponentInChildren<ViewmodelPresenter>();
            dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), presenter != null && presenter.ArmsSet == "lpsp" ? "QaScreens_lpsp" : "QaScreens");
            Directory.CreateDirectory(dir);

            player.QaSetFreeze(true);
            player.Motor.InputEnabled = false;
            player.Motor.QaSetPitch(5.5f); // aim at the chest from 4 m
            Vector3 origin = player.transform.position;
            Vector3 forward = player.transform.forward;
            ZombieBrain target = brains[0];
            Place(target, origin + forward * 4f, origin);
            for (int i = 1; i < brains.Length; i++) Place(brains[i], origin - forward * (5f + i) + player.transform.right * ((i % 2 == 0) ? 2f : -2f), origin);
            ZombieBrain walker = brains[1]; // untouched zombie for the walk / heavy-hit shots
            Debug.Log("QA_SCREENS arms=" + (presenter != null ? presenter.ArmsSet : "none") + " dir=" + dir);
            Debug.Log("QA_SCREENS start screen=" + Screen.width + "x" + Screen.height + " animated=" + (presenter != null && presenter.animated)
                + " armsAnimator=" + (presenter != null && presenter.armsAnimator != null) + " pistolAnimator=" + (presenter != null && presenter.pistolAnimator != null)
                + " muzzle=" + (presenter != null && presenter.muzzle != null) + " eject=" + (presenter != null && presenter.eject != null));
            if (presenter != null && presenter.eject != null)
            {
                Transform cam = player.AimPivot.GetComponentInChildren<Camera>().transform;
                Debug.Log("QA_SCREENS eject right(cam)=" + cam.InverseTransformDirection(presenter.eject.right).ToString("F2") + " up(cam)=" + cam.InverseTransformDirection(presenter.eject.up).ToString("F2")
                    + " pos(cam)=" + cam.InverseTransformPoint(presenter.eject.position).ToString("F3"));
            }
            if (presenter != null && presenter.muzzle != null)
            {
                Transform cam = player.AimPivot.GetComponentInChildren<Camera>().transform;
                Debug.Log("QA_SCREENS muzzle pos(cam)=" + cam.InverseTransformPoint(presenter.muzzle.position).ToString("F3") + " fwd(cam)=" + cam.InverseTransformDirection(presenter.muzzle.forward).ToString("F2"));
            }

            t0 = Time.time;
            yield return Until(1.5f);
            yield return Capture("01_hip");

            // ADS
            yield return Until(2.5f);
            player.QaSetAds(true);
            yield return Until(3.2f);
            yield return Capture("02_ads");

            // One real shot at the chest, then the flinch.
            yield return Until(3.6f);
            player.QaSetAds(false);
            float shotAt = Time.time;
            bool fired = player.QaFire();
            Debug.Log("QA_SCREENS shot fired=" + fired + " zombieHealth=" + target.Health + " state=" + target.State + " ammo=" + player.Weapon.Ammo);
            yield return null;
            yield return new WaitForEndOfFrame();
            Texture2D fire = Grab();
            while (Time.time < shotAt + 0.06f) yield return null;
            yield return new WaitForEndOfFrame();
            Texture2D flinch = Grab();
            Save("03_fire", fire);
            Save("04_flinch", flinch);
            yield return null;

            // Tactical reload from 5 rounds.
            yield return Until(4.5f);
            player.Weapon.QaSetAmmo(5);
            bool began = player.Weapon.BeginReload(Time.time);
            Debug.Log("QA_SCREENS reload began=" + began + " fromEmpty=" + player.Weapon.ReloadFromEmpty + " duration=" + player.Weapon.ReloadDuration);
            yield return UntilProgress(0.35f);
            yield return Capture("05_reload_mid");
            yield return UntilProgress(0.80f);
            yield return Capture("06_reload_late");
            while (player.Weapon.Reloading) yield return null;
            Debug.Log("QA_SCREENS reload done ammo=" + player.Weapon.Ammo);
            yield return new WaitForSeconds(1.3f);

            // Empty magazine: slide back, then reload from empty.
            player.Weapon.QaSetAmmo(0);
            yield return new WaitForSeconds(0.6f);
            yield return Capture("07_empty_slideback");
            began = player.Weapon.BeginReload(Time.time);
            Debug.Log("QA_SCREENS empty reload began=" + began + " fromEmpty=" + player.Weapon.ReloadFromEmpty + " duration=" + player.Weapon.ReloadDuration);
            yield return UntilProgress(0.50f);
            yield return Capture("08_empty_reload");
            while (player.Weapon.Reloading) yield return null;
            Debug.Log("QA_SCREENS empty reload done ammo=" + player.Weapon.Ammo);
            yield return new WaitForSeconds(1.2f);

            // Zombie at 1.2 m, attack wind-up pose.
            player.Motor.QaSetPitch(0f);
            Place(target, origin + forward * 1.2f, origin);
            ZombieAnimator targetAnim = target.GetComponent<ZombieAnimator>();
            if (targetAnim != null && targetAnim.ClipMode) targetAnim.QaPlayAttack(); // attack clip at its strike moment
            else target.QaSetWindup(1f);
            yield return new WaitForSeconds(0.7f);
            yield return Capture("09_zombie_close");

            // Against the near obstruction (same position as --qa-wall).
            target.QaSetWindup(0f);
            player.Motor.Teleport(new Vector3(4f, 0.05f, 2.1f));
            yield return new WaitForSeconds(0.9f);
            yield return Capture("10_wall");

            // Walking towards the camera (brain unfrozen for a moment), then a real heavy hit on the same zombie.
            target.QaSetWindup(0f);
            Place(target, origin - forward * 9f, origin); // out of the walk / heavy-hit shots
            player.Motor.Teleport(origin);
            player.Motor.QaSetPitch(0f);
            yield return new WaitForSeconds(0.3f);
            Place(walker, origin + forward * 4.5f, origin);
            var walkerAgent = walker.GetComponent<NavMeshAgent>();
            if (walkerAgent != null) { walkerAgent.enabled = true; walkerAgent.Warp(walker.transform.position); }
            foreach (ZombieBrain b in brains) if (b != walker) b.enabled = false; // only the walker moves
            player.QaSetFreeze(false);
            walker.Alert();
            float guard = Time.time + 4f;
            while (Time.time < guard && Vector3.Distance(walker.transform.position, origin) > 2.2f) yield return null;
            Debug.Log("QA_SCREENS walker distance=" + Vector3.Distance(walker.transform.position, origin).ToString("F2") + " speed=" + walker.PlanarSpeed.ToString("F2") + " state=" + walker.State);
            yield return Capture("11_zombie_walk");
            DamageInfo heavy = new DamageInfo { Amount = DamageRules.BaseDamage, Zone = HitZone.Head, Direction = forward, Point = walker.transform.position + Vector3.up * 1.7f, Source = player };
            walker.ApplyDamage(heavy);
            Debug.Log("QA_SCREENS heavy hit state=" + walker.State + " health=" + walker.Health);
            float hitAt = Time.time;
            while (Time.time < hitAt + 0.2f) yield return null;
            yield return Capture("12_zombie_heavyhit");
            player.QaSetFreeze(true);

            Debug.Log("QA_SCREENS_DONE");
            GameQuit.Request();
        }

        // Reads the finished frame from the screen (call after WaitForEndOfFrame). The project has no ImageConversion / ScreenCapture module.
        static Texture2D Grab()
        {
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            return tex;
        }

        IEnumerator Until(float seconds)
        {
            while (Time.time - t0 < seconds) yield return null;
        }

        IEnumerator UntilProgress(float progress)
        {
            float guard = Time.time + 4f;
            while (player.Weapon.ReloadProgress(Time.time) < progress && player.Weapon.Reloading && Time.time < guard) yield return null;
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            Save(name, Grab());
        }

        void Save(string name, Texture2D texture)
        {
            string path = Path.Combine(dir, name + ".png");
            QaPng.Write(path, texture);
            Destroy(texture);
            Debug.Log("QA_SCREENS saved " + name + ".png " + Screen.width + "x" + Screen.height + " t=" + (Time.time - t0).ToString("F2") + " ammo=" + player.Weapon.Ammo + " reloading=" + player.Weapon.Reloading);
        }

        // Places a zombie facing `lookAt` (agent off: the brain is frozen during this run).
        static void Place(ZombieBrain z, Vector3 position, Vector3 lookAt)
        {
            position.y = 0f;
            Vector3 facing = lookAt - position; facing.y = 0f;
            var agent = z.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.enabled = false;
            z.transform.position = position;
            if (facing.sqrMagnitude > 0.0001f) z.transform.rotation = Quaternion.LookRotation(facing);
        }
    }

    // Minimal PNG writer (RGB, deflate from System.IO.Compression) so screenshots need no extra engine module.
    static class QaPng
    {
        public static void Write(string path, Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            Color32[] px = tex.GetPixels32();
            var raw = new byte[(w * 3 + 1) * h];
            int o = 0;
            for (int y = h - 1; y >= 0; y--) // ReadPixels is bottom-up
            {
                raw[o++] = 0;
                for (int x = 0; x < w; x++) { Color32 c = px[y * w + x]; raw[o++] = c.r; raw[o++] = c.g; raw[o++] = c.b; }
            }
            byte[] z;
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x78); ms.WriteByte(0x01);
                using (var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionLevel.Fastest, true)) ds.Write(raw, 0, raw.Length);
                uint a = 1, b = 0;
                foreach (byte v in raw) { a = (a + v) % 65521; b = (b + a) % 65521; }
                uint adler = (b << 16) | a;
                ms.WriteByte((byte)(adler >> 24)); ms.WriteByte((byte)(adler >> 16)); ms.WriteByte((byte)(adler >> 8)); ms.WriteByte((byte)adler);
                z = ms.ToArray();
            }
            using (var fs = new FileStream(path, FileMode.Create))
            {
                fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
                var ihdr = new byte[13];
                Be(ihdr, 0, (uint)w); Be(ihdr, 4, (uint)h); ihdr[8] = 8; ihdr[9] = 2;
                Chunk(fs, "IHDR", ihdr);
                Chunk(fs, "IDAT", z);
                Chunk(fs, "IEND", new byte[0]);
            }
        }

        static void Be(byte[] d, int i, uint v) { d[i] = (byte)(v >> 24); d[i + 1] = (byte)(v >> 16); d[i + 2] = (byte)(v >> 8); d[i + 3] = (byte)v; }

        static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4]; Be(len, 0, (uint)data.Length); s.Write(len, 0, 4);
            var td = new byte[4 + data.Length];
            for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
            System.Array.Copy(data, 0, td, 4, data.Length);
            s.Write(td, 0, td.Length);
            var crc = new byte[4]; Be(crc, 0, Crc(td)); s.Write(crc, 0, 4);
        }

        static uint[] table;
        static uint Crc(byte[] d)
        {
            if (table == null)
            {
                table = new uint[256];
                for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; table[n] = c; }
            }
            uint r = 0xFFFFFFFFu;
            foreach (byte b in d) r = table[(r ^ b) & 0xFF] ^ (r >> 8);
            return r ^ 0xFFFFFFFFu;
        }
    }
}
