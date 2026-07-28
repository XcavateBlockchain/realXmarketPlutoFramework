using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using PlutoFrameworkCore.Keys;
using System.Collections.ObjectModel;

namespace PlutoFramework.Components.Settings;

public partial class MainKeyOption : ObservableObject
{
    public required MainKeyChain Chain { get; init; }

    public string Text => Chain switch
    {
        MainKeyChain.Solana => "Solana",
        MainKeyChain.Polkadot => "Polkadot",
        _ => Chain.ToString(),
    };

    public string AutomationId => $"mainKey{Chain}";

    /// <summary>
    /// False when the user holds no key on this chain. The chip stays visible but dimmed and
    /// inert: selecting it would resolve straight back to the other chain, which would read as
    /// the setting being broken.
    /// </summary>
    public required bool IsAvailable { get; init; }

    [ObservableProperty]
    private bool isSelected;
}

/// <summary>
/// Drives the main account segment in Settings. The selection is written through immediately -
/// there is no save button, so a user who changes it and navigates away must still end up on
/// the key they picked.
/// </summary>
public partial class MainKeySettingsViewModel : ObservableObject
{
    public ObservableCollection<MainKeyOption> Chains { get; }

    public MainKeySettingsViewModel()
    {
        // The resolved chain, not the stored preference: those differ for a user whose
        // preferred chain has no key, and the chip that is lit has to be the one the rest of
        // the app is actually using.
        var selected = MainKeyModel.ResolvedChain;

        Chains = new ObservableCollection<MainKeyOption>(
            MainKeyOptions.Selectable.Select(chain => new MainKeyOption
            {
                Chain = chain,
                IsAvailable = MainKeyModel.IsAvailable(chain),
                IsSelected = chain == selected,
            }));
    }

    [RelayCommand]
    private void SelectChain(MainKeyOption option)
    {
        if (option is null || option.IsSelected || !option.IsAvailable)
        {
            return;
        }

        MainKeyModel.SelectedChain = option.Chain;

        foreach (var chain in Chains)
        {
            chain.IsSelected = ReferenceEquals(chain, option);
        }
    }
}
