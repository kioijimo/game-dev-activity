using UnityEngine;

public class KillPlayer : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string playerTag = "Player";

    [Header("Game Over")]
    [SerializeField] private GameObject gameOverUI;

    private bool playerKilled = false;

    private void Start()
    {
        // Hide the Game Over UI when the game starts
        if (gameOverUI != null)
        {
            gameOverUI.SetActive(false);
        }

        Time.timeScale = 1f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (playerKilled || !other.CompareTag(playerTag))
            return;

        playerKilled = true;

        // Show Game Over UI
        if (gameOverUI != null)
        {
            gameOverUI.SetActive(true);
        }

        // Freeze the game
        Time.timeScale = 0f;

        // Unlock and show the mouse cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("Player killed! Game paused.");
    }
}