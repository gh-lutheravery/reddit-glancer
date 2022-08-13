﻿using System;
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

		readonly IConfiguration _config;
		public ApiController(IConfiguration config)
		{
			_config = config;
		}

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

		// redirect uri that reddit uses in oauth process
		[Route("auth-redirect")]
		public ActionResult AuthRedirect()
		{
			return View();
		}

		public string GenerateKey()
		{
			SymmetricSecurityKey secKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_KEY")));
			SigningCredentials credentials = new SigningCredentials(secKey, SecurityAlgorithms.HmacSha256);

			Claim[] claims = new Claim[] { new Claim(ClaimTypes.Name, HostAuthorizer) };

			JwtSecurityToken token = new JwtSecurityToken(_config["Jwt:Issuer"],
				_config["Jwt:Audience"],
				claims,
				expires: DateTime.Now.AddMinutes(5),
				signingCredentials: credentials
			);

			return new JwtSecurityTokenHandler().WriteToken(token);
		}



		private string GetRefreshToken()
		{
			bool apiError = false;
			string jsonResult = string.Empty;
			using (var httpClient = new HttpClient())
			{
				string jwtToken = GenerateKey();

				var result = httpClient.PostAsJsonAsync(String.Concat("http://", _config["Jwt:Audience"], "/"), jwtToken).Result;
				jsonResult = result.Content.ReadAsStringAsync().Result;

				JObject jobject = JObject.Parse(jsonResult);
				JToken errorToken = jobject.SelectToken("error");
				if (errorToken != null)
					apiError = true;
			}

			if (apiError)
				return SocketError;

			return JsonSerializer.Deserialize<OAuthToken>(jsonResult).RefreshToken;
		}

		[Route("login")]
		[ValidateAntiForgeryToken]
		[HttpPost]
		public ActionResult RedditLogin(RedditRequestViewModel viewRequest)
		{
			if (!IsRefreshTokenSet())
			{		
				string result = GetRefreshToken();

				if (result == SocketError)
				{
					TempData["ErrorMessage"] = SocketError;
					return RedirectToAction(nameof(Home));
				}
				else
				{
					SignIn(result, viewRequest.RememberMe);
					TempData["SuccessMessage"] = LoginSuccess;
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
