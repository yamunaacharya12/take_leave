using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Take_Leave.Models;
using Take_Leave.Data;
using Take_Leave.Services;
using Take_Leave.Models.ViewModels;

namespace Take_Leave.Controllers
{
    [Authorize(Roles = "DepartmentHead")]
    public class DepartmentHeadController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public DepartmentHeadController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            INotificationService notificationService)
        {
            _userManager = userManager;
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var head = await _userManager.GetUserAsync(User);
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.HeadUserId == head.Id);

            if (department == null)
            {
                return View(new DepartmentHeadDashboardViewModel());
            }

            var pendingLeaves = await _context.LeaveRequests
                .Include(l => l.User)
                .Where(l => l.DepartmentId == department.Id.ToString() && l.Status == LeaveStatus.Pending)
                .OrderBy(l => l.RequestDate)
                .ToListAsync();

            var pendingRegistrations = await _userManager.Users
                .Where(u => u.DepartmentId == department.Id && !u.IsApproved)
                .ToListAsync();

            var viewModel = new DepartmentHeadDashboardViewModel
            {
                Department = department,
                PendingLeaves = pendingLeaves,
                TotalStaff = await _userManager.Users.CountAsync(u => u.DepartmentId == department.Id && u.IsApproved),
                PendingLeavesCount = pendingLeaves.Count,
                PendingRegistrations = pendingRegistrations,
                PendingRegistrationsCount = pendingRegistrations.Count,
                ApprovedLeavesThisMonth = await _context.LeaveRequests
                    .CountAsync(l => l.DepartmentId == department.Id.ToString() &&
                               l.Status == LeaveStatus.ApprovedByDepartmentHead &&
                               l.RequestDate.Month == DateTime.Now.Month),
                RecentLeaves = pendingLeaves.Take(5).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            var head = await _userManager.GetUserAsync(User);
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.HeadUserId == head.Id);

            if (user == null || user.DepartmentId != department?.Id)
            {
                TempData["Error"] = "Unauthorized action or user not found.";
                return RedirectToAction("Dashboard");
            }

            user.IsApproved = true;
            await _userManager.UpdateAsync(user);

            TempData["Success"] = $"User {user.FullName} has been approved.";
            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> PendingLeaves()
        {
            var head = await _userManager.GetUserAsync(User);
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.HeadUserId == head.Id);

            var pendingLeaves = await _context.LeaveRequests
                .Include(l => l.User)
                .Where(l => l.DepartmentId == department.Id.ToString() && l.Status == LeaveStatus.Pending)
                .OrderBy(l => l.RequestDate)
                .ToListAsync();

            return View(pendingLeaves);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveLeave(int leaveId, string comments)
        {
            var leave = await _context.LeaveRequests
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == leaveId);

            if (leave != null)
            {
                var head = await _userManager.GetUserAsync(User);
                var department = await _context.Departments.FirstOrDefaultAsync(d => d.HeadUserId == head.Id);

                if (leave.DepartmentId != department.Id.ToString())
                {
                    TempData["Error"] = "You are not authorized to approve this leave.";
                    return RedirectToAction("PendingLeaves");
                }

                leave.Status = LeaveStatus.ApprovedByDepartmentHead;
                leave.DepartmentHeadComments = comments;
                leave.DepartmentHeadApprovalDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _notificationService.NotifyStatusChangeAsync(leave);
                await _notificationService.NotifyAdminAsync(leave);

                TempData["Success"] = "Leave request approved!";
            }

            return RedirectToAction("PendingLeaves");
        }

        [HttpPost]
        public async Task<IActionResult> RejectLeave(int leaveId, string comments)
        {
            var leave = await _context.LeaveRequests
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == leaveId);

            if (leave != null)
            {
                var head = await _userManager.GetUserAsync(User);
                var department = await _context.Departments.FirstOrDefaultAsync(d => d.HeadUserId == head.Id);

                if (leave.DepartmentId != department.Id.ToString())
                {
                    TempData["Error"] = "You are not authorized to reject this leave.";
                    return RedirectToAction("PendingLeaves");
                }

                leave.Status = LeaveStatus.RejectedByDepartmentHead;
                leave.DepartmentHeadComments = comments;
                leave.DepartmentHeadApprovalDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _notificationService.NotifyStatusChangeAsync(leave);

                TempData["Success"] = "Leave request rejected.";
            }

            return RedirectToAction("PendingLeaves");
        }

        public async Task<IActionResult> TeamLeaves()
        {
            var head = await _userManager.GetUserAsync(User);
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.HeadUserId == head.Id);

            var teamLeaves = await _context.LeaveRequests
                .Include(l => l.User)
                .Where(l => l.DepartmentId == department.Id.ToString())
                .OrderByDescending(l => l.RequestDate)
                .ToListAsync();

            return View(teamLeaves);
        }

        public async Task<IActionResult> StaffList()
        {
            var head = await _userManager.GetUserAsync(User);
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.HeadUserId == head.Id);

            var staff = await _userManager.Users
                .Where(u => u.DepartmentId == department.Id && (u.UserRole == UserRole.Staff || u.UserRole == UserRole.Student) && u.IsApproved)
                .ToListAsync();

            return View(staff);
        }

        [HttpGet]
        public async Task<IActionResult> StaffLeaveHistory(string userId)
        {
            var head = await _userManager.GetUserAsync(User);
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.HeadUserId == head.Id);
            var staff = await _userManager.FindByIdAsync(userId);

            if (staff == null || staff.DepartmentId != department?.Id)
            {
                return NotFound();
            }

            var leaves = await _context.LeaveRequests
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.RequestDate)
                .ToListAsync();

            ViewBag.StaffName = staff.FullName;
            return View(leaves);
        }
    }
}