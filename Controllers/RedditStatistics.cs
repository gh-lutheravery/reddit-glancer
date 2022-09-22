using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GlanceReddit.Controllers
{
	public class RedditStatistics : Controller
	{
		public SubredditStats GetSubredditStats(Reddit.Controllers.Subreddit sub, List<Reddit.Controllers.Post> queryList)
		{
			//-- the most commonly linked websites can be shown

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

			// find number of duplicates

			Dictionary<string, int> dups = hosts.GroupBy(host => host)
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

			// insert into obj

			stats.Percents = percents;

			//-- the most common subreddits that crosspost to it (difficult/impossible to be accurate with api's tools)

			// get all crossposts from other first raw data list

			// for self post, permalink is body, link post is nothing rn

			// identify all subs from results

			// make a list of all subs

			// find number of duplicates

			// make nums into percentages

			// insert into obj

			//-- this sub community's other frequented subs

			// get all users that made submissions in the sub

			List<string> usernames = queryList.Select(post => post.Author).ToList();


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

			// identify all subs from results

			// make a list of all subs

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

		public SearchStats GetSearchStats(Reddit.Controllers.Subreddit sub)
		{
			//-- most common subreddits

			// get all subreddits from post list

			// find number of duplicates

			// make nums into percentages

			// insert into obj

			//-- get the rate of interaction between subreddits

			// get results from last two methods in GetSubredditStats

			//-- show number of recent submissions mentioning search keyword(s)

			// get dates of all posts from GetSearchResults

			// get 100 block of posts from 3 months ago

			// get dates of 3month posts

			// find density of each date groups

			// decide if density is similar or different

			// decide if either density is high or low

			// insert info into obj
		}

		// GET: RedditStatistics
		public ActionResult Index()
		{
			return View();
		}

		// GET: RedditStatistics/Details/5
		public ActionResult Details(int id)
		{
			return View();
		}

		// GET: RedditStatistics/Create
		public ActionResult Create()
		{
			return View();
		}

		// POST: RedditStatistics/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Create(IFormCollection collection)
		{
			try
			{
				return RedirectToAction(nameof(Index));
			}
			catch
			{
				return View();
			}
		}

		// GET: RedditStatistics/Edit/5
		public ActionResult Edit(int id)
		{
			return View();
		}

		// POST: RedditStatistics/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Edit(int id, IFormCollection collection)
		{
			try
			{
				return RedirectToAction(nameof(Index));
			}
			catch
			{
				return View();
			}
		}

		// GET: RedditStatistics/Delete/5
		public ActionResult Delete(int id)
		{
			return View();
		}

		// POST: RedditStatistics/Delete/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Delete(int id, IFormCollection collection)
		{
			try
			{
				return RedirectToAction(nameof(Index));
			}
			catch
			{
				return View();
			}
		}
	}
}
