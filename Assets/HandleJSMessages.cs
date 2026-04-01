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
    private static extern void OnTimeUpdate(float time);

    [DllImport("__Internal")]
    private static extern void OnPlayStateChanged(int isPlaying);

    private bool lastPlayState = false;
    private float lastTimeUpdateSent = 0f;

    private GameMainManager GetManager()
    {
        if (gameMainManager == null)
            gameMainManager = GameObject.Find("GameMain").GetComponent<GameMainManager>();
        return gameMainManager;
    }

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

    private void Update()
    {
        if (Application.platform != RuntimePlatform.WebGLPlayer) return;
        var mgr = GetManager();
        if (mgr == null) return;

        // Send current time to JS at ~10 Hz to avoid flooding the JS bridge
        var now = Time.realtimeSinceStartup;
        if (now - lastTimeUpdateSent >= 0.1f)
        {
            lastTimeUpdateSent = now;
            OnTimeUpdate(mgr.timeProvider.AudioTime);
        }

        // Notify JS whenever the play/pause state changes
        bool currentPlayState = mgr.timeProvider.isStart;
        if (currentPlayState != lastPlayState)
        {
            lastPlayState = currentPlayState;
            OnPlayStateChanged(currentPlayState ? 1 : 0);
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
        var mgr = GetManager();

        mgr.WebLoad(maidata,
            bg,
            track,
            mv,
            int.Parse(level[2].ToString()));
        
    }

    /// <summary>
    /// Start or resume playback. Called from JS via SendMessage.
    /// </summary>
    public void JSPlay()
    {
        GetManager().JSPlay();
    }

    /// <summary>
    /// Pause playback. Called from JS via SendMessage.
    /// </summary>
    public void JSPause()
    {
        GetManager().JSPause();
    }

    /// <summary>
    /// Seek to a position in seconds (passed as a string, e.g. "12.5").
    /// Called from JS via SendMessage.
    /// </summary>
    /// <param name="timeStr">Target time in seconds as a string</param>
    public void JSSeek(string timeStr)
    {
        if (float.TryParse(timeStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float time))
        {
            GetManager().Seek(time);
        }
        else
        {
            Debug.LogWarning("JSSeek: invalid time string '" + timeStr + "'");
        }
    }
}