using GlanceReddit.ViewModels;
using GlanceReddit.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

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
			  .Where(grouping => grouping.Count() > 1)
			  .ToDictionary(g => g.Key, g => (double)g.Count());


			double sum = dups.Values.Sum();
			_logger.LogError("sum: " + sum);
			_logger.LogError("percent: " + (dups.Values.First() / sum) * 100);

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

			List<Reddit.Controllers.Post> postHistories = new List<Reddit.Controllers.Post>();

			foreach (Reddit.Controllers.User user in users)
			{ 
				postHistories.AddRange(user.PostHistory);
			}
			_logger.LogError("Loop has finished execution: " + postHistories[0].Subreddit);

			List<string> subs = postHistories.Select(p => p.Subreddit).ToList();

			List<string> foreignSubs = subs.Where(s => s != subName).ToList();

			_logger.LogError("foreignSub: " + foreignSubs[0]);

			return GetPercents(foreignSubs);
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

		public QueryPopularity GetQueryPopularity(RedditUser redditor, string query)
		{
			// find dates of posts right now
			Reddit.Inputs.Search.SearchGetSearchInput q =
					new Reddit.Inputs.Search.SearchGetSearchInput(query)
					{ t = "month", limit = 100, sort = "top" };

			var monthList = redditor.Client.Search(q).ToList();

			var nowDates = monthList.Select(p => p.Listing.CreatedUTC)
				.OrderByDescending(d => d).ToList();

			//_logger.LogError("monthList count and element: " + monthList.Count);

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

			var beforeMonthList = redditor.Client.Search(q2).ToList();
			_logger.LogError("beforeMonthList: " + beforeMonthList.Count);

			// get dates, then sort to get post frequency correctly
			var beforeDates = beforeMonthList.Select(p => p.Listing.CreatedUTC)
				.OrderByDescending(d => d).ToList();

			//_logger.LogError("beforeDates element: " + beforeDates[0].ToString());

			// check frequency of each dates before and dates now
			List<TimeSpan> nowTs = nowDates.Select(d => GetDistanceOfDates(d, nowDates)).ToList();
			List<TimeSpan> beforeTs = beforeDates.Select(d => GetDistanceOfDates(d, beforeDates)).ToList();

			//_logger.LogError("timespans: " + string.Join(", ", nowTs.Select(p => p.TotalMilliseconds)));
			//_logger.LogError("beforeTimespans: " + string.Join(", ", beforeTs.Select(p => p.TotalMilliseconds)));

			double avgDistanceNow = nowTs.Average(p => p.TotalSeconds);
			double avgDistanceBefore = beforeTs.Average(p => p.TotalSeconds);

			//_logger.LogError("distances: " + avgDistanceNow + ", " + avgDistanceBefore);

			double margin = 15000;

			// put data into object
			double lesserVariance = avgDistanceBefore - margin;
			double greaterVariance = avgDistanceBefore + margin;

			//_logger.LogError("variances: " + lesserVariance + ", " + greaterVariance);

			QueryPopularity queryPop = new QueryPopularity();

			queryPop.ResultFrequencyBefore = avgDistanceBefore;
			queryPop.ResultFrequencyNow = avgDistanceNow;
			queryPop.PercentDifference = (int)((avgDistanceNow - avgDistanceBefore) / avgDistanceBefore * 100);

			// is the post frequency right now somewhat similar to a month before?
			if (lesserVariance <= avgDistanceNow && avgDistanceNow <= greaterVariance)
				queryPop.SimilarDifference = true;

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
