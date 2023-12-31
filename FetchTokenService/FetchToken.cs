using Reddit.AuthTokenRetriever;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Azure.Containers.ContainerRegistry;
using Azure.Identity;

public class FetchTokenClass
{
	readonly string AppId = Environment.GetEnvironmentVariable("APP_ID");
	readonly string AppSecret = Environment.GetEnvironmentVariable("APP_SECRET");
	readonly string JwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
	readonly string JwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
	readonly string JwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");

	readonly string RedirectUri = "https://glancereddit.azurewebsites.net/auth-redirect";

	public static void Main(string[] args)
	{
		FetchTokenClass @class = new();

		HttpListener listener = new HttpListener();
		string host = "http://" + IPAddress.Loopback.ToString() + "/";
		listener.Prefixes.Add(host);
		listener.Start();
		
		using (listener)
		{
			while (true)
			{
				HttpListenerContext result = listener.GetContextAsync().Result;
				Console.Out.WriteLine("Request recieved.");
				string jsonResult = result.Request.ToString();
				

				if (!string.IsNullOrEmpty(jsonResult))
				{
					if (@class.ValidToken(jsonResult, Encoding.UTF8.GetBytes(@class.JwtKey)))
					{
						Console.Out.WriteLine("Request successful; retrieving OAuth tokens.");
						@class.RetrieveToken(result);
						continue;
					}
				}

				Console.Error.WriteLine("Request sent was empty; rejecting request.");
				@class.SendForbidden(listener);
			}
		}
		
	}

	public bool ValidToken(string token, byte[] secret)
	{
		var tokenHandler = new JwtSecurityTokenHandler();

		var validationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidateAudience = true,
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true,
			ValidIssuer = JwtIssuer,
			ValidAudience = JwtAudience,
			IssuerSigningKey = new SymmetricSecurityKey(secret)
		};

		SecurityToken validatedToken;
		try
		{
			tokenHandler.ValidateToken(token, validationParameters, out validatedToken);
		}

		// TODO: return specific error message to show in forbidden response
		catch (Exception)
		{
			return false;
		}

		return validatedToken != null;
	}

	public void RetrieveToken(HttpListenerContext context)
	{
		string hostIp = IPAddress.Loopback.ToString();
		int port = 80;

		AuthTokenRetrieverLib authLib = new AuthTokenRetrieverLib(
			AppId, port, host: hostIp,
			redirectUri: RedirectUri, AppSecret);

		try
		{
			authLib.AwaitCallback();
		}

		catch (SocketException ex)
		{
			authLib.StopListening();
			SendResult(true, null, context);
		}

		while (true)
		{
			if (authLib.RefreshToken != null)
			{
				break;
			}
		}

		authLib.StopListening();
		OAuthToken token = new OAuthToken(authLib.AccessToken, authLib.RefreshToken);
		SendResult(false, token, context);
	}

	public void SendResult(bool errorOccured, OAuthToken? token, HttpListenerContext context)
	{ 
		if (!errorOccured)
		{
			string jsonToken = JsonConvert.SerializeObject(token);
			byte[] buffer = Encoding.UTF8.GetBytes(jsonToken);

			context.Response.ContentLength64 = buffer.Length;
			Stream output = context.Response.OutputStream;
			output.Write(buffer, 0, buffer.Length);
			output.Close();
		}

		else
		{
			Dictionary<string, string> errDict = new();
			errDict.Add("error",
			"A socket error occured or the reddit auth page wasn't opened.");

			string jsonToken = JsonConvert.SerializeObject(errDict);
			byte[] buffer = Encoding.UTF8.GetBytes(jsonToken);

			context.Response.ContentLength64 = buffer.Length;
			Stream output = context.Response.OutputStream;
			output.Write(buffer, 0, buffer.Length);
			output.Close();
		}
	}

	public void SendForbidden(HttpListener listener)
	{
		HttpListenerContext context = listener.GetContext();
		HttpListenerResponse response = context.Response;

		Dictionary<string, string> errDict = new Dictionary<string, string>();
		errDict.Add("Request Denied",
		"Jwt authorization failed.");

		string jsonToken = JsonConvert.SerializeObject(errDict);
		byte[] buffer = Encoding.UTF8.GetBytes(jsonToken);
		
		response.ContentLength64 = buffer.Length;
		Stream output = response.OutputStream;
		output.Write(buffer, 0, buffer.Length);
		output.Close();
	}
}