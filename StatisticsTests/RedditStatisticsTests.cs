using GlanceReddit.Controllers;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Bogus;
using System.Linq;

namespace StatisticsTests
{
	public class RedditStatisticsTests
	{
		private readonly RedditStatistics rs;

		public RedditStatisticsTests()
		{ 
			rs = new RedditStatistics();
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
				double percent = (pair.Value / sum) * 100;
				percents.Add(pair.Key, percent);
			}

			return percents;
		}


		[Fact]
		public void GetLinkedWebsitesShouldReturnWebsiteOccurences()
		{
			// rulefor listing.urls on empty obj, assert data by 


			Randomizer.Seed = new Random(Seed: 69);

			var faker = new Faker();
			var uniqueUrls = new List<string>();

			foreach (var _ in Enumerable.Range(0, 4))
			{
				uniqueUrls.Add(faker.Internet.Url());
			}

			// get first five urls

			// get a random number for each url for dups
			List<int> dupOccurances = new List<int>() { 13, 5, 2, 1, 3 };

			// generate these in another list to put inside method. also make dict based on values.
			List<string> duplicateUrls = new List<string>();

			foreach(var url in uniqueUrls)
			{
				for(int num = 0; num < dupOccurances[uniqueUrls.IndexOf(url)] + 1; num++)
				{
					duplicateUrls.Add(url);
				}
			}

			Dictionary<string, double> expectedDuplicates = GetPercents(duplicateUrls);

			Dictionary<string, double> actualDuplicates = rs.GetLinkedWebsites(duplicateUrls);

			Assert.Equal(expectedDuplicates, actualDuplicates);
		}

		[Fact]
		public void GetRelatedSubredditsShouldReturnFrequentedSubredditsOfCommunity()
		{
			// use faker for objects. check by looping through total post history, returning expected dict.
			Faker faker = new Faker();

			List<string> fakeSubs = new List<string>();

			for (int i = 0; i < 20; i++)
			{
				Reddit.Controllers.Post post = new Faker<Reddit.Controllers.Post>()
				.RuleFor(p => p.Subreddit, p => p.Company.CompanyName());
			}

			List<Reddit.Controllers.User> users = new List<Reddit.Controllers.User>();

			for (int i = 0; i < 5; i++)
			{
				Reddit.Controllers.User post = new Faker<Reddit.Controllers.User>()
				.RuleFor(u => u.PostHistory, u => post);
			}

			Dictionary<string, double> expectedDuplicates = GetPercents(fakeSubDups);

			Dictionary<string, double> actualDuplicates = rs.GetRelatedSubreddits(users, fakeSubs[0]); 

			Assert.Equal(expectedDuplicates, actualDuplicates);

			// Subreddit.posts.hot
			// redditor.Client.User
		}

		// to check results, instead of making seperate expected dict, check the raw original data and check correlation between that and actual.

		[Fact]
		public void GetCrosspostedSubsShouldReturnSubsFrequentlyCrossposted()
		{
			// 
			List<Reddit.
		}

			/*
			private List<string> GetUrlDuplicates(string url, int index, List<string> urls, int maxIndex)
			{
				if (index == maxIndex) return null;
				urls.Add(url);
				return GetUrlDuplicates(url, index + 1, urls, maxIndex);
			}
			*/
	}
}
