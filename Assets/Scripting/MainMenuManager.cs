
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Gameplay";

    [Header("Settings UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeValue;

    [Header("Permanent Shop Prices (Emergency Kits)")]
    [SerializeField] private int hardHatPrice = 5;
    [SerializeField] private int cramponsPrice = 5;
    [SerializeField] private int oxygenTankPrice = 8;

    private bool shopOpen;
    private string shopMessage = "";

    private Texture2D backgroundTexture;
    private Texture2D dimTexture;

    private EventSystem eventSystem;
    private bool originalEventSystemState;

    private void Start()
    {
        Time.timeScale = 1f;
        AudioListener.volume = 1f;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        shopOpen = false;

        backgroundTexture = CreateTexture(
            new Color(0.10f, 0.12f, 0.16f, 1f));

        dimTexture = CreateTexture(
            new Color(0f, 0f, 0f, 0.65f));

        eventSystem = EventSystem.current;

        if (eventSystem != null)
            originalEventSystemState = eventSystem.enabled;

        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.wholeNumbers = false;

            volumeSlider.SetValueWithoutNotify(1f);
            volumeSlider.onValueChanged.AddListener(SetVolume);

            UpdateVolumeLabel(1f);
        }
        else
        {
            UpdateVolumeLabel(1f);
        }
    }

    private void OnDestroy()
    {
        if (volumeSlider != null)
            volumeSlider.onValueChanged.RemoveListener(SetVolume);

        if (eventSystem != null)
            eventSystem.enabled = originalEventSystemState;

        if (backgroundTexture != null)
            Destroy(backgroundTexture);

        if (dimTexture != null)
            Destroy(dimTexture);
    }

    private Texture2D CreateTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    public void PlayGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Gameplay");
    }

    public void OpenShop()
    {
        shopOpen = true;
        shopMessage = "";

        // The shop uses OnGUI, so Unity's normal UI buttons
        // must not receive clicks while it is open.
        if (eventSystem != null)
            eventSystem.enabled = false;
    }

    public void CloseShop()
    {
        shopOpen = false;

        if (eventSystem != null)
            eventSystem.enabled = originalEventSystemState;
    }

    private void OnGUI()
    {
        float scale = Mathf.Min(
            Screen.width / 1280f,
            Screen.height / 720f);

        scale = Mathf.Max(scale, 0.75f);

        Matrix4x4 oldMatrix = GUI.matrix;

        GUI.matrix = Matrix4x4.TRS(
            Vector3.zero,
            Quaternion.identity,
            new Vector3(scale, scale, 1f));

        float w = Screen.width / scale;
        float h = Screen.height / scale;

        GUIStyle currencyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        GUI.Label(
            new Rect(20f, 20f, 350f, 35f),
            "Emergency Kits: " + GameProgress.Currency,
            currencyStyle);

        if (!shopOpen)
        {
            if (GUI.Button(
                new Rect(w - 190f, 20f, 170f, 55f),
                "SHOP"))
            {
                OpenShop();
            }

            GUI.matrix = oldMatrix;
            return;
        }

        // Dim the entire main menu behind the shop.
        GUI.DrawTexture(
            new Rect(0f, 0f, w, h),
            dimTexture);

        float panelW = 620f;
        float panelH = 480f;
        float x = (w - panelW) / 2f;
        float y = (h - panelH) / 2f;

        // Draw a fully opaque shop background.
        GUIStyle panelStyle = new GUIStyle(GUI.skin.box)
        {
            normal =
            {
                background = backgroundTexture,
                textColor = Color.white
            },
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
            padding = new RectOffset(10, 10, 12, 10)
        };

        GUI.Box(
            new Rect(x, y, panelW, panelH),
            "EMERGENCY SHOP",
            panelStyle);

        GUIStyle headingStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        GUI.Label(
            new Rect(x + 20f, y + 42f, panelW - 40f, 35f),
            "Balance: " + GameProgress.Currency +
            " emergency kits",
            headingStyle);

        DrawShopItem(
            "Hard Hat",
            "Earthquakes take longer to kill you.",
            "HardHat",
            hardHatPrice,
            x,
            y + 90f);

        DrawShopItem(
            "Crampons",
            "Reduces tornado movement slowdown.",
            "Crampons",
            cramponsPrice,
            x,
            y + 175f);

        DrawShopItem(
            "O2 Tank",
            "Adds oxygen while underwater.",
            "OxygenTank",
            oxygenTankPrice,
            x,
            y + 260f);

        GUIStyle messageStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        GUI.Label(
            new Rect(x + 20f, y + 350f, panelW - 40f, 30f),
            shopMessage,
            messageStyle);

        if (GUI.Button(
            new Rect(x + 190f, y + 405f, 240f, 50f),
            "CLOSE SHOP"))
        {
            CloseShop();
        }

        GUI.matrix = oldMatrix;
    }

    private void DrawShopItem(
        string title,
        string description,
        string id,
        int price,
        float x,
        float y)
    {
        GUIStyle itemStyle = new GUIStyle(GUI.skin.box)
        {
            normal =
            {
                background = backgroundTexture,
                textColor = Color.white
            }
        };

        GUI.Box(
            new Rect(x + 20f, y, 580f, 75f),
            GUIContent.none,
            itemStyle);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        GUIStyle descriptionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            wordWrap = true,
            normal = { textColor = Color.white }
        };

        GUI.Label(
            new Rect(x + 35f, y + 8f, 350f, 28f),
            title,
            titleStyle);

        GUI.Label(
            new Rect(x + 35f, y + 36f, 350f, 32f),
            description,
            descriptionStyle);

        bool owned = GameProgress.IsOwned(id);

        string buttonText = owned
            ? "OWNED"
            : "BUY " + price;

        GUI.enabled =
            !owned && GameProgress.Currency >= price;

        if (GUI.Button(
            new Rect(x + 420f, y + 18f, 155f, 42f),
            buttonText))
        {
            if (GameProgress.TryBuyUpgrade(id, price))
            {
                shopMessage =
                    title + " purchased permanently!";
            }
            else
            {
                shopMessage =
                    "Not enough emergency kits.";
            }
        }

        GUI.enabled = true;
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void SetVolume(float volume)
    {
        AudioListener.volume = volume;
        UpdateVolumeLabel(volume);
    }

    private void UpdateVolumeLabel(float volume)
    {
        if (volumeValue != null)
        {
            int percentage = Mathf.RoundToInt(volume * 100f);
            volumeValue.text = percentage + "%";
        }
    }

    public void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        Debug.Log("Quit requested. This works in a built game.");
#endif
    }
}
