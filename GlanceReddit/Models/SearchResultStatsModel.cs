using System.Collections.Generic;

namespace GlanceReddit.Models
{
	public class SearchResultStatsModel
	{
		public Dictionary<string, int> CommonResultSubreddits { get; set; }

		public QueryPopularity SearchPopularity { get; set; }
	}
}
