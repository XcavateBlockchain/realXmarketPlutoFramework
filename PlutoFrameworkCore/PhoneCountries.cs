using System.Globalization;

namespace PlutoFramework.Model
{
    /// <summary>
    /// Every country and territory with an ISO 3166-1 alpha-2 code, paired with its E.164
    /// country calling code.
    /// </summary>
    /// <remarks>
    /// Hardcoded because .NET exposes no calling codes: <see cref="RegionInfo"/> knows a
    /// region's name and currency but not how to dial it, and the ICU data mobile targets ship
    /// can be trimmed. Codes are the real E.164 ones, so every North American Numbering Plan
    /// country is +1 and its three-digit area code belongs to the national number - the same
    /// choice the iOS and Android pickers make.
    /// </remarks>
    public static class PhoneCountries
    {
        /// <summary>
        /// Used when nothing else identifies the user's country: the app is an Xcavate property
        /// marketplace, and its properties are in the UK.
        /// </summary>
        public const string FALLBACK_ISO_CODE = "GB";

        /// <summary>
        /// Which country owns a calling code that several share. Without this, +1 would resolve
        /// to Antigua and Barbuda and +44 to Guernsey purely because the list is alphabetical.
        /// </summary>
        private static readonly Dictionary<string, string> PREFERRED_ISO_CODE_BY_DIAL_CODE = new()
        {
            ["1"] = "US",
            ["7"] = "RU",
            ["44"] = "GB",
            ["47"] = "NO",
            ["61"] = "AU",
            ["64"] = "NZ",
            ["212"] = "MA",
            ["262"] = "RE",
            ["358"] = "FI",
            ["500"] = "FK",
            ["590"] = "GP",
            ["599"] = "CW",
            ["672"] = "NF",
        };

        /// <summary>
        /// Ordered by name, which is the order the picker shows them in.
        /// </summary>
        public static IReadOnlyList<PhoneCountry> All { get; } = BuildAll();

        private static readonly Dictionary<string, PhoneCountry> BY_ISO_CODE =
            All.ToDictionary(country => country.IsoCode, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, PhoneCountry> BY_DIAL_CODE = BuildByDialCode();

        /// <summary>
        /// The country to preselect: the device's region when it is one we know, the UK
        /// otherwise.
        /// </summary>
        public static PhoneCountry Default => ByIsoCode(CurrentRegionIsoCode()) ?? BY_ISO_CODE[FALLBACK_ISO_CODE];

        public static PhoneCountry? ByIsoCode(string? isoCode) =>
            isoCode is not null && BY_ISO_CODE.TryGetValue(isoCode, out var country) ? country : null;

        /// <summary>
        /// The country a calling code belongs to, resolving shared codes through
        /// <see cref="PREFERRED_ISO_CODE_BY_DIAL_CODE"/>.
        /// </summary>
        public static PhoneCountry? ByDialCode(string? dialCode) =>
            dialCode is not null && BY_DIAL_CODE.TryGetValue(dialCode, out var country) ? country : null;

        /// <summary>
        /// Countries matching what has been typed into the picker's search box, by name, ISO
        /// code or calling code. Names that start with the query come first, so typing "ind"
        /// offers India before the British Indian Ocean Territory.
        /// </summary>
        public static IReadOnlyList<PhoneCountry> Search(string? query)
        {
            var trimmed = (query ?? "").Trim();

            if (trimmed == "")
            {
                return All;
            }

            // A leading + is how people write a calling code, and is never part of a name.
            var dialQuery = trimmed.TrimStart('+');

            var matches = All
                .Select(country => new
                {
                    Country = country,
                    Rank = RankMatch(country, trimmed, dialQuery),
                })
                .Where(match => match.Rank < NO_MATCH)
                .OrderBy(match => match.Rank)
                .ThenBy(match => match.Country.Name, StringComparer.OrdinalIgnoreCase)
                .Select(match => match.Country)
                .ToList();

            return matches;
        }

        private const int NO_MATCH = int.MaxValue;

        private static int RankMatch(PhoneCountry country, string query, string dialQuery)
        {
            if (country.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (country.IsoCode.Equals(query, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (dialQuery != "" && dialQuery.All(char.IsDigit) && country.DialCode.StartsWith(dialQuery, StringComparison.Ordinal))
            {
                return 2;
            }

            if (country.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            return NO_MATCH;
        }

        private static string CurrentRegionIsoCode()
        {
            try
            {
                return RegionInfo.CurrentRegion.TwoLetterISORegionName;
            }
            catch
            {
                // A device with no usable region data still has to get a picker default.
                return FALLBACK_ISO_CODE;
            }
        }

        private static Dictionary<string, PhoneCountry> BuildByDialCode()
        {
            var byDialCode = new Dictionary<string, PhoneCountry>(StringComparer.Ordinal);

            foreach (var country in All)
            {
                var preferred = PREFERRED_ISO_CODE_BY_DIAL_CODE.TryGetValue(country.DialCode, out var isoCode);

                if (preferred && !country.IsoCode.Equals(isoCode, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (preferred || !byDialCode.ContainsKey(country.DialCode))
                {
                    byDialCode[country.DialCode] = country;
                }
            }

            return byDialCode;
        }

        private static IReadOnlyList<PhoneCountry> BuildAll() => Table()
            .Select(entry => new PhoneCountry(entry.IsoCode, entry.Name, entry.DialCode))
            .OrderBy(country => country.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        private static (string IsoCode, string Name, string DialCode)[] Table() => new[]
        {
            ("AD", "Andorra", "376"),
            ("AE", "United Arab Emirates", "971"),
            ("AF", "Afghanistan", "93"),
            ("AG", "Antigua and Barbuda", "1"),
            ("AI", "Anguilla", "1"),
            ("AL", "Albania", "355"),
            ("AM", "Armenia", "374"),
            ("AO", "Angola", "244"),
            ("AQ", "Antarctica", "672"),
            ("AR", "Argentina", "54"),
            ("AS", "American Samoa", "1"),
            ("AT", "Austria", "43"),
            ("AU", "Australia", "61"),
            ("AW", "Aruba", "297"),
            ("AX", "Åland Islands", "358"),
            ("AZ", "Azerbaijan", "994"),
            ("BA", "Bosnia and Herzegovina", "387"),
            ("BB", "Barbados", "1"),
            ("BD", "Bangladesh", "880"),
            ("BE", "Belgium", "32"),
            ("BF", "Burkina Faso", "226"),
            ("BG", "Bulgaria", "359"),
            ("BH", "Bahrain", "973"),
            ("BI", "Burundi", "257"),
            ("BJ", "Benin", "229"),
            ("BL", "Saint Barthélemy", "590"),
            ("BM", "Bermuda", "1"),
            ("BN", "Brunei", "673"),
            ("BO", "Bolivia", "591"),
            ("BQ", "Caribbean Netherlands", "599"),
            ("BR", "Brazil", "55"),
            ("BS", "Bahamas", "1"),
            ("BT", "Bhutan", "975"),
            ("BV", "Bouvet Island", "47"),
            ("BW", "Botswana", "267"),
            ("BY", "Belarus", "375"),
            ("BZ", "Belize", "501"),
            ("CA", "Canada", "1"),
            ("CC", "Cocos (Keeling) Islands", "61"),
            ("CD", "Congo (Democratic Republic)", "243"),
            ("CF", "Central African Republic", "236"),
            ("CG", "Congo (Republic)", "242"),
            ("CH", "Switzerland", "41"),
            ("CI", "Côte d'Ivoire", "225"),
            ("CK", "Cook Islands", "682"),
            ("CL", "Chile", "56"),
            ("CM", "Cameroon", "237"),
            ("CN", "China", "86"),
            ("CO", "Colombia", "57"),
            ("CR", "Costa Rica", "506"),
            ("CU", "Cuba", "53"),
            ("CV", "Cabo Verde", "238"),
            ("CW", "Curaçao", "599"),
            ("CX", "Christmas Island", "61"),
            ("CY", "Cyprus", "357"),
            ("CZ", "Czechia", "420"),
            ("DE", "Germany", "49"),
            ("DJ", "Djibouti", "253"),
            ("DK", "Denmark", "45"),
            ("DM", "Dominica", "1"),
            ("DO", "Dominican Republic", "1"),
            ("DZ", "Algeria", "213"),
            ("EC", "Ecuador", "593"),
            ("EE", "Estonia", "372"),
            ("EG", "Egypt", "20"),
            ("EH", "Western Sahara", "212"),
            ("ER", "Eritrea", "291"),
            ("ES", "Spain", "34"),
            ("ET", "Ethiopia", "251"),
            ("FI", "Finland", "358"),
            ("FJ", "Fiji", "679"),
            ("FK", "Falkland Islands", "500"),
            ("FM", "Micronesia", "691"),
            ("FO", "Faroe Islands", "298"),
            ("FR", "France", "33"),
            ("GA", "Gabon", "241"),
            ("GB", "United Kingdom", "44"),
            ("GD", "Grenada", "1"),
            ("GE", "Georgia", "995"),
            ("GF", "French Guiana", "594"),
            ("GG", "Guernsey", "44"),
            ("GH", "Ghana", "233"),
            ("GI", "Gibraltar", "350"),
            ("GL", "Greenland", "299"),
            ("GM", "Gambia", "220"),
            ("GN", "Guinea", "224"),
            ("GP", "Guadeloupe", "590"),
            ("GQ", "Equatorial Guinea", "240"),
            ("GR", "Greece", "30"),
            ("GS", "South Georgia and the South Sandwich Islands", "500"),
            ("GT", "Guatemala", "502"),
            ("GU", "Guam", "1"),
            ("GW", "Guinea-Bissau", "245"),
            ("GY", "Guyana", "592"),
            ("HK", "Hong Kong", "852"),
            ("HM", "Heard Island and McDonald Islands", "672"),
            ("HN", "Honduras", "504"),
            ("HR", "Croatia", "385"),
            ("HT", "Haiti", "509"),
            ("HU", "Hungary", "36"),
            ("ID", "Indonesia", "62"),
            ("IE", "Ireland", "353"),
            ("IL", "Israel", "972"),
            ("IM", "Isle of Man", "44"),
            ("IN", "India", "91"),
            ("IO", "British Indian Ocean Territory", "246"),
            ("IQ", "Iraq", "964"),
            ("IR", "Iran", "98"),
            ("IS", "Iceland", "354"),
            ("IT", "Italy", "39"),
            ("JE", "Jersey", "44"),
            ("JM", "Jamaica", "1"),
            ("JO", "Jordan", "962"),
            ("JP", "Japan", "81"),
            ("KE", "Kenya", "254"),
            ("KG", "Kyrgyzstan", "996"),
            ("KH", "Cambodia", "855"),
            ("KI", "Kiribati", "686"),
            ("KM", "Comoros", "269"),
            ("KN", "Saint Kitts and Nevis", "1"),
            ("KP", "North Korea", "850"),
            ("KR", "South Korea", "82"),
            ("KW", "Kuwait", "965"),
            ("KY", "Cayman Islands", "1"),
            ("KZ", "Kazakhstan", "7"),
            ("LA", "Laos", "856"),
            ("LB", "Lebanon", "961"),
            ("LC", "Saint Lucia", "1"),
            ("LI", "Liechtenstein", "423"),
            ("LK", "Sri Lanka", "94"),
            ("LR", "Liberia", "231"),
            ("LS", "Lesotho", "266"),
            ("LT", "Lithuania", "370"),
            ("LU", "Luxembourg", "352"),
            ("LV", "Latvia", "371"),
            ("LY", "Libya", "218"),
            ("MA", "Morocco", "212"),
            ("MC", "Monaco", "377"),
            ("MD", "Moldova", "373"),
            ("ME", "Montenegro", "382"),
            ("MF", "Saint Martin", "590"),
            ("MG", "Madagascar", "261"),
            ("MH", "Marshall Islands", "692"),
            ("MK", "North Macedonia", "389"),
            ("ML", "Mali", "223"),
            ("MM", "Myanmar", "95"),
            ("MN", "Mongolia", "976"),
            ("MO", "Macao", "853"),
            ("MP", "Northern Mariana Islands", "1"),
            ("MQ", "Martinique", "596"),
            ("MR", "Mauritania", "222"),
            ("MS", "Montserrat", "1"),
            ("MT", "Malta", "356"),
            ("MU", "Mauritius", "230"),
            ("MV", "Maldives", "960"),
            ("MW", "Malawi", "265"),
            ("MX", "Mexico", "52"),
            ("MY", "Malaysia", "60"),
            ("MZ", "Mozambique", "258"),
            ("NA", "Namibia", "264"),
            ("NC", "New Caledonia", "687"),
            ("NE", "Niger", "227"),
            ("NF", "Norfolk Island", "672"),
            ("NG", "Nigeria", "234"),
            ("NI", "Nicaragua", "505"),
            ("NL", "Netherlands", "31"),
            ("NO", "Norway", "47"),
            ("NP", "Nepal", "977"),
            ("NR", "Nauru", "674"),
            ("NU", "Niue", "683"),
            ("NZ", "New Zealand", "64"),
            ("OM", "Oman", "968"),
            ("PA", "Panama", "507"),
            ("PE", "Peru", "51"),
            ("PF", "French Polynesia", "689"),
            ("PG", "Papua New Guinea", "675"),
            ("PH", "Philippines", "63"),
            ("PK", "Pakistan", "92"),
            ("PL", "Poland", "48"),
            ("PM", "Saint Pierre and Miquelon", "508"),
            ("PN", "Pitcairn Islands", "64"),
            ("PR", "Puerto Rico", "1"),
            ("PS", "Palestine", "970"),
            ("PT", "Portugal", "351"),
            ("PW", "Palau", "680"),
            ("PY", "Paraguay", "595"),
            ("QA", "Qatar", "974"),
            ("RE", "Réunion", "262"),
            ("RO", "Romania", "40"),
            ("RS", "Serbia", "381"),
            ("RU", "Russia", "7"),
            ("RW", "Rwanda", "250"),
            ("SA", "Saudi Arabia", "966"),
            ("SB", "Solomon Islands", "677"),
            ("SC", "Seychelles", "248"),
            ("SD", "Sudan", "249"),
            ("SE", "Sweden", "46"),
            ("SG", "Singapore", "65"),
            ("SH", "Saint Helena", "290"),
            ("SI", "Slovenia", "386"),
            ("SJ", "Svalbard and Jan Mayen", "47"),
            ("SK", "Slovakia", "421"),
            ("SL", "Sierra Leone", "232"),
            ("SM", "San Marino", "378"),
            ("SN", "Senegal", "221"),
            ("SO", "Somalia", "252"),
            ("SR", "Suriname", "597"),
            ("SS", "South Sudan", "211"),
            ("ST", "São Tomé and Príncipe", "239"),
            ("SV", "El Salvador", "503"),
            ("SX", "Sint Maarten", "1"),
            ("SY", "Syria", "963"),
            ("SZ", "Eswatini", "268"),
            ("TC", "Turks and Caicos Islands", "1"),
            ("TD", "Chad", "235"),
            ("TF", "French Southern Territories", "262"),
            ("TG", "Togo", "228"),
            ("TH", "Thailand", "66"),
            ("TJ", "Tajikistan", "992"),
            ("TK", "Tokelau", "690"),
            ("TL", "Timor-Leste", "670"),
            ("TM", "Turkmenistan", "993"),
            ("TN", "Tunisia", "216"),
            ("TO", "Tonga", "676"),
            ("TR", "Türkiye", "90"),
            ("TT", "Trinidad and Tobago", "1"),
            ("TV", "Tuvalu", "688"),
            ("TW", "Taiwan", "886"),
            ("TZ", "Tanzania", "255"),
            ("UA", "Ukraine", "380"),
            ("UG", "Uganda", "256"),
            ("UM", "United States Minor Outlying Islands", "1"),
            ("US", "United States", "1"),
            ("UY", "Uruguay", "598"),
            ("UZ", "Uzbekistan", "998"),
            ("VA", "Vatican City", "379"),
            ("VC", "Saint Vincent and the Grenadines", "1"),
            ("VE", "Venezuela", "58"),
            ("VG", "British Virgin Islands", "1"),
            ("VI", "United States Virgin Islands", "1"),
            ("VN", "Vietnam", "84"),
            ("VU", "Vanuatu", "678"),
            ("WF", "Wallis and Futuna", "681"),
            ("WS", "Samoa", "685"),
            ("YE", "Yemen", "967"),
            ("YT", "Mayotte", "262"),
            ("ZA", "South Africa", "27"),
            ("ZM", "Zambia", "260"),
            ("ZW", "Zimbabwe", "263"),
        };
    }
}
