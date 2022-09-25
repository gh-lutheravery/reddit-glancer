using System.Collections.Generic;

namespace GlanceReddit.Models
{
	public class SubredditStatsModel
	{
		public Dictionary<string, double> ForeignWebsites { get; set; }

		public Dictionary<string, double> RelatedSubreddits { get; set; }

		public Dictionary<string, double> CrosspostedSubreddits { get; set; }
	}
}
