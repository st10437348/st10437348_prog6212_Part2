using CMCSPart2.Models;
using CMCSPart2.Services;
using Microsoft.AspNetCore.Mvc;

namespace CMCSPart2.Controllers
{
    public class CoordinatorController : Controller
    {
        private readonly InMemoryStore _store;
        public CoordinatorController(InMemoryStore store) => _store = store;

        private string RequireRole()
        {
            var role = HttpContext.Session.GetString("Role");
            if (!string.Equals(role, "Coordinator", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Not a coordinator");
            return HttpContext.Session.GetString("Username") ?? "Coordinator";
        }

        public IActionResult Index()
        {
            var name = RequireRole();
            ViewBag.Username = name;
            return View();
        }

        public async Task<IActionResult> Claims()
        {
            RequireRole();
            var claims = await _store.GetAllClaimsAsync();

            var list = new List<Claim>();
            foreach (var c in claims)
            {
                var lecturer = await _store.GetLecturerByIdAsync(c.LecturerId);

                var latest = await _store.GetLatestApprovalAsync(c.ClaimId);
                var approvals = new List<Approval>();
                if (latest != null) approvals.Add(latest);

                list.Add(new Claim
                {
                    ClaimId = c.ClaimId,
                    LecturerId = c.LecturerId,
                    LecturerUsername = lecturer?.Name ?? $"Lecturer {c.LecturerId}",
                    HoursWorked = c.HoursWorked,
                    HourlyRate = c.HourlyRate,
                    TotalAmount = c.TotalAmount,
                    Status = c.Status,
                    SubmittedAt = c.SubmittedAt,
                    Notes = c.Notes,
                    Documents = await _store.GetDocumentsForClaimAsync(c.ClaimId),
                    Approvals = approvals
                });
            }

            return View(list);
        }

        public async Task<IActionResult> Edit(int id)
        {
            RequireRole();

            var c = await _store.GetClaimAsync(id);
            if (c == null) return NotFound();

            var lecturer = await _store.GetLecturerByIdAsync(c.LecturerId);
            var latest = await _store.GetLatestApprovalAsync(c.ClaimId);
            var docs = await _store.GetDocumentsForClaimAsync(c.ClaimId);

            return View(new Claim
            {
                ClaimId = c.ClaimId,
                LecturerId = c.LecturerId,
                LecturerUsername = lecturer?.Name,
                HoursWorked = c.HoursWorked,
                HourlyRate = c.HourlyRate,
                TotalAmount = c.TotalAmount,
                Status = c.Status,
                SubmittedAt = c.SubmittedAt,
                Notes = c.Notes,
                Documents = docs,
                Approvals = latest != null ? new List<Approval> { latest } : new List<Approval>()
            });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Approval approval)
        {
            RequireRole();
            await _store.UpdateClaimStatusAsync(id, approval.Decision, "Coordinator", approval.Comments ?? "");
            return RedirectToAction("Claims");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            RequireRole();
            await _store.DeleteClaimAsync(id);
            TempData["Info"] = $"Claim #{id} deleted.";
            return RedirectToAction("Claims");
        }
    }
}












