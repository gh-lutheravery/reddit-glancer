using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace BlogApplication.ViewModels
{
	public class PostCreateViewModel
	{
        public string Title { get; set; }

        public string Content { get; set; }

        public string UserId { get; set; }
    }
}
