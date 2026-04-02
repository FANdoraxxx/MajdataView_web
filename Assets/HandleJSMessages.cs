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
    private static extern void ReportPlaybackState(string state, float currentTime, float duration);

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

        ReportPlaybackStateToJs();
    }

    private bool EnsureGameMainManager()
    {
        if (gameMainManager != null)
        {
            return true;
        }

        var gameMain = GameObject.Find("GameMain");
        if (gameMain == null)
        {
            return false;
        }

        gameMainManager = gameMain.GetComponent<GameMainManager>();
        return gameMainManager != null;
    }

    private void ReportPlaybackStateToJs()
    {
        if (Application.platform != RuntimePlatform.WebGLPlayer)
        {
            return;
        }

        var state = "uninitialized";
        float currentTime = 0f;
        float duration = 0f;

        if (EnsureGameMainManager() && gameMainManager.timeProvider != null)
        {
            var timeProvider = gameMainManager.timeProvider;
            currentTime = Mathf.Max(0f, timeProvider.AudioTime);
            if (timeProvider.bgm != null && timeProvider.bgm.clip != null)
            {
                duration = timeProvider.bgm.clip.length;
            }

            if (timeProvider.isStart)
            {
                state = "playing";
            }
            else if (gameMainManager.menuManager != null &&
                     gameMainManager.menuManager.loadingText != null &&
                     gameMainManager.menuManager.loadingText.gameObject.activeSelf)
            {
                state = "loading";
            }
            else if (currentTime > 0f)
            {
                state = "paused";
            }
            else
            {
                state = "stopped";
            }
        }

        try
        {
            ReportPlaybackState(state, currentTime, duration);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public void JSPlay()
    {
        if (!EnsureGameMainManager())
        {
            ReportPlaybackStateToJs();
            return;
        }

        if (!gameMainManager.timeProvider.isStart)
        {
            gameMainManager.OnPlayPauseButtonClick();
        }

        ReportPlaybackStateToJs();
    }

    public void JSPause()
    {
        if (!EnsureGameMainManager())
        {
            ReportPlaybackStateToJs();
            return;
        }

        if (gameMainManager.timeProvider.isStart)
        {
            gameMainManager.OnPlayPauseButtonClick();
        }

        ReportPlaybackStateToJs();
    }

    public void JSPlayPause()
    {
        if (!EnsureGameMainManager())
        {
            ReportPlaybackStateToJs();
            return;
        }

        gameMainManager.OnPlayPauseButtonClick();
        ReportPlaybackStateToJs();
    }

    public void JSStop()
    {
        if (!EnsureGameMainManager())
        {
            ReportPlaybackStateToJs();
            return;
        }

        gameMainManager.OnStopButtonClick();
        ReportPlaybackStateToJs();
    }

    public void JSSeek(string seconds)
    {
        if (!EnsureGameMainManager())
        {
            ReportPlaybackStateToJs();
            return;
        }

        if (!float.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var seekTime))
        {
            Debug.Log("JSSeek ignored invalid seek time: " + seconds);
            ReportPlaybackStateToJs();
            return;
        }

        var timeProvider = gameMainManager.timeProvider;
        if (timeProvider == null)
        {
            ReportPlaybackStateToJs();
            return;
        }

        var duration = 0f;
        if (timeProvider.bgm != null && timeProvider.bgm.clip != null)
        {
            duration = timeProvider.bgm.clip.length;
        }
        seekTime = duration > 0f ? Mathf.Clamp(seekTime, 0f, duration) : Mathf.Max(0f, seekTime);
        var wasPlaying = timeProvider.isStart;
        if (wasPlaying)
        {
            timeProvider.Pause();
        }

        timeProvider.AudioTime = seekTime;
        timeProvider.playStartTime = seekTime;
        gameMainManager.startTime = seekTime;

        if (gameMainManager.bgManager != null && gameMainManager.bgManager.videoPlayer != null)
        {
            var videoTime = Mathf.Max(0f, seekTime - gameMainManager.offset);
            gameMainManager.bgManager.videoPlayer.time = videoTime;
            if (wasPlaying && gameMainManager.bgManager.videoPlayer.isPrepared)
            {
                gameMainManager.bgManager.videoPlayer.playbackSpeed = gameMainManager.audioSpeed;
                gameMainManager.bgManager.videoPlayer.Play();
            }
            else
            {
                gameMainManager.bgManager.videoPlayer.Pause();
            }
        }

        if (wasPlaying)
        {
            timeProvider.Resume();
        }

        ReportPlaybackStateToJs();
    }

    public void JSGetPlaybackState()
    {
        ReportPlaybackStateToJs();
    }
}
