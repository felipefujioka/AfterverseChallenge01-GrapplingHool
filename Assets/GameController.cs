using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using DG.Tweening;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    public CinemachineVirtualCamera dollyVirtualCamera;
    public CinemachineSmoothPath sceneDollyPath;

    public CinemachineVirtualCamera playerCamera;

    public TextMeshProUGUI uiText;
    public TextMeshProUGUI timerText;
    
    public ThirdPersonController playerController;

    public CinemachineVirtualCamera enemyCamera;
    public CinemachineSmoothPath enemyDollypath;

    public FleeingTarget enemyController;

    public GameObject targetCanvas;
    
    private CinemachineTrackedDolly dollyTrack;
    private CinemachineTrackedDolly enemyTrack;

    private float totalTime = 3 * 60;

    private bool gameStarted = false;
    private bool gameFailed = false;
    
    void Start()
    {
        Time.timeScale = 1;
        
        dollyTrack = dollyVirtualCamera.GetCinemachineComponent<CinemachineTrackedDolly>();
        enemyTrack = enemyCamera.GetCinemachineComponent<CinemachineTrackedDolly>();
        StartCoroutine(InitGame());
    }

    private void Update()
    {
        if (gameFailed) return;
        
        if (!gameFailed && gameStarted)
        {
            totalTime -= Time.deltaTime;    
        }

        if (totalTime <= 0)
        {
            gameFailed = true;
            totalTime = 0;
            FinishGame(false);
        }

        timerText.text = FormatTime(totalTime);
    }

    private string FormatTime(float f)
    {
        var minutes = (int)f / 60;
        var seconds = (int)f % 60;
        var milliseconds = (int)((f * 1000) % 1000);

        return $"{minutes:00}:{seconds:00}:{milliseconds:000}";
    }

    private IEnumerator InitGame()
    {
        bool completedDolly = false;
        DOTween.To(
            () => dollyTrack.m_PathPosition,
            (value) => dollyTrack.m_PathPosition = value,
            sceneDollyPath.PathLength,
            15).OnComplete(() =>
        {
            completedDolly = true;
        });

        yield return new WaitUntil(() => completedDolly );
        
        dollyVirtualCamera.enabled = false;
        enemyCamera.enabled = true;

        completedDolly = false;
        
        DOTween.To(
            () => enemyTrack.m_PathPosition,
            (value) => enemyTrack.m_PathPosition = value,
            enemyDollypath.PathLength * 4,
            10).OnComplete(() =>
        {
            completedDolly = true;
        });

        yield return FlashText("Chase the enemy", Color.white);
        
        yield return new WaitUntil(() => completedDolly);

        enemyCamera.enabled = false;
        playerCamera.enabled = true;

        yield return new WaitForSeconds(2);

        yield return FlashText("3", Color.white);
        yield return FlashText("2", Color.white);
        yield return FlashText("1", Color.white);
        yield return FlashText("GO!", Color.white);
        
        uiText.color = Color.clear;
        
        playerController.enabled = true;
        enemyController.enabled = true;
        targetCanvas.SetActive(true);
        gameStarted = true;
    }

    private IEnumerator FlashText(string text, Color color)
    {

        bool finish = false;
        
        uiText.text = text;
        
        uiText.color = Color.clear;

        uiText.DOColor(color, 0.5f).SetEase(Ease.InQuad).OnComplete(() => {
            finish = true;
        });
        
        yield return new WaitUntil(() => finish);
    }
    
    public void FinishGame(bool win)
    {

        if (win)
        {
            StartCoroutine(PlayFlash());
        }
        else
        {
            StartCoroutine(PlayFailed());
        }
    }

    private IEnumerator PlayFlash()
    {
        yield return FlashText("GOT IT!!!", Color.white);
        
        Time.timeScale = 0.1f;
        
        yield return new WaitForSecondsRealtime(5);

        SceneManager.LoadScene("Main");
    }

    private IEnumerator PlayFailed()
    {
        yield return FlashText("FAILED", Color.red);
        
        Time.timeScale = 0.1f;
        
        yield return new WaitForSecondsRealtime(5);

        SceneManager.LoadScene("Main");
    }
}
