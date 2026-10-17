
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Temporary automated test rig for the flood system. An editor script drops
/// this on a scene object, plays the game and reads the report it writes.
/// Safe to delete once the flood system has been verified.
/// </summary>
public class FloodPlayTestRunner : MonoBehaviour
{
    public string configPath;
    public string reportPath;
    public string shotPath;
    public string shotPeakPath;
    public string shotUnderPath;

    private FloodEvent flood;
    private Transform player;
    private CharacterController controller;

    private IEnumerator Start()
    {
        if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
        {
            Debug.LogWarning("FloodPlayTestRunner: no config at " + configPath);
            yield break;
        }

        flood = FindFirstObjectByType<FloodEvent>();

        if (flood == null || flood.playerRoot == null)
        {
            Log("ERROR: no FloodEvent / playerRoot in the scene.");
            Finish();
            yield break;
        }

        player = flood.playerRoot;
        controller = player.GetComponent<CharacterController>();

        yield return Run();
    }

    private IEnumerator Run()
    {
        Log("=== flood play test ===");

        flood.autoStart = false;

        EarthquakeEvent earthquake = FindFirstObjectByType<EarthquakeEvent>();

        if (earthquake != null)
        {
            earthquake.enabled = false;
            Log("earthquake event disabled for the test");
        }

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "measured: floor={0:F3} ({5}) furnitureTop={1:F3} ({6}) " +
            "eye={2:F3} peak={3:F3} headClearance={4:F3}",
            flood.FloorLevel,
            flood.StandableFurnitureTop,
            flood.EyeHeight,
            flood.PeakWaterLevel,
            flood.headClearance,
            flood.FloorColliderName,
            flood.TallestStandableName));

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "house: center=({0:F2}, {1:F2}, {2:F2}) footprint={3:F2} x {4:F2}",
            flood.HouseCenter.x,
            flood.HouseCenter.y,
            flood.HouseCenter.z,
            flood.WaterFootprint.x,
            flood.WaterFootprint.z));

        yield return new WaitForSeconds(0.75f);

        // ---------------- scenario 1: on top of the furniture ------------
        Vector3 furnitureSpot = FindTallestStandableSpot();

        Teleport(furnitureSpot);
        yield return new WaitForSeconds(1.2f);

        LogStanding("furniture, before the flood");

        Log("StartFlood (furniture) -> " + flood.StartFlood());

        float timer = 0f;

        // Wait for the flood to actually get going.
        while (flood.State == FloodEvent.FloodState.Idle && timer < 8f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        Log("rise started after " + timer.ToString("F2") + "s, state=" +
            flood.State);

        timer = 0f;

        while (flood.State == FloodEvent.FloodState.Rising && timer < 40f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.75f);

        float cameraY = Camera.main != null
            ? Camera.main.transform.position.y
            : 0f;

        LogStanding("furniture, at the peak");

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "PEAKCHECK state={0} water={1:F3} cameraY={2:F3} " +
            "gap={3:F3} (negative = water below the eyes) lost={4}",
            flood.State,
            flood.WaterLevel,
            cameraY,
            flood.WaterLevel - cameraY,
            flood.PlayerLost));

        LogRendererBounds("water box", flood.waterRenderer);

        if (!string.IsNullOrEmpty(shotPeakPath))
        {
            ScreenCapture.CaptureScreenshot(shotPeakPath);
            yield return new WaitForSeconds(0.4f);
        }

        // ---------------- scenario 2: on the bare floor -----------------
        flood.CancelFlood();

        // Wait for the water to finish draining before triggering again.
        float drainTimer = 0f;

        while (flood.State != FloodEvent.FloodState.Idle && drainTimer < 30f)
        {
            drainTimer += Time.deltaTime;
            yield return null;
        }

        Log("water back down after " + drainTimer.ToString("F2") + "s (state=" +
            flood.State + ", level=" + flood.WaterLevel.ToString("F3") + ")");

        yield return new WaitForSeconds(0.5f);

        Vector3 floorSpot = FindFloorSpot();

        Teleport(floorSpot);
        yield return new WaitForSeconds(1.2f);

        LogStanding("floor, before the flood");

        Log("StartFlood (floor) -> " + flood.StartFlood());

        timer = 0f;
        bool shot = false;
        bool shotUnder = false;
        bool wentUnder = false;
        float lastWater = flood.WaterLevel;
        float lastCamera = Camera.main != null
            ? Camera.main.transform.position.y
            : 0f;

        while (timer < 45f)
        {
            timer += Time.deltaTime;

            lastWater = flood.WaterLevel;
            lastCamera = Camera.main != null
                ? Camera.main.transform.position.y
                : 0f;

            if (!shot &&
                lastWater - flood.FloorLevel > 0.85f &&
                !string.IsNullOrEmpty(shotPath))
            {
                shot = true;
                ScreenCapture.CaptureScreenshot(shotPath);

                yield return new WaitForSeconds(0.4f);
                continue;
            }

            if (!wentUnder && lastCamera < lastWater)
            {
                wentUnder = true;

                Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "first frame underwater: water={0:F3} cameraY={1:F3} " +
                    "t={2:F2}",
                    lastWater,
                    lastCamera,
                    timer));

                if (!shotUnder && !string.IsNullOrEmpty(shotUnderPath))
                {
                    shotUnder = true;
                    ScreenCapture.CaptureScreenshot(shotUnderPath);

                    yield return new WaitForSeconds(0.4f);
                    continue;
                }
            }

            if (flood.PlayerLost)
                break;

            yield return null;
        }

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "FLOORCHECK drowned={0} waterAtDeath={1:F3} " +
            "cameraYAtDeath={2:F3} gap={3:F3} lost={4}",
            flood.PlayerLost,
            lastWater,
            lastCamera,
            lastWater - lastCamera,
            flood.PlayerLost));

        LogRendererBounds("water box at the end", flood.waterRenderer);
        LogFollowCheck("follow check");

        Finish();
    }

    private void Finish()
    {
        Log("=== done ===");

        try
        {
            File.WriteAllText(reportPath + ".done", "done");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("FloodPlayTestRunner: " + e.Message);
        }

        enabled = false;
    }

    // Helpers --------------------------------------------------------------

    private void Teleport(Vector3 position)
    {
        if (controller != null)
            controller.enabled = false;

        player.position = position;

        if (controller != null)
            controller.enabled = true;

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "teleported to ({0:F2}, {1:F2}, {2:F2})",
            position.x, position.y, position.z));
    }

    private Collider[] HouseColliders()
    {
        return flood.houseRoot != null
            ? flood.houseRoot.GetComponentsInChildren<Collider>(true)
            : new Collider[0];
    }

    private bool IsPlayer(Collider c)
    {
        return c != null && c.transform.IsChildOf(player);
    }

    private bool Ignored(string objectName)
    {
        if (flood.standableIgnoreNames == null || string.IsNullOrEmpty(objectName))
            return false;

        foreach (string ignore in flood.standableIgnoreNames)
        {
            if (!string.IsNullOrEmpty(ignore) &&
                objectName.IndexOf(
                    ignore,
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private bool RayDown(Vector3 origin, out RaycastHit hit)
    {
        hit = default(RaycastHit);

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            6f,
            ~0,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            if (h.collider == null || IsPlayer(h.collider))
                continue;

            hit = h;
            return true;
        }

        return false;
    }

    /// <summary>
    /// The surface the flood system used to size the water: stand on this and
    /// the water should stop just below the player's eyes.
    /// </summary>
    private Vector3 FindTallestStandableSpot()
    {
        Collider[] colliders = HouseColliders();

        float floor = flood.FloorLevel;
        float target = flood.StandableFurnitureTop;
        float bestDelta = float.MaxValue;
        float bestTop = float.MinValue;
        Collider best = null;

        foreach (Collider c in colliders)
        {
            if (c == null || c.isTrigger)
                continue;

            float top = c.bounds.max.y;
            float height = top - floor;

            if (height < flood.standableMinHeight ||
                height > flood.standableMaxHeight)
                continue;

            if (Ignored(c.name))
                continue;

            float delta = Mathf.Abs(top - target);

            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestTop = top;
                best = c;
            }
        }

        Vector3 spot = new Vector3(
            flood.HouseCenter.x,
            flood.FloorLevel + 1.5f,
            flood.HouseCenter.z);

        if (best == null)
        {
            Log("WARNING: no standable furniture found.");
            return spot;
        }

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "tallest standable object: '{0}' top={1:F3} size={2:F2} x {3:F2}",
            best.name,
            bestTop,
            best.bounds.size.x,
            best.bounds.size.z));

        Bounds b = best.bounds;

        // Start from the middle of the surface and work outwards, so the
        // player is not dropped right on an edge and slides off.
        int[] order = { 2, 1, 3, 0, 4 };

        for (int a = 0; a < order.Length; a++)
        {
            for (int c = 0; c < order.Length; c++)
            {
                float fx = Mathf.Lerp(
                    b.min.x + 0.3f, b.max.x - 0.3f, order[a] / 4f);

                float fz = Mathf.Lerp(
                    b.min.z + 0.3f, b.max.z - 0.3f, order[c] / 4f);

                RaycastHit hit;

                if (!RayDown(new Vector3(fx, bestTop + 0.6f, fz), out hit))
                    continue;

                if (Mathf.Abs(hit.point.y - bestTop) > 0.08f)
                    continue;

                return new Vector3(fx, hit.point.y + 0.2f, fz);
            }
        }

        return new Vector3(b.center.x, bestTop + 0.6f, b.center.z);
    }

    /// <summary>A spot inside the house where the floor is the only surface.</summary>
    private Vector3 FindFloorSpot()
    {
        float inset = 1.6f;

        float minX = flood.HouseCenter.x - flood.WaterFootprint.x * 0.5f + inset;
        float maxX = flood.HouseCenter.x + flood.WaterFootprint.x * 0.5f - inset;
        float minZ = flood.HouseCenter.z - flood.WaterFootprint.z * 0.5f + inset;
        float maxZ = flood.HouseCenter.z + flood.WaterFootprint.z * 0.5f - inset;

        float scanTop = flood.FloorLevel + flood.standableMaxHeight - 0.05f;

        for (int i = 0; i <= 12; i++)
        {
            for (int j = 0; j <= 12; j++)
            {
                float x = Mathf.Lerp(minX, maxX, i / 12f);
                float z = Mathf.Lerp(minZ, maxZ, j / 12f);

                RaycastHit hit;

                if (!RayDown(new Vector3(x, scanTop, z), out hit))
                    continue;

                if (Mathf.Abs(hit.point.y - flood.FloorLevel) > 0.2f)
                    continue;

                // Distance from the spot to the player is irrelevant, we
                // only want empty floor.
                return new Vector3(x, hit.point.y + 0.2f, z);
            }
        }

        Log("WARNING: no clear floor spot found, using the house center.");

        return new Vector3(
            flood.HouseCenter.x,
            flood.FloorLevel + 0.2f,
            flood.HouseCenter.z);
    }

    private void LogStanding(string label)
    {
        float cameraY = Camera.main != null
            ? Camera.main.transform.position.y
            : 0f;

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "{0}: root=( {1:F2}, {2:F2}, {3:F2} ) cameraY={4:F3} " +
            "feet={5:F3} grounded={6}",
            label,
            player.position.x,
            player.position.y,
            player.position.z,
            cameraY,
            player.position.y -
                (controller != null ? controller.height * 0.5f : 0f),
            controller != null && controller.isGrounded));
    }

    private void LogFollowCheck(string label)
    {
        if (flood.waterVolume == null)
            return;

        Vector3 box = flood.waterVolume.position;
        Vector3 playerXZ = player.position;

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "{0}: box centre=({1:F2}, {2:F2}) playerXZ=({3:F2}, {4:F2}) " +
            "offset=({5:F2}, {6:F2}) size=({7:F2} x {8:F2})",
            label,
            box.x,
            box.z,
            playerXZ.x,
            playerXZ.z,
            box.x - playerXZ.x,
            box.z - playerXZ.z,
            flood.waterVolume.localScale.x,
            flood.waterVolume.localScale.z));
    }

    private void LogRendererBounds(string label, Renderer renderer)
    {
        if (renderer == null)
            return;

        Bounds b = renderer.bounds;

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "{0}: min=({1:F2}, {2:F2}, {3:F2}) max=({4:F2}, {5:F2}, {6:F2}) " +
            "enabled={7}",
            label,
            b.min.x, b.min.y, b.min.z,
            b.max.x, b.max.y, b.max.z,
            renderer.enabled));
    }

    private void Log(string message)
    {
        Debug.Log("[flood test] " + message);

        if (string.IsNullOrEmpty(reportPath))
            return;

        try
        {
            File.AppendAllText(
                reportPath,
                message + "\n",
                Encoding.UTF8);
        }
        catch (System.Exception)
        {
            // Reporting must never break the test.
        }
    }
}
