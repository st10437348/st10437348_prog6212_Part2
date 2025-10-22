using CMCSPart2.Models;
using CMCSPart2.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CMCSPart2.Controllers
{
    public class LecturersController : Controller
    {
        private readonly InMemoryStore _store;

        private const long MaxFileBytes = 10L * 1024 * 1024;
        private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx", ".xlsx" };
        private static readonly HashSet<string> AllowedMime = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        public LecturersController(InMemoryStore store)
        {
            _store = store;
        }

        private (int lecturerId, string username) RequireLecturer()
        {
            var role = HttpContext.Session.GetString("Role");
            if (!string.Equals(role, "Lecturer", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Not a lecturer");

            var lecturerId = HttpContext.Session.GetInt32("LecturerId") ?? 0;
            var username = HttpContext.Session.GetString("Username") ?? "";
            if (lecturerId == 0) throw new InvalidOperationException("Lecturer ID missing in session.");
            return (lecturerId, username);
        }

        public IActionResult Index()
        {
            var (lecturerId, username) = RequireLecturer();
            ViewBag.Username = username;
            ViewBag.LecturerId = lecturerId;

            return View(new Lecturer
            {
                LecturerId = lecturerId,
                UserId = HttpContext.Session.GetInt32("UserId") ?? 0,
                Name = username,
                Email = $"{username}@lecturer.com"
            });
        }

        public IActionResult Create()
        {
            RequireLecturer();
            return View(new Claim());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Claim claim)
        {
            var (lecturerId, _) = RequireLecturer();
            var id = await _store.CreateClaimAsync(lecturerId, claim.HoursWorked, claim.HourlyRate, claim.Notes ?? "");
            TempData["Info"] = $"Claim #{id} submitted.";
            return RedirectToAction("Details");
        }

        public async Task<IActionResult> Details()
        {
            var (lecturerId, _) = RequireLecturer();
            var claims = await _store.GetClaimsForLecturerAsync(lecturerId);

            var list = new List<Claim>();
            foreach (var c in claims)
            {
                var vm = new Claim
                {
                    ClaimId = c.ClaimId,
                    LecturerId = c.LecturerId,
                    HoursWorked = c.HoursWorked,
                    HourlyRate = c.HourlyRate,
                    TotalAmount = c.TotalAmount,
                    Status = c.Status,
                    SubmittedAt = c.SubmittedAt,
                    Notes = c.Notes,
                    Approvals = new List<Approval>(),
                    Documents = await _store.GetDocumentsForClaimAsync(c.ClaimId)
                };

                var latest = await _store.GetLatestApprovalAsync(c.ClaimId);
                if (latest != null)
                {
                    vm.Approvals.Add(new Approval
                    {
                        ApprovalId = latest.ApprovalId,
                        ClaimId = latest.ClaimId,
                        ApprovedBy = latest.ApprovedBy,
                        Decision = latest.Decision,
                        DecisionDate = latest.DecisionDate,
                        Comments = latest.Comments
                    });
                }

                list.Add(vm);
            }

            return View(list);
        }

        public async Task<IActionResult> UploadDocument()
        {
            var (lecturerId, _) = RequireLecturer();

            var types = new[] { "Timesheet", "Proof of work", "Invoice/Receipt", "Attendance", "Other" }
                .Select(t => new SelectListItem { Value = t, Text = t })
                .ToList();
            ViewBag.DocumentTypes = types;

            var claims = await _store.GetClaimsForLecturerAsync(lecturerId);
            ViewBag.Claims = claims
                .OrderByDescending(c => c.SubmittedAt)
                .Select(c => new SelectListItem
                {
                    Value = c.ClaimId.ToString(),
                    Text = $"ClaimId: {c.ClaimId} submitted on {c.SubmittedAt:yyyy-MM-dd}"
                })
                .ToList();

            return View(new SupportingDocument());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocument(int ClaimId, string FileType, IFormFile file)
        {
            var (lecturerId, _) = RequireLecturer();

            var myClaims = await _store.GetClaimsForLecturerAsync(lecturerId);
            if (!myClaims.Any(c => c.ClaimId == ClaimId))
            {
                TempData["Error"] = "Invalid claim selection.";
                return RedirectToAction("UploadDocument");
            }

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please choose a file.";
                return RedirectToAction("UploadDocument");
            }

            using var stream = file.OpenReadStream();
            await _store.UploadDocumentAsync(ClaimId, lecturerId, file.FileName, file.ContentType, stream);

            TempData["Info"] = "File uploaded.";
            return RedirectToAction("Details");
        }


    }
}




