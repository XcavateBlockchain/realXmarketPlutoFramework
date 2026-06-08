namespace PlutoFramework.Model;

public class MauiNavigationService : INavigationService
{
    public Task NavigateToAsync(Page page)
    {
        return NavigationModel.PushAsync(page);
    }
}
