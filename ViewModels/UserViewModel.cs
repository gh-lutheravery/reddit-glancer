namespace BlogApplication.ViewModels
{
    public class UserViewModel
    {
        public Reddit.Controllers.User User { get; set; }

        public Reddit.Controllers.Post[] TcPostArr { get; set; }

        public Reddit.Controllers.Comment[] TcComArr { get; set; }
    }
}
