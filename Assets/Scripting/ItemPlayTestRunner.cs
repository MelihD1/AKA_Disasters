
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Temporary test rig for the item system: walks the player onto every
/// collectible in the scene, then writes down what the inventory and the HUD
/// ended up showing. Created by the editor bridge before play starts.
/// </summary>
public class ItemPlayTestRunner : MonoBehaviour
{
    public string reportPath;

    private PlayerInventory inventory;
    private InventoryHUD hud;

    private IEnumerator Start()
    {
        inventory = FindFirstObjectByType<PlayerInventory>();
        hud = FindFirstObjectByType<InventoryHUD>();

        if (inventory == null)
        {
            Log("ERROR: no PlayerInventory in the scene.");
            Finish();
            yield break;
        }

        yield return Run();
    }

    private IEnumerator Run()
    {
        Log("=== item system test ===");

        Log("HUD in scene: " +
            (hud != null ? hud.gameObject.name : "NONE"));

        Log("recipe: " +
            (inventory.ActiveRecipe != null
                ? inventory.ActiveRecipe.name
                : "NONE"));

        if (hud != null)
        {
            CanvasGroup group = hud.GetComponent<CanvasGroup>();

            if (group != null)
            {
                Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "HUD before: alpha {0:F2}, interactable {1}",
                    group.alpha, group.interactable));

                hud.SetVisibility(true);

                Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "HUD after forcing visible: alpha {0:F2}, " +
                    "interactable {1}, blocksRaycasts {2}",
                    group.alpha, group.interactable, group.blocksRaycasts));
            }
        }

        LogSlots("HUD slots at start");

        WorldItemPickup[] pickups = FindObjectsByType<WorldItemPickup>(
            FindObjectsInactive.Include);

        Log("collectible items in scene: " + pickups.Length);

        if (pickups.Length == 0)
        {
            Log("nothing to collect, stopping.");
            Finish();
            yield break;
        }

        // Remember what and where they are: picking one up destroys it.
        List<ItemData> items = new List<ItemData>();
        List<Vector3> positions = new List<Vector3>();

        foreach (WorldItemPickup pickup in pickups)
        {
            if (pickup == null)
                continue;

            items.Add(pickup.Item);
            positions.Add(pickup.transform.position);
        }

        Transform player = FindPlayer();

        if (player == null)
        {
            Log("ERROR: no player in the scene.");
            Finish();
            yield break;
        }

        CharacterController controller = player.GetComponent<CharacterController>();

        yield return new WaitForSeconds(0.5f);

        int collected = 0;

        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 target = positions[i];

            // Stand just inside the pickup's trigger.
            Vector3 approach = target + new Vector3(1f, 0f, 0f);

            Teleport(player, controller, approach);
            yield return new WaitForSeconds(0.25f);

            Teleport(player, controller, target);
            yield return new WaitForSeconds(0.45f);

            ItemData item = items[i];
            int count = item != null ? inventory.GetItemCount(item) : -1;

            Log(string.Format(
                CultureInfo.InvariantCulture,
                "item {0}: '{1}' count now {2} (merged {3})",
                i + 1,
                item != null ? item.displayName : "NULL",
                count,
                inventory.MergedItemCount));

            if (item != null && (count > 0 || item.itemType == ItemType.MergedResult))
                collected++;
        }

        yield return new WaitForSeconds(0.5f);

        LogSlots("HUD slots at the end");

        Log("");
        Log("=== summary ===");
        Log("items picked up: " + collected + " of " + positions.Count);
        Log("merged items held: " + inventory.MergedItemCount);
        Log("collapsed value (GameOverManager reads this): " +
            inventory.MergedItemCount);

        Log("=== done ===");

        Finish();
    }

    private Transform FindPlayer()
    {
        FloodEvent flood = FindFirstObjectByType<FloodEvent>();

        if (flood != null && flood.playerRoot != null)
            return flood.playerRoot;

        GameObject named = GameObject.Find("PlayerRoot");

        return named != null ? named.transform : null;
    }

    private void Teleport(
        Transform player,
        CharacterController controller,
        Vector3 position)
    {
        if (controller != null)
            controller.enabled = false;

        player.position = position;
        Physics.SyncTransforms();

        if (controller != null)
            controller.enabled = true;
    }

    /// <summary>Reads the numbers the inventory HUD is showing right now.</summary>
    private void LogSlots(string label)
    {
        ItemSlotUI[] slots = FindObjectsByType<ItemSlotUI>(
            FindObjectsInactive.Include);

        Log(label + ": " + slots.Length + " slot(s)");

        foreach (ItemSlotUI slot in slots)
        {
            if (slot == null)
                continue;

            TextMeshProUGUI text =
                slot.GetComponentInChildren<TextMeshProUGUI>(true);

            Log(string.Format(
                CultureInfo.InvariantCulture,
                "   slot '{0}' shows '{1}'",
                slot.TrackedItem != null
                    ? slot.TrackedItem.displayName
                    : "NULL",
                text != null ? text.text : "no text"));
        }
    }

    private void Finish()
    {
        try
        {
            File.WriteAllText(reportPath + ".done", "done");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("ItemPlayTestRunner: " + e.Message);
        }

        enabled = false;
    }

    private void Log(string message)
    {
        Debug.Log("[item test] " + message);

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
