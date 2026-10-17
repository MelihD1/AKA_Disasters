
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "oldahmad";

    [Header("Settings UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeValue;

    private void Start()
    {
        Time.timeScale = 1f;

        // Start at 100% volume.
        AudioListener.volume = 1f;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

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
    }

    public void PlayGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
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
