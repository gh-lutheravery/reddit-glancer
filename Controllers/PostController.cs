using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlogApplication.Data;
using Microsoft.AspNetCore.Mvc;
using BlogApplication.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using BlogApplication.Controllers;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using BlogApplication.ViewModels;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.AspNetCore.Identity;

namespace BlogApplication.Controllers
{
	
	public class PostController : Controller
	{
		private readonly BlogContext _context;
		private readonly UserManager<BlogUser> userManager;

		public PostController(BlogContext context, IHttpContextAccessor httpContextAccessor, UserManager<BlogUser> userMgr)
		{
			_context = context;
			userManager = userMgr;
		}

		[HttpPost, ActionName("PostConfirmDelete")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> PostConfirmDelete(string id)
		{
			var post = await _context.Posts.FindAsync(id);
			if (post == null)
			{
				return NotFound();
			}

			try
			{
				_context.Posts.Remove(post);
				await _context.SaveChangesAsync();
				return RedirectToAction(nameof(HomeController.Home), TrimController(nameof(HomeController)));
			}
			catch (DbUpdateException /* ex */)
			{
				return RedirectToAction(nameof(PostDelete), new { id = id, saveChangesError = true });
			}
		}

		public string TrimController(string controller)
		{
			if (!controller.Contains("Controller"))
			{
				throw new Exception("Given controller name does not contain the word Controller.");
			}

			return controller.Replace("Controller", "");
		}

		public  IActionResult PostDelete(string id, bool? saveChangesError = false)
		{
			Post post = GetPost(id);
			if (saveChangesError == true)
			{
				ModelState.AddModelError("", "Unable to save changes. Try again.");
			}

			return View(post);
		}

		public IActionResult PostDetail(string id)
		{
			if (id == null)
			{
				return NotFound();
			}

			Post post = GetPost(id);
			return View(post);
		}

		public IActionResult PostUpdate(string id)
		{
			Post post = GetPost(id);
			return View(post);
		}

		public IActionResult PostCreate(string userId)
		{
			PostCreateViewModel model = new PostCreateViewModel();
			model.UserId = userId;
			return View(model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddPost(
		[Bind("Title,Content")] PostCreateViewModel viewPost)
		{	
			try
			{
				if (ModelState.IsValid)
				{
					Post post = new Post();

					post.Title = viewPost.Title;
					post.Content = viewPost.Content;
					post.User = userManager.Users.FirstOrDefault(u => u.Id == viewPost.UserId);

					EntityEntry<Post> pEntity = _context.Add(post);
					await _context.SaveChangesAsync();
					return RedirectToAction(nameof(PostDetail), new { id = pEntity.Entity.PostId });
				}
			}
			catch (DbUpdateException /* ex */)
			{
				ViewData["ErrorMessage"] = "Unable to save changes. Try again.";
			}

			return RedirectToAction(nameof(PostCreate));
		}

		public Post GetPost(string id)
		{
			// Include function must be done first to include author foriegn key property
			List<Post> posts = _context.Posts.Include(p => p.User).ToList();
			Post post = posts.FirstOrDefault(s => s.PostId == id);
			return post;
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EditPost(string id)
		{
			if (id == null)
			{
				return NotFound();
			}
			
			Post postToUpdate = GetPost(id);

			// Uses model binding to update db
			if (await TryUpdateModelAsync<Post>(
				postToUpdate,
				"",
				s => s.Title, s => s.Content))
			{
				try
				{
					await _context.SaveChangesAsync();

					return RedirectToAction(nameof(PostDetail), new { id = postToUpdate.PostId });
				}
				catch (DbUpdateException /* ex */)
				{
					ModelState.AddModelError("", "Unable to save changes. " +
						"Try again.");
				}
			}
			return RedirectToAction(nameof(PostUpdate));
		}
	}
}
