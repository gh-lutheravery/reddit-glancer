using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlogApplication.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Reddit;
using Reddit.AuthTokenRetriever;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using X.PagedList;
using Newtonsoft.Json;
using AttributeRouting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Net.Sockets;
using Reddit.Exceptions;

namespace BlogApplication.Controllers
{
	public class ApiController : Controller
	{
        readonly string AppId = Environment.GetEnvironmentVariable("APP_ID", EnvironmentVariableTarget.User);
        readonly string AppSecret = Environment.GetEnvironmentVariable("APP_SECRET", EnvironmentVariableTarget.User);

		readonly string GenericError = "Something went wrong... try again.";
		readonly string NotAuthError = "You're not logged into reddit here; try again.";
		readonly string AlreadyAuthError = "You're already authenticated.";
		readonly string NoSubError = "There seems to be no subreddit with that name; " +
			"remember that there has to be a subreddit with that exact name.";
		readonly string NoUserError = "There seems to be no user with that name; " +
			"remember that there has to be a user with that exact name.";
		readonly string TooManySocketError = "You connected to reddit too many times; try again.";
		readonly string ForbiddenError = "Reddit says you are forbidden from accessing that; it " +
			"might have been deleted or privated.";
		readonly string LoginSuccess = "Logging in was successful!";
		readonly string LogOutSuccess = "Logging out was successful!";

		readonly int SubmissionLimit = 15;

		readonly int SearchSubmissionLimit = 25;

		private bool IsRefreshTokenSet()
		{
			if (Request != null)
			{
				IEnumerable<Claim> claims = Request.HttpContext.User.Claims;
				if (claims.Where(c => c.Type == "RefreshToken").Count() == 1)
					return true;
			}

			return false;
		}

		private string AuthorizeUser(string AppId, string AppSecret, int port = 8080)
		{
			AuthTokenRetrieverLib authLib = new AuthTokenRetrieverLib(AppId, AppSecret, port);
			
			try
			{
				authLib.AwaitCallback();
			}

			catch (SocketException ex)
			{
				return TooManySocketError;
			}

			// wait until refresh token is sent from reddit
			while (true)
			{
				if (authLib.RefreshToken != null)
				{
					break;
				}
			}

			authLib.StopListening();
			return authLib.RefreshToken;
		}

		private void OpenReddit(string authUrl, string browserPath = "C:\\Program Files\\Mozilla Firefox\\firefox.exe")
		{
			try
			{
				ProcessStartInfo processStartInfo = new ProcessStartInfo(authUrl);
				processStartInfo.UseShellExecute = true;
				processStartInfo.CreateNoWindow = true;
				Process.Start(processStartInfo);
			}
			
			catch (System.ComponentModel.Win32Exception)
			{
				ProcessStartInfo processStartInfo = new ProcessStartInfo(browserPath)
				{
					Arguments = authUrl
				};
				Process.Start(processStartInfo);
			}
		}

		[Route("login")]
		[ValidateAntiForgeryToken]
		[HttpPost]
		public async Task<ActionResult> RedditLogin(RedditRequestViewModel viewRequest)
		{
			if (!IsRefreshTokenSet())
			{		
				string result = AuthorizeUser(AppId, AppSecret);
				if (result != TooManySocketError)
				{
					SignIn(result, viewRequest.RememberMe);
					TempData["SuccessMessage"] = LoginSuccess;
					return RedirectToAction(nameof(Home));
				}

				else
				{
					TempData["ErrorMessage"] = TooManySocketError;
					return RedirectToAction(nameof(Home));
				}
			}

			TempData["ErrorMessage"] = AlreadyAuthError;
			return RedirectToAction(nameof(Home));
		}

		[Route("profile")]
		public ActionResult Profile()
		{
			if (!IsRefreshTokenSet())
			{
				TempData["ErrorMessage"] = NotAuthError;
				return RedirectToAction(nameof(Home));
			}

			RedditUser redditor = InitRedditor(isProfile: true);

			return View(redditor);
		}

		private async void SignIn(string refreshToken, bool rememberUser)
		{
			var claims = new List<Claim>
			{
				new Claim("RefreshToken", refreshToken)
			};

			ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, 
				CookieAuthenticationDefaults.AuthenticationScheme);

			var authProperties = new AuthenticationProperties
			{
				AllowRefresh = true,
				IsPersistent = rememberUser == true,
				IssuedUtc = DateTime.UtcNow,
				RedirectUri = "/profile"
			};

			

			await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, 
				new ClaimsPrincipal(claimsIdentity), authProperties);
		}

		public async Task<ActionResult> SignOut()
		{
			if (IsRefreshTokenSet())
			{
				await HttpContext.SignOutAsync();
				TempData["SuccessMessage"] = LogOutSuccess;
			}

			else
				TempData["ErrorMessage"] = NotAuthError;

			return RedirectToAction(nameof(Home));
		}

		[Route("user")]
		public ActionResult RedditGetUser(string username)
		{
			if (!IsRefreshTokenSet())
			{
				TempData["ErrorMessage"] = NotAuthError;
				return RedirectToAction(nameof(Home));
			}

			RedditUser redditor = InitRedditor();
			Reddit.Inputs.Search.SearchGetSearchInput q =
					new Reddit.Inputs.Search.SearchGetSearchInput(username)
					{ type = "user" };

			try
			{
				var user = redditor.Client.User(username).About();

				UserViewModel vm = new UserViewModel();

				vm.User = user;
				vm.TcPostArr = user.GetPostHistory(limit: SubmissionLimit).ToArray();
				vm.TcComArr = user.GetCommentHistory(limit: SubmissionLimit).ToArray();

				return View(vm);
			}

			catch (RedditForbiddenException ex)
			{
				TempData["ErrorMessage"] = ForbiddenError;
				return RedirectToAction(nameof(Home));
			}

			// below two exceptions are both caused by element not existing
			catch (RedditBadRequestException ex)
			{
				TempData["ErrorMessage"] = NoUserError;
				return RedirectToAction(nameof(Home));
			}

			catch (RedditNotFoundException ex)
			{
				TempData["ErrorMessage"] = NoUserError;
				return RedirectToAction(nameof(Home));
			}
		}


		[Route("subreddit")]
		public ActionResult RedditGetSubreddit(string name)
		{
			if (!IsRefreshTokenSet())
			{
				TempData["ErrorMessage"] = NotAuthError;
				return RedirectToAction(nameof(Home));
			}

			RedditUser redditor = InitRedditor();

			try
			{
				var subreddit = redditor.Client.Subreddit(name).About();
				SubredditViewModel vm = new SubredditViewModel();

				vm.Sub = subreddit;
				vm.TcPostArr = subreddit.Posts.GetNew(limit: SubmissionLimit).ToArray();
				vm.TcComArr = subreddit.Comments.GetNew(limit: SubmissionLimit).ToArray();

				return View(vm);
			}

			catch (RedditForbiddenException ex)
			{
				TempData["ErrorMessage"] = ForbiddenError;
				return RedirectToAction(nameof(Home));
			}

			// below two exceptions are both caused by element not existing
			catch (RedditBadRequestException ex)
			{
				TempData["ErrorMessage"] = NoSubError;
				return RedirectToAction(nameof(Home));
			}

			catch (RedditNotFoundException ex)
			{
				TempData["ErrorMessage"] = NoSubError;
				return RedirectToAction(nameof(Home));
			}
		}

		private RedditUser InitRedditor(bool isProfile = false)
		{
			var redditor = new RedditUser();
			if (IsRefreshTokenSet())
			{
				string refreshToken = Request.HttpContext.User.Claims.ElementAt(0).Value;

				redditor.Client = new RedditClient(AppId, refreshToken, AppSecret);

				if (isProfile)
				{
					redditor.TcPostHistory = redditor.Client.Account.Me.GetPostHistory(limit: SubmissionLimit).ToArray();
					redditor.TcCommentHistory = redditor.Client.Account.Me.GetCommentHistory(limit: SubmissionLimit).ToArray();
				}
			}

			else
				return null;
			
			return redditor;
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult ApiRequest(HomeViewModel viewRequest)
		{
			try
			{
				if (ModelState.IsValid)
				{
					if (viewRequest.RedditorName != null)
					{
						if (!IsRefreshTokenSet())
						{
							ViewData["ErrorMessage"] = NotAuthError;
							return RedirectToAction(nameof(Home));
						}

						return RedirectToAction(nameof(RedditGetUser),
								new { username = viewRequest.RedditorName });
					}

					else if (viewRequest.SubredditName != null)
					{
						if (!IsRefreshTokenSet())
						{
							ViewData["ErrorMessage"] = NotAuthError;
							return RedirectToAction(nameof(Home));
						}

						return RedirectToAction(nameof(RedditGetSubreddit),
								new { name = viewRequest.SubredditName });
					}
				}
			}

			catch (Exception ex)
			{
				ViewData["ErrorMessage"] = GenericError;
			}

			return RedirectToAction(nameof(Home));
		}

		public ActionResult Home()
		{
			HomeViewModel vm = new HomeViewModel();

			if (!IsRefreshTokenSet())
				vm.RedditUrl = new AuthTokenRetrieverLib(AppId, AppSecret, 8080).AuthURL();

			else
				vm.IsAuth = true;


			if (TempData["ErrorMessage"] != null)
				vm.ErrorMessage = TempData["ErrorMessage"].ToString();

			else if (TempData["SuccessMessage"] != null)
				vm.SuccessMessage = TempData["SuccessMessage"].ToString();

			return View(vm);
		}

		private List<Reddit.Controllers.Post> Search(string query)
		{
			if (!IsRefreshTokenSet())
			{
				return null;
			}

			var redditor = InitRedditor();

			Reddit.Inputs.Search.SearchGetSearchInput q =
							new Reddit.Inputs.Search.SearchGetSearchInput(query) { limit = SearchSubmissionLimit };

			var queryList = redditor.Client.Search(q).ToList();

			return queryList;
		}

		[Route("search")]
		public IActionResult SearchResult(int? page, string searchBar)
		{
			var queryList = Search(searchBar);

			if (queryList == null)
			{
				TempData["ErrorMessage"] = NotAuthError;
				return RedirectToAction(nameof(Home));
			}
			

			int pageSize = 5;
			int pageNumber = (page ?? 1);

			SearchResultViewModel vm = 
				new SearchResultViewModel(
					queryList.ToPagedList(pageNumber, pageSize), 
					searchBar);

			return View(vm);
		}
	}
}
