using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Reddit.AuthTokenRetriever;
using RestSharp;
using System;
using System.Net;
using System.Text;

namespace GlanceReddit.Controllers
{
	public class OauthController : Controller
	{
		readonly string RedirectUri = "https://glancereddit.azurewebsites.net/auth-redirect";
		readonly string AppId = Environment.GetEnvironmentVariable("APP_ID");
		readonly string AppSecret = Environment.GetEnvironmentVariable("APP_SECRET");

		public string ExecuteRequest(RestRequest restRequest)
		{
			IRestResponse res = new RestClient("https://www.reddit.com").Execute(restRequest);
			if (res != null && res.IsSuccessful)
			{
				return res.Content;
			}
			else
			{
				Exception ex = new Exception("API returned non-success response.");

				ex.Data.Add("res", res);

				throw ex;
			}
		}

		// GET: OauthController/Create
		public OAuthToken FetchToken(string code, string state)
		{
			/*
			AuthTokenRetrieverLib authLib = new AuthTokenRetrieverLib(
			AppId, 8080, host: GetIPAddress(),
			redirectUri: RedirectUri, AppSecret);

			authLib.AwaitCallback();
			*/
			RestRequest restRequest = new RestRequest("/api/v1/access_token", Method.POST);

			restRequest.AddHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(state)));
			restRequest.AddHeader("Content-Type", "application/x-www-form-urlencoded");

			restRequest.AddParameter("grant_type", "authorization_code");
			restRequest.AddParameter("code", code);
			restRequest.AddParameter("redirect_uri", RedirectUri);  // This must be an EXACT match in the app settings on Reddit!  --Kris

			OAuthToken oAuthToken = JsonConvert.DeserializeObject<OAuthToken>(ExecuteRequest(restRequest));

			return oAuthToken;
		}
	}
}
