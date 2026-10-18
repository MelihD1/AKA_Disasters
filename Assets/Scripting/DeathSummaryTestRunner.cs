using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Temporary rig: checks the death screen counts only this run's emergency
/// kits, and that the item HUD is on screen from the start.
/// </summary>
public class DeathSummaryTestRunner : MonoBehaviour
{
    public string reportPath;

    private IEnumerator Start()
    {
        GameOverManager gameOver = FindFirstObjectByType<GameOverManager>();
        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        FloodEvent flood = FindFirstObjectByType<FloodEvent>();
        InventoryHUD hud = FindFirstObjectByType<InventoryHUD>();

        Log("=== death summary test ===");

        if (hud == null)
        {
            Log("HUD: not in the scene");
        }
        else
        {
            CanvasGroup group = hud.GetComponent<CanvasGroup>();

            Log(string.Format(
                CultureInfo.InvariantCulture,
                "HUD on screen at start: alpha={0:F2} interactable={1} " +
                "blocksRaycasts={2}",
                group != null ? group.alpha : -1f,
                group != null ? group.interactable : false,
                group != null ? group.blocksRaycasts : false));

            Log("HUD built " +
                FindObjectsByType<ItemSlotUI>(FindObjectsInactive.Include).Length +
                " slot(s)");
        }

        if (gameOver == null || inventory == null || flood == null)
        {
            Log("ERROR: the scene is missing something the test needs.");
            Finish();
            yield break;
        }

        Log("shop total before this run: " + GameProgress.Currency);
        Log("kits earned earlier this run: " + inventory.MergedItemCount);

        // Hand over the six ingredients lying in the scene so the recipe
        // merges one emergency kit, the same as picking them up by hand.
        int handed = 0;

        foreach (WorldItemPickup pickup in
            FindObjectsByType<WorldItemPickup>(FindObjectsInactive.Include))
        {
            if (pickup == null || pickup.Item == null)
                continue;

            if (inventory.AddItem(pickup.Item))
                handed++;
        }

        Log("handed the player " + handed + " items");

        yield return null;
        yield return null;

        Log("kits earned after the merge: " + inventory.MergedItemCount);

        // Kill him: with no O2 Tank the flood drowns him as soon as it covers
        // his head.
        PlacePlayerOnFloor(flood);
        flood.ApplyDifficulty(0f);
        flood.BeginFloodNow();

        float waited = 0f;

        while (!flood.PlayerLost && waited < 25f)
        {
            yield return null;
            waited += Time.deltaTime;
        }

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "flood reached him after {0:F2}s, player lost: {1}",
            waited, flood.PlayerLost));

        // Give the game over manager a few frames to notice and bank.
        for (int i = 0; i < 12; i++)
            yield return null;

        Log("");
        Log("--- what the death screen is counting ---");
        Log("kits saved this run: " + gameOver.KitsThisRun +
            "   <- this is what the screen shows");
        Log("total kits in the whole game: " + GameProgress.Currency +
            "   <- still banked for the shop");
        Log("inventory merged count: " + inventory.MergedItemCount);
        Log("timeScale after the death: " + Time.timeScale);

        Log("");
        Log("=== done ===");

        Finish();
    }

    private void PlacePlayerOnFloor(FloodEvent flood)
    {
        Transform player = flood.playerRoot != null
            ? flood.playerRoot
            : GameObject.Find("PlayerRoot")?.transform;

        if (player == null)
        {
            Log("no player to place");
            return;
        }

        CharacterController controller = player.GetComponent<CharacterController>();

        Vector3 spot = flood.HouseCenter;
        spot.y = flood.FloorLevel + 0.6f;

        if (controller != null)
            controller.enabled = false;

        player.position = spot;
        Physics.SyncTransforms();

        if (controller != null)
            controller.enabled = true;

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "player placed inside the house at ({0:F2}, {1:F2}, {2:F2})",
            spot.x, spot.y, spot.z));
    }

    private void Finish()
    {
        try
        {
            File.WriteAllText(reportPath + ".done", "done");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("DeathSummaryTestRunner: " + e.Message);
        }

        enabled = false;
    }

    private void Log(string message)
    {
        Debug.Log("[death test] " + message);

        if (string.IsNullOrEmpty(reportPath))
            return;

        try
        {
            File.AppendAllText(reportPath, message + "\n", Encoding.UTF8);
        }
        catch (System.Exception)
        {
            // Reporting must never break the test.
        }
    }
}
