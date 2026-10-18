
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Disable These Controls On Death")]
    [Tooltip("Drag your camera-look script and any other player control scripts here.")]
    [SerializeField] private Behaviour[] controlsToDisable;

    [Header("Death Screen")]
    [SerializeField] private int titleFontSize = 54;
    [SerializeField] private int buttonFontSize = 26;

    private bool gameOverShown;
    private PlayerInventory inventory;

    /// <summary>Kits earned in this run only - what the death screen reports.</summary>
    private int kitsThisRun;

    /// <summary>Kits saved this run, before they are banked into the total.</summary>
    public int KitsThisRun => kitsThisRun;

    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle infoStyle;
    private GUIStyle buttonStyle;

    private void Start()
    {
        inventory = FindFirstObjectByType<PlayerInventory>();
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (gameOverShown)
            return;

        EarthquakeEvent earthquake =
            FindFirstObjectByType<EarthquakeEvent>();

        FloodEvent flood =
            FindFirstObjectByType<FloodEvent>();

        bool lost =
            (earthquake != null && earthquake.PlayerLost) ||
            (flood != null && flood.PlayerLost);

        if (!lost)
            return;

        gameOverShown = true;

        if (inventory == null)
            inventory = FindFirstObjectByType<PlayerInventory>();

        int kitsEarned =
            inventory != null ? inventory.MergedItemCount : 0;

        // Only this run's kits count for the death screen.
        kitsThisRun = kitsEarned;

        GameProgress.AddCurrency(kitsEarned);

        Debug.Log(
            $"Run ended. Banked {kitsEarned} kits this run. " +
            $"Total: {GameProgress.Currency}");

        // Stop gameplay and release the mouse.
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Disable explicitly assigned movement and camera controls.
        if (controlsToDisable != null)
        {
            foreach (Behaviour control in controlsToDisable)
            {
                if (control != null)
                    control.enabled = false;
            }
        }

        // Disable common first-person controls automatically.
        DisableComponentByName("PlayerMotor");
        DisableComponentByName("PlayerInputReader");
    }

    private void DisableComponentByName(string typeName)
    {
        MonoBehaviour[] components =
            FindObjectsByType<MonoBehaviour>(
                FindObjectsSortMode.None);

        foreach (MonoBehaviour component in components)
        {
            if (component != null &&
                component != this &&
                component.GetType().Name == typeName)
            {
                component.enabled = false;
            }
        }
    }

    private void EnsureStyles()
    {
        if (panelStyle != null)
            return;

        panelStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24
        };

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = titleFontSize,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        infoStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            normal = { textColor = Color.white }
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = buttonFontSize,
            fontStyle = FontStyle.Bold
        };
    }

    private void OnGUI()
    {
        if (!gameOverShown)
            return;

        EnsureStyles();

        float scale = Mathf.Min(
            Screen.width / 1280f,
            Screen.height / 720f);

        scale = Mathf.Max(0.75f, scale);

        Matrix4x4 oldMatrix = GUI.matrix;

        GUI.matrix = Matrix4x4.TRS(
            Vector3.zero,
            Quaternion.identity,
            new Vector3(scale, scale, 1f));

        float w = Screen.width / scale;
        float h = Screen.height / scale;

        // Darken the entire screen.
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(
            new Rect(0, 0, w, h),
            Texture2D.whiteTexture);
        GUI.color = Color.white;

        float panelW = Mathf.Min(650f, w - 40f);
        float panelH = 450f;

        float x = (w - panelW) / 2f;
        float y = (h - panelH) / 2f;

        GUI.Box(
            new Rect(x, y, panelW, panelH),
            GUIContent.none,
            panelStyle);

        GUI.Label(
            new Rect(x + 20f, y + 25f, panelW - 40f, 85f),
            "YOU DIED",
            titleStyle);

        GUI.Label(
            new Rect(x + 20f, y + 115f, panelW - 40f, 45f),
            "Emergency kits saved this run: " + kitsThisRun,
            infoStyle);

        if (GUI.Button(
            new Rect(x + 100f, y + 195f,
                panelW - 200f, 70f),
            "RETRY",
            buttonStyle))
        {
            RestartGame();
        }

        if (GUI.Button(
            new Rect(x + 100f, y + 290f,
                panelW - 200f, 70f),
            "MAIN MENU",
            buttonStyle))
        {
            ReturnToMenu();
        }

        GUI.matrix = oldMatrix;
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex);
    }

    private void ReturnToMenu()
    {
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
