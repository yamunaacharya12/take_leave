using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Take_Leave.Data;
using Take_Leave.Models;
using Take_Leave.Models.ViewModels;
using Take_Leave.Services;

namespace Take_Leave.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;   // NEW
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IActivityLogService _activityLog;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,               // NEW
            ApplicationDbContext context,
            IEmailService emailService,
            IActivityLogService activityLog)
        {
            _userManager = userManager;
            _roleManager = roleManager;                          // NEW
            _context = context;
            _emailService = emailService;
            _activityLog = activityLog;
        }

        // GET: Admin/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                bool isAdmin = User.IsInRole("Admin");

                // Fetch Pending Registrations based on role
                IQueryable<ApplicationUser> userQuery = _userManager.Users.Include(u => u.Department);
                if (isAdmin)
                {
                    userQuery = userQuery.Where(u => !u.IsApproved && (u.UserRole == UserRole.Staff || u.UserRole == UserRole.Student));
                }
                else
                {
                    userQuery = userQuery.Where(u => !u.IsApproved && (u.UserRole == UserRole.Staff || u.UserRole == UserRole.Student) && u.Department.HeadUserId == currentUserId);
                }
                var pendingRegistrations = await userQuery.ToListAsync();

                // Fetch Pending Leaves based on role
                IQueryable<LeaveRequest> leaveQuery = _context.LeaveRequests.Include(l => l.User).ThenInclude(u => u.Department);
                if (isAdmin)
                {
                    leaveQuery = leaveQuery.Where(l => l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.ApprovedByDepartmentHead);
                }
                else
                {
                    leaveQuery = leaveQuery.Where(l => (l.Status == LeaveStatus.Pending) && l.User.Department.HeadUserId == currentUserId);
                }
                var pendingLeaves = await leaveQuery.OrderBy(l => l.RequestDate).ToListAsync();

                // Populate ViewModel
                var viewModel = new AdminDashboardViewModel
                {
                    PendingRegistrations = pendingRegistrations,
                    PendingLeaves = pendingLeaves,
                    TotalStaff = await _userManager.Users.CountAsync(u => u.IsApproved && (u.UserRole == UserRole.Staff || u.UserRole == UserRole.Student)),
                    PendingLeavesCount = pendingLeaves.Count,
                    PendingRegistrationsCount = pendingRegistrations.Count,
                    TotalLeavesThisMonth = await _context.LeaveRequests.CountAsync(l => l.RequestDate.Month == DateTime.Now.Month && l.RequestDate.Year == DateTime.Now.Year),
                    ApprovedLeavesThisMonth = await _context.LeaveRequests.CountAsync(l => l.Status == LeaveStatus.Approved && l.RequestDate.Month == DateTime.Now.Month && l.RequestDate.Year == DateTime.Now.Year)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return View(new AdminDashboardViewModel());
            }
        }

        // GET: Admin/StaffList
        public async Task<IActionResult> StaffList()
        {
            // Needed for the "Add Staff" modal's Department dropdown — populated
            // before the try block so it's always available, even on the error path.
            ViewBag.Departments = await _context.Departments.Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();

            try
            {
                // 1. Fetch the filtered user data into memory from the database ledger first
                var staffList = await _userManager.Users
                    .Include(u => u.Department)
                    .Where(u => u.UserRole == UserRole.Staff || u.UserRole == UserRole.DepartmentHead || u.UserRole == UserRole.Student)
                    .ToListAsync();

                // 2. Safely perform fallback sorting using LINQ-to-Objects to prevent expression errors
                var sortedStaff = staffList
                    .OrderBy(u => u.Department != null ? u.Department.Name : "Unknown")
                    .ThenBy(u => u.FullName)
                    .ToList();

                return View(sortedStaff);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading staff list: {ex.Message}";
                return View(new List<ApplicationUser>());
            }
        }

        // GET: Admin/GetStaffDetails/{id}
        [HttpGet]
        public async Task<IActionResult> GetStaffDetails(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return BadRequest("User ID is required");
                }

                var user = await _userManager.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    return NotFound($"Staff member with ID {id} not found");
                }

                return PartialView("_StaffDetailsPartial", user);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }


        //authorize the staff 
        private async Task<bool> IsAuthorizedToApprove(int leaveId)
        {
            var leave = await _context.LeaveRequests.Include(l => l.User).ThenInclude(u => u.Department).FirstOrDefaultAsync(l => l.Id == leaveId);
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Admin (or whatever role you use) can always approve
            if (User.IsInRole("Admin")) return true;

            // Check if current user is the Head of the requester's department
            return leave?.User.Department?.HeadUserId == currentUserId;
        }

        // GET: Admin/GetStaffLeaveBalance/{id}
        [HttpGet]
        public async Task<IActionResult> GetStaffLeaveBalance(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return BadRequest("User ID is required");
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"Staff member with ID {id} not found");
                }

                return PartialView("_LeaveBalancePartial", user);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }

        // GET: Admin/StaffLeaveHistory/{id}
        [HttpGet]
        public async Task<IActionResult> StaffLeaveHistory(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return BadRequest("User ID is required");
                }

                var staff = await _userManager.FindByIdAsync(id);
                if (staff == null)
                {
                    return NotFound();
                }

                var leaves = await _context.LeaveRequests
                    .Where(l => l.UserId == id)
                    .OrderByDescending(l => l.RequestDate)
                    .ToListAsync();

                ViewBag.StaffName = staff.FullName;
                ViewBag.StaffId = staff.EmployeeId;
                return View(leaves);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("StaffList");
            }
        }

        // GET: Admin/EditStaff/{id}
        [HttpGet]
        public async Task<IActionResult> EditStaff(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return BadRequest("User ID is required");
                }

                var user = await _userManager.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    return NotFound();
                }

                ViewBag.Departments = await _context.Departments.Where(d => d.IsActive).ToListAsync();
                return View(user);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("StaffList");
            }
        }

        // POST: Admin/EditStaff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStaff(string id, ApplicationUser model, int DepartmentId)
        {
            try
            {
                if (id != model.Id)
                {
                    return BadRequest("User ID mismatch");
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound();
                }

                user.FullName = model.FullName;
                user.Designation = model.Designation;
                user.DepartmentId = DepartmentId;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    TempData["Success"] = "Staff updated successfully!";
                }
                else
                {
                    TempData["Error"] = "Failed to update staff.";
                }

                return RedirectToAction("StaffList");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("StaffList");
            }
        }

        // POST: Admin/CreateStaff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(string FullName, string EmployeeId, string Email, int DepartmentId, string Designation, DateTime JoiningDate, string Password)
        {
            try
            {
                var existingUser = await _userManager.FindByEmailAsync(Email);
                if (existingUser != null)
                {
                    TempData["Error"] = "Email already exists!";
                    return RedirectToAction("StaffList");
                }

                var existingEmployee = string.IsNullOrWhiteSpace(EmployeeId)
                    ? null
                    : await _userManager.Users.FirstOrDefaultAsync(u => u.EmployeeId == EmployeeId);
                if (existingEmployee != null)
                {
                    TempData["Error"] = "Employee ID already exists!";
                    return RedirectToAction("StaffList");
                }

                var user = new ApplicationUser
                {
                    UserName = Email,
                    Email = Email,
                    FullName = FullName,
                    EmployeeId = EmployeeId,
                    DepartmentId = DepartmentId,
                    Designation = Designation,
                    JoiningDate = JoiningDate == DateTime.MinValue ? DateTime.UtcNow : JoiningDate,
                    UserRole = UserRole.Staff,
                    IsApproved = true,
                    IsActive = true,
                    EmailConfirmed = true,
                    RegistrationDate = DateTime.UtcNow,
                    AnnualLeaveBalance = 20,
                    SickLeaveBalance = 12,
                    EmergencyLeaveBalance = 5
                };

                var result = await _userManager.CreateAsync(user, Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Staff");

                    await _activityLog.LogAsync(
                        category: "Staff",
                        message: $"New staff member {user.FullName} registered by Admin.",
                        level: ActivityLevel.Success,
                        performedByName: "Admin");

                    TempData["Success"] = $"Staff {FullName} created successfully!";
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    TempData["Error"] = $"Failed to create staff: {errors}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error creating staff: {ex.Message}";
            }

            return RedirectToAction("StaffList");
        }

        // POST: Admin/ApproveUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.IsApproved = true;
                user.ApprovalDate = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                string emailSubject = "Account Approved - Texas College Leave System";
                string emailBody = $"<h3>Welcome to Texas College!</h3><p>Dear {user.FullName},</p><p>Your account has been approved. You can now login.</p>";
                await _emailService.SendEmailAsync(user.Email, emailSubject, emailBody);

                await _activityLog.LogAsync(
                    category: "Staff",
                    message: $"Registration for {user.FullName} has been approved.",
                    level: ActivityLevel.Success,
                    performedByName: "Admin",
                    recipientUserId: user.Id);

                TempData["Success"] = $"User {user.FullName} approved successfully!";
            }
            return RedirectToAction("StaffList");
        }

        // POST: Admin/DeactivateStaff
        [HttpPost]
        public async Task<IActionResult> DeactivateStaff(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.IsActive = false;
                user.IsApproved = false;
                await _userManager.UpdateAsync(user);

                await _activityLog.LogAsync(
                    category: "Security",
                    message: $"Staff account for {user.FullName} was deactivated.",
                    level: ActivityLevel.Warning,
                    performedByName: "Admin",
                    recipientUserId: user.Id);

                return Json(new { success = true });
            }
            return Json(new { success = false, message = "User not found" });
        }

        // POST: Admin/ActivateStaff
        [HttpPost]
        public async Task<IActionResult> ActivateStaff(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.IsActive = true;
                user.IsApproved = true;
                await _userManager.UpdateAsync(user);

                await _activityLog.LogAsync(
                    category: "Security",
                    message: $"Staff account for {user.FullName} was reactivated.",
                    level: ActivityLevel.Info,
                    performedByName: "Admin",
                    recipientUserId: user.Id);

                return Json(new { success = true });
            }
            return Json(new { success = false, message = "User not found" });
        }

        // GET: Admin/GetLeaveDetails
        [HttpGet]
        public async Task<IActionResult> GetLeaveDetails(int leaveId)
        {
            var leave = await _context.LeaveRequests
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == leaveId);

            if (leave == null) return NotFound();

            return PartialView("_LeaveDetailsPartial", leave);
        }

        //staff
        [HttpGet]
        public async Task<IActionResult> GetEligibleStaff()
        {
            var staff = await _context.Users
                .Select(u => new { u.Id, FullName = $"{u.FullName} ({u.EmployeeId})" })
                .ToListAsync();
            return Json(staff);
        }

        // POST: Admin/ApproveLeave
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveLeave(int leaveId, string comments)
        {
            if (!await IsAuthorizedToApprove(leaveId)) return Forbid();

            var leave = await _context.LeaveRequests.Include(l => l.User).FirstOrDefaultAsync(l => l.Id == leaveId);
            if (leave != null)
            {
                leave.Status = LeaveStatus.Approved;
                leave.AdminComments = comments;
                leave.AdminApprovalDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await UpdateLeaveBalance(leave.UserId, leave.LeaveType, leave.TotalDays);
                await _emailService.SendEmailAsync(leave.User.Email, "Leave Request Approved", "...");

                await _activityLog.LogAsync(
                    category: "Leave",
                    message: $"Leave request by {leave.User.FullName} ({leave.LeaveType} Leave) has been approved.",
                    level: ActivityLevel.Success,
                    performedByName: "Admin",
                    leaveRequestId: leave.Id,
                    recipientUserId: leave.UserId);

                TempData["Success"] = "Leave request approved.";
            }
            return RedirectToAction("Dashboard");
        }

        // POST: Admin/RejectLeave
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectLeave(int leaveId, string comments)
        {
            // Authorization check to allow Admin or the relevant Department Head
            if (!await IsAuthorizedToApprove(leaveId))
            {
                return Forbid();
            }

            var leave = await _context.LeaveRequests
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == leaveId);

            if (leave != null)
            {
                leave.Status = LeaveStatus.Rejected;
                leave.AdminComments = comments;
                leave.AdminApprovalDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _emailService.SendEmailAsync(leave.User.Email,
                    "Leave Request Rejected",
                    $"<h3>Leave Request Rejected</h3><p>Dear {leave.User.FullName},</p><p>We regret to inform you that your leave request has been rejected.</p><p><strong>Details:</strong></p><ul><li>Type: {leave.LeaveType}</li><li>From: {leave.StartDate:MMMM dd, yyyy}</li><li>To: {leave.EndDate:MMMM dd, yyyy}</li><li>Days: {leave.TotalDays}</li></ul>{(string.IsNullOrEmpty(comments) ? "" : $"<p><strong>Reason:</strong> {comments}</p>")}");

                await _activityLog.LogAsync(
                    category: "Leave",
                    message: $"Leave request by {leave.User.FullName} ({leave.LeaveType} Leave) has been rejected.",
                    level: ActivityLevel.Warning,
                    performedByName: "Admin",
                    leaveRequestId: leave.Id,
                    recipientUserId: leave.UserId);

                TempData["Success"] = $"Leave request for {leave.User.FullName} has been rejected!";
            }

            return RedirectToAction("AllLeaveRequests");
        }

        // GET: Admin/AllLeaveRequests
        public async Task<IActionResult> AllLeaveRequests(string status, string leaveType, DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var query = _context.LeaveRequests
                    .Include(l => l.User)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(status))
                {
                    if (Enum.TryParse<LeaveStatus>(status, out var statusEnum))
                    {
                        query = query.Where(l => l.Status == statusEnum);
                    }
                }

                if (!string.IsNullOrEmpty(leaveType))
                {
                    if (Enum.TryParse<LeaveType>(leaveType, out var typeEnum))
                    {
                        query = query.Where(l => l.LeaveType == typeEnum);
                    }
                }

                if (fromDate.HasValue)
                {
                    query = query.Where(l => l.RequestDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(l => l.RequestDate <= toDate.Value);
                }

                var leaves = await query
                    .OrderByDescending(l => l.RequestDate)
                    .ToListAsync();

                return View(leaves);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading leave requests: {ex.Message}";
                return View(new List<LeaveRequest>());
            }
        }

        // GET: Admin/Departments
        public async Task<IActionResult> Departments()
        {
            var departments = await _context.Departments
                .Include(d => d.HeadUser)
                .ToListAsync();

            return View(departments);
        }

        // GET: Admin/NotificationLogs
        public async Task<IActionResult> NotificationLogs()
        {
            var logs = await _context.ActivityLogs
                .Include(a => a.LeaveRequest)
                    .ThenInclude(l => l.User)
                .Include(a => a.RecipientUser)
                .OrderByDescending(a => a.CreatedDate)
                .Take(100)
                .ToListAsync();

            return View(logs);
        }

        // GET: Admin/ExportLogs
        [HttpGet]
        public async Task<IActionResult> ExportLogs()
        {
            var logs = await _context.ActivityLogs
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Category,Level,Message,PerformedBy,Date");
            foreach (var log in logs)
            {
                var message = (log.Message ?? "").Replace("\"", "\"\"");
                sb.AppendLine($"\"{log.Category}\",\"{log.Level}\",\"{message}\",\"{log.PerformedByName}\",\"{log.CreatedDate:yyyy-MM-dd HH:mm}\"");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"activity-logs-{DateTime.Now:yyyyMMdd}.csv");
        }

        // POST: Admin/ClearLogs
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearLogs()
        {
            var logs = await _context.ActivityLogs.ToListAsync();
            _context.ActivityLogs.RemoveRange(logs);
            await _context.SaveChangesAsync();

            TempData["Success"] = "All activity logs have been cleared.";
            return RedirectToAction("NotificationLogs");
        }




        // POST: Admin/CreateDepartment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDepartment(Department department)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingDept = await _context.Departments
                        .FirstOrDefaultAsync(d => d.Name == department.Name);

                    if (existingDept != null)
                    {
                        TempData["Error"] = "Department with this name already exists!";
                        return RedirectToAction("Departments");
                    }

                    department.CreatedDate = DateTime.UtcNow;
                    department.IsActive = true;
                    _context.Departments.Add(department);
                    await _context.SaveChangesAsync();

                    await _activityLog.LogAsync(
                        category: "System",
                        message: $"New department \"{department.Name}\" added to the system.",
                        level: ActivityLevel.Info,
                        performedByName: "Admin");

                    TempData["Success"] = $"Department '{department.Name}' created successfully!";
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Error creating department: {ex.Message}";
                }
            }
            return RedirectToAction("Departments");
        }

        // POST: Admin/EditDepartment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDepartment(int id, string Name, string Description)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Name))
                {
                    TempData["Error"] = "Department name cannot be empty.";
                    return RedirectToAction("Departments");
                }

                var department = await _context.Departments.FindAsync(id);
                if (department == null)
                {
                    TempData["Error"] = "Department not found.";
                    return RedirectToAction("Departments");
                }

                // Prevent renaming to a name that collides with a different department
                var nameCollision = await _context.Departments
                    .FirstOrDefaultAsync(d => d.Name == Name && d.Id != id);
                if (nameCollision != null)
                {
                    TempData["Error"] = "Another department already uses this name.";
                    return RedirectToAction("Departments");
                }

                string oldName = department.Name;
                department.Name = Name.Trim();
                department.Description = Description?.Trim() ?? string.Empty;

                await _context.SaveChangesAsync();

                await _activityLog.LogAsync(
                    category: "System",
                    message: oldName == department.Name
                        ? $"Department \"{department.Name}\" details were updated."
                        : $"Department \"{oldName}\" was renamed to \"{department.Name}\".",
                    level: ActivityLevel.Info,
                    performedByName: "Admin");

                TempData["Success"] = $"Department '{department.Name}' updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating department: {ex.Message}";
            }

            return RedirectToAction("Departments");
        }

        // POST: Admin/DeleteDepartment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            try
            {
                var department = await _context.Departments.FindAsync(id);
                if (department == null)
                {
                    TempData["Error"] = "Department not found.";
                    return RedirectToAction("Departments");
                }

                // Block deletion if staff are still assigned to this department —
                // deleting it out from under them would either fail on a DB
                // constraint or, worse, silently orphan/cascade-delete their records.
                bool hasStaff = await _userManager.Users.AnyAsync(u => u.DepartmentId == id);
                if (hasStaff)
                {
                    TempData["Error"] = $"Cannot delete '{department.Name}' — it still has staff assigned. Reassign them to another department first.";
                    return RedirectToAction("Departments");
                }

                string deptName = department.Name;
                _context.Departments.Remove(department);
                await _context.SaveChangesAsync();

                await _activityLog.LogAsync(
                    category: "System",
                    message: $"Department \"{deptName}\" was deleted by Admin.",
                    level: ActivityLevel.Warning,
                    performedByName: "Admin");

                TempData["Success"] = $"Department '{deptName}' deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting department: {ex.Message}";
            }

            return RedirectToAction("Departments");
        }

        // POST: Admin/AssignDepartmentHead
        [HttpPost]
        public async Task<IActionResult> AssignDepartmentHead(int departmentId, string userId)
        {
            try
            {
                var department = await _context.Departments.FindAsync(departmentId);
                if (department == null)
                {
                    return Json(new { success = false, message = "Department not found" });
                }

                var newHead = await _userManager.FindByIdAsync(userId);
                if (newHead == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // NEW: make sure both roles actually exist in AspNetRoles before
                // anything below tries to add/remove them — this is what was
                // throwing "Role DepartmentHead does not exist."
                if (!await _roleManager.RoleExistsAsync("DepartmentHead"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("DepartmentHead"));
                }
                if (!await _roleManager.RoleExistsAsync("Staff"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Staff"));
                }

                // Demote the CURRENT head of THIS department (if any), since they're being replaced
                if (!string.IsNullOrEmpty(department.HeadUserId) && department.HeadUserId != userId)
                {
                    var oldHead = await _userManager.FindByIdAsync(department.HeadUserId);
                    if (oldHead != null)
                    {
                        oldHead.UserRole = UserRole.Staff;
                        await _userManager.UpdateAsync(oldHead);
                        if (await _userManager.IsInRoleAsync(oldHead, "DepartmentHead"))
                        {
                            await _userManager.RemoveFromRoleAsync(oldHead, "DepartmentHead");
                        }
                        if (!await _userManager.IsInRoleAsync(oldHead, "Staff"))
                        {
                            await _userManager.AddToRoleAsync(oldHead, "Staff");
                        }
                    }
                }

                // Enforce one-person-one-department: if the person being assigned is
                // already Head of a DIFFERENT department, strip that headship first
                // so they can't lead two departments at once.
                var otherDeptsLedByThisUser = await _context.Departments
                    .Where(d => d.HeadUserId == userId && d.Id != departmentId)
                    .ToListAsync();

                foreach (var otherDept in otherDeptsLedByThisUser)
                {
                    otherDept.HeadUserId = null;
                }

                department.HeadUserId = userId;
                await _context.SaveChangesAsync();

                newHead.UserRole = UserRole.DepartmentHead;
                await _userManager.UpdateAsync(newHead);

                // NEW: drop a stale "Staff" role entry first so the user doesn't
                // end up sitting in both roles simultaneously
                if (await _userManager.IsInRoleAsync(newHead, "Staff"))
                {
                    await _userManager.RemoveFromRoleAsync(newHead, "Staff");
                }

                if (!await _userManager.IsInRoleAsync(newHead, "DepartmentHead"))
                {
                    var addResult = await _userManager.AddToRoleAsync(newHead, "DepartmentHead");
                    if (!addResult.Succeeded)
                    {
                        var errors = string.Join(" ", addResult.Errors.Select(e => e.Description));
                        return Json(new { success = false, message = $"Saved, but role assignment failed: {errors}" });
                    }
                }

                string activityMessage = otherDeptsLedByThisUser.Any()
                    ? $"{newHead.FullName} was reassigned as Head of {department.Name} (removed as Head of {string.Join(", ", otherDeptsLedByThisUser.Select(d => d.Name))})."
                    : $"{newHead.FullName} was assigned as Head of {department.Name}.";

                await _activityLog.LogAsync(
                    category: "Staff",
                    message: activityMessage,
                    level: ActivityLevel.Info,
                    performedByName: "Admin",
                    recipientUserId: newHead.Id);

                return Json(new { success = true, message = "Department head assigned successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: Admin/GenerateReport
        [HttpGet]
        public async Task<IActionResult> GenerateReport(DateTime? startDate, DateTime? endDate)
        {
            startDate ??= DateTime.Now.AddMonths(-1);
            endDate ??= DateTime.Now;

            var leaves = await _context.LeaveRequests
                .Include(l => l.User)
                    .ThenInclude(u => u.Department)
                .Where(l => l.RequestDate >= startDate && l.RequestDate <= endDate)
                .ToListAsync();

            var report = new LeaveReportViewModel
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                TotalApplications = leaves.Count,
                ApprovedApplications = leaves.Count(l => l.Status == LeaveStatus.Approved),
                RejectedApplications = leaves.Count(l => l.Status == LeaveStatus.Rejected || l.Status == LeaveStatus.RejectedByDepartmentHead),
                PendingApplications = leaves.Count(l => l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.ApprovedByDepartmentHead),
                LeavesByDepartment = leaves.GroupBy(l => l.User?.Department?.Name ?? "Unknown")
                    .Select(g => new DepartmentLeaveSummary { Department = g.Key, Count = g.Count() })
                    .ToList(),
                LeavesByType = leaves.GroupBy(l => l.LeaveType)
                    .Select(g => new LeaveTypeSummary { LeaveType = g.Key, Count = g.Count() })
                    .ToList()
            };

            return View(report);
        }

        private async Task UpdateLeaveBalance(string userId, LeaveType leaveType, int days)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return;

            switch (leaveType)
            {
                case LeaveType.Annual:
                    user.AnnualLeaveBalance -= days;
                    break;
                case LeaveType.Sick:
                    user.SickLeaveBalance -= days;
                    break;
                case LeaveType.Emergency:
                    user.EmergencyLeaveBalance -= days;
                    break;
            }

            await _userManager.UpdateAsync(user);
        }
    }
}