using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Reddit.AuthTokenRetriever;
using RestSharp;
using System;
using System.Text;

namespace GlanceReddit.Controllers
{
	public class OauthController : Controller
	{
		readonly string RedirectUri = "https://glancereddit.azurewebsites.net/auth-redirect";

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

		// construct oauth request then deserialize returned token
		public OAuthToken FetchToken(string code, string state)
		{
			RestRequest restRequest = new RestRequest("/api/v1/access_token", Method.POST);

			restRequest.AddHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(state)));
			restRequest.AddHeader("Content-Type", "application/x-www-form-urlencoded");

			restRequest.AddParameter("grant_type", "authorization_code");
			restRequest.AddParameter("code", code);
			restRequest.AddParameter("redirect_uri", RedirectUri);

			OAuthToken oAuthToken = JsonConvert.DeserializeObject<OAuthToken>(ExecuteRequest(restRequest));

			return oAuthToken;
		}
	}
}
