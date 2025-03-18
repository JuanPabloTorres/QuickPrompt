namespace QuickPrompt.Pages;

public class BaseContentPage : ContentPage
{
	public string FinalPrompt;
	public BaseContentPage(string finalPrompt)
	{
		FinalPrompt = finalPrompt;
	}

    public BaseContentPage()
    {
        
    }
}