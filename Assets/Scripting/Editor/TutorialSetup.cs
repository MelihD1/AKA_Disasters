
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wires the tutorial menu up in the open scene: finds the tutorial panel and
/// its pages, fills in the TutorialMenu fields, hooks the page buttons, adds a
/// Back button if there is not one, and adds a Tutorial button to the main menu
/// that opens it all. The page text is left completely alone for you to write.
/// </summary>
public static class TutorialSetup
{
    private const string PanelName = "TutorialPanel";
    private const string PagePrefix = "Page";
    private const string MenuPanelName = "MainPanel";

    [MenuItem("Tools/UI/Set Up Tutorial Menu", priority = 60)]
    private static void SetupMenu()
    {
        string report = SetupActiveScene();

        Debug.Log("TutorialSetup:\n" + report);
        EditorUtility.DisplayDialog("Tutorial menu", report, "OK");
    }

    [MenuItem("Tools/UI/Check Tutorial Menu", priority = 61)]
    private static void CheckMenu()
    {
        string report = Check();

        Debug.Log("TutorialSetup:\n" + report);
        EditorUtility.DisplayDialog("Tutorial menu", report, "OK");
    }

    public static string SetupActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        StringBuilder log = new StringBuilder();

        if (!scene.IsValid() || !scene.isLoaded)
            return "ERROR: no active scene.";

        TutorialMenu tutorial =
            Object.FindFirstObjectByType<TutorialMenu>(
                FindObjectsInactive.Include);

        if (tutorial == null)
        {
            return "ERROR: no TutorialMenu in this scene. Put the component " +
                "on the Canvas first, then run this again.";
        }

        log.AppendLine("TutorialMenu is on '" + tutorial.gameObject.name + "'");

        GameObject panel = tutorial.tutorialPanel;

        if (panel == null)
            panel = FindObject(PanelName, log);

        if (panel == null)
            panel = FindCommonAncestorOfTitles(log);

        if (panel == null)
        {
            return log + "\nERROR: could not find the tutorial panel. Name it " +
                "'" + PanelName + "' (or drag it into the Tutorial Menu " +
                "component) and run this again.";
        }

        log.AppendLine("tutorial panel: " + panel.name);

        List<GameObject> pages = CollectPages(panel, log);

        if (pages.Count == 0)
            log.AppendLine("WARNING: no pages found under the panel.");

        // ---- fill in the component ----
        SerializedObject target = new SerializedObject(tutorial);

        SerializedProperty panelField = target.FindProperty("tutorialPanel");

        if (panelField != null)
            panelField.objectReferenceValue = panel;

        SerializedProperty pagesField = target.FindProperty("pages");

        if (pagesField != null)
        {
            pagesField.arraySize = pages.Count;

            for (int i = 0; i < pages.Count; i++)
            {
                pagesField.GetArrayElementAtIndex(i).objectReferenceValue =
                    pages[i];
            }
        }

        target.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tutorial);

        log.AppendLine("pages assigned: " + pages.Count);

        // Every page stays switched on in the saved scene so you can see and
        // edit them all; the script shows one at a time when it runs.
        foreach (GameObject page in pages)
        {
            if (!page.activeSelf)
            {
                page.SetActive(true);
                EditorUtility.SetDirty(page);
            }

            log.AppendLine("   " + DescribePage(page));
        }

        // ---- hook up the buttons ----
        int wired = 0;

        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            string name = button.name.Trim().ToLowerInvariant();

            if (name.StartsWith("next"))
            {
                Wire(button, tutorial.NextPage);
                wired++;
                log.AppendLine("   '" + button.name + "' -> NextPage");
            }
            else if (name.StartsWith("back") || name.StartsWith("prev"))
            {
                Wire(button, tutorial.PreviousPage);
                wired++;
                log.AppendLine("   '" + button.name + "' -> PreviousPage");
            }
            else if (name.StartsWith("close"))
            {
                Wire(button, tutorial.CloseTutorial);
                wired++;
                log.AppendLine("   '" + button.name + "' -> CloseTutorial");
            }
        }

        if (!HasButton(panel, "back") && !HasButton(panel, "prev"))
        {
            Button back = EnsureBackButton(panel, log);

            if (back != null)
            {
                Wire(back, tutorial.BackOrClose);
                wired++;
            }
        }
        else
        {
            // It exists already: make sure it sits on the panel (so it shows on
            // every page), beside the close button, and is labelled properly.
            Button back = EnsureBackButton(panel, log);

            if (back != null)
            {
                Wire(back, tutorial.BackOrClose);
                wired++;
            }
        }

        // ---- the button on the main menu ----
        GameObject menuPanel = FindObject(MenuPanelName, log);

        if (menuPanel == null)
        {
            log.AppendLine("WARNING: no '" + MenuPanelName + "' found, so no " +
                "Tutorial button was added to the menu.");
        }
        else if (HasButton(menuPanel, "tutorial"))
        {
            Button existing = FindButton(menuPanel, "tutorial");
            Wire(existing, tutorial.OpenTutorial);
            PlaceBelowOtherButtons(existing, menuPanel, log);
            log.AppendLine("   main menu already had a Tutorial button, rewired it");
            wired++;
        }
        else
        {
            Button created = CreateMenuButton(menuPanel, tutorial, log);

            if (created != null)
            {
                wired++;
                log.AppendLine("   added '" + created.name + "' to the main menu");
            }
        }

        log.AppendLine("buttons wired: " + wired);

        UnblockRaycasts(panel, log);
        BringButtonsToFront(panel, log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        log.AppendLine("saved: " + scene.path);
        log.AppendLine("");
        log.AppendLine("Page text is untouched - open the page objects and type " +
            "your lines into 'TutorialTitle' and 'TutorialDescription'.");

        return log.ToString();
    }

    public static string Check()
    {
        StringBuilder log = new StringBuilder();

        TutorialMenu tutorial =
            Object.FindFirstObjectByType<TutorialMenu>(
                FindObjectsInactive.Include);

        if (tutorial == null)
        {
            log.AppendLine("scene: " + SceneManager.GetActiveScene().path);
            log.AppendLine("TutorialMenu: NOT IN THIS SCENE");
            return log.ToString();
        }

        SerializedObject target = new SerializedObject(tutorial);
        SerializedProperty panelField = target.FindProperty("tutorialPanel");
        SerializedProperty pagesField = target.FindProperty("pages");

        log.AppendLine("TutorialMenu on '" + tutorial.gameObject.name + "'");
        log.AppendLine("panel: " + (panelField != null &&
            panelField.objectReferenceValue != null
                ? panelField.objectReferenceValue.name
                : "NONE"));

        int count = pagesField != null ? pagesField.arraySize : 0;

        log.AppendLine("pages: " + count);

        for (int i = 0; i < count; i++)
        {
            GameObject page = pagesField.GetArrayElementAtIndex(i)
                .objectReferenceValue as GameObject;

            log.AppendLine("   [" + i + "] " +
                (page != null ? DescribePage(page) : "NULL"));
        }

        log.AppendLine("--- buttons ---");

        foreach (Button button in Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include))
        {
            string calls = ButtonCalls(button);

            if (calls.Length == 0)
                continue;

            log.AppendLine("   '" + button.name + "' -> " + calls);
        }

        return log.ToString();
    }

    // Helpers --------------------------------------------------------------

    private static List<GameObject> CollectPages(
        GameObject panel, StringBuilder log)
    {
        List<GameObject> pages = new List<GameObject>();

        // Pages are the panel's children called Page1, Page2, ...
        foreach (Transform child in panel.transform)
        {
            string name = child.name.Trim();

            if (name.StartsWith(PagePrefix) &&
                name.Length > PagePrefix.Length &&
                char.IsDigit(name[PagePrefix.Length]))
            {
                pages.Add(child.gameObject);
            }
        }

        if (pages.Count > 0)
        {
            pages.Sort(delegate (GameObject a, GameObject b)
            {
                return PageNumber(a.name).CompareTo(PageNumber(b.name));
            });

            return pages;
        }

        // Fall back to any direct child holding a TutorialTitle.
        foreach (Transform child in panel.transform)
        {
            if (child.GetComponentInChildren<TMP_Text>(true) != null)
                pages.Add(child.gameObject);
        }

        if (pages.Count > 0)
            log.AppendLine("(no 'PageN' children, using the panel's children " +
                "that hold text instead)");

        return pages;
    }

    private static int PageNumber(string name)
    {
        int number = 0;
        bool found = false;

        foreach (char c in name)
        {
            if (char.IsDigit(c))
            {
                number = number * 10 + (c - '0');
                found = true;
            }
            else if (found)
            {
                break;
            }
        }

        return number;
    }

    private static string DescribePage(GameObject page)
    {
        TMP_Text title = null;

        foreach (TMP_Text text in page.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name.ToLowerInvariant().Contains("title"))
            {
                title = text;
                break;
            }
        }

        string label = title != null && !string.IsNullOrEmpty(title.text)
            ? title.text
            : "(title empty)";

        if (label.Length > 40)
            label = label.Substring(0, 40) + "...";

        return page.name + "  active=" + page.activeSelf + "  title=\"" +
            label.Replace("\n", " ") + "\"";
    }

    /// <summary>
    /// Makes sure there is a properly labelled Back button sitting on the panel
    /// (so it shows on every page), beside the close button.
    /// </summary>
    private static Button EnsureBackButton(GameObject panel, StringBuilder log)
    {
        Button back = FindButton(panel, "back");

        if (back == null)
            back = FindButton(panel, "prev");

        // Copy the look from a real button on this panel.
        Button source = FindButton(panel, "close");

        if (source == null)
            source = FindButton(panel, "next");

        if (back == null)
        {
            if (source == null)
            {
                log.AppendLine("   no existing button to copy, skipped Back");
                return null;
            }

            GameObject copy = Object.Instantiate(source.gameObject,
                panel.transform);

            copy.name = "BackButton";
            back = copy.GetComponent<Button>();

            log.AppendLine("   added 'BackButton' -> PreviousPage");
        }
        else if (back.transform.parent != panel.transform)
        {
            // It was sitting inside one page, so it only showed there.
            back.transform.SetParent(panel.transform, false);
            log.AppendLine("   moved the Back button up to the panel so it " +
                "shows on every page");
        }

        if (back == null)
            return null;

        if (source != null)
        {
            RectTransform sourceRect = source.GetComponent<RectTransform>();
            RectTransform backRect = back.GetComponent<RectTransform>();

            if (sourceRect != null && backRect != null)
            {
                float width = sourceRect.sizeDelta.x > 1f
                    ? sourceRect.sizeDelta.x
                    : 160f;

                backRect.anchoredPosition = sourceRect.anchoredPosition +
                    new Vector2(-(width + 12f), 0f);
            }
        }

        SetLabel(back, "Go back", log);

        return back;
    }

    /// <summary>Gives a button a readable caption instead of a copied arrow.</summary>
    private static void SetLabel(Button button, string text, StringBuilder log)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

        if (label == null)
        {
            log.AppendLine("   ('" + button.name + "' has no text child)");
            return;
        }

        if (label.text == text)
            return;

        label.text = text;
        EditorUtility.SetDirty(label);

        log.AppendLine("   '" + button.name + "' now reads \"" + text + "\"");
    }

    /// <summary>
    /// Moves a menu button under the lowest existing button, so it can never
    /// land on top of another one.
    /// </summary>
    private static void PlaceBelowOtherButtons(
        Button button, GameObject menuPanel, StringBuilder log)
    {
        if (button == null || menuPanel == null)
            return;

        RectTransform mine = button.GetComponent<RectTransform>();

        if (mine == null)
            return;

        float lowest = float.MaxValue;
        float height = 30f;

        foreach (Button other in menuPanel.GetComponentsInChildren<Button>(true))
        {
            if (other == null || other == button)
                continue;

            RectTransform otherRect = other.GetComponent<RectTransform>();

            if (otherRect == null || otherRect == mine)
                continue;

            if (otherRect.anchoredPosition.y < lowest)
            {
                lowest = otherRect.anchoredPosition.y;
                height = Mathf.Max(1f, otherRect.sizeDelta.y);
            }
        }

        if (lowest == float.MaxValue)
            return;

        mine.anchoredPosition = new Vector2(
            mine.anchoredPosition.x, lowest - (height + 36f));

        EditorUtility.SetDirty(button);

        log.AppendLine("   placed '" + button.name + "' at y=" +
            mine.anchoredPosition.y.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) +
            ", clear of the other buttons");
    }

    /// <summary>
    /// Stops decorative images from swallowing clicks. Anything that is not
    /// part of a button stops being a raycast target, so a page background can
    /// never sit over a button and eat the click.
    /// </summary>
    private static void UnblockRaycasts(GameObject panel, StringBuilder log)
    {
        int cleared = 0;

        foreach (Graphic graphic in panel.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null || !graphic.raycastTarget)
                continue;

            if (graphic.GetComponentInParent<Button>(true) != null)
                continue;

            graphic.raycastTarget = false;
            EditorUtility.SetDirty(graphic);
            cleared++;
        }

        log.AppendLine("decorations no longer block clicks: " + cleared);
    }

    /// <summary>Puts the panel's own buttons on top of the pages.</summary>
    private static void BringButtonsToFront(GameObject panel, StringBuilder log)
    {
        int moved = 0;

        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            if (button == null || button.transform.parent != panel.transform)
                continue;

            button.transform.SetAsLastSibling();
            EditorUtility.SetDirty(button);
            moved++;
        }

        log.AppendLine("panel buttons drawn on top: " + moved);
    }

    /// <summary>
    /// Shows or hides the tutorial panel in the EDITOR so you can look at it
    /// without pressing play. It is only a preview - do not save the scene
    /// while it is showing, or the tutorial will be open in the menu.
    /// </summary>
    [MenuItem("Tools/UI/Preview Tutorial Panel (editor only)", priority = 62)]
    private static void PreviewPanel()
    {
        GameObject panel = FindObject(PanelName, new StringBuilder());

        if (panel == null)
        {
            Debug.LogWarning("TutorialSetup: no '" + PanelName + "' in the scene.");
            return;
        }

        panel.SetActive(!panel.activeSelf);

        // Bring the pages on too, otherwise a preview only shows page one.
        if (panel.activeSelf)
        {
            foreach (Transform child in panel.transform)
            {
                string name = child.name.Trim();

                if (name.StartsWith(PagePrefix) && name.Length > PagePrefix.Length &&
                    char.IsDigit(name[PagePrefix.Length]))
                {
                    child.gameObject.SetActive(true);
                }
            }
        }

        Debug.Log("TutorialSetup: '" + PanelName + "' is now " +
            (panel.activeSelf ? "VISIBLE" : "hidden") +
            " in the editor. Do not save the scene while it is visible - the " +
            "tutorial panel is meant to start hidden.");
    }

    private static Button CreateMenuButton(
        GameObject menuPanel, TutorialMenu tutorial, StringBuilder log)
    {
        Button source = FindButton(menuPanel, "options");

        if (source == null)
            source = FindButton(menuPanel, "play");

        if (source == null)
        {
            log.AppendLine("   no existing menu button to copy");
            return null;
        }

        GameObject copy = Object.Instantiate(source.gameObject,
            source.transform.parent);

        copy.name = "Tutorial";

        TMP_Text label = copy.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
            label.text = "Tutorial";

        RectTransform sourceRect = source.GetComponent<RectTransform>();
        RectTransform copyRect = copy.GetComponent<RectTransform>();

        if (sourceRect != null && copyRect != null)
        {
            float height = sourceRect.sizeDelta.y > 1f
                ? sourceRect.sizeDelta.y
                : 30f;

            copyRect.anchoredPosition =
                sourceRect.anchoredPosition + new Vector2(0f, -(height + 36f));
        }

        Button button = copy.GetComponent<Button>();

        if (button != null)
            Wire(button, tutorial.OpenTutorial);

        PlaceBelowOtherButtons(button, menuPanel, log);

        return button;
    }

    private static void Wire(Button button, UnityAction action)
    {
        if (button == null)
            return;

        while (button.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(button.onClick, 0);

        button.onClick.RemoveAllListeners();

        if (action != null)
            UnityEventTools.AddPersistentListener(button.onClick, action);

        EditorUtility.SetDirty(button);
    }

    private static string ButtonCalls(Button button)
    {
        StringBuilder calls = new StringBuilder();

        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            Object target = button.onClick.GetPersistentTarget(i);

            calls.Append(target != null ? target.GetType().Name : "null")
                .Append('.')
                .Append(button.onClick.GetPersistentMethodName(i))
                .Append(' ');
        }

        return calls.ToString().Trim();
    }

    private static bool HasButton(GameObject root, string nameStart)
    {
        return FindButton(root, nameStart) != null;
    }

    private static Button FindButton(GameObject root, string nameStart)
    {
        if (root == null)
            return null;

        string wanted = nameStart.ToLowerInvariant();

        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            if (button.name.Trim().ToLowerInvariant().StartsWith(wanted))
                return button;
        }

        return null;
    }

    private static GameObject FindObject(string name, StringBuilder log)
    {
        foreach (GameObject root in
            SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform child in
                root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Trim() == name)
                    return child.gameObject;
            }
        }

        return null;
    }

    private static GameObject FindCommonAncestorOfTitles(StringBuilder log)
    {
        Transform found = null;

        foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include))
        {
            if (!text.name.ToLowerInvariant().Contains("tutorialtitle"))
                continue;

            Transform candidate = text.transform;

            // Walk up to a parent that looks like a page container.
            while (candidate.parent != null &&
                candidate.parent.name.ToLowerInvariant().Contains("page"))
            {
                candidate = candidate.parent;
            }

            found = candidate.parent != null ? candidate.parent : candidate;
            break;
        }

        return found != null ? found.gameObject : null;
    }
}
