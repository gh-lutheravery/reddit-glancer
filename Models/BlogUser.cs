using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace BlogApplication.Models
{
    public class BlogUser : IdentityUser
    {
        public BlogUser()
		{
            this.DateCreated = DateTime.Now;
        }

        public string Img {get; set; }
        
        public string Bio { get; set; }

        [Required]
        public DateTime DateCreated { get; set; }

        [Required]
        public override string NormalizedUserName { get; set; }

        public override string NormalizedEmail { get; set; }

        [Required]
        public override string PasswordHash { get; set; }

        public string ToStr()
        {
            return this.NormalizedUserName;
        }
    }
}

