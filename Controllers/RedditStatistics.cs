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
			Dictionary<string, int> dups = list.GroupBy(host => host)
			  .Where(grouping => grouping.Count() > 1)
			  .ToDictionary(g => g.Key, g => g.Count());

			int sum = dups.Values.Sum();

			// make nums into percentages

			Dictionary<string, double> percents = new Dictionary<string, double>();

			foreach (var pair in dups)
			{
				double percent = pair.Value / sum * 100;
				percents.Add(pair.Key, percent);
			}

			return percents;
		}

		public Dictionary<string, double> GetLinkedWebsites(List<string> urls)
		{
			List<string> validUrls = urls.Where(u => !string.IsNullOrEmpty(u)).ToList();

			List<string> foreignUrls = validUrls
				.Where(url => !url.Contains(".redd.it")).ToList();

			_logger.LogError("foreigns: " + foreignUrls.Count + ", " + foreignUrls[0]);

			return GetPercents(foreignUrls);
		}

		public Dictionary<string, double> GetRelatedSubreddits(List<Reddit.Controllers.User> users, string subName)
		{
			// this sub community's other frequented subs

			List<Reddit.Controllers.Post> postHistories = new List<Reddit.Controllers.Post>();

			foreach (Reddit.Controllers.User user in users)
			{ 
				postHistories.AddRange(user.PostHistory);
			}

			List<string> subs = postHistories.Select(p => p.Subreddit).ToList();

			List<string> foreignSubs = subs.Where(s => s != subName).ToList();

			return GetPercents(foreignSubs);
		}


		public Dictionary<string, double> GetCrosspostedSubs(Reddit.Controllers.Subreddit sub)
		{
			var crosspostables = sub.Posts.Hot.Where(p => p.Listing.IsCrosspostable);

			if (!crosspostables.Any())
			{
				return null;
			}

			var crossposts = crosspostables.Where(p => p.Listing.URL.StartsWith("/r/"));

			var crosspostSubs = new List<string>();

			foreach (Reddit.Controllers.Post p in crossposts)
			{
				string trimmed = p.Listing.URL.Remove(0, 3);
				int firstSlash = trimmed.IndexOf('/');

				int count = trimmed.Length - firstSlash;
				string subName = trimmed.Remove(firstSlash, count);
				crosspostSubs.Add(subName);
			}

			return GetPercents(crosspostSubs);
		}

		public QueryPopularity GetQueryPopularity(RedditUser redditor, string query)
		{
			// find dates of posts right now
			Reddit.Inputs.Search.SearchGetSearchInput q =
					new Reddit.Inputs.Search.SearchGetSearchInput(query)
					{ t = "month", limit = 100, sort = "top" };

			var monthList = redditor.Client.Search(q).ToList();

			var nowDates = monthList.Select(p => p.Listing.CreatedUTC).ToList();


			// find dates a month before
			var beforeAnchorPost = monthList.OrderByDescending(p => p.Listing.CreatedUTC).ToList()[0];

			Reddit.Inputs.Search.SearchGetSearchInput q2 =
					new Reddit.Inputs.Search.SearchGetSearchInput(query) { after = "t3_" + beforeAnchorPost.Id, count = 100 };

			var beforeMonthList = redditor.Client.Search(q2).ToList();

			var beforeDates = beforeMonthList.Select(p => p.Listing.CreatedUTC).ToList();



			// check frequency of each block of posts
			List<TimeSpan> nowTs = nowDates.Select((d, i) => d - nowDates[i + 1]).ToList();
			List<TimeSpan> beforeTs = beforeDates.Select((d, i) => d - nowDates[i + 1]).ToList();

			double avgDistanceNow = nowTs.Average(p => p.Milliseconds);
			double avgDistanceBefore = beforeTs.Average(p => p.Milliseconds);

			double margin = 15000;

			// put data into object
			double lesserVariance = avgDistanceBefore - margin;
			double greaterVariance = avgDistanceBefore + margin;

			QueryPopularity queryPop = new QueryPopularity();

			queryPop.ResultFrequencyBefore = avgDistanceBefore;
			queryPop.ResultFrequencyNow = avgDistanceNow;

			if (lesserVariance <= avgDistanceNow && avgDistanceNow <= greaterVariance)
				queryPop.SimilarDifference = true;

			return queryPop;
		}

		public Dictionary<string, double> GetCommonSubreddits(List<Reddit.Controllers.Post> queryList)
		{
			List<string> subs = queryList.Select(p => p.Subreddit).ToList();

			return GetPercents(subs);
		}
	}
}
