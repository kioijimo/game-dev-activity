using UnityEngine;
using TMPro;

public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance;

    [Header("Coin Settings")]
    [SerializeField, Min(1)] private int maxCoins = 10;

    [Header("UI Settings")]
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private GameObject winUI;

    private int collectedCoins = 0;
    private bool gameEnded = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Time.timeScale = 1f;

        if (winUI != null)
            winUI.SetActive(false);

        UpdateCoinUI();
    }

    public void CollectCoin(int amount)
    {
        if (gameEnded)
            return;

        collectedCoins += amount;
        collectedCoins = Mathf.Min(collectedCoins, maxCoins);

        UpdateCoinUI();

        if (collectedCoins >= maxCoins)
        {
            WinGame();
        }
    }

    private void UpdateCoinUI()
    {
        if (coinText != null)
        {
            coinText.text = "Coins: " + collectedCoins + " / " + maxCoins;
        }
    }

    private void WinGame()
    {
        gameEnded = true;

        if (winUI != null)
            winUI.SetActive(true);

        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("YOU WIN!");
    }
}