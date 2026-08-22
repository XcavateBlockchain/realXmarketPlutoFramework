using System.Text.RegularExpressions;

namespace PlutoFramework.Model
{
    public class FormModel
    {
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            string emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            var regex = new Regex(emailRegex, RegexOptions.IgnoreCase);

            return regex.IsMatch(email);
        }

        /// <summary>
        /// Null when the address is acceptable, otherwise one sentence naming the thing to fix.
        /// </summary>
        /// <remarks>
        /// Every branch mirrors a way <see cref="IsValidEmail"/> can fail, so the two never
        /// disagree: the field cannot show a reason while the button stays enabled, or the other
        /// way round.
        /// </remarks>
        public static string? DescribeEmailProblem(string? email)
        {
            var text = email ?? "";

            if (string.IsNullOrWhiteSpace(text))
            {
                return "Enter your email address.";
            }

            if (text.Any(char.IsWhiteSpace))
            {
                return "An email address cannot contain spaces.";
            }

            var atCount = text.Count(character => character == '@');

            if (atCount == 0)
            {
                return "An email address needs an @, like name@example.com.";
            }

            if (atCount > 1)
            {
                return "An email address can only contain one @.";
            }

            var parts = text.Split('@');

            if (parts[0] == "")
            {
                return "Add the part before the @, like name@example.com.";
            }

            if (parts[1] == "")
            {
                return "Add the domain after the @, like name@example.com.";
            }

            if (!parts[1].Contains('.'))
            {
                return "The domain after the @ needs a dot, like example.com.";
            }

            if (parts[1].StartsWith(".") || parts[1].EndsWith("."))
            {
                return "The domain after the @ cannot start or end with a dot.";
            }

            return IsValidEmail(text) ? null : "This does not look like an email address.";
        }
    }
}
