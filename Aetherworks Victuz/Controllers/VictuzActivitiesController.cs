﻿// Controllers/VictuzActivitiesController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Aetherworks_Victuz.Data;
using Aetherworks_Victuz.Models;
using static Aetherworks_Victuz.Models.VictuzActivity;
using System.Diagnostics;

namespace Aetherworks_Victuz.Controllers
{
    public class VictuzActivitiesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public VictuzActivitiesController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: VictuzActivities
        public async Task<IActionResult> Index()
        {
            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(30);

            var activities = await _context.VictuzActivities
                .Where(a => a.ActivityDate.Date >= startDate && a.ActivityDate.Date <= endDate)
                .OrderBy(a => a.ActivityDate)
                .Include(v => v.Host)
                .Include(v => v.Location)
                .Include(v => v.ParticipantsList)
                .ToListAsync();

            var calendarViewModel = new CalendarViewModel
            {
                StartDate = startDate,
                EndDate = endDate,
                Activities = activities
            };

            var viewModel = new CompositeViewModel
            {
                Calendar = calendarViewModel,
                Activities = activities
            };

            return View(viewModel);
        }

// POST: VictuzActivities/Register
[HttpPost]
[Authorize]
public async Task<IActionResult> Register(int activityId)
{
    var activity = await _context.VictuzActivities
        .Include(a => a.ParticipantsList)
        .FirstOrDefaultAsync(a => a.Id == activityId);

    if (activity == null)
    {
        return NotFound();
    }

    if (activity.ParticipantLimit > 0 && activity.ParticipantsList.Count >= activity.ParticipantLimit)
    {
        TempData["ErrorMessage"] = "De activiteit zit vol.";
        return RedirectToAction("Index");
    }

    // Haal het IdentityUserId op van de ingelogde gebruiker
    var identityUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

    if (string.IsNullOrEmpty(identityUserId))
    {
        TempData["ErrorMessage"] = "Kan gebruikers-ID niet ophalen. Controleer je loginstatus.";
        return RedirectToAction("Index");
    }

    // Zoek de bijbehorende User in jouw database op basis van de Credential.Id van IdentityUser
    var user = await _context.User
        .FirstOrDefaultAsync(u => u.Credential != null && u.Credential.Id == identityUserId);

    if (user == null)
    {
        TempData["ErrorMessage"] = "Gebruiker niet gevonden.";
        return RedirectToAction("Index");
    }

    // Controleer of de gebruiker al is ingeschreven voor de activiteit
    if (activity.ParticipantsList.Any(p => p.UserId == user.Id))
    {
        TempData["ErrorMessage"] = "Je bent al ingeschreven voor deze activiteit.";
        return RedirectToAction("Index");
    }

    // Voeg de gebruiker toe aan de deelnemerslijst
    var participation = new Participation
    {
        UserId = user.Id
    };

    activity.ParticipantsList.Add(participation);
    _context.Update(activity);
    await _context.SaveChangesAsync();

    TempData["SuccessMessage"] = "Je bent succesvol ingeschreven voor de activiteit.";
    return RedirectToAction("Index");
}





        // GET: VictuzActivities/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var victuzActivity = await _context.VictuzActivities
                .Include(v => v.Host)
                .Include(v => v.Location)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (victuzActivity == null)
            {
                return NotFound();
            }

            var attendees = _context.Participation
                .Include(p => p.User)
                .ThenInclude(u => u.Credential)
                .Where(p => p.ActivityId == id);

            var viewModel = new VictuzActivityViewModel() 
            { 
                VictuzActivity = victuzActivity, 
                Attendees = attendees.ToList() 
            };
            viewModel.SetOldPicture();



            return View(viewModel);
        }

        // GET: VictuzActivities/Create
        public IActionResult Create()
        {
            ViewData["HostId"] = new SelectList(_context.User, "Id", "Id");
            ViewData["LocationId"] = new SelectList(_context.Set<Location>(), "Id", "Id");
            var enumCategories = Enum.GetValues(typeof(VictuzActivity.ActivityCategories))
                .Cast<VictuzActivity.ActivityCategories>()
                .ToDictionary(
                    category => category,
                    category => GetDisplayNameForCategory(category)
                );
            ViewData["Category"] = new SelectList(enumCategories, "Key", "Value");
            return View();
        }

        // POST: VictuzActivities/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Category,Name,Description,LocationId,ActivityDate,HostId,Price,MemberPrice,ParticipantLimit")] VictuzActivity victuzActivity, IFormFile? PictureFile)
        {
            if (ModelState.IsValid)
            {
                if (PictureFile != null && PictureFile.Length > 0)
                {
                    string imgFolderPath = Path.Combine(_webHostEnvironment.WebRootPath, "img");

                    if (!Directory.Exists(imgFolderPath))
                    {
                        Directory.CreateDirectory(imgFolderPath);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(PictureFile.FileName);
                    string filePath = Path.Combine(imgFolderPath, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await PictureFile.CopyToAsync(stream);
                    }

                    victuzActivity.Picture = "\\img\\" + uniqueFileName;
                }

                _context.Add(victuzActivity);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["HostId"] = new SelectList(_context.User, "Id", "Id", victuzActivity.HostId);
            ViewData["LocationId"] = new SelectList(_context.Locations, "Id", "Id", victuzActivity.LocationId);
            var enumCategories = Enum.GetValues(typeof(VictuzActivity.ActivityCategories))
                .Cast<VictuzActivity.ActivityCategories>()
                .ToDictionary(
                    category => category,
                    category => GetDisplayNameForCategory(category)
                );
            ViewData["Category"] = new SelectList(enumCategories, "Key", "Value");
            return View(victuzActivity);
        }

        // GET: VictuzActivities/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var victuzActivity = await _context.VictuzActivities.FindAsync(id);
            if (victuzActivity == null)
            {
                return NotFound();
            }
            ViewData["HostId"] = new SelectList(_context.User, "Id", "Id", victuzActivity.HostId);
            ViewData["LocationId"] = new SelectList(_context.Locations, "Id", "Id", victuzActivity.LocationId);
            var enumCategories = Enum.GetValues(typeof(VictuzActivity.ActivityCategories))
                .Cast<VictuzActivity.ActivityCategories>()
                .ToDictionary(
                    category => category,
                    category => GetDisplayNameForCategory(category)
                );
            ViewData["Category"] = new SelectList(enumCategories, "Key", "Value");
            ViewData["CurrentCategory"] = GetDisplayNameForCategory(victuzActivity.Category);
            return View(victuzActivity);
        }

        // POST: VictuzActivities/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Category,Name,Description,LocationId,ActivityDate,HostId,Price,MemberPrice,ParticipantLimit,Picture")] VictuzActivity victuzActivity, IFormFile? PictureFile)
        {
            if (id != victuzActivity.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                if (PictureFile != null && PictureFile.Length > 0)
                {
                    string imgFolderPath = Path.Combine(_webHostEnvironment.WebRootPath, "img");

                    if (!Directory.Exists(imgFolderPath))
                    {
                        Directory.CreateDirectory(imgFolderPath);
                    }
                    else
                    {
                        string fullImagePath = Path.Combine(_webHostEnvironment.WebRootPath, victuzActivity.Picture.TrimStart('\\'));
                        if (System.IO.File.Exists(fullImagePath))
                        {
                            System.IO.File.Delete(fullImagePath);
                        }
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(PictureFile.FileName);
                    string filePath = Path.Combine(imgFolderPath, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await PictureFile.CopyToAsync(stream);
                    }

                    victuzActivity.Picture = "\\img\\" + uniqueFileName;
                }

                _context.Update(victuzActivity);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewData["HostId"] = new SelectList(_context.User, "Id", "Id", victuzActivity.HostId);
            ViewData["LocationId"] = new SelectList(_context.Locations, "Id", "Id", victuzActivity.LocationId);
            var enumCategories = Enum.GetValues(typeof(VictuzActivity.ActivityCategories))
                .Cast<VictuzActivity.ActivityCategories>()
                .ToDictionary(
                    category => category,
                    category => GetDisplayNameForCategory(category)
                );
            ViewData["Category"] = new SelectList(enumCategories, "Key", "Value");

            return View(victuzActivity);
        }

        // GET: VictuzActivities/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var victuzActivity = await _context.VictuzActivities
                .Include(v => v.Host)
                .Include(v => v.Location)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (victuzActivity == null)
            {
                return NotFound();
            }

            return View(victuzActivity);
        }

        // POST: VictuzActivities/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var victuzActivity = await _context.VictuzActivities.FindAsync(id);
            if (victuzActivity != null)
            {
                _context.VictuzActivities.Remove(victuzActivity);
            }
            await _context.SaveChangesAsync();
            if (!string.IsNullOrEmpty(victuzActivity.Picture))
            {
                var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, victuzActivity.Picture.TrimStart('\\'));
                if (System.IO.File.Exists(oldFilePath))
                {
                    System.IO.File.Delete(oldFilePath);
                }
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: VictuzActivities/reservaties
        [Authorize]
        public async Task<IActionResult> Reservations()
        {
            var identityUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(identityUserId))
            {
                TempData["ErrorMessage"] = "Kan gebruikers-ID niet ophalen. Controleer je loginstatus.";
                return RedirectToAction("Index");
            }

            var user = await _context.User
                .FirstOrDefaultAsync(u => u.Credential != null && u.Credential.Id == identityUserId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "Gebruiker niet gevonden.";
                return RedirectToAction("Index");
            }

            var reservations = _context.Participation
                .Include(p => p.Activity)
                .ThenInclude(a => a.Host)
                .Include(p => p.Activity)
                .ThenInclude(a => a.Location)
                .Where(p => p.UserId == user.Id);

            return View(reservations);
        }


        private bool VictuzActivityExists(int id)
        {
            return _context.VictuzActivities.Any(e => e.Id == id);
        }

        private string GetDisplayNameForCategory(ActivityCategories category)
        {
            return category switch
            {
                ActivityCategories.Free => "Free Activity",
                ActivityCategories.MemFree => "Free for Members",
                ActivityCategories.PayAll => "Paid for All",
                ActivityCategories.MemOnlyFree => "Members Only - Free",
                ActivityCategories.MemOnlyPay => "Members Only - Paid",
                _ => category.ToString()
            };
        }

        [HttpPost]
        public IActionResult ToggleAttendance(int participationId)
        {
            // Assuming you have a DbContext instance called _context
            var participation = _context.Participation.FirstOrDefault(p => p.Id == participationId);
            if (participation == null)
            {
                return NotFound(); // Handle case where participation is not found
            }

            // Toggle the attendance status
            participation.Attended = !participation.Attended;

            // Save changes to the database
            _context.SaveChanges();

            // Redirect back to the activity details page (or use RedirectToAction if needed)
            return RedirectToAction(nameof(Details), new { id = participation.ActivityId });
        }


    }
}
