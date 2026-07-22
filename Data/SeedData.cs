using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Take_Leave.Models;

namespace Take_Leave.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            Console.WriteLine("Starting database seeding...");

            // Ensure database is created
            await context.Database.EnsureCreatedAsync();
            Console.WriteLine("Database ensured.");

            // 1. Create roles
            var roles = new[] { "Admin", "DepartmentHead", "Staff" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var result = await roleManager.CreateAsync(new IdentityRole(role));
                    if (result.Succeeded)
                    {
                        Console.WriteLine($"Created role: {role}");
                    }
                }
                else
                {
                    Console.WriteLine($"Role already exists: {role}");
                }
            }

            // 2. Create departments FIRST so we can use their real tracking IDs
            var deptNames = new[] { "BIT", "BCS", "MCS", "Administration", "Account" };
            var departments = new List<Department>();

            foreach (var deptName in deptNames)
            {
                var dept = await context.Departments.FirstOrDefaultAsync(d => d.Name == deptName);
                if (dept == null)
                {
                    dept = new Department
                    {
                        Name = deptName,
                        Description = $"{deptName} Department",
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow,
                        HeadUserId = null
                    };
                    context.Departments.Add(dept);
                    await context.SaveChangesAsync();
                    Console.WriteLine($"Created department: {deptName}");
                }
                departments.Add(dept);
            }

            // Find the database entity entry for the administration division to link to our root profile
            var adminDept = await context.Departments.FirstOrDefaultAsync(d => d.Name == "Administration");
            int adminDeptId = adminDept?.Id ?? 1; // Fallback to 1 if it somehow fails to fetch

            // 3. Create Admin user
            var adminEmail = "admin@texascollege.edu.np";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            Console.WriteLine($"Looking for admin user: {adminEmail}");

            if (adminUser == null)
            {
                Console.WriteLine("Admin user not found, creating...");

                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    EmployeeId = "ADMIN001",
                    Designation = "System Administrator",

                    // FIXED: Assigned the relational integer foreign key instead of the raw string word
                    DepartmentId = adminDeptId,

                    JoiningDate = DateTime.UtcNow.AddYears(-5),
                    UserRole = UserRole.Admin,
                    IsApproved = true,
                    EmailConfirmed = true,
                    ApprovalDate = DateTime.UtcNow,
                    AnnualLeaveBalance = 30,
                    SickLeaveBalance = 15,
                    EmergencyLeaveBalance = 10,
                    RegistrationDate = DateTime.UtcNow,
                    IsActive = true
                };

                // Use Admin@1991 password (more reliable)
                var result = await userManager.CreateAsync(adminUser, "Admin@1991");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    Console.WriteLine("Admin user created successfully!");
                    Console.WriteLine($"Admin Email: {adminEmail}");
                    Console.WriteLine($"Admin Password: Admin@1991");
                }
                else
                {
                    Console.WriteLine("Failed to create admin: " + string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                Console.WriteLine("Admin user already exists.");

                // Ensure admin has role
                if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    Console.WriteLine("Admin role assigned.");
                }
            }

            // Verify admin was created
            var verifyAdmin = await userManager.FindByEmailAsync(adminEmail);
            if (verifyAdmin != null)
            {
                var roles2 = await userManager.GetRolesAsync(verifyAdmin);
                Console.WriteLine($"Admin verified: {verifyAdmin.Email}, Roles: {string.Join(", ", roles2)}");
            }

            Console.WriteLine("Seed data completed successfully!");
        }
    }
}