using UnityEngine;
using UnityEngine.UI;

public class HideUIOnStart : MonoBehaviour
{
    public GameObject[] uiElements; // Assign all 8 UI panels or canvases in the Inspector
    public Button startButton; // Assign the start button in the Inspector

    void Start()
    {
        startButton.onClick.AddListener(HideUI);
    }

    public void HideUI()
    {
        foreach (GameObject ui in uiElements)
        {
            ui.SetActive(false); // Hide all assigned UI elements
        }
    }
}
