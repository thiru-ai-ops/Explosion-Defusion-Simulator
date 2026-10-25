using UnityEngine;
using UnityEngine.SceneManagement;
using Meta.WitAi; // Required for MetaXR functionality

public class LoadSceneMetaXR : MonoBehaviour
{
    public string sceneName; // Assign in Inspector

    public void LoadNewScene()
    {
        SceneManager.LoadScene(1);
    }
}
