using GlanceReddit.Models;
using System;
using System.Collections.Generic;

namespace GlanceReddit.ViewModels
{
    public class SubredditViewModel
    {
        public Reddit.Controllers.Subreddit Sub { get; set; }

        public Reddit.Controllers.Post[] TcPostArr { get; set; }

        public Reddit.Controllers.Comment[] TcComArr { get; set; }

        public SubredditStatsModel StatsModel { get; set; }
    }
}
