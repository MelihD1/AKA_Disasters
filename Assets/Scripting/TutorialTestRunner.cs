using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Temporary rig: opens the tutorial, walks the pages, and reports what each
/// step actually showed. Run it in the menu scene.
/// </summary>
public class TutorialTestRunner : MonoBehaviour
{
    public string reportPath;

    private IEnumerator Start()
    {
        TutorialMenu tutorial = FindFirstObjectByType<TutorialMenu>();

        Log("=== tutorial test ===");

        if (tutorial == null)
        {
            Log("ERROR: no TutorialMenu in this scene.");
            Finish();
            yield break;
        }

        GameObject panel = tutorial.tutorialPanel;

        Log("panel: " + (panel != null ? panel.name : "NONE"));

        if (panel == null)
        {
            Finish();
            yield break;
        }

        List<GameObject> pages = Pages(panel);

        Log("pages found under the panel: " + pages.Count);

        // The main menu button that should open it.
        foreach (Button button in FindObjectsByType<Button>(
            FindObjectsInactive.Include))
        {
            if (!button.name.ToLowerInvariant().StartsWith("tutorial"))
                continue;

            StringBuilder calls = new StringBuilder();

            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                Object target = button.onClick.GetPersistentTarget(i);

                calls.Append(target != null ? target.GetType().Name : "null")
                    .Append('.')
                    .Append(button.onClick.GetPersistentMethodName(i))
                    .Append(' ');
            }

            Log("menu button '" + button.name + "' calls [" +
                calls.ToString().Trim() + "]");
        }

        Log("");
        Log("panel active before opening: " + panel.activeSelf);
        LogPages(pages, "as the scene starts");

        tutorial.OpenTutorial();
        yield return null;
        Log("");
        Log("panel active after OpenTutorial: " + panel.activeSelf);
        LogPages(pages, "after OpenTutorial");

        tutorial.NextPage();
        yield return null;
        LogPages(pages, "after NextPage");

        tutorial.NextPage();
        yield return null;
        LogPages(pages, "after NextPage (2)");

        tutorial.PreviousPage();
        yield return null;
        LogPages(pages, "after PreviousPage");

        tutorial.CloseTutorial();
        yield return null;
        Log("");
        Log("panel active after CloseTutorial: " + panel.activeSelf);

        // ---- can these buttons actually be clicked, and do they fire? ----
        Log("");
        Log("--- click check ---");

        Canvas canvas = panel.GetComponentInParent<Canvas>();
        GraphicRaycaster raycaster = canvas != null
            ? canvas.GetComponent<GraphicRaycaster>()
            : null;

        EventSystem system = EventSystem.current;

        tutorial.OpenTutorial();
        yield return null;

        if (raycaster == null || system == null)
        {
            Log("no GraphicRaycaster / EventSystem, cannot check clicking");
        }
        else
        {
            foreach (Button button in panel.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy)
                    continue;

                Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                    null, button.transform.position);

                PointerEventData pointer = new PointerEventData(system)
                {
                    position = screen
                };

                List<RaycastResult> hits = new List<RaycastResult>();
                raycaster.Raycast(pointer, hits);

                string top = hits.Count > 0 && hits[0].gameObject != null
                    ? hits[0].gameObject.name
                    : "NOTHING";

                Button handler = hits.Count > 0 && hits[0].gameObject != null
                    ? hits[0].gameObject.GetComponentInParent<Button>()
                    : null;

                Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "   '{0}' at ({1:F0},{2:F0}) -> top hit '{3}', would fire '{4}'",
                    button.name, screen.x, screen.y, top,
                    handler != null ? handler.name : "NOTHING"));
            }
        }

        Log("");
        Log("--- pressing the buttons for real ---");

        for (int press = 1; press <= 3; press++)
        {
            Button next = FindActiveButton(panel, "next");

            if (next == null)
            {
                Log("   no active Next button to press");
                break;
            }

            Log("   pressing '" + next.name + "'");
            next.onClick.Invoke();
            yield return null;
            LogPages(pages, "after pressing Next #" + press);
        }

        Button backButton = FindActiveButton(panel, "back");

        if (backButton == null)
        {
            Log("   no active Back button");
        }
        else
        {
            Log("   pressing '" + backButton.name + "'");
            backButton.onClick.Invoke();
            yield return null;
            LogPages(pages, "after pressing Back");
        }

        Button closeButton = FindActiveButton(panel, "close");

        if (closeButton == null)
        {
            Log("   no active Close button");
        }
        else
        {
            Log("   pressing '" + closeButton.name + "'");
            closeButton.onClick.Invoke();
            yield return null;
            Log("   panel active after pressing Close: " + panel.activeSelf);
        }

        Log("");
        Log("=== done ===");
        Finish();
    }

    private Button FindActiveButton(GameObject root, string nameStart)
    {
        string wanted = nameStart.ToLowerInvariant();

        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            if (button.name.Trim().ToLowerInvariant().StartsWith(wanted) &&
                button.gameObject.activeInHierarchy)
            {
                return button;
            }
        }

        return null;
    }

    private void LogPages(List<GameObject> pages, string label)
    {
        foreach (GameObject page in pages)
        {
            TMP_Text title = null;

            foreach (TMP_Text text in page.GetComponentsInChildren<TMP_Text>(
                true))
            {
                if (text.name.ToLowerInvariant().Contains("title"))
                {
                    title = text;
                    break;
                }
            }

            Log(string.Format(
                CultureInfo.InvariantCulture,
                "   {0}: {1} showing={2} title=\"{3}\"",
                label, page.name,
                page.activeSelf,
                title != null ? title.text.Replace("\n", " ") : "-"));
        }
    }

    private List<GameObject> Pages(GameObject panel)
    {
        List<GameObject> pages = new List<GameObject>();

        foreach (Transform child in panel.transform)
        {
            string name = child.name.Trim();

            if (name.StartsWith("Page") && name.Length > 4 &&
                char.IsDigit(name[4]))
            {
                pages.Add(child.gameObject);
            }
        }

        return pages;
    }

    private void Finish()
    {
        try
        {
            File.WriteAllText(reportPath + ".done", "done");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("TutorialTestRunner: " + e.Message);
        }

        enabled = false;
    }

    private void Log(string message)
    {
        Debug.Log("[tutorial test] " + message);

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
