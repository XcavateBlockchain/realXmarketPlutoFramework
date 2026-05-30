namespace PlutoFrameworkCore
{
    public class TimeModel
    {
        public static string GetTimeLeftText(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
            {
                if (Math.Round(timeSpan.TotalDays) == 1)
                {
                    return "1 day left";
                }
                return $"{Math.Round(timeSpan.TotalDays)} days left";
            }
            else if (timeSpan.TotalHours >= 1)
            {
                if (Math.Round(timeSpan.TotalHours) == 1)
                {
                    return "1 hour left";
                }
                return $"{Math.Round(timeSpan.TotalHours)} hours left";
            }
            else if (timeSpan.TotalSeconds > 0)
            {
                if (timeSpan.TotalMinutes <= 1)
                {
                    return "1 minute left";
                }

                return $"{Math.Round(timeSpan.TotalMinutes)} minutes left";
            }

            return "Expired";
        }
    }
}
