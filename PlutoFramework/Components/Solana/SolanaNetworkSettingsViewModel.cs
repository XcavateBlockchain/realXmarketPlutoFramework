using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using PlutoFrameworkCore.Solana;
using System.Collections.ObjectModel;

namespace PlutoFramework.Components.Solana;

public partial class SolanaNetworkOption : ObservableObject
{
    public required SolanaCluster Cluster { get; init; }

    public string Text => Cluster.GetName();

    public string AutomationId => $"solanaNetwork{Cluster}";

    [ObservableProperty]
    private bool isSelected;
}

/// <summary>
/// Drives the Solana network segment in Settings. The selection is written through
/// immediately - there is no save button, so a user who changes it and navigates away must
/// still end up on the network they picked.
/// </summary>
public partial class SolanaNetworkSettingsViewModel : ObservableObject
{
    public ObservableCollection<SolanaNetworkOption> Networks { get; }

    public SolanaNetworkSettingsViewModel()
    {
        var selected = SolanaNetworkModel.SelectedCluster;

        Networks = new ObservableCollection<SolanaNetworkOption>(
            SolanaNetworkModel.SelectableClusters.Select(cluster => new SolanaNetworkOption
            {
                Cluster = cluster,
                IsSelected = cluster == selected,
            }));
    }

    [RelayCommand]
    private void SelectNetwork(SolanaNetworkOption option)
    {
        if (option is null || option.IsSelected)
        {
            return;
        }

        SolanaNetworkModel.SelectedCluster = option.Cluster;

        foreach (var network in Networks)
        {
            network.IsSelected = ReferenceEquals(network, option);
        }
    }
}
