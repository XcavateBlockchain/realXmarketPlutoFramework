using PlutoFramework.Constants;
using PlutoFramework.Types;
using System.ComponentModel;
using UniqueryPlus.Nfts;
using NftKey = (UniqueryPlus.NftTypeEnum, System.Numerics.BigInteger, System.Numerics.BigInteger);

namespace PlutoFramework.Model
{
    public record NftAssetWrapper : NftWrapper
    {
        public required Asset? AssetPrice { get; set; }
        public required NftOperation Operation { get; set; }
    }

    public record NftWrapper : INotifyPropertyChanged
    {
        public NftKey Key => (NftBase.Type, NftBase.CollectionId, NftBase.Id);
        public required INftBase NftBase { get; set; }
        public required Endpoint Endpoint { get; set; }

        private bool favourite = false;
        public bool Favourite
        {
            get => favourite;
            set
            {
                if (favourite != value)
                {
                    favourite = value;
                    OnPropertyChanged(nameof(Favourite));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public override int GetHashCode()
        {
            return HashCode.Combine(NftBase?.Metadata?.Name, NftBase?.Metadata?.Description, NftBase?.Metadata?.Image, Endpoint?.Key);
        }

        public override string ToString()
        {
            return this.NftBase?.Metadata?.Name + " - " + this.NftBase?.Metadata?.Image;
        }
    }
}
