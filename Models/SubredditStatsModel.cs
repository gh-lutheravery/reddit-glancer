using System.Collections.Generic;

namespace GlanceReddit.Models
{
	public class SubredditStatsModel
	{
		public Dictionary<string, int> ForeignWebsites { get; set; }

		public List<string> RelatedSubreddits { get; set; }

		public Dictionary<string, int> CrosspostedSubreddits { get; set; }
	}
}
