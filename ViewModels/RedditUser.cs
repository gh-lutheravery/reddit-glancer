using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Reddit;

namespace BlogApplication.ViewModels
{
	public class RedditUser
	{
		public RedditClient Client { get; set; }

		public Reddit.Controllers.Post[] TcPostHistory { get; set; }

		public Reddit.Controllers.Comment[] TcCommentHistory { get; set; }
	}
}
