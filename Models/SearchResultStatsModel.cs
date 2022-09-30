using System.Collections.Generic;

namespace GlanceReddit.Models
{
	public class SearchResultStatsModel
	{
		public Dictionary<string, double> CommonResultSubreddits { get; set; }

		public QueryPopularity SearchPopularity { get; set; }
	}
}
