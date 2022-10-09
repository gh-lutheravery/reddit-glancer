using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Web;
using System.Collections.Specialized;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Reddit;
using Reddit.AuthTokenRetriever;
using Reddit.Exceptions;
using X.PagedList;
using Newtonsoft.Json.Linq;
using GlanceReddit.ViewModels;
using Azure.Containers.ContainerRegistry;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using uhttpsharp.Headers;
using Microsoft.Extensions.Primitives;
using GlanceReddit.Models;

namespace GlanceReddit.Controllers
{
	public class ApiController : Controller
	{
        readonly string AppId = Environment.GetEnvironmentVariable("APP_ID");
        readonly string AppSecret = Environment.GetEnvironmentVariable("APP_SECRET");

		readonly string HostAuthorizer = Environment.GetEnvironmentVariable("SITE_AUTHORIZER");

		readonly string HostName = "glancereddit.azurewebsites.net";
		readonly string RedirectUri = "https://glancereddit.azurewebsites.net/auth-redirect";
		readonly int Port = 0;

		readonly string GenericError = "Something went wrong... try again.";
		readonly string NotAuthError = "You're not logged into reddit here; try again.";
		readonly string AlreadyAuthError = "You're already authenticated.";
		readonly string NoSubError = "There seems to be no subreddit with that name; " +
			"remember that there has to be a subreddit with that exact name.";
		readonly string NoUserError = "There seems to be no user with that name; " +
			"remember that there has to be a user with that exact name.";
		readonly string SocketError = "Authentication failed; try again.";
		readonly string ForbiddenError = "Reddit says you are forbidden from accessing that; it " +
			"might have been deleted or privated.";
		readonly string LoginSuccess = "Logging in was successful! ";
		readonly string LogOutSuccess = "Logging out was successful!";

		readonly int SubmissionLimit = 15;
		readonly int SearchSubmissionLimit = 25;
		private readonly ILogger<ApiController> _logger;

		public ApiController(ILogger<ApiController> logger)
		{ 
			_logger = logger;
		}

		private bool IsRefreshTokenSet()
		{
			if (Request != null)
			{
				if (Request.Cookies["RefreshToken"] != null)
					return true;
			}

			return false;
		}

		private string GetQueryString(string key)
		{
			StringValues stateVals = new StringValues();

			bool result = Request.Query.TryGetValue(key, out stateVals);
			if (result)
				return stateVals.ToString();
			
			else
			{
				string err = key + " fetching failed, throwing exception.";
				_logger.LogError(err);
				throw new Exception(err);
			}
		}

		// redirect uri that reddit uses in oauth process
		[Route("auth-redirect")]
		public ActionResult AuthRedirect()
		{
			OauthController oauthController = new OauthController();

			string state = GetQueryString("state");
			string code = GetQueryString("code");

			string token = oauthController.FetchToken(code, state).RefreshToken;

			CookieOptions options = new CookieOptions();
			options.Expires = DateTime.Now.AddDays(2);
			options.Secure = true;
			Response.Cookies.Append("RefreshToken", token, options);

			return View();
		}

		public string GenerateKey()
		{
			SymmetricSecurityKey secKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_KEY")));
			SigningCredentials credentials = new SigningCredentials(secKey, SecurityAlgorithms.HmacSha256);

			Claim[] claims = new Claim[] { new Claim(ClaimTypes.Name, HostAuthorizer) };

			JwtSecurityToken token = new JwtSecurityToken(Environment.GetEnvironmentVariable("JWT_ISSUER"),
				Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
				claims,
				expires: DateTime.Now.AddMinutes(5),
				signingCredentials: credentials
			);

			return new JwtSecurityTokenHandler().WriteToken(token);
		}

		[Route("login")]
		[ValidateAntiForgeryToken]
		[HttpPost]
		public ActionResult BeginLogin(RedditRequestViewModel viewRequest)
		{
			if (!IsRefreshTokenSet())
			{
				return RedirectToAction(nameof(Home));
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

		public async Task<ActionResult> SignOut()
		{
			if (IsRefreshTokenSet())
			{
				Response.Cookies.Delete("RefreshToken");
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

		private Dictionary<string, int> CastValueDoubleToInt(Dictionary<string, double> originalDict)
		{
			// convert each value to an int from websiteOccurences dictionary
			return originalDict.Select(p =>
				new KeyValuePair<string, int>(p.Key, (int)p.Value))
				.ToDictionary(p => p.Key, p => p.Value);
		}

		private SubredditStatsModel PopulateSubredditStatsModel(Reddit.Controllers.Subreddit sub, RedditUser client)
		{
			SubredditStatsModel statsModel = new SubredditStatsModel();
			RedditStatistics redditStatistics = new RedditStatistics(_logger);

			List<string> urls = sub.Posts.Hot.Select(p => p.Listing.URL).ToList();

			var websiteOccurences = redditStatistics.GetLinkedWebsites(urls);

			if (websiteOccurences != null)
				statsModel.ForeignWebsites = CastValueDoubleToInt(websiteOccurences);

			// for every post selected, generate a user object from the author string
			_logger.LogError("creating users");
			var names = sub.Moderators.Select(m => m.Name).Take(50);

			List <Reddit.Controllers.User> mods = names
				.Select(n => client.Client.User(n)).ToList();

			statsModel.RelatedSubreddits = CastValueDoubleToInt(redditStatistics.GetRelatedSubreddits(mods, sub.Name));

			statsModel.ForeignWebsites.OrderByDescending(p => p.Value);
			statsModel.RelatedSubreddits.OrderByDescending(p => p.Value);


			//var crosspostedSubs = redditStatistics.GetCrosspostedSubs(sub);
			//statsModel.CrosspostedSubreddits = CastValueDoubleToInt(crosspostedSubs);


			return statsModel;
		}

		private async Task<SearchResultStatsModel> PopulateSearchStatsModel(string query, RedditUser client, List<Reddit.Controllers.Post> queryList)
		{
			SearchResultStatsModel statsModel = new SearchResultStatsModel();
			RedditStatistics redditStatistics = new RedditStatistics(_logger);

			statsModel.SearchPopularity = await redditStatistics.GetQueryPopularity(client, query);
			statsModel.CommonResultSubreddits = redditStatistics.GetCommonSubreddits(queryList);

			return statsModel;
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
				vm.StatsModel = PopulateSubredditStatsModel(subreddit, redditor);

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

			string refreshToken = Request.Cookies["RefreshToken"];

			redditor.Client = new RedditClient(AppId, refreshToken, AppSecret);

			if (isProfile)
			{
				redditor.TcPostHistory = redditor.Client.Account.Me.GetPostHistory(limit: SubmissionLimit).ToArray();
				redditor.TcCommentHistory = redditor.Client.Account.Me.GetCommentHistory(limit: SubmissionLimit).ToArray();
			}
			
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
							TempData["ErrorMessage"] = NotAuthError;
							return RedirectToAction(nameof(Home));
						}

						return RedirectToAction(nameof(RedditGetUser),
								new { username = viewRequest.RedditorName });
					}

					else if (viewRequest.SubredditName != null)
					{
						if (!IsRefreshTokenSet())
						{
							TempData["ErrorMessage"] = NotAuthError;
							return RedirectToAction(nameof(Home));
						}
						
						return RedirectToAction(nameof(RedditGetSubreddit),
								new { name = viewRequest.SubredditName });
					}
				}
			}

			catch (Exception ex)
			{
				TempData["ErrorMessage"] = GenericError;
			}

			return RedirectToAction(nameof(Home));
		}

		public Uri SetQueryVal(string url, string name, string newValue)
		{
			NameValueCollection nvc = HttpUtility.ParseQueryString(url);
			nvc[name] = (newValue ?? string.Empty).ToString();

			Uri uri = new Uri(url);
			return new UriBuilder(uri) { Query = nvc.ToString() }.Uri;
		}

		// replace with compact to make authorize page better on mobile
		private string ToCompactUrl(string url)
		{
			return url.Replace("authorize?", "authorize.compact?");
		}

		private string ToDeployedRedirectUri(string url)
		{
			// remove first 48 characters to remove duplicated host
			return SetQueryVal(url, "redirect_uri", RedirectUri).ToString().Remove(0, 40);
		}
		
		public ActionResult Home()
		{
			HomeViewModel vm = new HomeViewModel();

			if (!IsRefreshTokenSet())
			{
				string originalUrl = new AuthTokenRetrieverLib(AppId, Port, host: HostName,
					redirectUri: RedirectUri, AppSecret).AuthURL();

				string serverRedirectUri = ToDeployedRedirectUri(originalUrl);
				vm.RedditUrl = ToCompactUrl(serverRedirectUri);
			}

			else
				vm.IsAuth = true;

			if (TempData["ErrorMessage"] != null)
				vm.ErrorMessage = TempData["ErrorMessage"].ToString();
			 
			else if (TempData["SuccessMessage"] != null)
				vm.SuccessMessage = TempData["SuccessMessage"].ToString();

			return View(vm);
		}

		private List<Reddit.Controllers.Post> Search(string query, RedditUser redditor)
		{
			if (!IsRefreshTokenSet())
			{
				return null;
			}

			Reddit.Inputs.Search.SearchGetSearchInput q =
							new Reddit.Inputs.Search.SearchGetSearchInput(query) { limit = SearchSubmissionLimit, sort = "hot" };

			var queryList = redditor.Client.Search(q).ToList();



			return queryList;
		}

		[Route("search")]
		public async Task<IActionResult> SearchResult(int? page, string searchBar)
		{
			var redditor = InitRedditor();
			var queryList = Search(searchBar, redditor);

			if (queryList == null)
			{
				TempData["ErrorMessage"] = NotAuthError;
				return RedirectToAction(nameof(Home));
			}

			else if (!queryList.Any())
			{ 
				return View(new SearchResultViewModel(
					queryList.ToPagedList(0, 0),
					searchBar));
			}
			
			int pageSize = 5;
			int pageNumber = (page ?? 1);

			SearchResultViewModel vm = 
				new SearchResultViewModel(
					queryList.ToPagedList(pageNumber, pageSize), 
					searchBar);

			vm.StatsModel = await PopulateSearchStatsModel(searchBar, redditor, queryList);

			return View(vm);
		}
	}
}
