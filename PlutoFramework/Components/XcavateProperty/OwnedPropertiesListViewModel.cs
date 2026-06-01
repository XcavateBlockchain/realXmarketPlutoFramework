using PlutoFramework.Components.Nft;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Xcavate;
using UniqueryPlus.Nfts;
using NftKey = (UniqueryPlus.NftTypeEnum, System.Numerics.BigInteger, System.Numerics.BigInteger);

namespace PlutoFramework.Components.XcavateProperty
{
    public partial class OwnedPropertiesListViewModel : BaseListViewModel<NftKey, XcavateNftWrapper>
    {
        public void UpdateFavourite(INftXcavateBase nftBase, bool newValue)
        {
            if (ItemsDict.ContainsKey((nftBase.Type, nftBase.CollectionId, nftBase.Id)))
            {
                ItemsDict[(nftBase.Type, nftBase.CollectionId, nftBase.Id)].Favourite = newValue;
            }
        }

        public override string Title => "Owned properties";

        public override Task InitialLoadAsync(CancellationToken token)
        {
            // Unused
            throw new NotImplementedException();
        }

        public override Task LoadMoreAsync(CancellationToken token)
        {
            // Unused
            throw new NotImplementedException();
        }

        public async Task UpdateAsync(CancellationToken token)
        {
            Console.WriteLine("Update owned properties list: " + XcavateOwnedPropertiesModel.ItemsDict.Values.Count());

            foreach (var property in XcavateOwnedPropertiesModel.ItemsDict.Values)
            {
                var newProperty = await XcavatePropertyModel.ToXcavateNftWrapperAsync((XcavatePaseoNftsPalletNft)property.NftBase, token);

                if (!ItemsDict.ContainsKey(newProperty.Key))
                {
                    ItemsDict.Add(newProperty.Key, newProperty);

                    // Save to DB
                    // await NftDatabase.SaveItemAsync(newNft).ConfigureAwait(false);

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Items.Add(newProperty);
                    });
                }
            }

            OnPropertyChanged(nameof(NoItems));
            OnPropertyChanged(nameof(AnyItems));
        }

        private Task LoadSavedNftsAsync()
        {
            return Task.FromResult(0);
            /*
            foreach (var savedNft in await NftDatabase.GetNftsOwnedByAsync(KeysModel.GetSubstrateKey()).ConfigureAwait(false))
            {
                if (savedNft.Key is not null && !ItemsDict.ContainsKey((NftKey)savedNft.Key))
                {
                    ItemsDict.Add((NftKey)savedNft.Key, savedNft);

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Items.Add(savedNft);
                    });
                }
            }*/
        }

        private async Task<XcavateNftWrapper> ToWrappedButUnwrappedAsync(XcavateNftWrapper info, CancellationToken token)
        {
            var wrapped = await XcavatePropertyModel.ToXcavateNftWrapperAsync((XcavatePaseoNftsPalletNft)info.NftBase, token);

            return wrapped;
        }
    }
}