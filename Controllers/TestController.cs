using Microsoft.AspNetCore.Mvc;
using J_Tutors_Web_Platform.Services.Storage;

namespace J_Tutors_Web_Platform.Controllers
{
    public class TestController : Controller
    {
        private readonly FileShareService _fs;
        public TestController(FileShareService fs) { _fs = fs; }

        // GET /Test/FileShare
        [HttpGet]
        public async Task<IActionResult> FileShare()
        {
            var files = await _fs.ListAsync();
            return View("FileShare", files);
        }

        // POST /Test/FileShareUpload
        [HttpPost]
        [RequestSizeLimit(524_288_000)]
        public async Task<IActionResult> FileShareUpload(IFormFile file)
        {
            if (file is null || file.Length == 0)
                return RedirectToAction(nameof(FileShare));

            using var s = file.OpenReadStream();
            await _fs.UploadAsync(s, file.Length, file.FileName);
            return RedirectToAction(nameof(FileShare));
        }

        // GET /Test/FileShareDownload?name=MyDoc.pdf
        [HttpGet]
        public async Task<IActionResult> FileShareDownload(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("Missing name.");
            var stream = await _fs.DownloadAsync(name);
            var downloadName = System.IO.Path.GetFileName(name);
            return File(stream, "application/octet-stream", downloadName);
        }
    }
}
