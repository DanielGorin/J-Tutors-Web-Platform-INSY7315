using AspNetCoreGeneratedDocument;
using J_Tutors_Web_Platform.Services.Storage;
using J_Tutors_Web_Platform.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace J_Tutors_Web_Platform.Controllers.Testing
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
            string Username = User.FindFirst(ClaimTypes.Name)?.Value;

            if (file is null || file.Length == 0)
                return RedirectToAction("GetFileShareRows", "Test");

            using var s = file.OpenReadStream();
            await _fs.UploadAsync(Username,s, file.Length, file.FileName);
            return RedirectToAction("GetFileShareRows", "Test");
        }

        // GET /Test/FileShareDownload?name=MyDoc.pdf
        [HttpGet]
        public async Task<IActionResult> FileShareDownload(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("Missing name.");
            var stream = await _fs.DownloadAsync(name);
            var downloadName = Path.GetFileName(name);
            return File(stream, "application/octet-stream", downloadName);
        }

        [HttpGet]
        public IActionResult GetFileShareRows()
        {
            var Username  = User.FindFirst(ClaimTypes.Name)?.Value;

            var AFilesVM = new AFilesViewModel
            {
                FSR = _fs.GetFileShareRows(Username),
            };

            return View("~/Views/Admin/AFiles.cshtml", AFilesVM);
        }

        [HttpPost]
        public IActionResult ManageAccess(string FileName)
        {
            var fileID = _fs.GetFileID(FileName);

            var AFilesVM = new AFilesViewModel
            {
                FSAR = _fs.GetFileShareAccessRows(FileName),
                CurrentFileID = fileID
            };

            return View("~/Views/Admin/AManageAccess.cshtml", AFilesVM);
        }

        public IActionResult DeleteFile(string fileName)
        {
            _fs.DeleteAsync(fileName);

            return RedirectToAction("GetFileShareRows", "Test");
        }

        public IActionResult AddFileAccess(int FileID, string Username, DateTime StartDate, DateTime EndDate) 
        {
            _fs.AddFileAccess(FileID, Username, StartDate, EndDate);

            return RedirectToAction("ManageAccess", "Test");
        }
    }
}
