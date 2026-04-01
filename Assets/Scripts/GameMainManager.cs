using System;
using System.Runtime;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using API;

public class GameMainManager : MonoBehaviour
{
    [Header("Manager")]
    public SimaiDataLoader simailoader;
    public AudioTimeProvider timeProvider;
    public BGManager bgManager;
    public SpriteRenderer bgCover;
    public MultTouchHandler multTouchHandler;
    public ObjectCounter objectCounter;
    public Transform Notes;
    public SoundEffect SE;
    public MenuManager menuManager;
    public SettingsManager settings;
    
    [Space(10)]
    [Header("AudioRef")]
    public AudioSource bgm;

    [Space(10)]
    [Header("Settings")]
    public float startTime = 0f;
    public float audioSpeed = 1f;
    public float offset;
    
    [Space(10)]
    [Header("Debug")]
    public string editorInitPath;

    private bool inited = false;
    private int status = 0;

    // init loading & start playing method
    public void Play()
    {
        simailoader.noteSpeed = settings.noteSpeed;
        simailoader.touchSpeed = settings.touchSpeed;
        //SimaiProcess.Serialize(SimaiProcess.fumens[menuManager.level]);
        simailoader.PlayLevel(startTime);
        timeProvider.SetStartTime(startTime - offset, audioSpeed);
        objectCounter.ComboSetActive(settings.combo);
        multTouchHandler.clearSlots();
        Notes.GetComponent<PlayAllPerfect>().enabled = false;
        inited = true;
        // set btn states
        menuManager.SetPlayMode();
        var vtime = startTime - offset;
        if (vtime == 0)
        {
            bgManager.videoPlayer.playbackSpeed = audioSpeed;
            bgManager.videoPlayer.Play();
        }
    }

    // callback of play/pause button
    public void OnPlayPauseButtonClick()
    {
        if (!inited) {
            //startTime = timeProvider.AudioTime;
            Play();
            return;
        }
        if (timeProvider.isStart) {
            startTime = timeProvider.AudioTime;
            timeProvider.playStartTime = startTime;
            timeProvider.Pause();
            bgManager.videoPlayer.Pause();
            menuManager.SetPauseMode();
        } else {
            timeProvider.Resume();
            var vtime = startTime - offset;
            if (vtime == 0)
            {
                bgManager.videoPlayer.playbackSpeed = audioSpeed;
                bgManager.videoPlayer.Play();
            }
            //bgManager.videoPlayer.Play();
            menuManager.SetPlayMode();
        }
    }

    // callback of stop button
    public void OnStopButtonClick()
    {
        // hide bgcover
        bgCover.color = new Color(0f, 0f, 0f, 0f);
        // reset audiotime
        timeProvider.ResetStartTime();
        // destroy all notes
        foreach (Transform child in Notes.transform) {
            GameObject.Destroy(child.gameObject);
        }
        // re-init on next start
        inited = false;
        // reset counter
        objectCounter.Reset();
        // set btn states
        menuManager.SetReadyMode();
        bgManager.videoPlayer.Stop();
    }

    public void WebLoad(string chartpath, string bgpath, string audiopath,string videopath, int level)
    {
        StopAllCoroutines();
        OnStopButtonClick();
        timeProvider.AudioTime = 0f;
        timeProvider.playStartTime = 0f;
        menuManager.SetInitMode();
        bgManager.isAnyErr = false;
        if(videopath != null)
        {
            bgManager.videoPlayer.url = videopath;
            
        }
        status = 0;
        //���������Դ����ɺ�׼���˵�
        void checkReady()
        {
            menuManager.SetLoadingText(status);
            if(status >= 4f ) {
                menuManager.SetReadyMode();
                string fumens = SimaiProcess.fumens[level];
                if (fumens == null)
                {
                    Debug.Log("Null level!");
                    menuManager.DisablePlay();
                    return;
                }
                if (SimaiProcess.Serialize(fumens) == -1)
                {
                    menuManager.DisablePlay();
                    return;
                }
                Debug.Log("Total notes: " + SimaiProcess.notelist.Count);
                if (SimaiProcess.notelist.Count <= 0)
                {
                    Debug.Log("Empty level!");
                    menuManager.DisablePlay();
                    return;
                }
                else {
                    menuManager.SetReadyMode(); 
                }
            }
        }

        Action videoCallback = () =>
        {
            status += 1;
            checkReady();
        };
        StartCoroutine(WaitVideoPrepare(videoCallback));

        // open maidata.txt
        Action successCallback = () =>
        {
            status += 1;
            checkReady();
        };

        StartCoroutine(simailoader.initFromWeb(chartpath, successCallback));

        Action audioCallback = () =>
        {
            status += 1;
            checkReady();
        };

        Action<float> progressCallback = (float progress) =>
        {
            menuManager.SetLoadingText(status,progress);
        };

        StartCoroutine(SE.LoadWebAudio(audiopath, progressCallback, audioCallback));

        Action bgCallback = () =>
        {
            status += 1;
            checkReady();
        };

        StartCoroutine(WebLoader.LoadBGFromWeb(bgpath, bgCallback));
        
    }

    IEnumerator WaitVideoPrepare(Action callback)
    {
        bgManager.videoPlayer.Prepare();
        var startTime = Time.time;
        while (!bgManager.videoPlayer.isPrepared)
        {
            yield return new WaitForEndOfFrame();
            if(Time.time - startTime  > 2f)
            {
                Debug.Log("No video for this song? maybe because it does not throw, FUCK YOU UNITY");
                callback.Invoke();
                StartCoroutine(SeeIfitisDoneLater());
                yield break;
            }
        }
        bgManager.UpdateVideoRatio();
        callback.Invoke();
    }
    IEnumerator SeeIfitisDoneLater()
    {
        while (!bgManager.videoPlayer.isPrepared)
        {
            yield return new WaitForEndOfFrame();
        }
        bgManager.UpdateVideoRatio();
    }

    /// <summary>
    /// Seek to a specific time position. If currently playing, stops and restarts from the new position.
    /// If not playing, just sets the position so the next Play() starts from there.
    /// </summary>
    public void SeekTo(float time)
    {
        bool wasPlaying = inited && timeProvider.isStart;

        // Stop current playback and destroy notes
        OnStopButtonClick();

        // Clamp time to valid range
        if (timeProvider.bgm.clip != null)
            time = Mathf.Clamp(time, 0f, timeProvider.bgm.clip.length);
        else
            time = Mathf.Max(0f, time);

        // Set the new start position
        startTime = time;
        timeProvider.AudioTime = time;
        timeProvider.playStartTime = time;

        // If was playing, restart from new position
        if (wasPlaying)
        {
            Play();
        }
    }

    /// <summary>
    /// Returns true if a chart is loaded and ready to play.
    /// </summary>
    public bool IsReady()
    {
        return status >= 4;
    }

    /// <summary>
    /// Returns true if playback is currently active (not paused).
    /// </summary>
    public bool IsPlaying()
    {
        return inited && timeProvider.isStart;
    }

    /// <summary>
    /// Returns true if playback was started but is currently paused.
    /// </summary>
    public bool IsPaused()
    {
        return inited && !timeProvider.isStart;
    }

    /// <summary>
    /// Returns the total duration of the loaded audio in seconds, or 0 if no audio is loaded.
    /// </summary>
    public float GetDuration()
    {
        if (timeProvider.bgm.clip != null)
            return timeProvider.bgm.clip.length;
        return 0f;
    }

    /// <summary>
    /// Returns the current playback time in seconds.
    /// </summary>
    public float GetCurrentTime()
    {
        return timeProvider.AudioTime;
    }

    public void OnSpeedDropDownClick(int value)
    {
        audioSpeed = 1f-value*0.25f;
    }
}
