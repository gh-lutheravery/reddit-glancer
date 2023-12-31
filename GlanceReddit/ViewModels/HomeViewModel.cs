namespace GlanceReddit.ViewModels
{
	public class HomeViewModel
	{
		// fields for authenticating to reddit
		public string ErrorMessage { get; set; }

		public string SuccessMessage { get; set; }

		public string RedditUrl { get; set; }

		public bool IsAuth = false;

		public bool RememberMe { get; set; }

		public string SubredditName { get; set; }
	}
}
