using Substrate.NET.Wallet;

namespace PlutoFramework.Model
{
    /// <summary>
    /// The rules an app password has to satisfy.
    /// </summary>
    /// <remarks>
    /// Lives in Core so the labels a user reads and the rule the Continue button obeys are
    /// the same expression, and so they can be tested - nothing in the MAUI project can be.
    /// The expressions are copied unchanged from the screen that used to hold them inline.
    /// </remarks>
    public static class PasswordRulesModel
    {
        public const int MINIMUM_LENGTH = 6;

        public const int MAXIMUM_LENGTH = 20;

        public static bool HasAllowedLength(string? password) =>
            WordManager.Create()
                .WithMinimumLength(MINIMUM_LENGTH)
                .WithMaximumLength(MAXIMUM_LENGTH)
                .IsValid(password ?? "");

        public static bool HasLowercase(string? password) =>
            WordManager.Create().Should().AtLeastOneLowercase().IsValid(password ?? "");

        public static bool HasUppercase(string? password) =>
            WordManager.Create().Should().AtLeastOneUppercase().IsValid(password ?? "");

        public static bool HasDigit(string? password) =>
            WordManager.Create().Should().AtLeastOneDigit().IsValid(password ?? "");

        public static bool IsValid(string? password) =>
            HasAllowedLength(password) &&
            HasLowercase(password) &&
            HasUppercase(password) &&
            HasDigit(password);
    }
}
