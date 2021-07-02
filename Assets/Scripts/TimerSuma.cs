using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TimerSuma : MonoBehaviour
{
    public float timeElapsed;
    public float currentLapTime;
    public float accumulativeTime;
    public bool timerIsRunning = false;
    public int lastLap;
    // public Text timeText;

    public void Start()
    {
        // Starts the timer automatically
    }
    public void StartTimer()
    {
        timerIsRunning = true;
    }
    public void Pause()
    {
        timerIsRunning = false;
    }
    void Update()
    {
        if (timerIsRunning)
        {
            timeElapsed += Time.deltaTime;
            currentLapTime += Time.deltaTime;
            DisplayTime(timeElapsed);

        }
    }
    public void resetLapTime()
    { 
        currentLapTime = 0;
    }
    public void SetCurrentLapTime()
    {
        currentLapTime = timeElapsed - accumulativeTime;
        accumulativeTime += currentLapTime;
    }

    public void DisplayTime(float timeToDisplay)
    {
        timeToDisplay += 1;

        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);

        //timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}