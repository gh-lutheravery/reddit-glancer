using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace BlogApplication.Models
{
	public class Post
	{
        public Post()
        {
            this.DatePosted = DateTime.Now;
        }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string PostId { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        public DateTime DatePosted { get; set; }

        [Required]
        [ForeignKey("UsersId")]
        public BlogUser User { get; set; }

        public string Img { get; set; }

        public string ToStr()
        {
            return this.Title;
        }
    }
}
