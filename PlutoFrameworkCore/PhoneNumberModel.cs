namespace PlutoFramework.Model
{
    /// <summary>
    /// What makes a typed phone number acceptable, why a rejected one was rejected, and how the
    /// selected country plus the national digits become the E.164 string the backend stores.
    /// </summary>
    /// <remarks>
    /// Lives in Core so the sentence the user reads, the rule the Continue button obeys and the
    /// number sent to Sumsub all come out of one expression, and so they can be tested - nothing
    /// in the MAUI project can be.
    ///
    /// The country is picked from a list rather than typed, so the only thing left to judge is
    /// the national part. E.164 caps a whole number at 15 digits and allocates none shorter than
    /// 7, which is as far as this can go without shipping a numbering plan per country.
    /// </remarks>
    public static class PhoneNumberModel
    {
        public const int MINIMUM_E164_DIGITS = 7;

        public const int MAXIMUM_E164_DIGITS = 15;

        /// <summary>
        /// The longest country calling code, and so the most digits worth testing when splitting
        /// an international number back into a country and a national part.
        /// </summary>
        private const int MAXIMUM_DIAL_CODE_DIGITS = 4;

        /// <summary>
        /// Punctuation people write phone numbers with. Ignored rather than rejected, so
        /// "(020) 7946 0958" is accepted exactly as typed.
        /// </summary>
        private const string ALLOWED_SEPARATORS = " -()./";

        /// <summary>
        /// Numbering plans that keep the national trunk "0" in the international number instead
        /// of dropping it after the country code. Italy is the well-known exception, and Vatican
        /// City is served by the Italian plan.
        /// </summary>
        private static readonly HashSet<string> KEEPS_LEADING_ZERO =
            new(StringComparer.OrdinalIgnoreCase) { "IT", "VA" };

        /// <summary>
        /// Null when the number is acceptable, otherwise one sentence saying what to change.
        /// </summary>
        public static string? DescribeProblem(PhoneCountry? country, string? nationalNumber)
        {
            if (country is null)
            {
                return "Select the country your number belongs to.";
            }

            var text = (nationalNumber ?? "").Trim();

            if (text == "")
            {
                return "Enter your phone number.";
            }

            if (text.Any(char.IsLetter))
            {
                return "A phone number cannot contain letters.";
            }

            if (text.Contains('+'))
            {
                return $"The country code is already set to +{country.DialCode} - enter only the rest of the number here.";
            }

            var unexpected = text.FirstOrDefault(character => !char.IsDigit(character) && !ALLOWED_SEPARATORS.Contains(character));

            if (unexpected != '\0')
            {
                return $"'{unexpected}' cannot be part of a phone number.";
            }

            var significant = SignificantDigits(country, text);

            if (significant == "")
            {
                return "Enter your phone number.";
            }

            var shortest = Math.Max(1, MINIMUM_E164_DIGITS - country.DialCode.Length);
            var longest = MAXIMUM_E164_DIGITS - country.DialCode.Length;

            if (significant.Length < shortest)
            {
                return $"This number is too short - after +{country.DialCode} it needs at least {shortest} digits.";
            }

            if (significant.Length > longest)
            {
                return $"This number is too long - after +{country.DialCode} it can have at most {longest} digits.";
            }

            return null;
        }

        public static bool IsValid(PhoneCountry? country, string? nationalNumber) =>
            DescribeProblem(country, nationalNumber) is null;

        /// <summary>
        /// The digits that follow the country code internationally: separators dropped, and the
        /// national trunk "0" removed unless the country keeps it.
        /// </summary>
        public static string SignificantDigits(PhoneCountry country, string? nationalNumber)
        {
            var digits = new string((nationalNumber ?? "").Where(char.IsDigit).ToArray());

            if (KEEPS_LEADING_ZERO.Contains(country.IsoCode))
            {
                return digits;
            }

            // Only the one trunk prefix: "0044..." is someone writing the country code the old
            // way, and eating every zero would silently turn it into a different number.
            return digits.StartsWith("0", StringComparison.Ordinal) ? digits.Substring(1) : digits;
        }

        /// <summary>
        /// The number as the backend and Sumsub want it - a plus, the country code, the national
        /// digits, nothing else.
        /// </summary>
        public static string ToE164(PhoneCountry country, string? nationalNumber) =>
            $"+{country.DialCode}{SignificantDigits(country, nationalNumber)}";

        /// <summary>
        /// Whether a stored value is already a well formed international number. This is what a
        /// Continue button should ask, because it is the only form the field ever writes out.
        /// </summary>
        public static bool IsValidE164(string? value)
        {
            var text = (value ?? "").Trim();

            if (!text.StartsWith("+", StringComparison.Ordinal))
            {
                return false;
            }

            var digits = text.Substring(1);

            if (digits.Length < MINIMUM_E164_DIGITS || digits.Length > MAXIMUM_E164_DIGITS)
            {
                return false;
            }

            // A country code never starts with zero, so a leading zero means the plus was typed
            // in front of a national number rather than an international one.
            return digits.All(char.IsDigit) && !digits.StartsWith("0", StringComparison.Ordinal);
        }

        /// <summary>
        /// Splits a value back into the country to preselect and the national part to show in the
        /// entry, so reopening the form lands on what was saved. Anything that is not
        /// international is treated as a national number for <paramref name="fallbackCountry"/>.
        /// </summary>
        public static (PhoneCountry Country, string NationalNumber) Parse(string? value, PhoneCountry? fallbackCountry = null)
        {
            var fallback = fallbackCountry ?? PhoneCountries.Default;
            var text = (value ?? "").Trim();
            var digits = new string(text.Where(char.IsDigit).ToArray());

            if (!text.StartsWith("+", StringComparison.Ordinal) || digits == "")
            {
                return (fallback, digits);
            }

            // Longest code first, so +35 (nothing) never wins over +351 (Portugal).
            for (var length = Math.Min(MAXIMUM_DIAL_CODE_DIGITS, digits.Length); length >= 1; length--)
            {
                var country = PhoneCountries.ByDialCode(digits.Substring(0, length));

                if (country is not null)
                {
                    return (country, digits.Substring(length));
                }
            }

            return (fallback, digits);
        }
    }
}
