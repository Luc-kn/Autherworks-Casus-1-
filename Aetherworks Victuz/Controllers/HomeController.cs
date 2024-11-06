// Controllers/HomeController.cs
using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Aetherworks_Victuz.Data;
using Aetherworks_Victuz.Models;
using Microsoft.EntityFrameworkCore; // Voor Include
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Aetherworks_Victuz.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        // Index Actie
        public IActionResult Index()
        {
            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(30); // Volgende 31 dagen inclusief vandaag

            var activities = _context.VictuzActivities
                .Where(a => a.ActivityDate.Date >= startDate && a.ActivityDate.Date <= endDate)
                .OrderBy(a => a.ActivityDate) // Sorteer activiteiten op datum
                .Include(a => a.ParticipantsList)
                .ToList();

            // Haal de drie meest recente suggesties op
            var suggestions = _context.Suggestions
                .OrderByDescending(s => s.Id) // Sorteer op Id om de meest recente suggesties te krijgen
                .Take(3)
                .ToList();

            var model = new HomeViewModel
            {
                StartDate = startDate,
                EndDate = endDate,
                Activities = activities,
                Suggestions = suggestions // Voeg de suggesties toe aan het model
            };


            return View(model);
        }

        // Privacy Actie
        public IActionResult Privacy()
        {
            return View();
        }

        // Overzicht van alle activiteiten (optioneel)
        public IActionResult Activities()
        {
            var activities = _context.VictuzActivities
                .OrderBy(a => a.ActivityDate)
                .Include(a => a.ParticipantsList)
                .ToList();

            return View(activities);
        }

        // Details van een specifieke activiteit
        public IActionResult Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var activity = _context.VictuzActivities
                .Include(a => a.ParticipantsList)
                .FirstOrDefault(a => a.Id == id);

            if (activity == null)
            {
                return NotFound();
            }

            return View(activity);
        }

        public IActionResult Voordelen()
        {
            return View();
        }

        // Foutafhandeling
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> RegisterGuest([FromBody] RegisterGuestModel model)
        {
            if (ModelState.IsValid)
            {
                // Maak een nieuwe IdentityUser aan
                var user = new IdentityUser
                {
                    UserName = model.Username,
                    Email = model.Email,
                    LockoutEnabled = true // Optioneel: standaard vergrendeling
                };

                // Maak de gebruiker aan met een wachtwoord
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // Voeg de gebruiker toe aan de rol die uit het formulier komt
                    var role = string.IsNullOrEmpty(model.Role) ? "Guest" : model.Role; // Als geen rol is opgegeven, standaard naar Guest
                    await _userManager.AddToRoleAsync(user, role);

                    return Json(new { success = true });
                }

                // Verwerk eventuele fouten
                return Json(new { success = false, errors = result.Errors.Select(e => e.Description).ToList() });
            }

            // Terugkeren als ModelState niet geldig is
            return Json(new { success = false, errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList() });
        }


        [Authorize(Roles = "Organizer")]
        public async Task<IActionResult> ManageAccounts()
        {
            var users = await _userManager.Users.ToListAsync();
            var userRolesViewModel = new List<UserRoleViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userRolesViewModel.Add(new UserRoleViewModel
                {
                    User = user,
                    Roles = roles.ToList() // Assuming a single role per user, use roles.FirstOrDefault() if needed
                });
            }

            var model = new ManageAccountsViewModel
            {
                AllUsers = userRolesViewModel,
                PendingAccounts = userRolesViewModel.Where(u => u.User.LockoutEnabled && u.User.LockoutEnd != null).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Organizer")]
        public async Task<IActionResult> UnblockAccount(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.LockoutEnd = null;
                user.LockoutEnabled = false;
                await _userManager.UpdateAsync(user);
            }
            return RedirectToAction("ManageAccounts");
        }

        [HttpPost]
        [Authorize(Roles = "Organizer")]
        public async Task<IActionResult> ChangeRole(string userId, string newRole)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles); // Optionally remove all existing roles
                await _userManager.AddToRoleAsync(user, newRole);
            }
            return RedirectToAction("ManageAccounts");
        }

        [HttpPost]
        [Authorize(Roles = "Organizer")]
        public async Task<IActionResult> LockAccount(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100); // Arbitrary long lockout period
                await _userManager.UpdateAsync(user);
            }
            return RedirectToAction("ManageAccounts");
        }

        [HttpPost]
        [Authorize(Roles = "Organizer")]
        public async Task<IActionResult> DeleteAccount(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    // Optional: Add a success message here (e.g., ViewData["Message"] = "User deleted successfully.")
                    return RedirectToAction("ManageAccounts");
                }
                else
                {
                    // Optional: Handle errors
                    ModelState.AddModelError(string.Empty, "Er is een fout opgetreden bij het verwijderen van het account.");
                }
            }

            return RedirectToAction("ManageAccounts");
        }




    }
}
