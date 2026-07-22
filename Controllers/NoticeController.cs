using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Take_Leave.Data;
using Take_Leave.Models;
using Take_Leave.Services;

namespace Take_Leave.Controllers
{
    public class NoticeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityLogService _activityLog;

        public NoticeController(ApplicationDbContext context, IActivityLogService activityLog)
        {
            _context = context;
            _activityLog = activityLog;
        }

        // GET: Notice
        public async Task<IActionResult> Index()
        {
            // Populate the dropdown/checkbox list for the create/edit forms
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();

            // Load the notices including their target departments
            var noticesQuery = _context.Notices.Include(n => n.Departments).AsQueryable();

            // Security Audience Filter: Admins see everything, staff only see
            // notices with no department set (= All Departments) or notices
            // that include their own department.
            if (!User.IsInRole("Admin"))
            {
                var userEmail = User.Identity?.Name;

                var currentIdentityUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == userEmail || u.Email == userEmail);

                if (currentIdentityUser != null)
                {
                    int? employeeDeptId = currentIdentityUser.DepartmentId;

                    // Pull everything first, then filter in memory — EF Core can't
                    // translate "collection contains matching id OR collection is empty"
                    // cleanly against a many-to-many navigation in all providers.
                    var allNotices = await noticesQuery.OrderByDescending(n => n.SentDate).ToListAsync();

                    var visible = allNotices.Where(n =>
                        !n.Departments.Any() ||
                        (employeeDeptId.HasValue && n.Departments.Any(d => d.Id == employeeDeptId.Value))
                    ).ToList();

                    return View(visible);
                }
                else
                {
                    var allNotices = await noticesQuery.OrderByDescending(n => n.SentDate).ToListAsync();
                    return View(allNotices.Where(n => !n.Departments.Any()).ToList());
                }
            }

            return View(await noticesQuery.OrderByDescending(n => n.SentDate).ToListAsync());
        }

        // GET: Notice/Create
        public async Task<IActionResult> Create()
        {
            if (!User.IsInRole("Admin"))
            {
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            return View();
        }

        // POST: Notice/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string Subject, string Content, List<int>? DepartmentIds)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(Subject) || string.IsNullOrWhiteSpace(Content))
            {
                TempData["Error"] = "Subject and content are required.";
                return RedirectToAction(nameof(Index));
            }

            var notice = new Notice
            {
                Subject = Subject,
                Content = Content,
                SentBy = User.Identity?.Name ?? "Administrator",
                SentDate = DateTime.Now
            };

            if (DepartmentIds != null && DepartmentIds.Any())
            {
                notice.Departments = await _context.Departments
                    .Where(d => DepartmentIds.Contains(d.Id))
                    .ToListAsync();
            }

            _context.Notices.Add(notice);
            await _context.SaveChangesAsync();

            string audienceLabel = notice.Departments.Any()
                ? string.Join(", ", notice.Departments.Select(d => d.Name))
                : "All Departments";

            await _activityLog.LogAsync(
                category: "Notice",
                message: $"New notice \"{notice.Subject}\" posted to {audienceLabel} by {notice.SentBy}.",
                level: ActivityLevel.Info,
                performedByName: notice.SentBy);

            TempData["Success"] = "Notice broadcasted successfully to the designated team ledger.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Notice/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            if (id == null)
            {
                return NotFound();
            }

            var notice = await _context.Notices
                .Include(n => n.Departments)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (notice == null)
            {
                return NotFound();
            }

            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            ViewBag.SelectedDepartmentIds = notice.Departments.Select(d => d.Id).ToList();
            return View(notice);
        }

        // POST: Notice/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string Subject, string Content, List<int>? DepartmentIds)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var existing = await _context.Notices
                .Include(n => n.Departments)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (existing == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(Subject) || string.IsNullOrWhiteSpace(Content))
            {
                TempData["Error"] = "Subject and content are required.";
                ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
                ViewBag.SelectedDepartmentIds = DepartmentIds ?? new List<int>();
                return View(existing);
            }

            existing.Subject = Subject;
            existing.Content = Content;

            existing.Departments.Clear();
            if (DepartmentIds != null && DepartmentIds.Any())
            {
                var selectedDepartments = await _context.Departments
                    .Where(d => DepartmentIds.Contains(d.Id))
                    .ToListAsync();

                foreach (var dept in selectedDepartments)
                {
                    existing.Departments.Add(dept);
                }
            }

            await _context.SaveChangesAsync();

            await _activityLog.LogAsync(
                category: "Notice",
                message: $"Notice \"{existing.Subject}\" was edited by {User.Identity?.Name ?? "Administrator"}.",
                level: ActivityLevel.Info,
                performedByName: User.Identity?.Name ?? "Administrator");

            TempData["Success"] = "Notice updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Notice/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var notice = await _context.Notices.FindAsync(id);
            if (notice == null)
            {
                TempData["Error"] = "Notice not found.";
                return RedirectToAction(nameof(Index));
            }

            string subject = notice.Subject;
            _context.Notices.Remove(notice);
            await _context.SaveChangesAsync();

            await _activityLog.LogAsync(
                category: "Notice",
                message: $"Notice \"{subject}\" was deleted by {User.Identity?.Name ?? "Administrator"}.",
                level: ActivityLevel.Warning,
                performedByName: User.Identity?.Name ?? "Administrator");

            TempData["Success"] = "Notice deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
