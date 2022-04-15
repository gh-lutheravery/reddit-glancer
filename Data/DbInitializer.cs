using BlogApplication.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;

namespace BlogApplication.Data
{
    public static class DbInitializer
    {
        public static void Initialize(BlogContext context)
        {
            context.Database.EnsureCreated();

            string readText = File.ReadAllText("C:\\Users\\HP\\Desktop\\posts2.json");
            var postList = JsonConvert.DeserializeObject<List<Post>>(readText);

            int tally = 0;
            foreach (var i in postList)
            {
                tally++;
                if (tally % 2 == 0)
                {
                    i.User = context.Users.ToList()[0];
                }
                else
                {
                    i.User = context.Users.ToList()[1];
                }
            }

            // Look for any users.
            if (context.Posts.Any())
            {
                return;   // DB has been seeded
            }

            foreach (Post u in postList)
            {
                context.Posts.Add(u);
            }
            context.SaveChanges();
        }
    }
}



















