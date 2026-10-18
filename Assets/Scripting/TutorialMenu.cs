
using UnityEngine;

public class TutorialMenu : MonoBehaviour
{
    public GameObject tutorialPanel;
    public GameObject[] pages;

    private int currentPage = 0;

    public void OpenTutorial()
    {
        tutorialPanel.SetActive(true);
        currentPage = 0;
        ShowPage();
    }

    public void NextPage()
    {
        if (currentPage < pages.Length - 1)
        {
            currentPage++;
            ShowPage();
        }
    }

    public void PreviousPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            ShowPage();
        }
    }

    /// <summary>
    /// What the Back button calls: steps back a page, and on the first page it
    /// closes the tutorial instead of doing nothing.
    /// </summary>
    public void BackOrClose()
    {
        if (currentPage > 0)
            PreviousPage();
        else
            CloseTutorial();
    }

    public void CloseTutorial()
    {
        tutorialPanel.SetActive(false);
    }

    private void ShowPage()
    {
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == currentPage);
        }
    }
}
