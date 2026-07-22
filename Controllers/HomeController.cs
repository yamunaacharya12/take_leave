using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Take_Leave.Models;

namespace Take_Leave.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // If someone is already logged in, skip the public landing page
            // and send them straight to their role's dashboard.
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Admin"))
                {
                    return RedirectToAction("Dashboard", "Admin");
                }
                if (User.IsInRole("DepartmentHead"))
                {
                    return RedirectToAction("Dashboard", "DepartmentHead");
                }
                if (User.IsInRole("Staff"))
                {
                    return RedirectToAction("Dashboard", "Staff");
                }
            }

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
