using System.Text.Json;

namespace PlutoFramework.Components.Notifications
{
    /// <summary>
    /// The device-local record of push notifications, backing <see cref="NotificationsPage"/>.
    /// The notifications API keeps no history - a push is fire-and-forget - so what the
    /// user can revisit is exactly what this device observed: pushes delivered while the
    /// app was in the foreground, and tray notifications the user tapped.
    /// </summary>
    public static class NotificationsModel
    {
        private const string PreferencesKey = "received_notifications";

        /// <summary>
        /// Newest first; the oldest entries fall off past this count so an install that
        /// never clears anything cannot grow the preferences store forever.
        /// </summary>
        private const int MaxStored = 100;

        /// <summary>
        /// A tap on a tray notification can follow a foreground delivery of the same
        /// push. Entries matching on content within this window are treated as one.
        /// </summary>
        private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(5);

        private static readonly object Lock = new();

        /// <summary>
        /// Raised after every change, on whatever thread made it - Firebase callbacks
        /// arrive off the UI thread, so listeners must marshal before touching bindings.
        /// </summary>
        public static event Action? Changed;

        public static IReadOnlyList<Notification> GetAll()
        {
            lock (Lock)
            {
                return Load();
            }
        }

        /// <summary>Records a push delivered while the app was in the foreground.</summary>
        public static void AddReceived(string? title, string? body) => Add(title, body, wasRead: false);

        /// <summary>
        /// Records the push behind a tray notification the user tapped. Tapping is
        /// reading, so the entry lands already read - and when the same push was already
        /// recorded from a foreground delivery, that entry is marked read instead of
        /// being duplicated.
        /// </summary>
        public static void AddTapped(string? title, string? body) => Add(title, body, wasRead: true);

        public static void MarkRead(Guid id)
        {
            var changed = false;

            lock (Lock)
            {
                var notifications = Load();
                var target = notifications.FirstOrDefault(n => n.Id == id);

                if (target is not null && !target.WasRead)
                {
                    target.WasRead = true;
                    Save(notifications);
                    changed = true;
                }
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        private static void Add(string? title, string? body, bool wasRead)
        {
            // The API requires a title, but the payload arrives through enough layers
            // that an empty push is worth guarding against rather than displaying.
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            var changed = false;

            lock (Lock)
            {
                var notifications = Load();

                var duplicate = notifications.FirstOrDefault(n =>
                    n.Title == (title ?? "")
                    && n.Message == body
                    && DateTime.Now - n.Date < DuplicateWindow);

                if (duplicate is not null)
                {
                    if (wasRead && !duplicate.WasRead)
                    {
                        duplicate.WasRead = true;
                        Save(notifications);
                        changed = true;
                    }
                }
                else
                {
                    notifications.Insert(0, new Notification
                    {
                        Title = title ?? "",
                        Message = body,
                        Date = DateTime.Now,
                        Type = NotificationType.System,
                        WasRead = wasRead,
                    });

                    Save([.. notifications.Take(MaxStored)]);
                    changed = true;
                }
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        private static List<Notification> Load()
        {
            try
            {
                var json = Preferences.Default.Get(PreferencesKey, "");

                return string.IsNullOrEmpty(json)
                    ? []
                    : JsonSerializer.Deserialize<List<Notification>>(json) ?? [];
            }
            catch
            {
                // A corrupt store should cost the history, not the page.
                return [];
            }
        }

        private static void Save(List<Notification> notifications)
        {
            Preferences.Default.Set(PreferencesKey, JsonSerializer.Serialize(notifications));
        }
    }
}
