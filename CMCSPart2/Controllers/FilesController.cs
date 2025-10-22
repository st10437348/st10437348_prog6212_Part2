using CMCS.Services;
using Microsoft.AspNetCore.Mvc;

namespace CMCS.Controllers
{
    public class FilesController : Controller
    {
        private readonly InMemoryStore _store;
        public FilesController(InMemoryStore store) => _store = store;

        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var doc = await _store.GetDocumentAsync(id);
            if (doc == null) return NotFound();

            var bytes = await _store.DecryptDocumentAsync(doc);
            return File(bytes, doc.FileType, fileDownloadName: doc.FileName);
        }
    }
}


