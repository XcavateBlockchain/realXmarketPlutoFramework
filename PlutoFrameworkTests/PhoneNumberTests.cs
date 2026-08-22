using PlutoFramework.Model;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The phone rules the profile form enforces. Tested here rather than through the form
    /// because nothing in the MAUI project is reachable from this test project.
    /// </summary>
    public class PhoneNumbers
    {
        private static PhoneCountry Country(string isoCode) =>
            PhoneCountries.ByIsoCode(isoCode) ?? throw new InvalidOperationException($"{isoCode} missing from the country list");

        private static readonly PhoneCountry UnitedKingdom = Country("GB");
        private static readonly PhoneCountry UnitedStates = Country("US");
        private static readonly PhoneCountry Italy = Country("IT");

        [Test]
        public void EveryIsoCodeAppearsOnce()
        {
            var duplicates = PhoneCountries.All
                .GroupBy(country => country.IsoCode)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            Assert.That(duplicates, Is.Empty);
        }

        [Test]
        public void TheListCoversEveryIso3166Country()
        {
            // ISO 3166-1 currently assigns 249 alpha-2 codes.
            Assert.That(PhoneCountries.All, Has.Count.EqualTo(249));
        }

        [Test]
        public void EveryCountryHasATwoLetterCodeAndADigitsOnlyDialCode()
        {
            Assert.Multiple(() =>
            {
                foreach (var country in PhoneCountries.All)
                {
                    Assert.That(country.IsoCode, Has.Length.EqualTo(2), country.Name);
                    Assert.That(country.IsoCode.All(char.IsAsciiLetterUpper), Is.True, country.Name);
                    Assert.That(country.DialCode, Is.Not.Empty, country.Name);
                    Assert.That(country.DialCode.All(char.IsDigit), Is.True, country.Name);
                    Assert.That(country.DialCode, Does.Not.StartWith("0"), country.Name);
                    Assert.That(country.Name, Is.Not.Empty, country.IsoCode);
                }
            });
        }

        [Test]
        public void EveryFlagIsTheRegionalIndicatorPairForItsIsoCode()
        {
            Assert.That(UnitedKingdom.Flag, Is.EqualTo("\U0001F1EC\U0001F1E7"));
            Assert.That(UnitedStates.Flag, Is.EqualTo("\U0001F1FA\U0001F1F8"));

            // Two regional indicators are two surrogate pairs.
            Assert.That(PhoneCountries.All.All(country => country.Flag.Length == 4), Is.True);
        }

        [Test]
        public void SharedDialCodesResolveToTheExpectedCountry()
        {
            Assert.Multiple(() =>
            {
                Assert.That(PhoneCountries.ByDialCode("1")?.IsoCode, Is.EqualTo("US"));
                Assert.That(PhoneCountries.ByDialCode("7")?.IsoCode, Is.EqualTo("RU"));
                Assert.That(PhoneCountries.ByDialCode("44")?.IsoCode, Is.EqualTo("GB"));
                Assert.That(PhoneCountries.ByDialCode("61")?.IsoCode, Is.EqualTo("AU"));
                Assert.That(PhoneCountries.ByDialCode("358")?.IsoCode, Is.EqualTo("FI"));
            });
        }

        [Test]
        public void SearchPrefersNamesThatStartWithTheQuery()
        {
            var matches = PhoneCountries.Search("ind");

            Assert.That(matches.First().IsoCode, Is.EqualTo("IN"));
            Assert.That(matches.Select(country => country.IsoCode), Does.Contain("IO"));
        }

        [Test]
        public void SearchAlsoMatchesIsoCodesAndDialCodes()
        {
            Assert.That(PhoneCountries.Search("gb").First().IsoCode, Is.EqualTo("GB"));
            Assert.That(PhoneCountries.Search("+351").Select(country => country.IsoCode), Does.Contain("PT"));
            Assert.That(PhoneCountries.Search("351").Select(country => country.IsoCode), Does.Contain("PT"));
        }

        [Test]
        public void AnEmptySearchReturnsEverything()
        {
            Assert.That(PhoneCountries.Search(""), Has.Count.EqualTo(PhoneCountries.All.Count));
            Assert.That(PhoneCountries.Search(null), Has.Count.EqualTo(PhoneCountries.All.Count));
        }

        [Test]
        public void SearchFindsNothingForAQueryNoCountryMatches()
        {
            Assert.That(PhoneCountries.Search("zzzzz"), Is.Empty);
        }

        [Test]
        public void NumbersWrittenWithSeparatorsAreAccepted()
        {
            Assert.Multiple(() =>
            {
                Assert.That(PhoneNumberModel.DescribeProblem(UnitedKingdom, "07700 900123"), Is.Null);
                Assert.That(PhoneNumberModel.DescribeProblem(UnitedKingdom, "(020) 7946-0958"), Is.Null);
                Assert.That(PhoneNumberModel.DescribeProblem(UnitedStates, "202.555.0181"), Is.Null);
            });
        }

        [Test]
        public void AnEmptyNumberIsRejected()
        {
            Assert.That(PhoneNumberModel.DescribeProblem(UnitedKingdom, ""), Is.EqualTo("Enter your phone number."));
            Assert.That(PhoneNumberModel.DescribeProblem(UnitedKingdom, "   "), Is.EqualTo("Enter your phone number."));
            Assert.That(PhoneNumberModel.DescribeProblem(UnitedKingdom, null), Is.EqualTo("Enter your phone number."));
        }

        [Test]
        public void SeparatorsWithoutAnyDigitsAreRejected()
        {
            Assert.That(PhoneNumberModel.DescribeProblem(UnitedKingdom, "()-"), Is.EqualTo("Enter your phone number."));
        }

        [Test]
        public void LettersAreRejectedWithTheirOwnReason()
        {
            Assert.That(
                PhoneNumberModel.DescribeProblem(UnitedKingdom, "0770 CALL ME"),
                Is.EqualTo("A phone number cannot contain letters."));
        }

        [Test]
        public void RetypingTheCountryCodeIsRejectedWithTheirOwnReason()
        {
            Assert.That(
                PhoneNumberModel.DescribeProblem(UnitedKingdom, "+447700900123"),
                Is.EqualTo("The country code is already set to +44 - enter only the rest of the number here."));
        }

        [Test]
        public void AnUnexpectedCharacterIsNamedInTheReason()
        {
            Assert.That(
                PhoneNumberModel.DescribeProblem(UnitedKingdom, "7700*900123"),
                Is.EqualTo("'*' cannot be part of a phone number."));
        }

        [Test]
        public void TooShortAndTooLongNumbersSayHowManyDigitsAreExpected()
        {
            Assert.Multiple(() =>
            {
                // +44 plus four digits is six, one short of the E.164 minimum of seven.
                Assert.That(
                    PhoneNumberModel.DescribeProblem(UnitedKingdom, "1234"),
                    Is.EqualTo("This number is too short - after +44 it needs at least 5 digits."));

                Assert.That(
                    PhoneNumberModel.DescribeProblem(UnitedKingdom, "1234567890123456"),
                    Is.EqualTo("This number is too long - after +44 it can have at most 13 digits."));
            });
        }

        [Test]
        public void LengthIsCountedAgainstTheWholeInternationalNumber()
        {
            // 7 digits total is the E.164 minimum: +376 leaves room for four more.
            var andorra = Country("AD");

            Assert.That(PhoneNumberModel.DescribeProblem(andorra, "123"), Is.Not.Null);
            Assert.That(PhoneNumberModel.DescribeProblem(andorra, "1234"), Is.Null);
        }

        [Test]
        public void TheTrunkZeroIsDroppedFromTheInternationalNumber()
        {
            Assert.That(PhoneNumberModel.ToE164(UnitedKingdom, "07700 900123"), Is.EqualTo("+447700900123"));
            Assert.That(PhoneNumberModel.ToE164(UnitedKingdom, "7700900123"), Is.EqualTo("+447700900123"));
        }

        [Test]
        public void OnlyOneTrunkZeroIsDropped()
        {
            Assert.That(PhoneNumberModel.SignificantDigits(UnitedKingdom, "0044770"), Is.EqualTo("044770"));
        }

        [Test]
        public void ItalyKeepsItsLeadingZero()
        {
            // Italy is the numbering plan that carries the trunk 0 into the international form,
            // so +39 06 6982 is the Vatican switchboard rather than +39 66982.
            Assert.That(PhoneNumberModel.ToE164(Italy, "06 6982"), Is.EqualTo("+3906 6982".Replace(" ", "")));
            Assert.That(PhoneNumberModel.SignificantDigits(Italy, "06 6982"), Is.EqualTo("066982"));
        }

        [Test]
        public void ValidE164StringsRoundTripBackToTheirCountry()
        {
            var (country, nationalNumber) = PhoneNumberModel.Parse("+447700900123");

            Assert.That(country.IsoCode, Is.EqualTo("GB"));
            Assert.That(nationalNumber, Is.EqualTo("7700900123"));
        }

        [Test]
        public void ParseTakesTheLongestMatchingDialCode()
        {
            Assert.That(PhoneNumberModel.Parse("+351912345678").Country.IsoCode, Is.EqualTo("PT"));
            Assert.That(PhoneNumberModel.Parse("+998901234567").Country.IsoCode, Is.EqualTo("UZ"));
            Assert.That(PhoneNumberModel.Parse("+12025550181").Country.IsoCode, Is.EqualTo("US"));
        }

        [Test]
        public void ParseFallsBackForNumbersThatAreNotInternational()
        {
            var (country, nationalNumber) = PhoneNumberModel.Parse("07700 900123", UnitedKingdom);

            Assert.That(country, Is.EqualTo(UnitedKingdom));
            Assert.That(nationalNumber, Is.EqualTo("07700900123"));
        }

        [Test]
        public void ParseHandlesNullAndEmptyWithoutThrowing()
        {
            Assert.That(PhoneNumberModel.Parse(null, UnitedKingdom).NationalNumber, Is.Empty);
            Assert.That(PhoneNumberModel.Parse("", UnitedKingdom).NationalNumber, Is.Empty);
            Assert.That(PhoneNumberModel.Parse("+", UnitedKingdom).Country, Is.EqualTo(UnitedKingdom));
        }

        [Test]
        public void OnlyPlusPrefixedNumbersOfTheRightLengthCountAsE164()
        {
            Assert.Multiple(() =>
            {
                Assert.That(PhoneNumberModel.IsValidE164("+447700900123"), Is.True);
                Assert.That(PhoneNumberModel.IsValidE164("+1234567"), Is.True);

                Assert.That(PhoneNumberModel.IsValidE164("447700900123"), Is.False, "no plus");
                Assert.That(PhoneNumberModel.IsValidE164("+123456"), Is.False, "six digits");
                Assert.That(PhoneNumberModel.IsValidE164("+1234567890123456"), Is.False, "sixteen digits");
                Assert.That(PhoneNumberModel.IsValidE164("+44 7700 900123"), Is.False, "spaces");
                Assert.That(PhoneNumberModel.IsValidE164("+0447700900123"), Is.False, "leading zero");
                Assert.That(PhoneNumberModel.IsValidE164(""), Is.False);
                Assert.That(PhoneNumberModel.IsValidE164(null), Is.False);
            });
        }

        [Test]
        public void EveryAcceptedNumberComposesIntoAValidE164String()
        {
            Assert.Multiple(() =>
            {
                foreach (var country in PhoneCountries.All)
                {
                    // The shortest national part this country will accept.
                    var shortest = Math.Max(1, PhoneNumberModel.MINIMUM_E164_DIGITS - country.DialCode.Length);
                    var nationalNumber = new string('9', shortest);

                    Assert.That(PhoneNumberModel.DescribeProblem(country, nationalNumber), Is.Null, country.Name);
                    Assert.That(PhoneNumberModel.IsValidE164(PhoneNumberModel.ToE164(country, nationalNumber)), Is.True, country.Name);
                }
            });
        }

        [Test]
        public void NoCountryIsRejected()
        {
            Assert.That(
                PhoneNumberModel.DescribeProblem(null, "7700900123"),
                Is.EqualTo("Select the country your number belongs to."));
        }
    }
}
