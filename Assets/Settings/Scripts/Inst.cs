using UnityEngine;

public class PageSwitcher : MonoBehaviour
{
    public GameObject[] pages; // Array to hold pages
    [Tooltip("Ignore a second click that arrives within this many seconds (ray + poke can both fire).")]
    public float clickCooldown = 0.2f;

    private int currentPage = 0;
    private float lastClickTime = -10f;

    void Start()
    {
        ShowPage(currentPage);
    }

    public void NextPage()
    {
        if (!AcceptClick()) return;
        if (currentPage < pages.Length - 1)
        {
            currentPage++;
            ShowPage(currentPage);
        }
        Debug.Log($"[PageSwitcher] Next -> page {currentPage + 1}/{pages.Length}");
    }

    public void PreviousPage()
    {
        if (!AcceptClick()) return;
        if (currentPage > 0)
        {
            currentPage--;
            ShowPage(currentPage);
        }
        Debug.Log($"[PageSwitcher] Previous -> page {currentPage + 1}/{pages.Length}");
    }

    private bool AcceptClick()
    {
        if (Time.unscaledTime - lastClickTime < clickCooldown) return false;
        lastClickTime = Time.unscaledTime;
        return true;
    }

    private void ShowPage(int pageIndex)
    {
        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null) pages[i].SetActive(i == pageIndex);
        }
    }
}
