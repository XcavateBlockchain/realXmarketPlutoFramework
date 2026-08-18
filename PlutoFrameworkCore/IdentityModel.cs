namespace PlutoFramework.Model
{
	public class OnChainIdentity
	{
		public required string DisplayName { get; set; }
		public Judgement FinalJudgement { get; set; }
	}

	public enum Judgement
	{
		Unknown,
		Reasonable,
        KnownGood,
        OutOfDate,
        LowQuality,
        Erroneous,
    }
}
