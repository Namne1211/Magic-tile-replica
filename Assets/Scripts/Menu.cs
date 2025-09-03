using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    public void LoadSceneByIndex(int buildIndex)
    {
        if (buildIndex >= 0)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(buildIndex);
        }
    }

    public void ReloadActiveScene()
    {
        Time.timeScale = 1f;
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void SetActive(GameObject target, bool active)
    {
        if (target) target.SetActive(active);
    }

    public void ToggleActive(GameObject target)
    {
        if (target) target.SetActive(!target.activeSelf);
    }
}
