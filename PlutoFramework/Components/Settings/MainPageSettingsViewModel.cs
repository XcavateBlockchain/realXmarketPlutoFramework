using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Xcavate;
using System.Collections.ObjectModel;

namespace PlutoFramework.Components.Settings;

public partial class MainPageRoleOption : ObservableObject
{
    public required MainPageRole Role { get; init; }

    public string Text => Role == MainPageRole.LettingAgent ? "Letting Agent" : Role.ToString();

    public string AutomationId => $"mainPage{Role}";

    [ObservableProperty]
    private bool isSelected;
}

public partial class MainPageSettingsViewModel : ObservableObject
{
    public ObservableCollection<MainPageRoleOption> Roles { get; }

    [ObservableProperty]
    private bool isReady;

    public MainPageSettingsViewModel()
    {
        Roles = new ObservableCollection<MainPageRoleOption>(
            MainPageRoleOptions.Selectable.Select(role => new MainPageRoleOption
            {
                Role = role,
            }));

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        MainPageRole selected;

        try
        {
            selected = await MainPageRoleModel.GetSelectedRoleAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Main-page role setting could not be initialized:");
            Console.WriteLine(ex);
            selected = MainPageRole.Investor;
        }

        foreach (var role in Roles)
        {
            role.IsSelected = role.Role == selected;
        }

        IsReady = true;
    }

    [RelayCommand]
    private void SelectRole(MainPageRoleOption option)
    {
        if (option is null || !IsReady || option.IsSelected)
        {
            return;
        }

        MainPageRoleModel.SetOverride(option.Role);

        foreach (var role in Roles)
        {
            role.IsSelected = ReferenceEquals(role, option);
        }
    }
}
