using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class HandleJSMessages : MonoBehaviour
{
    public GameMainManager gameMainManager;

    [DllImport("__Internal")]
    private static extern void UnityLoaded();

    [DllImport("__Internal")]
    private static extern void NotifyPlaybackState(string state, float time, float duration);

    [DllImport("__Internal")]
    private static extern void NotifyChartInfo(string json);

    [DllImport("__Internal")]
    private static extern void NotifyNoteCount(string json);

    [DllImport("__Internal")]
    private static extern void NotifyComboStatus(string json);

    [DllImport("__Internal")]
    private static extern void NotifySettings(string json);

    [DllImport("__Internal")]
    private static extern void NotifyChartReloaded(bool success);

    private void Awake()
    {
        Application.targetFrameRate = 5;
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            WebGLInput.captureAllKeyboardInput = false;
            Debug.Log("HandleJSMessages Activated");//look for this message in the browser to ensure its working, delete before production
            DontDestroyOnLoad(this);
            try
            {
                UnityLoaded();
            }
            catch (Exception e)
            {
                Debug.Log("UnityLoaded() failed: " + e.Message);
            }
        }

    }

#if UNITY_EDITOR
    public void Start()
    {
        StartCoroutine(startAfter());
    }
    IEnumerator startAfter()
    {
        yield return new WaitForSeconds(1);
        Application.targetFrameRate = -1;
        var apiroot = "https://majdata.net/api3/api/maichart/";
        var id = "2d4599de-6cc2-4572-ae84-78a14767614e";
        gameMainManager = GameObject.Find("GameMain").GetComponent<GameMainManager>();

        gameMainManager.WebLoad(apiroot + id + "/chart" ,
            apiroot + id +  "/image?fullImage=true",
            apiroot + id + "/Track",
            apiroot + id + "/Video",
            3);
    }
#endif

    private GameMainManager GetGameMainManager()
    {
        if (gameMainManager == null)
            gameMainManager = GameObject.Find("GameMain").GetComponent<GameMainManager>();
        return gameMainManager;
    }

    private SettingsManager GetSettingsManager()
    {
        var mgr = GetGameMainManager();
        return mgr != null ? mgr.settings : null;
    }

    private ObjectCounter GetObjectCounter()
    {
        var mgr = GetGameMainManager();
        return mgr != null ? mgr.objectCounter : null;
    }

    /// <summary>
    /// Receive message from the nextjs app that has webgl-nextjs package
    /// </summary>
    /// <param name="message"></param>
    public void ReceiveMessage(string message)
    {
        Application.targetFrameRate = -1;
        var parts = message.Split('\n');//type\ncontent
        var maidata = parts[0];
        var track = parts[1];
        var bg = parts[2];
        var mv = parts[3];
        var level = parts[4];
        Debug.Log("level:"+level);
        gameMainManager = GameObject.Find("GameMain").GetComponent<GameMainManager>();

        gameMainManager.WebLoad(maidata,
            bg,
            track,
            mv,
            int.Parse(level[2].ToString()));
        
    }

    // ==================== Playback Control API ====================
    // All methods below can be called from JavaScript via:
    //   unityInstance.SendMessage("HandleJSMessages", "MethodName")
    //   unityInstance.SendMessage("HandleJSMessages", "MethodName", value)

    /// <summary>
    /// Toggle play/pause. If not started, begins playback.
    /// If playing, pauses. If paused, resumes.
    /// JS: unityInstance.SendMessage("HandleJSMessages", "PlayPause")
    /// </summary>
    public void PlayPause()
    {
        var mgr = GetGameMainManager();
        if (mgr != null)
            mgr.OnPlayPauseButtonClick();
        SendPlaybackState();
    }

    /// <summary>
    /// Start or resume playback. Does nothing if already playing.
    /// JS: unityInstance.SendMessage("HandleJSMessages", "Play")
    /// </summary>
    public void Play()
    {
        var mgr = GetGameMainManager();
        if (mgr == null) return;
        if (!mgr.IsPlaying())
            mgr.OnPlayPauseButtonClick();
        SendPlaybackState();
    }

    /// <summary>
    /// Pause playback. Does nothing if not playing.
    /// JS: unityInstance.SendMessage("HandleJSMessages", "Pause")
    /// </summary>
    public void Pause()
    {
        var mgr = GetGameMainManager();
        if (mgr == null) return;
        if (mgr.IsPlaying())
            mgr.OnPlayPauseButtonClick();
        SendPlaybackState();
    }

    /// <summary>
    /// Stop playback and reset to beginning.
    /// JS: unityInstance.SendMessage("HandleJSMessages", "Stop")
    /// </summary>
    public void Stop()
    {
        var mgr = GetGameMainManager();
        if (mgr != null)
            mgr.OnStopButtonClick();
        SendPlaybackState();
    }

    /// <summary>
    /// Seek to a specific time in seconds. If currently playing, restarts from new position.
    /// JS: unityInstance.SendMessage("HandleJSMessages", "Seek", "12.5")
    /// </summary>
    /// <param name="timeStr">Time in seconds as a string (e.g. "12.5")</param>
    public void Seek(string timeStr)
    {
        var mgr = GetGameMainManager();
        if (mgr == null) return;
        if (float.TryParse(timeStr, System.Globalization.NumberStyles.Float, 
            System.Globalization.CultureInfo.InvariantCulture, out float time))
        {
            mgr.SeekTo(time);
        }
        else
        {
            Debug.LogError("HandleJSMessages.Seek: invalid time string: " + timeStr);
        }
        SendPlaybackState();
    }

    /// <summary>
    /// Request the current playback state. Triggers window.onPlaybackState(state, time, duration) callback.
    /// state: "playing", "paused", "stopped", or "loading"
    /// JS: unityInstance.SendMessage("HandleJSMessages", "GetPlaybackState")
    /// </summary>
    public void GetPlaybackState()
    {
        SendPlaybackState();
    }

    /// <summary>
    /// Re-download the maidata from the last chart URL and re-serialize the current level.
    /// Stops playback if active. Triggers window.onChartReloaded(success) callback when done.
    /// JS: unityInstance.SendMessage("HandleJSMessages", "ReloadChart")
    /// </summary>
    public void ReloadChart()
    {
        var mgr = GetGameMainManager();
        if (mgr == null) return;
        mgr.ReloadChart((success) =>
        {
            try { NotifyChartReloaded(success); }
            catch (Exception e) { Debug.LogError("NotifyChartReloaded() failed: " + e.Message); }
        });
    }

    /// <summary>
    /// Update chart data from raw maidata text. Stops playback if active.
    /// Format: "level\nmaidataText" where level is 0-6 (or -1 to keep current level).
    /// Triggers window.onChartReloaded(success) callback.
    /// JS: unityInstance.SendMessage("HandleJSMessages", "UpdateChart", "3\n&title=...")
    /// </summary>
    public void UpdateChart(string levelAndText)
    {
        var mgr = GetGameMainManager();
        if (mgr == null) return;

        // Split on first newline: "level\nrest_of_maidata"
        int newlineIdx = levelAndText.IndexOf('\n');
        if (newlineIdx < 0)
        {
            Debug.LogError("HandleJSMessages.UpdateChart: expected format 'level\\nmaidataText'");
            try { NotifyChartReloaded(false); }
            catch (Exception e) { Debug.LogError("NotifyChartReloaded() failed: " + e.Message); }
            return;
        }

        string levelStr = levelAndText.Substring(0, newlineIdx);
        string maidataText = levelAndText.Substring(newlineIdx + 1);

        int level = -1;
        // Accept both plain integer ("3") and "lvX" format ("lv3") used by ReceiveMessage
        levelStr = levelStr.Trim();
        if (!int.TryParse(levelStr, out level))
        {
            if (levelStr.StartsWith("lv") && levelStr.Length > 2 &&
                int.TryParse(levelStr.Substring(2), out level))
            {
                // parsed "lv3" → 3
            }
            else
            {
                Debug.LogError("HandleJSMessages.UpdateChart: invalid level: " + levelStr);
                try { NotifyChartReloaded(false); }
                catch (Exception e) { Debug.LogError("NotifyChartReloaded() failed: " + e.Message); }
                return;
            }
        }

        bool success = mgr.UpdateChartData(maidataText, level);
        try { NotifyChartReloaded(success); }
        catch (Exception e) { Debug.LogError("NotifyChartReloaded() failed: " + e.Message); }
    }

    private void SendPlaybackState()
    {
        try
        {
            var mgr = GetGameMainManager();
            string state;
            float time = 0f;
            float duration = 0f;

            if (mgr == null)
            {
                state = "stopped";
            }
            else
            {
                time = mgr.GetCurrentTime();
                duration = mgr.GetDuration();

                if (!mgr.IsReady())
                    state = "loading";
                else if (mgr.IsPlaying())
                    state = "playing";
                else if (mgr.IsPaused())
                    state = "paused";
                else
                    state = "stopped";
            }

            NotifyPlaybackState(state, time, duration);
        }
        catch (Exception e)
        {
            Debug.LogError("NotifyPlaybackState() failed: " + e.Message);
        }
    }

    // ==================== Speed / Settings API ====================

    /// <summary>
    /// Set audio playback speed (0.25 – 2.0).  Applies on next Play().
    /// JS: unityInstance.SendMessage("HandleJSMessages", "SetSpeed", "0.75")
    /// </summary>
    public void SetSpeed(string speedStr)
    {
        var mgr = GetGameMainManager();
        if (mgr == null) return;
        if (float.TryParse(speedStr, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float speed))
        {
            mgr.audioSpeed = Mathf.Clamp(speed, 0.25f, 2f);
        }
        else
        {
            Debug.LogError("HandleJSMessages.SetSpeed: invalid value: " + speedStr);
        }
    }

    /// <summary>
    /// Set note falling speed (display value, e.g. "7.0"). Slider internally stores value * 10.
    /// JS: unityInstance.SendMessage("HandleJSMessages", "SetNoteSpeed", "7.0")
    /// </summary>
    public void SetNoteSpeed(string valueStr)
    {
        var settings = GetSettingsManager();
        if (settings == null) return;
        if (float.TryParse(valueStr, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value))
        {
            settings.GetSlider("NoteSpeed").value = value * 10f;
            settings.UpdateSliders();
        }
        else
        {
            Debug.LogError("HandleJSMessages.SetNoteSpeed: invalid value: " + valueStr);
        }
    }

    /// <summary>
    /// Set touch note speed (display value, e.g. "7.5"). Slider internally stores value * 10.
    /// JS: unityInstance.SendMessage("HandleJSMessages", "SetTouchSpeed", "7.5")
    /// </summary>
    public void SetTouchSpeed(string valueStr)
    {
        var settings = GetSettingsManager();
        if (settings == null) return;
        if (float.TryParse(valueStr, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value))
        {
            settings.GetSlider("TouchSpeed").value = value * 10f;
            settings.UpdateSliders();
        }
        else
        {
            Debug.LogError("HandleJSMessages.SetTouchSpeed: invalid value: " + valueStr);
        }
    }

    /// <summary>
    /// Set background cover opacity (0.0 – 1.0). Slider internally stores value * 10.
    /// JS: unityInstance.SendMessage("HandleJSMessages", "SetBGCover", "0.5")
    /// </summary>
    public void SetBGCover(string valueStr)
    {
        var settings = GetSettingsManager();
        if (settings == null) return;
        if (float.TryParse(valueStr, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value))
        {
            settings.GetSlider("BGCover").value = value * 10f;
            settings.UpdateSliders();
        }
        else
        {
            Debug.LogError("HandleJSMessages.SetBGCover: invalid value: " + valueStr);
        }
    }

    /// <summary>
    /// Set audio offset in milliseconds (e.g. "50" for 50 ms).
    /// JS: unityInstance.SendMessage("HandleJSMessages", "SetOffset", "50")
    /// </summary>
    public void SetOffset(string valueStr)
    {
        var settings = GetSettingsManager();
        if (settings == null) return;
        if (float.TryParse(valueStr, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value))
        {
            settings.GetSlider("Offset").value = value;
            settings.UpdateSliders();
        }
        else
        {
            Debug.LogError("HandleJSMessages.SetOffset: invalid value: " + valueStr);
        }
    }

    /// <summary>
    /// Set volume for a specific audio channel in dB.
    /// channelAndValue format: "channelName:dBValue", e.g. "BGM:-5" or "Answer:0"
    /// Valid channels: BGM, Answer, Judge, Slide, Break, EX, Touch, Hanabi, Others
    /// JS: unityInstance.SendMessage("HandleJSMessages", "SetVolume", "BGM:-5")
    /// </summary>
    public void SetVolume(string channelAndValue)
    {
        var settings = GetSettingsManager();
        if (settings == null) return;
        var parts = channelAndValue.Split(':');
        if (parts.Length != 2)
        {
            Debug.LogError("HandleJSMessages.SetVolume: expected format 'channel:dB', got: " + channelAndValue);
            return;
        }
        string channel = parts[0];
        if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float db))
        {
            try
            {
                settings.GetSlider(channel).value = db;
                settings.UpdateVolume();
            }
            catch (Exception e)
            {
                Debug.LogError("HandleJSMessages.SetVolume: failed for channel '" + channel + "': " + e.Message);
            }
        }
        else
        {
            Debug.LogError("HandleJSMessages.SetVolume: invalid dB value: " + parts[1]);
        }
    }

    /// <summary>
    /// Toggle combo display on ("true") or off ("false").
    /// JS: unityInstance.SendMessage("HandleJSMessages", "ToggleCombo", "true")
    /// </summary>
    public void ToggleCombo(string enableStr)
    {
        var settings = GetSettingsManager();
        if (settings == null) return;
        bool enable = enableStr == "true" || enableStr == "1";
        settings.combo = enable;
        var counter = GetObjectCounter();
        if (counter != null)
            counter.ComboSetActive(enable);
    }

    /// <summary>
    /// Set combo indicator mode by name.
    /// Valid values: "None","Combo","ScoreClassic","AchievementClassic","AchievementDownClassic",
    ///              "AchievementDeluxe","AchievementDownDeluxe","ScoreDeluxe",
    ///              "CScoreDedeluxe","CScoreDownDedeluxe"
    /// JS: unityInstance.SendMessage("HandleJSMessages", "SetComboIndicator", "AchievementDeluxe")
    /// </summary>
    public void SetComboIndicator(string modeName)
    {
        var counter = GetObjectCounter();
        if (counter == null) return;
        try
        {
            var mode = (EditorComboIndicator)Enum.Parse(typeof(EditorComboIndicator), modeName, true);
            counter.ComboSetActive(mode);
        }
        catch (Exception e)
        {
            Debug.LogError("HandleJSMessages.SetComboIndicator: invalid mode '" + modeName + "': " + e.Message);
        }
    }

    // ==================== Info Query API ====================

    /// <summary>
    /// Get chart metadata (title, artist, designer, levels). Triggers window.onChartInfo(json) callback.
    /// JS: unityInstance.SendMessage("HandleJSMessages", "QueryChartInfo")
    /// </summary>
    public void QueryChartInfo()
    {
        try
        {
            var levelsArr = "";
            for (int i = 0; i < SimaiProcess.levels.Length; i++)
            {
                if (i > 0) levelsArr += ",";
                var lv = SimaiProcess.levels[i] ?? "";
                levelsArr += "\"" + EscapeJsonString(lv) + "\"";
            }

            string json = "{" +
                "\"title\":\"" + EscapeJsonString(SimaiProcess.title ?? "") + "\"," +
                "\"artist\":\"" + EscapeJsonString(SimaiProcess.artist ?? "") + "\"," +
                "\"designer\":\"" + EscapeJsonString(SimaiProcess.designer ?? "") + "\"," +
                "\"first\":" + SimaiProcess.first.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                "\"levels\":[" + levelsArr + "]," +
                "\"noteCount\":" + SimaiProcess.notelist.Count +
            "}";

            NotifyChartInfo(json);
        }
        catch (Exception e)
        {
            Debug.LogError("QueryChartInfo() failed: " + e.Message);
        }
    }

    /// <summary>
    /// Get total note counts by type. Triggers window.onNoteCount(json) callback.
    /// Returns expected totals from the chart (Sum fields on ObjectCounter).
    /// JS: unityInstance.SendMessage("HandleJSMessages", "QueryNoteCount")
    /// </summary>
    public void QueryNoteCount()
    {
        try
        {
            var counter = GetObjectCounter();
            string json;
            if (counter != null)
            {
                json = "{" +
                    "\"tap\":" + counter.tapSum + "," +
                    "\"hold\":" + counter.holdSum + "," +
                    "\"slide\":" + counter.slideSum + "," +
                    "\"touch\":" + counter.touchSum + "," +
                    "\"break\":" + counter.breakSum + "," +
                    "\"total\":" + (counter.tapSum + counter.holdSum + counter.slideSum + counter.touchSum + counter.breakSum) +
                "}";
            }
            else
            {
                json = "{\"tap\":0,\"hold\":0,\"slide\":0,\"touch\":0,\"break\":0,\"total\":0}";
            }

            NotifyNoteCount(json);
        }
        catch (Exception e)
        {
            Debug.LogError("QueryNoteCount() failed: " + e.Message);
        }
    }

    /// <summary>
    /// Get current combo/score/progress status. Triggers window.onComboStatus(json) callback.
    /// Returns current hit counts and total counts.
    /// JS: unityInstance.SendMessage("HandleJSMessages", "QueryComboStatus")
    /// </summary>
    public void QueryComboStatus()
    {
        try
        {
            var counter = GetObjectCounter();
            string json;
            if (counter != null)
            {
                int combo = counter.tapCount + counter.holdCount + counter.slideCount + counter.touchCount + counter.breakCount;
                int total = counter.tapSum + counter.holdSum + counter.slideSum + counter.touchSum + counter.breakSum;
                json = "{" +
                    "\"combo\":" + combo + "," +
                    "\"total\":" + total + "," +
                    "\"tapCount\":" + counter.tapCount + "," +
                    "\"holdCount\":" + counter.holdCount + "," +
                    "\"slideCount\":" + counter.slideCount + "," +
                    "\"touchCount\":" + counter.touchCount + "," +
                    "\"breakCount\":" + counter.breakCount +
                "}";
            }
            else
            {
                json = "{\"combo\":0,\"total\":0,\"tapCount\":0,\"holdCount\":0,\"slideCount\":0,\"touchCount\":0,\"breakCount\":0}";
            }

            NotifyComboStatus(json);
        }
        catch (Exception e)
        {
            Debug.LogError("QueryComboStatus() failed: " + e.Message);
        }
    }

    /// <summary>
    /// Get current settings values. Triggers window.onSettings(json) callback.
    /// JS: unityInstance.SendMessage("HandleJSMessages", "QuerySettings")
    /// </summary>
    public void QuerySettings()
    {
        try
        {
            var mgr = GetGameMainManager();
            var settings = GetSettingsManager();
            string json;
            if (settings != null && mgr != null)
            {
                json = "{" +
                    "\"noteSpeed\":" + (settings.GetSlider("NoteSpeed").value / 10f).ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                    "\"touchSpeed\":" + (settings.GetSlider("TouchSpeed").value / 10f).ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                    "\"bgCover\":" + (settings.GetSlider("BGCover").value / 10f).ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                    "\"offset\":" + settings.GetSlider("Offset").value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                    "\"speed\":" + mgr.audioSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                    "\"combo\":" + (settings.combo ? "true" : "false") +
                "}";
            }
            else
            {
                json = "{\"noteSpeed\":0,\"touchSpeed\":0,\"bgCover\":0,\"offset\":0,\"speed\":1,\"combo\":false}";
            }

            NotifySettings(json);
        }
        catch (Exception e)
        {
            Debug.LogError("QuerySettings() failed: " + e.Message);
        }
    }

    /// <summary>
    /// Escape special characters for JSON string values.
    /// </summary>
    private static string EscapeJsonString(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}