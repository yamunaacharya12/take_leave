using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Take_Leave.Models;
using Take_Leave.Models.ViewModels;
using Take_Leave.Data;
using Take_Leave.Services;

namespace Take_Leave.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public StaffController(UserManager<ApplicationUser> userManager, ApplicationDbContext context, INotificationService notificationService)
        {
            _userManager = userManager;
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            var leaveRequests = await _context.LeaveRequests
                .Where(l => l.UserId == user.Id)
                .OrderByDescending(l => l.RequestDate)
                .ToListAsync();

            ViewBag.PendingRequests = leaveRequests.Count(l => l.Status == LeaveStatus.Pending);
            ViewBag.ApprovedRequests = leaveRequests.Count(l => l.Status == LeaveStatus.Approved);
            ViewBag.RejectedRequests = leaveRequests.Count(l => l.Status == LeaveStatus.Rejected);

            return View(leaveRequests);
        }

        [HttpGet]
        public async Task<IActionResult> ApplyLeave()
        {
            // Needed so the view can bind asp-for="..." without a null-reference,
            // and so the leave-balance panel at the bottom of the form has data.
            ViewBag.CurrentUser = await _userManager.GetUserAsync(User);
            return View(new LeaveRequestViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyLeave(LeaveRequestViewModel model, IFormFile? document)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill all required fields correctly.";
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            if (!await CheckLeaveBalance(user, model.LeaveType, model.TotalDays))
            {
                TempData["Error"] = $"Insufficient {model.LeaveType} leave balance!";
                return View(model);
            }

            var leaveRequest = new LeaveRequest
            {
                UserId = user.Id,
                LeaveType = model.LeaveType,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                TotalDays = model.TotalDays,
                Reason = model.Reason,
                Status = LeaveStatus.Pending,
                RequestDate = DateTime.UtcNow
            };

            if (document != null && document.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                Directory.CreateDirectory(uploadsFolder);
                var uniqueFileName = $"{user.EmployeeId}_{DateTime.Now.Ticks}_{document.FileName}";
                using (var stream = new FileStream(Path.Combine(uploadsFolder, uniqueFileName), FileMode.Create))
                    await document.CopyToAsync(stream);
                leaveRequest.DocumentUrl = $"/uploads/{uniqueFileName}";
            }

            _context.LeaveRequests.Add(leaveRequest);
            await _context.SaveChangesAsync();
            await _notificationService.NotifyLeaveApplicationAsync(leaveRequest);

            TempData["Success"] = "Leave application submitted successfully!";
            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> LeaveHistory()
        {
            var user = await _userManager.GetUserAsync(User);
            var requests = await _context.LeaveRequests
                .Where(l => l.UserId == user.Id)
                .OrderByDescending(l => l.RequestDate)
                .ToListAsync();
            return View(requests);
        }

        // Serves Views/Staff/LeaveBalance.cshtml (@model ApplicationUser)
        public async Task<IActionResult> LeaveBalance() => View(await _userManager.GetUserAsync(User));

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            // NEW: departments list for the Department dropdown on the profile form
            ViewBag.Departments = await _context.Departments.Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
            return View(await _userManager.GetUserAsync(User));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ApplicationUser model, int DepartmentId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.Designation = model.Designation;
            user.Address = model.Address;       // NEW
            user.DepartmentId = DepartmentId;   // NEW

            await _userManager.UpdateAsync(user);
            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction("Profile");
        }

        [HttpGet]
        public async Task<IActionResult> GetLeaveBalance(LeaveType leaveType)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false });

            int balance = leaveType switch
            {
                LeaveType.Annual => user.AnnualLeaveBalance,
                LeaveType.Sick => user.SickLeaveBalance,
                LeaveType.Emergency => user.EmergencyLeaveBalance,
                _ => 0
            };
            return Json(new { success = true, balance });
        }

        private async Task<bool> CheckLeaveBalance(ApplicationUser user, LeaveType leaveType, int days)
        {
            int balance = leaveType switch
            {
                LeaveType.Annual => user.AnnualLeaveBalance,
                LeaveType.Sick => user.SickLeaveBalance,
                LeaveType.Emergency => user.EmergencyLeaveBalance,
                _ => 0
            };
            return balance >= days;
        }
    }
}
