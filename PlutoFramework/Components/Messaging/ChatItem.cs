namespace PlutoFramework.Components.Messaging;

public record ChatItem
{
    public required int NamespaceId { get; set; }
    public required int BucketId { get; set; }
    public required string Title { get; set; }
    public required string State { get; set; }
    public required string Time { get; set; }
    public required string IsApproved { get; set; }
    public required Color IsApprovedColor { get; set; }
    public required Color IsApprovedBgColor { get; set; }
}
