using GlanceReddit.ViewModels;
using GlanceReddit.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace GlanceReddit.Controllers
{
	public class RedditStatistics : Controller
	{
		private readonly ILogger<ApiController> _logger;

		public RedditStatistics(ILogger<ApiController> logger)
		{
			_logger = logger;
		}

		private Dictionary<string, double> GetPercents(List<string> list)
		{
			Dictionary<string, double> dups = list.GroupBy(host => host)
			  .ToDictionary(g => g.Key, g => (double)g.Count());

			double sum = dups.Values.Sum();

			// make nums into percentages
			Dictionary<string, double> percents = new Dictionary<string, double>();

			foreach (var pair in dups)
			{
				double percent = (pair.Value / sum) * 100;
				percents.Add(pair.Key, percent);
			}

			return percents;
		}

		public Dictionary<string, double> GetLinkedWebsites(List<string> urls)
		{
			List<string> validUrls = urls.Where(u => !string.IsNullOrEmpty(u)).ToList();

			List<string> foreignUrls = validUrls
				.Where(url => !url.Contains(".redd.it")).ToList();

			List<string> hosts = foreignUrls.Select(u => new Uri(u).Host).ToList();

			return GetPercents(hosts);
		}

		
		public Dictionary<string, double> GetRelatedSubreddits(List<Reddit.Controllers.User> users, string subName)
		{
			// this sub community's other frequented subs
			List<string> subs = new List<string>();

			subs = users.Select((u, i) => 
				GetSubreddits(u.PostHistory, i, u.PostHistory.Count - 1))
				.SelectMany(p => p)
				.ToList();

			_logger.LogError("Loop has finished execution: " + subs[0]);

			List<string> foreignSubs = subs.Where(s => s != subName).ToList();

			_logger.LogError("foreignSub: " + foreignSubs[0]);

			return GetPercents(foreignSubs);
		}

		private List<string> GetSubreddits(List<Reddit.Controllers.Post> posts, int index, int lastIndex)
		{
			if (index == lastIndex)
				return new List<string>();

			return GetSubreddits(posts, index + 1, lastIndex)
				.Append(posts[index].Subreddit)
				.ToList();
		}


		public Dictionary<string, double> GetCrosspostedSubs(Reddit.Controllers.Subreddit sub)
		{
			var crosspostables = sub.Posts.Hot.Where(p => p.Listing.IsCrosspostable && !p.Listing.IsSelf).ToList();

			if (!crosspostables.Any())
			{
				var err = new Dictionary<string, double>() { { "Error: No crosspostables.", 0 } };
				return err;
			}

			_logger.LogError("crosspostables: " + crosspostables.Count);

			var crossposts = crosspostables.Cast<Reddit.Controllers.LinkPost>()
				.Where(p => !p.URL.Contains(sub.Name));

			_logger.LogError("crossposts: " + string.Join(",", crossposts.Select(c => c.URL)));

			if (!crossposts.Any())
			{
				var err = new Dictionary<string, double>() { { "Error: No crossposts in hot.", 0 } };
				return err;
			}

			var crosspostSubs = new List<string>();

			foreach (Reddit.Controllers.LinkPost p in crossposts)
			{
				string trimmedUrl = p.URL;

				if (p.URL.StartsWith("http") && !p.URL.Contains('?'))
				{
					// delete https and reddit domain
					trimmedUrl = p.URL.Remove(0, 22);
				}

				if (p.URL.StartsWith("/r/"))
				{
					// remove /r/
					trimmedUrl = trimmedUrl.Remove(0, 3);

					// find the char that ends the subreddit name
					int firstSlash = trimmedUrl.IndexOf('/');

					// get the length of the subreddit name
					int count = trimmedUrl.Length - firstSlash;

					// chop off the rest of the url
					string subName = trimmedUrl.Remove(firstSlash, count);
					crosspostSubs.Add(subName);
				}
			}

			_logger.LogError("crosspostSubs: " + crosspostSubs.Count());

			return GetPercents(crosspostSubs);
		}

		public async Task<List<Reddit.Controllers.Post>> RedditSearchAsync(
			RedditUser redditor,
			Reddit.Inputs.Search.SearchGetSearchInput searchInput
			)
		{
			Func<List<Reddit.Controllers.Post>> searchFunc = 
				new Func<List<Reddit.Controllers.Post>>(() => 
				redditor.Client.Search(searchInput)
				.ToList());

			return await Task.Run(searchFunc);
		}

		public async Task<QueryPopularity> GetQueryPopularity(RedditUser redditor, string query)
		{
			_logger.LogError("GetQueryPopularity begun");
			QueryPopularity queryPop = new QueryPopularity();

			// find dates of posts right now
			Reddit.Inputs.Search.SearchGetSearchInput q =
					new Reddit.Inputs.Search.SearchGetSearchInput(query)
					{ t = "month", limit = 100, sort = "top" };

			var monthList = await RedditSearchAsync(redditor, q);

			var nowDates = monthList.Select(p => p.Listing.CreatedUTC)
				.OrderByDescending(d => d).ToList();

			// find dates month before
			var beforeAnchorPost = monthList.OrderBy(p => p.Listing.CreatedUTC).ToList()[0];

			Reddit.Inputs.Search.SearchGetSearchInput q2 =
					new Reddit.Inputs.Search.SearchGetSearchInput(query)
					{
						after = "t3_" + beforeAnchorPost.Id,
						limit = 100,
						q = query,
						t = "month",
						sort = "top"
					};

			var beforeMonthList = await RedditSearchAsync(redditor, q2);

			if (beforeMonthList.Count == 0)
			{
				q2.t = "all";
				beforeMonthList = await RedditSearchAsync(redditor, q2);
			}

			_logger.LogError("beforeMonthList: " + beforeMonthList.Count);

			// get dates, then sort to get post frequency correctly
			var beforeDates = beforeMonthList.Select(p => p.Listing.CreatedUTC)
				.OrderByDescending(d => d).ToList();

			//_logger.LogError("beforeDates element: " + beforeDates[0].ToString());

			// check frequency of each dates before and dates now
			int LowDataThreshold = 40;
			List<TimeSpan> nowTs = nowDates.Select(d => GetDistanceOfDates(d, nowDates)).ToList();
			List<TimeSpan> beforeTs = beforeDates.Select(d => GetDistanceOfDates(d, beforeDates)).ToList();

			if (beforeTs.Count < LowDataThreshold)
			{
				List<TimeSpan> trimmedNowTs = nowTs.Take(beforeTs.Count).ToList();
				queryPop.LowData = true;
			}

			//_logger.LogError("timespans: " + string.Join(", ", nowTs.Select(p => p.TotalMilliseconds)));
			//_logger.LogError("beforeTimespans: " + string.Join(", ", beforeTs.Select(p => p.TotalMilliseconds)));

			double avgDistanceNow = nowTs.Average(p => p.TotalSeconds);
			double avgDistanceBefore = beforeTs.Average(p => p.TotalSeconds);

			//_logger.LogError("distances: " + avgDistanceNow + ", " + avgDistanceBefore);

			// put data into object
			queryPop.ResultFrequencyBefore = avgDistanceBefore;
			queryPop.ResultFrequencyNow = avgDistanceNow;

			// get rounded percentage, then negate positive/negative
			int percent = (int)Math.Round(((avgDistanceNow - avgDistanceBefore) / avgDistanceBefore * 100));
			percent = percent * -1;

			queryPop.PercentDifference = percent;

			// check if frequency now is effectively the same as before
			int PopularTopicThreshold = 50000;

			double similarityMargin = avgDistanceBefore < PopularTopicThreshold ? 5000 : 15000;

			double lesserVariance = avgDistanceBefore - similarityMargin;
			double greaterVariance = avgDistanceBefore + similarityMargin;

			//_logger.LogError("Distance: " + avgDistanceNow + ", " + avgDistanceBefore);
			//_logger.LogError("variances: " + lesserVariance + ", " + greaterVariance);

			if (lesserVariance <= avgDistanceNow && avgDistanceNow <= greaterVariance)
				queryPop.SimilarDifference = true;

			_logger.LogError("GetQueryPopularity ended");
			return queryPop;
		}

		private TimeSpan GetDistanceOfDates(DateTime date, List<DateTime> dateList)
		{
			int lastIndex = dateList.Count - 1;
			int currentIndex = dateList.IndexOf(date);

			if (currentIndex != lastIndex)
			{
				return date - dateList[currentIndex + 1];
			}
				
			else
			{
				return new TimeSpan();
			}
		}

		public Dictionary<string, double> GetCommonSubreddits(List<Reddit.Controllers.Post> queryList)
		{
			List<string> subs = queryList.Select(p => p.Subreddit).ToList();

			_logger.LogError("subs count and element: " + subs.Count + ", " + subs[^1]);

			return GetPercents(subs);
		}
	}
}
