namespace PlutoFramework.Model.Xcavate
{
    public sealed record LawyerRegistryOperationContext(int Generation, string Address)
    {
        public bool Matches(int generation, string? address) =>
            Generation == generation
            && Address.Length > 0
            && string.Equals(Address, address, StringComparison.Ordinal);
    }
}
