using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Take_Leave.Data;
using Take_Leave.Models;
using Take_Leave.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity with Role support
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Register Services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();

// Configure cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

var app = builder.Build();

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Force recreate admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var context = services.GetRequiredService<ApplicationDbContext>();

    try
    {
        // Ensure Admin role exists
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        // Delete existing admin if exists
        var adminEmail = "admin@texascollege.edu.np";
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin != null)
        {
            await userManager.DeleteAsync(existingAdmin);
            Console.WriteLine("Deleted existing admin user");
        }

        // FETCH ACTIVE DIVISION ID: Query the database to retrieve a valid structural reference key
        var adminDept = await context.Departments.FirstOrDefaultAsync(d => d.Name == "Administration");

        // Dynamic fallback initialization guard if seeding sequencing order shifts
        if (adminDept == null)
        {
            adminDept = new Department
            {
                Name = "Administration",
                Description = "Administration Department",
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };
            context.Departments.Add(adminDept);
            await context.SaveChangesAsync();
        }

        // Create new admin matching updated Model constraints
        var newAdmin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "System Administrator",
            EmployeeId = "ADMIN001",
            Designation = "System Administrator",

            // FIXED: Assigned the required relational primary key ID integer instead of the raw text string
            DepartmentId = adminDept.Id,

            JoiningDate = DateTime.UtcNow.AddYears(-5),
            UserRole = UserRole.Admin,
            IsApproved = true,
            EmailConfirmed = true,
            IsActive = true,
            RegistrationDate = DateTime.UtcNow,
            AnnualLeaveBalance = 30,
            SickLeaveBalance = 15,
            EmergencyLeaveBalance = 10
        };

        var result = await userManager.CreateAsync(newAdmin, "Admin@1991");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(newAdmin, "Admin");
            Console.WriteLine("Admin user created successfully!");
            Console.WriteLine("Email: admin@texascollege.edu.np");
            Console.WriteLine("Password: Admin@1991");
        }
        else
        {
            Console.WriteLine($"Failed to create admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error seeding admin: {ex.Message}");
    }
}

app.Run();
