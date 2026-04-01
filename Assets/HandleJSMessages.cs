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
            Debug.Log("NotifyPlaybackState() failed: " + e.Message);
        }
    }
}