﻿using GlanceReddit.ViewModels;
using GlanceReddit.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

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
				.Where(url => !url.Contains("redd")).ToList();

			if (foreignUrls.Count == 0)
			{
				Dictionary<string, double> dict = new Dictionary<string, double>();
				dict.Add("No websites found.", 0);
				return dict;
			}

			// remove string slice after third slash occurence, leaving just the host
			List<string> hosts = foreignUrls.Select(u => u.Remove(NthIndexOf(u, "/", 3))).ToList();

			return GetPercents(hosts);
		}

		private int NthIndexOf(string target, string value, int n)
		{
			Match m = Regex.Match(target, "((" + Regex.Escape(value) + ").*?){" + n + "}");

			if (m.Success)
				return m.Groups[2].Captures[n - 1].Index;
			else
				return -1;
		}

		public List<string> GetRelatedSubreddits(RedditUser redditor, string subName)
		{
			List<Reddit.Controllers.Subreddit> searchedSubs = redditor.Client
				.SearchSubreddits(subName);

			List<string> subs = searchedSubs.Select(p => p.Name).ToList();
			subs.Remove(subs.Find(s => s == subName));

			return subs;
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

			_logger.LogError(nowTs[0].Seconds.ToString());
			double avgDistanceNow = nowTs.Average(p => p.TotalSeconds);
			double avgDistanceBefore = beforeTs.Average(p => p.TotalSeconds);


			// put data into object
			queryPop.ResultFrequencyBefore = avgDistanceBefore;
			queryPop.ResultFrequencyNow = avgDistanceNow;

			queryPop.ReducedResultFrequencyBefore = 500 / (avgDistanceBefore / avgDistanceNow);
			queryPop.ReducedResultFrequencyNow = 500 / (avgDistanceNow / avgDistanceBefore);


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

			return GetPercents(subs);
		}
	}
}
