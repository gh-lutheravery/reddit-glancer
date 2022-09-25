using GlanceReddit.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GlanceReddit.Controllers
{
	public class RedditStatistics : Controller
	{
		public SubredditStats GetLinkedWebsites()
		{
			// get all link posts from subreddit

			List<Reddit.Controllers.Post> totalLinkPosts = sub.Posts.Hot
				.Where(post => post.Listing.URL != null).ToList();

			// get all link posts not from reddit

			List<Reddit.Controllers.Post> foreignLinkPosts = sub.Posts.Hot
				.Where(post => !post.Listing.URL.Contains(".redd.it")).ToList();

			// make a list of all sites

			List<string> sites = foreignLinkPosts.Select(post => post.Listing.URL).ToList();

			// identify all sites from results

			List<string> hosts = sites.Select(site => new Uri(site))
								.Select(post => post.Host).ToList();

			stats.Percents = GetPercents(hosts);
		}

		public SubredditStats GetRelatedSubreddits(Reddit.Controllers.Subreddit sub)
		{
			//-- this sub community's other frequented subs

			// get all users that made submissions in the sub

			List<string> usernames = sub.Posts.Hot.Select(post => post.Author).ToList();

			// get all posts from each

			Reddit.Inputs.Search.SearchGetSearchInput q =
					new Reddit.Inputs.Search.SearchGetSearchInput(username)
					{ type = "user" };

			List<Reddit.Controllers.User> users = usernames.Select(u => redditor.Client.User(u).About());

			List<Reddit.Controllers.Post> postHistories = new List<Reddit.Controllers.Post>();

			foreach (Reddit.Controllers.User user in users)
			{ 
				postHistories.AddRange(user.PostHistory);
			}

			// filter all that are from the current sub

			List<string> subs = postHistories.Select(p => p.Subreddit).ToList();

			List<string> foreignSubs = subs.Where(s => s != sub.Name).ToList();

			stats.Percents = GetPercents(foreignSubs);
		}

		private Dictionary<string, float> GetPercents(List<string> list)
		{
			Dictionary<string, int> dups = list.GroupBy(host => host)
			  .Where(grouping => grouping.Count() > 1)
			  .ToDictionary(g => g.Key, g => g.Count());

			int sum = dups.Values.Sum();

			// make nums into percentages

			Dictionary<string, float> percents = new Dictionary<string, float>();

			foreach (var pair in dups)
			{
				float percent = pair.Value / sum * 100;
				percents.Add(pair.Key, percent);
			}

			return percents;
		}


		public SearchStats GetCrosspostedSubs(Reddit.Controllers.Subreddit sub)
		{
			// get all crossposts from other first raw data list
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

			stats.Percents = GetPercents(crosspostSubs);
		}

		public SearchStats GetQueryPopularity(RedditUser redditor, string query)
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

			double avgDistanceNow = nowTs.Average(p => p.Ticks);
			double avgDistanceBefore = beforeTs.Average(p => p.Ticks);


			// put data into object
			int lesserVariance = avgDistanceBefore - Less;
			int greaterVariance = avgDistanceBefore + More;

			stats.DistBefore = avgDistanceBefore;
			stats.DistNow = avgDistanceNow;

			if (lesserVariance <= avgDistanceNow && avgDistanceNow <= greaterVariance)
				stats.Similarity = true;

			else
			{
				if (avgDistanceNow > moreVal)
				{
					stats.Decreasing = true;
				}
				else
				{
					stats.Increasing = true;
				}
			}
		}

		public SearchStats GetCommonSubreddits(Reddit.Controllers.Subreddit sub)
		{
			List<string> subs = queryList.Select(p => p.Subreddit).ToList();

			stats.Percents = GetPercents(subs);
		}

		public SearchStats GetInteractionRate(Reddit.Controllers.Subreddit sub)
		{
			// get results from last two methods in GetSubredditStats
		}
	}
}
