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
			_logger.LogError("begin");
			// this sub community's other frequented subs
			List<string> subs = new List<string>();
			/*
			var postHist = users.SelectMany(u => u.PostHistory.Take(5));

			subs = postHist.Select(p => p.Subreddit).ToList();

			*/

			users.RemoveAll(u => u.PostHistory.Count < 5);

			subs = users.Select((u, i) => 
				GetSubreddits(u.PostHistory.Take(5).ToList(), i, 4))
				.SelectMany(p => p)
				.ToList();

			
			List<string> foreignSubs = subs.Where(s => s != subName).ToList();
			_logger.LogError("end");
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
			QueryPopularity queryPop = new QueryPopularity();

			// find dates of posts right now
			Reddit.Inputs.Search.SearchGetSearchInput searchPostsNow =
					new Reddit.Inputs.Search.SearchGetSearchInput(query)
					{ 
						t = "month", 
						limit = 100, 
						sort = "top" 
					};

			var monthList = await RedditSearchAsync(redditor, searchPostsNow);

			var nowDates = monthList.Select(p => p.Listing.CreatedUTC)
				.OrderByDescending(d => d).ToList();

			// find dates month before
			var beforeAnchorPost = monthList.OrderBy(p => p.Listing.CreatedUTC).ToList()[0];

			Reddit.Inputs.Search.SearchGetSearchInput searchPostsBefore =
					new Reddit.Inputs.Search.SearchGetSearchInput(query)
					{
						after = "t3_" + beforeAnchorPost.Id,
						limit = 100,
						q = query,
						t = "month",
						sort = "top"
					};

			var beforeMonthList = await RedditSearchAsync(redditor, searchPostsBefore);

			if (beforeMonthList.Count == 0)
			{
				searchPostsBefore.t = "all";
				beforeMonthList = await RedditSearchAsync(redditor, searchPostsBefore);
			}

			var beforeDates = beforeMonthList.Select(p => p.Listing.CreatedUTC)
				.OrderByDescending(d => d).ToList();

			// check frequency of each dates before and dates now
			int LowDataThreshold = 40;
			List<TimeSpan> nowTs = nowDates.Select(d => GetDistanceOfDates(d, nowDates)).ToList();
			List<TimeSpan> beforeTs = beforeDates.Select(d => GetDistanceOfDates(d, beforeDates)).ToList();

			if (beforeTs.Count < LowDataThreshold)
			{
				List<TimeSpan> trimmedNowTs = nowTs.Take(beforeTs.Count).ToList();
				queryPop.LowData = true;
			}

			double avgDistanceNow = nowTs.Average(p => p.TotalSeconds);
			double avgDistanceBefore = beforeTs.Average(p => p.TotalSeconds);

			// put data into object
			queryPop.ResultFrequencyBefore = avgDistanceBefore;
			queryPop.ResultFrequencyNow = avgDistanceNow;

			// get rounded percentage, then negate positive/negative
			int percent = (int)Math.Round(((avgDistanceNow - avgDistanceBefore) / avgDistanceBefore * 100));
			percent *= -1;

			queryPop.PercentDifference = percent;

			// check if frequency now is effectively the same as before
			int PopularTopicThreshold = 50000;

			double similarityMargin = avgDistanceBefore < PopularTopicThreshold ? 5000 : 15000;

			double lesserVariance = avgDistanceBefore - similarityMargin;
			double greaterVariance = avgDistanceBefore + similarityMargin;

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
