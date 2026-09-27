using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MainMenu: Game entry point, settings, credits.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;

    [Header("Settings")]
    public Slider masterVolumeSlider;
    public Slider sfxVolumeSlider;
    public Slider musicVolumeSlider;
    public Toggle fullscreenToggle;
    public Dropdown qualityDropdown;
    public Dropdown resolutionDropdown;

    [Header("Buttons")]
    public Button newGameButton;
    public Button continueButton;
    public Button settingsButton;
    public Button creditsButton;
    public Button quitButton;

    void Awake()
    {
        // Setup button listeners
        newGameButton?.onClick.AddListener(OnNewGame);
        continueButton?.onClick.AddListener(OnContinue);
        settingsButton?.onClick.AddListener(OnSettings);
        creditsButton?.onClick.AddListener(OnCredits);
        quitButton?.onClick.AddListener(OnQuit);

        // Settings controls
        masterVolumeSlider?.onValueChanged.AddListener(v => AudioListener.volume = v);
        sfxVolumeSlider?.onValueChanged.AddListener(v => { /* SFX volume */ });
        musicVolumeSlider?.onValueChanged.AddListener(v => { /* Music volume */ });
        fullscreenToggle?.onValueChanged.AddListener(OnFullscreen);
        qualityDropdown?.onValueChanged.AddListener(OnQuality);
        resolutionDropdown?.onValueChanged.AddListener(OnResolution);

        // Load saved settings
        LoadSettings();

        // Show main panel
        ShowPanel(mainPanel);
    }

    void LoadSettings()
    {
        masterVolumeSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("MasterVolume", 1f));
        sfxVolumeSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("SFXVolume", 1f));
        musicVolumeSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("MusicVolume", 1f));
        fullscreenToggle?.SetIsOnWithoutNotify(PlayerPrefs.GetInt("Fullscreen", 1) == 1);
        qualityDropdown?.SetValueWithoutNotify(PlayerPrefs.GetInt("Quality", 2));

        AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        Screen.fullScreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        QualitySettings.SetQualityLevel(PlayerPrefs.GetInt("Quality", 2));
    }

    public void OnNewGame()
    {
        // Reset game state
        PlayerPrefs.DeleteKey("SaveData");
        SceneManager.LoadScene("Core"); // Main gameplay scene
    }

    public void OnContinue()
    {
        // Load saved game
        SceneManager.LoadScene("Core");
        // TODO: Load save data
    }

    public void OnSettings()
    {
        ShowPanel(settingsPanel);
    }

    public void OnCredits()
    {
        ShowPanel(creditsPanel);
    }

    public void OnBack()
    {
        ShowPanel(mainPanel);
        SaveSettings();
    }

    public void OnQuit()
    {
        SaveSettings();
#if UNITY_WEBGL
        Application.ExternalCall("window.location.reload");
#else
        Application.Quit();
#endif
    }

    void ShowPanel(GameObject panel)
    {
        mainPanel?.SetActive(panel == mainPanel);
        settingsPanel?.SetActive(panel == settingsPanel);
        creditsPanel?.SetActive(panel == creditsPanel);
    }

    void OnFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    void OnQuality(int index)
    {
        QualitySettings.SetQualityLevel(index);
    }

    void OnResolution(int index)
    {
        var resolutions = Screen.resolutions;
        if (index >= 0 && index < resolutions.Length)
        {
            var res = resolutions[index];
            Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        }
    }

    void SaveSettings()
    {
        PlayerPrefs.SetFloat("MasterVolume", masterVolumeSlider?.value ?? 1f);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolumeSlider?.value ?? 1f);
        PlayerPrefs.SetFloat("MusicVolume", musicVolumeSlider?.value ?? 1f);
        PlayerPrefs.SetInt("Fullscreen", fullscreenToggle?.isOn == true ? 1 : 0);
        PlayerPrefs.SetInt("Quality", qualityDropdown?.value ?? 2);
        PlayerPrefs.Save();
    }
}