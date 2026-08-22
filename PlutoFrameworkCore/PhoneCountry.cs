namespace PlutoFramework.Model
{
    /// <summary>
    /// One row of the phone country picker: the ISO 3166-1 alpha-2 code, the name shown in the
    /// list, and the E.164 country calling code dialled before the national number.
    /// </summary>
    public sealed record PhoneCountry(string IsoCode, string Name, string DialCode)
    {
        /// <summary>
        /// The flag as the pair of Unicode regional indicator symbols for the ISO code, which is
        /// how Android and iOS draw country flags - no image asset per country, and it follows
        /// whatever emoji font the system ships. Desktop Windows renders the two letters
        /// instead, which is fine because the app only targets the two mobile platforms.
        /// </summary>
        public string Flag => string.Concat(
            IsoCode.Select(letter => char.ConvertFromUtf32(REGIONAL_INDICATOR_A + (letter - 'A'))));

        /// <summary>
        /// What the picker shows next to the flag, and the text the search box matches against.
        /// </summary>
        public string NameWithDialCode => $"{Name} (+{DialCode})";

        private const int REGIONAL_INDICATOR_A = 0x1F1E6;
    }
}
