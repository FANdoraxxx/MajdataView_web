using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;

public class HandleJSMessages : MonoBehaviour
{
    public GameMainManager gameMainManager;

    [DllImport("__Internal")]
    private static extern void UnityLoaded();

    [DllImport("__Internal")]
    private static extern void ReportPlaybackState(string state, float time, float duration);

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

    private void EnsureGameMainManager()
    {
        if (gameMainManager == null)
            gameMainManager = GameObject.Find("GameMain").GetComponent<GameMainManager>();
    }

    /// <summary>
    /// Start or resume playback. Callable from JS via SendMessage.
    /// </summary>
    public void JSPlay()
    {
        EnsureGameMainManager();
        if (!gameMainManager.timeProvider.isStart)
            gameMainManager.OnPlayPauseButtonClick();
    }

    /// <summary>
    /// Pause playback. Callable from JS via SendMessage.
    /// </summary>
    public void JSPause()
    {
        EnsureGameMainManager();
        if (gameMainManager.timeProvider.isStart)
            gameMainManager.OnPlayPauseButtonClick();
    }

    /// <summary>
    /// Toggle play/pause. Callable from JS via SendMessage.
    /// </summary>
    public void JSPlayPause()
    {
        EnsureGameMainManager();
        gameMainManager.OnPlayPauseButtonClick();
    }

    /// <summary>
    /// Stop playback and reset. Callable from JS via SendMessage.
    /// </summary>
    public void JSStop()
    {
        EnsureGameMainManager();
        gameMainManager.OnStopButtonClick();
    }

    /// <summary>
    /// Seek to the given time in seconds (as a string). Callable from JS via SendMessage.
    /// </summary>
    public void JSSeek(string timeStr)
    {
        EnsureGameMainManager();
        if (!float.TryParse(timeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float time))
        {
            Debug.LogWarning("JSSeek: invalid time value: " + timeStr);
            return;
        }
        bool wasPlaying = gameMainManager.timeProvider.isStart;
        gameMainManager.OnStopButtonClick();
        gameMainManager.startTime = time;
        gameMainManager.timeProvider.AudioTime = time;
        if (wasPlaying)
            gameMainManager.Play();
    }

    /// <summary>
    /// Query current playback state. Fires window.onPlaybackState(state, time, duration) callback.
    /// Callable from JS via SendMessage.
    /// </summary>
    public void JSGetPlaybackState()
    {
        EnsureGameMainManager();
        string state = gameMainManager.GetPlaybackState();
        float time = gameMainManager.timeProvider.AudioTime;
        var bgm = gameMainManager.timeProvider.bgm;
        float duration = (bgm != null && bgm.clip != null) ? bgm.clip.length : 0f;
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            try { ReportPlaybackState(state, time, duration); }
            catch (Exception e) { Debug.Log("ReportPlaybackState() failed: " + e.Message); }
        }
        else
        {
            Debug.Log($"PlaybackState: {state}, time: {time}, duration: {duration}");
        }
    }
}