using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    // This method loads any scene by name
    public void Menu(string Menu)
    {
        SceneManager.LoadScene(Menu);
    }

    public void MainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    // This method loads the "SelectCharacter" scene specifically
    public void SelectCharacter()
    {
        SceneManager.LoadScene("SelectCharacter");
    }
}