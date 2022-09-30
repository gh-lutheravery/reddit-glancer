namespace GlanceReddit.Models
{
	public class QueryPopularity
	{
		public double ResultFrequencyBefore { get; set; }
		public double ResultFrequencyNow { get; set; }

		public int DifferenceOfFrequencyNowFromFrequencyBefore { get; set; }
		public bool SimilarDifference { get; set; }
	}
}
