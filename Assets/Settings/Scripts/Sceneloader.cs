using UnityEngine;
using UnityEngine.SceneManagement;
using Meta.WitAi.TTS.Utilities; // Optional for voice feedback

public class MetaXRSceneLoader : MonoBehaviour
{
    public void LoadGameScene()
    {
        SceneManager.LoadScene("Mainscene"); // Change to your actual game scene name
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}
