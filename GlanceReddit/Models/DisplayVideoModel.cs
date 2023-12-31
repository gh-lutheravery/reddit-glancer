namespace GlanceReddit.Models
{
	public class DisplayVideoModel
	{
		public string VideoURL { get; set; }

		public string VideoHtml { get; set; }

		public bool IsYoutubeVideo { get; set; }

		public bool IsRedditVideo { get; set; }

		public string OriginalURL { get; set; }

		public bool IsValid { get; set; }
	}
}
