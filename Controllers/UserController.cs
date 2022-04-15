using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BlogApplication.Data;
using BlogApplication.Models;
using BlogApplication.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BlogApplication.Controllers
{
	[Authorize]
	public class UserController : Controller
	{
		private readonly BlogContext _context;
		private readonly UserManager<BlogUser> userManager;
		private readonly SignInManager<BlogUser> _signInManager;

		public UserController(BlogContext context, UserManager<BlogUser> userMgr, SignInManager<BlogUser> signInMgr)
		{
			_context = context;
			userManager = userMgr;
			_signInManager = signInMgr;
		}

		[AllowAnonymous]
		public async Task<IActionResult> Register()
		{
			return View();
		}

		[AllowAnonymous]
		public async Task<IActionResult> Login()
		{
			return View();
		}

		public async Task<IActionResult> Logout()
		{
			await _signInManager.SignOutAsync();
			return View();
		}

		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AuthUser([Bind("NormalizedUserName,PasswordHash")] AuthUserViewModel inputUser)
		{
			if (ModelState.IsValid)
			{
				BlogUser user = await userManager.FindByNameAsync(inputUser.NormalizedUserName);
				user.UserName = inputUser.NormalizedUserName;
				user.Email = user.NormalizedEmail;

				await _signInManager.SignOutAsync();

				var signInResult = await _signInManager.PasswordSignInAsync(
					user.UserName, user.PasswordHash, false, false);
			}

			// TODO: Make dbcontext IdentityDbContext to make signin possible
			return View(nameof(Login), inputUser);
		}

		public string TrimController(string controller)
		{
			if (!controller.Contains("Controller"))
			{
				throw new Exception("Given controller " +
					"name does not contain the word Controller.");
			}

			return controller.Replace("Controller", "");
		}

		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateUser(
		[Bind("NormalizedUserName,NormalizedEmail,PasswordHash,ConfirmPassword")] RegisterViewModel viewUser)
		{
			try
			{
				if (ModelState.IsValid)
				{
					BlogUser user = new BlogUser();

					user.NormalizedUserName = viewUser.NormalizedUserName;
					user.NormalizedEmail = viewUser.NormalizedEmail;

					PasswordHasher<BlogUser> hasher = new PasswordHasher<BlogUser>();
					
					string hashedPassword = hasher.HashPassword(user, viewUser.PasswordHash);
					user.PasswordHash = hashedPassword;

					_context.Add(user);
					await _context.SaveChangesAsync();
					return RedirectToAction(nameof(Login));
				}
			}
			catch (DbUpdateException /* ex */)
			{
				ModelState.AddModelError("", "Unable to save changes. " +
					"Try again.");
			}
			
			return RedirectToAction(nameof(Register), viewUser);
		}
	}
}
