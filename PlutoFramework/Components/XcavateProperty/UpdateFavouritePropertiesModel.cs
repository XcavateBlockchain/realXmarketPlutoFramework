using UniqueryPlus.Nfts;

namespace PlutoFramework.Components.XcavateProperty
{
    public class UpdateFavouritePropertiesModel
    {
        public static void UpdateFavourite(INftXcavateBase nftBase, bool favourite)
        {
            var ownedListViewModel = DependencyService.Get<OwnedPropertiesListViewModel>();
            ownedListViewModel.UpdateFavourite(nftBase, favourite);
        }
    }
}
