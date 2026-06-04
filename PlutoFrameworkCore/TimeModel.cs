namespace PlutoFrameworkCore
{
    public class TimeModel
    {
        public static string GetTimeLeftText(TimeSpan timeSpan, string endMessage = "left")
        {
            if (timeSpan.TotalDays >= 1)
            {
                if (Math.Round(timeSpan.TotalDays) == 1)
                {
                    return $"1 day {endMessage}";
                }

                return $"{Math.Round(timeSpan.TotalDays)} days {endMessage}";
            }
            else if (timeSpan.TotalHours >= 1)
            {
                if (Math.Round(timeSpan.TotalHours) == 1)
                {
                    return $"1 hour {endMessage}";
                }

                return $"{Math.Round(timeSpan.TotalHours)} hours {endMessage}";
            }
            else if (timeSpan.TotalSeconds > 0)
            {
                if (timeSpan.TotalMinutes <= 1)
                {
                    return $"1 minute {endMessage}";
                }

                return $"{Math.Round(timeSpan.TotalMinutes)} minutes {endMessage}";
            }

            return "Expired";
        }
    }
}
