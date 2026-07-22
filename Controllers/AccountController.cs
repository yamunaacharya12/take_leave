using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Take_Leave.Data;
using Take_Leave.Models;
using Take_Leave.Models.ViewModels;
using Take_Leave.Services;

namespace Take_Leave.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IActivityLogService _activityLog;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            IEmailService emailService,
            IActivityLogService activityLog)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
            _emailService = emailService;
            _activityLog = activityLog;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                // Find user by email
                var user = await _userManager.FindByEmailAsync(model.Email);

                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt. Email not found.");
                    return View(model);
                }

                // Check if account is approved
                if (!user.IsApproved)
                {
                    ModelState.AddModelError(string.Empty, "Your account is pending admin approval. Please check back later.");
                    return View(model);
                }

                // Check if account is active
                if (!user.IsActive)
                {
                    ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact administrator.");
                    return View(model);
                }

                // Attempt login
                var result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    // Update last login date
                    user.LastLoginDate = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);

                    // Redirect based on role
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                    {
                        return RedirectToLocal(returnUrl) ?? RedirectToAction("Dashboard", "Admin");
                    }
                    else if (await _userManager.IsInRoleAsync(user, "DepartmentHead"))
                    {
                        return RedirectToLocal(returnUrl) ?? RedirectToAction("Dashboard", "DepartmentHead");
                    }
                    else if (await _userManager.IsInRoleAsync(user, "Staff"))
                    {
                        return RedirectToLocal(returnUrl) ?? RedirectToAction("Dashboard", "Staff");
                    }
                }

                if (result.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty, "Account locked out. Please try again later.");

                    await _activityLog.LogAsync(
                        category: "Security",
                        message: $"Account for {user.FullName} was locked out after repeated failed login attempts.",
                        level: ActivityLevel.Warning,
                        performedByName: "System",
                        recipientUserId: user.Id);
                }
                else if (result.IsNotAllowed)
                {
                    ModelState.AddModelError(string.Empty, "Login not allowed. Please confirm your email.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid password. Please try again.");
                }
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            ViewBag.Departments = _context.Departments.Where(d => d.IsActive).ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if employee ID already exists (skip check if left blank —
                // multiple staff, e.g. HOD/Admin/Reception, may legitimately have no ID)
                var existingUser = string.IsNullOrWhiteSpace(model.EmployeeId)
                    ? null
                    : await _userManager.Users
                        .FirstOrDefaultAsync(u => u.EmployeeId == model.EmployeeId);

                if (existingUser != null)
                {
                    ModelState.AddModelError("EmployeeId", "Employee ID already exists");
                    ViewBag.Departments = _context.Departments.Where(d => d.IsActive).ToList();
                    return View(model);
                }

                // Check if email already exists
                var existingEmail = await _userManager.FindByEmailAsync(model.Email);
                if (existingEmail != null)
                {
                    ModelState.AddModelError("Email", "Email already registered");
                    ViewBag.Departments = _context.Departments.Where(d => d.IsActive).ToList();
                    return View(model);
                }

                var department = await _context.Departments.FindAsync(model.DepartmentId);

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    EmployeeId = model.EmployeeId,
                    DepartmentId = model.DepartmentId,
                    Designation = model.Designation,

                    JoiningDate = model.JoiningDate,
                    UserRole = UserRole.Staff,
                    IsApproved = false,
                    IsActive = true,
                    EmailConfirmed = true, // Set to false if email confirmation is required
                    RegistrationDate = DateTime.UtcNow,
                    AnnualLeaveBalance = 20,
                    SickLeaveBalance = 12,
                    EmergencyLeaveBalance = 5
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Ensure Staff role exists
                    if (!await _roleManager.RoleExistsAsync("Staff"))
                    {
                        await _roleManager.CreateAsync(new IdentityRole("Staff"));
                    }

                    await _userManager.AddToRoleAsync(user, "Staff");

                    await _activityLog.LogAsync(
                        category: "Staff",
                        message: $"New staff member {user.FullName} registered and awaiting approval.",
                        level: ActivityLevel.Info,
                        performedByName: "System",
                        recipientUserId: user.Id);

                    // Send notification to all admins
                    var admins = await _userManager.GetUsersInRoleAsync("Admin");
                    foreach (var admin in admins)
                    {
                        await _emailService.SendEmailAsync(admin.Email,
                            "New Staff Registration Pending Approval",
                            $@"<h3>New Staff Registration</h3>
                            <p><strong>Name:</strong> {user.FullName}</p>
                            <p><strong>Employee ID:</strong> {user.EmployeeId}</p>
                            <p><strong>Department:</strong> {user.Department}</p>
                            <p><strong>Email:</strong> {user.Email}</p>
                            <p>Please login to the admin dashboard to approve or reject this registration.</p>");
                    }

                    // ---> FIXED: Direct notification alert routed to your actual personal email <---
                    await _emailService.SendEmailAsync("sherpadorjee2580@gmail.com",
                        "System Alert: New Registration Request",
                        $@"<h3>Registration Alert</h3>
                        <p>A new staff profile for <strong>{user.FullName}</strong> (ID: {user.EmployeeId}) has been successfully logged.</p>
                        <p>Please review the administrative dashboard portal to process these credentials.</p>");

                    TempData["Success"] = "Registration successful! Please wait for admin approval.";
                    return RedirectToAction("RegistrationPending");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ViewBag.Departments = _context.Departments.Where(d => d.IsActive).ToList();
            return View(model);
        }

        [HttpGet]
        public IActionResult RegistrationPending()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            TempData["Success"] = "You have been logged out successfully.";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var resetLink = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, Request.Scheme);

                    await _emailService.SendEmailAsync(user.Email,
                        "Password Reset Request",
                        $"<h3>Password Reset</h3><p>Click <a href='{resetLink}'>here</a> to reset your password. This link expires in 24 hours.</p>");

                    await _activityLog.LogAsync(
                        category: "Security",
                        message: $"Password reset requested for {user.FullName}.",
                        level: ActivityLevel.Warning,
                        performedByName: user.FullName,
                        performedByUserId: user.Id,
                        recipientUserId: user.Id);

                    TempData["Success"] = "Password reset link has been sent to your email.";
                }
                else
                {
                    // Don't reveal that the user doesn't exist
                    TempData["Success"] = "If the email exists, a password reset link has been sent.";
                }
                return RedirectToAction("Login");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            var model = new ResetPasswordViewModel { Token = token, Email = email };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
                    if (result.Succeeded)
                    {
                        await _activityLog.LogAsync(
                            category: "Security",
                            message: $"Password was reset via email link for {user.FullName}.",
                            level: ActivityLevel.Warning,
                            performedByName: user.FullName,
                            performedByUserId: user.Id,
                            recipientUserId: user.Id);

                        TempData["Success"] = "Password has been reset successfully.";
                        return RedirectToAction("Login");
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid reset attempt.");
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ApplicationUser model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            user.FullName = model.FullName;
            user.Designation = model.Designation;

            // NEW: allow changing your own role from the profile page.
            // Keeps the UserRole column AND the Identity role table in sync,
            // same pattern used in AdminController.AssignDepartmentHead.
            if (model.UserRole != user.UserRole)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (currentRoles.Any())
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                }

                var newRoleName = model.UserRole.ToString();
                if (!await _roleManager.RoleExistsAsync(newRoleName))
                {
                    await _roleManager.CreateAsync(new IdentityRole(newRoleName));
                }

                await _userManager.AddToRoleAsync(user, newRoleName);
                user.UserRole = model.UserRole;
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                // Role changed while signed in — refresh the auth cookie so the
                // sidebar/permissions reflect the new role immediately.
                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "Profile updated successfully!";
            }
            else
            {
                TempData["Error"] = "Failed to update profile.";
            }

            return RedirectToAction("Profile");
        }

        // NEW: shared password-change endpoint — used by Admin, DepartmentHead, and Staff profile pages
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the errors and try again.";
                return RedirectToAction("Profile");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);

                await _activityLog.LogAsync(
                    category: "Security",
                    message: $"{user.FullName} changed their account password.",
                    level: ActivityLevel.Warning,
                    performedByName: user.FullName,
                    performedByUserId: user.Id,
                    recipientUserId: user.Id);

                TempData["Success"] = "Password changed successfully!";
            }
            else
            {
                TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("Profile");
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return null;
        }
    }
}
