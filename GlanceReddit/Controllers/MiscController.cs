using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GlanceReddit.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using X.PagedList;
using Microsoft.EntityFrameworkCore;
using System.IO;
using GlanceReddit.ViewModels;
using AttributeRouting;

namespace GlanceReddit.Controllers
{
	public class MiscController : Controller
	{
		private readonly ILogger<MiscController> _logger;

		public MiscController(ILogger<MiscController> logger)
		{
			_logger = logger;
		}

		[Route("about")]
		public IActionResult About()
		{
			return View();
		}

		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error()
		{	
			return View();
		}

		public IActionResult PageNotFound()
		{
			return View();
		}

	}
}
