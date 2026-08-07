using System.Text.Json.Serialization;

namespace PlutoFramework.Components.Notifications
{
    public enum NotificationType
    {
        None,

        All,

        System,
        Announcement,
    }

    public record Notification
    {
        /// <summary>
        /// Identity for mark-as-read: pushes carry no server id, and two pushes can
        /// legitimately share title, message and type.
        /// </summary>
        public Guid Id { get; init; } = Guid.NewGuid();

        public required string Title { get; set; }
        public string? Message { get; set; }
        public required DateTime Date { get; set; }
        public NotificationType Type { get; set; } = NotificationType.None;
        public required bool WasRead { get; set; }

        [JsonIgnore]
        public bool WasNotRead => !WasRead;
    }
}
