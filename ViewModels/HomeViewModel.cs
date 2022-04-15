using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlogApplication.Models;
using X.PagedList;

namespace BlogApplication.ViewModels
{
	public class HomeViewModel
	{
		public IPagedList<Post> PostList { get; set; }

		public string AuthenticatedId { get; set; }
	}
}
