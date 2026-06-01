using System.Numerics;

namespace UniqueryPlus.Nfts
{
    public record XcavateRealWorldAssetDetails
    {
        public required uint Tokens { get; set; }
        public required BigInteger Price { get; set; }
        public required bool SpvCreated { get; set; }
        public required bool Finalized { get; set; }
    }
    public interface INftXcavateRealWorldAssetDetails
    {
        public XcavateRealWorldAssetDetails? RealWorldAssetDetails { get; set; }
    }
}
