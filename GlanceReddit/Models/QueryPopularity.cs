namespace GlanceReddit.Models
{
	public class QueryPopularity
	{
		public double ResultFrequencyBefore { get; set; }

		public double ResultFrequencyNow { get; set; }

		public double ReducedResultFrequencyBefore { get; set; }

		public double ReducedResultFrequencyNow { get; set; }

		public bool SimilarDifference { get; set; }

		public int PercentDifference { get; set; }

		public bool LowData { get; set; }
	}
}
