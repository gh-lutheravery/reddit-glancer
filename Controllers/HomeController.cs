using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BlogApplication.Data;
using BlogApplication.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using X.PagedList;
using Microsoft.EntityFrameworkCore;
using System.IO;
using BlogApplication.ViewModels;

namespace BlogApplication.Controllers
{
	public class HomeController : Controller
	{
		private readonly ILogger<HomeController> _logger;
		private readonly BlogContext _context;

		public HomeController(ILogger<HomeController> logger, BlogContext context)
		{
			_logger = logger;
			_context = context;
		}

		public IActionResult Home(int? page, string userId)
		{
			List<Post> list = _context.Posts.Include(p => p.User).ToList();
			int pageSize = 10;
			int pageNumber = (page ?? 1);

			HomeViewModel home = new HomeViewModel();
			home.PostList = list.ToPagedList(pageNumber, pageSize);
			home.AuthenticatedId = userId;
			
			return View(home);
		}

		public IActionResult About()
		{
			return View();
		}

		public IActionResult SearchResults (int? page, string searchBar)
		{
			List<Post> list = _context.Posts.Include(p => p.User).ToList();

			List<Post> queryList = list.Where(s => s.Title.ToLower().Contains(searchBar)).ToList();
			queryList.AddRange(list.Where(s => s.Content.ToLower().Contains(searchBar)).ToList());

			// remove duplicates from queryList
			HashSet<Post> querySet = new HashSet<Post>(queryList);
			queryList = querySet.ToList();

			int pageSize = 10;
			int pageNumber = (page ?? 1);

			return View(queryList.ToPagedList(pageNumber, pageSize));
		}

		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error()
		{
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}
	}
}
