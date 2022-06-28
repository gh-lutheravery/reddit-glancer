using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlogApplication.Controllers;
using Microsoft.AspNetCore.Mvc;
using Reddit;

namespace BlogApplication.ViewModels
{
	public class RedditRequestViewModel
	{
		public string RedditorName { get; set; }

		public string SubredditName { get; set; }

		public bool RememberMe { get; set; }
	}
}
