namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// The off-chain lifecycle states a developer's property travels through. They are the
    /// web app's <c>PROPERTY_STATUSES</c> verbatim, and <c>Pending</c> is deliberately
    /// absent: the web enum has it but its page never queries it, and nothing is ever written
    /// with it, so offering it would be a pill that is always empty.
    /// </summary>
    public enum DeveloperPropertyStatus
    {
        Draft,
        Submitted,
        Verified,
        Rejected,
        Listed,
    }
}
