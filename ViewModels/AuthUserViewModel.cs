using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using BlogApplication.Models;
using Microsoft.AspNetCore.Identity;

namespace BlogApplication.ViewModels
{
	public class AuthUserViewModel
	{
        [Required]
        public string NormalizedUserName { get; set; }

        [Required]
        public string PasswordHash { get; set; }
    }
}
