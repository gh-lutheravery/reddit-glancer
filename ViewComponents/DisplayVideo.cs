using GlanceReddit.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace GlanceReddit.ViewComponents
{
	public class DisplayVideo : ViewComponent
	{

		public async Task<IViewComponentResult> InvokeAsync(
                                                Reddit.Controllers.Post post)
        {
			DisplayVideoModel vm = new DisplayVideoModel();

			if (post.Listing.IsSelf)
			{
				vm.IsValid = false;
				return View(vm);
			}

			vm.OriginalURL = ((Reddit.Controllers.LinkPost)post).URL;

			if (IsYoutubeVid(post))
			{
				vm.VideoHtml = GetYoutubeHtml(post);
				vm.IsValid = true;

				if (vm.VideoHtml == null)
					vm.IsValid = false;
				
				vm.IsYoutubeVideo = true;
			} 
			
			else if (IsRedditVid(post))
			{
				vm.VideoURL = GetRedditUrl(post);
				vm.IsRedditVideo = true;
				vm.IsValid = true;

				if (vm.VideoURL == null)
					vm.IsValid = false;
			}

			else
			{
				vm.IsValid = false;
				return View(vm);
			}

			
            return View(vm);
        }

		private bool IsYoutubeVid(Reddit.Controllers.Post post)
		{
			var linkPost = ((Reddit.Controllers.LinkPost)post);

			// don't display shorts since reddit can't seem to embed them, only show url
			if (linkPost.URL.StartsWith("https://www.youtube.com/shorts/"))
				return false;

			if (linkPost.URL.StartsWith("https://youtu.be/") || 
				linkPost.URL.StartsWith("https://www.youtube.com/") ||
				linkPost.URL.StartsWith("https://m.youtube.com/"))
			{
					return true;
			}

			else
			{
				return false;
			}
		}

		private bool IsRedditVid(Reddit.Controllers.Post post)
		{
			var linkPost = ((Reddit.Controllers.LinkPost)post);
			if (linkPost.URL.StartsWith("https://v.redd.it/"))
			{
				return true;
			}

			else
			{
				return false;
			}
		}

		private string GetYoutubeHtml(Reddit.Controllers.Post post)
		{
			if (post.Listing.Media != null)
			{
				JObject mediaJson = (JObject)post.Listing.Media;

				string html = mediaJson.SelectToken("oembed.html").ToString();

				return ResizeIframe(html);
			}

			return null;
		}

		private string ResizeIframe(string iframe)
		{
			// replaces width, height values respectively
			string newIframe = iframe.Replace("356", "512").Replace("200", "324");
			return newIframe;
		}

		private string GetRedditUrl(Reddit.Controllers.Post post)
        {
			Reddit.Controllers.LinkPost linkPost = ((Reddit.Controllers.LinkPost)post);
			JObject mediaJson = (JObject)post.Listing.Media;

			try 
			{
				string url = mediaJson.SelectToken("reddit_video.fallback_url").ToString();
				return url;
			}

			catch (NullReferenceException)
			{ 
				return null;
			}
		}
    }
}
