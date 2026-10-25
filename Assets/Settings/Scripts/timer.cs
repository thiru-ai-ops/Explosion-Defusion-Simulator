using UnityEngine;
using TMPro;

public class BombTimer : MonoBehaviour
{
    public float countdownTime = 30.0f;  // Set the timer in seconds
    public TextMeshProUGUI timerText;    // Assign the TimerText object here
    public GameObject explosionPrefab;   // Drag the explosion prefab here

    private bool isTimerActive = true;

    private void Start()
    {
        UpdateTimerDisplay(countdownTime);
    }

    private void Update()
    {
        if (isTimerActive && countdownTime > 0)
        {
            countdownTime -= Time.deltaTime;
            UpdateTimerDisplay(countdownTime);
        }
        else if (countdownTime <= 0 && isTimerActive)
        {
            TriggerExplosion();
        }
    }

    private void UpdateTimerDisplay(float time)
    {
        // Format time into minutes and seconds
        int minutes = Mathf.FloorToInt(time / 60);
        int seconds = Mathf.FloorToInt(time % 60);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void TriggerExplosion()
    {
        isTimerActive = false;
        Debug.Log("Time's up! Explosion triggered!");

        // Instantiate the explosion at the center of the scene (or adjust the position)
        Instantiate(explosionPrefab, transform.position, Quaternion.identity);
    }
}
