using AspNetCoreGeneratedDocument;
using J_Tutors_Web_Platform.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using J_Tutors_Web_Platform.Services;

namespace J_Tutors_Web_Platform.Controllers
{
    public class FileController : Controller
    {
        private readonly FileShareService _fs;
        public FileController(FileShareService fs) { _fs = fs; }

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
            string username = User.FindFirst(ClaimTypes.Name)?.Value;

            if (file is null || file.Length == 0)
                return RedirectToAction("GetFileShareRows", "File");

            using var s = file.OpenReadStream();
            await _fs.UploadAsync(username,s, file.Length, file.FileName);

            return RedirectToAction("GetFileShareRows", "File");
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

            return RedirectToAction("GetFileShareRows", "File");
        }

        public IActionResult AddFileAccess(int FileID, string Username, DateTime StartDate, DateTime EndDate) 
        {
            _fs.AddFileAccess(FileID, Username, StartDate, EndDate);

            return RedirectToAction("GetFileShareRows", "File");
        }

        public IActionResult GetUserFiles() 
        {
            string username = User.FindFirst(ClaimTypes.Name)?.Value;

            Console.WriteLine("Getting user files" + username);

            var AFilesVM = new AFilesViewModel
            {
                FSAR = _fs.GetUserFileShares(username)
            };

            return View("~/Views/User/UFileLibrary.cshtml", AFilesVM);
        }

        //file access management for users
        [HttpPost]
        public IActionResult UpdateFileAccess(int FileID, int UserID, DateOnly StartDate, DateOnly EndDate)
        {
            _fs.UpdateFileAccess(FileID, UserID, StartDate, EndDate);

            string fileName = _fs.GetFileName(FileID);
            Console.WriteLine("Updated access for file: " + fileName);
            return RedirectToAction("GetFileShareRows", "File");
        }

        [HttpPost]
        public IActionResult RemoveFileAccess(int FileID, int UserID)
        {

            _fs.DeleteAccess(FileID, UserID);
            
            string fileName = _fs.GetFileName(FileID);
                        Console.WriteLine("Deleted access for file: " + fileName);
            return RedirectToAction("GetFileShareRows", "File");
        }
    }
}
