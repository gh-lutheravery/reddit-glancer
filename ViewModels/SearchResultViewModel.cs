﻿using GlanceReddit.Models;
using Reddit.Controllers;
using System.IO;
using X.PagedList;

namespace GlanceReddit.ViewModels
{
	public class SearchResultViewModel
	{
		public IPagedList<Post> QueryList { get; set; }

		public string Query { get; set; }

		public SearchResultStatsModel StatsModel { get; set; }

		public SearchResultViewModel(IPagedList<Post> queryList, string query)
		{
			QueryList = queryList;
			Query = query;
		}
	}
}
