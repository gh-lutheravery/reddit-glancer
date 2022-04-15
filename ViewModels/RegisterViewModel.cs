using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace BlogApplication.ViewModels
{
	public class RegisterViewModel
	{
        [Required]
        public string NormalizedUserName { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        [Required]
        public string NormalizedEmail { get; set; }

        [Required]
        public DateTime DateCreated { get; set; }

        [Compare(nameof(PasswordHash), ErrorMessage = "Confirm password doesn't match.")]
        public string ConfirmPassword { get; set; }
    }
}
