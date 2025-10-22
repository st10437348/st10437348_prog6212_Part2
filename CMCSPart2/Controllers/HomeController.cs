using CMCS.Models;
using CMCS.Services;
using Microsoft.AspNetCore.Mvc;

namespace CMCS.Controllers
{
    public class HomeController : Controller
    {
        private readonly InMemoryStore _store;

        public HomeController(InMemoryStore store)
        {
            _store = store;
        }

        public IActionResult Index()
        {
            ViewBag.Error = TempData["Error"];
            return View(new UserAccount());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(UserAccount account)
        {
            if (string.IsNullOrWhiteSpace(account.Username))
            {
                TempData["Error"] = "Username is required.";
                return RedirectToAction("Index");
            }

            var ok =
                (account.Role == "Lecturer" && account.Password == "lecturer") ||
                (account.Role == "Coordinator" && account.Password == "coordinator") ||
                (account.Role == "Manager" && account.Password == "manager");

            if (!ok)
            {
                TempData["Error"] = "Invalid role or password.";
                return RedirectToAction("Index");
            }

            var user = await _store.GetOrCreateUserAsync(account.Username.Trim(), account.Role);

            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("Role", user.Role);

            if (user.Role == "Lecturer")
            {
                var lec = await _store.GetOrCreateLecturerForUserAsync(
                    user.UserId, user.Username, $"{user.Username}@example.com");
                HttpContext.Session.SetInt32("LecturerId", lec.LecturerId);
                return RedirectToAction("Index", "Lecturers");
            }

            if (user.Role == "Coordinator") return RedirectToAction("Index", "Coordinator");
            if (user.Role == "Manager") return RedirectToAction("Index", "Manager");

            TempData["Error"] = "Unknown role.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["Error"] = "You have been logged out.";
            return RedirectToAction("Index");
        }
    }
}






