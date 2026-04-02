using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MenuManager : MonoBehaviour
{
    public GameObject OverlayWindow;
    public GameObject OverlayMenu;
    public Button PlayPause;
    public Button Stop;
    public TMP_Dropdown speedSelector;
    public TMP_Text loadingText;
    public Sprite ic_home;
    public Sprite ic_settings;
    public Sprite ic_play;
    public Sprite ic_pause;
    public Sprite ic_upload;
    public Sprite ic_reset;

    void Start()
    {
        SetInitMode();
        OverlayMenu.SetActive(false);

        // Hide built-in playback controls — these are managed by the host web page via JS API
        HidePlaybackUI();
    }

    /// <summary>
    /// Deactivate all built-in overlay UI elements so the Unity canvas shows
    /// only the game rendering.  Playback is controlled entirely by the host
    /// web page through HandleJSMessages / JS API.
    /// </summary>
    private void HidePlaybackUI()
    {
        PlayPause.gameObject.SetActive(false);
        Stop.gameObject.SetActive(false);
        speedSelector.gameObject.SetActive(false);

        string[] hideByName = { "TimeText", "MenuButton", "RightPanel", "Fps" };
        foreach (var name in hideByName)
        {
            var go = GameObject.Find(name);
            if (go != null) go.SetActive(false);
        }
    }

    public void SetInitMode()
    {
        loadingText.gameObject.SetActive(false);
    }

    public void SetLoadingText(int step,float progress=0f)
    {
        loadingText.gameObject.SetActive(true);
        if (progress != 0f)
            loadingText.text = $"Loading {progress:P1}";
        else
            loadingText.text = $"Loading ({step}/4)";
    }

    public void SetPlayMode()
    {
        // No-op: playback UI is hidden; state is reported to JS via NotifyPlaybackState
    }

    public void SetPauseMode()
    {
        // No-op: playback UI is hidden; state is reported to JS via NotifyPlaybackState
    }

    public void SetReadyMode()
    {
        loadingText.gameObject.SetActive(false);
    }

    public void DisablePlay()
    {
        // No-op: playback UI is hidden
    }


    public void ShowWindow(string fumen)
    {
        OverlayWindow.transform.Find("FumenView").GetComponent<maihighlight>().UpdateHighlight(fumen);
        OverlayWindow.SetActive(true);
    }

    public void OnMenuButtonClick()
    {
        if(OverlayMenu.activeInHierarchy)
            OverlayMenu.SetActive(false);
        else
            OverlayMenu.SetActive(true);
    }
}
