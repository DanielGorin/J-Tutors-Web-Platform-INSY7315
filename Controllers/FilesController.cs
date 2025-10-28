#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using J_Tutors_Web_Platform.Services.Storage;
using J_Tutors_Web_Platform.Data;
using J_Tutors_Web_Platform.Models.AppFiles;

namespace J_Tutors_Web_Platform.Controllers
{
    /// <summary>
    /// File Share–backed documents:
    /// - GET  /files/my                      -> list files current user can access (via FileShareAccess)
    /// - GET  /files/download/{id}           -> download if authorized
    ///
    /// Admin ops (optional; keep here or in a separate AdminFilesController):
    /// - POST /files/admin/upload            -> upload + grant access to user IDs
    /// - POST /files/admin/delete/{id}       -> delete file + metadata
    /// </summary>
    [Route("files")]
    public class FilesController : Controller
    {
        private readonly FileShareService _fs;
        private readonly AppDbContext _db;

        public FilesController(FileShareService fs, AppDbContext db)
        {
            _fs = fs;
            _db = db;
        }

        // --- USER SIDE ---

        // GET /files/my?userId=123   (If you have auth, resolve userId from claims instead)
        [HttpGet("my")]
        public async Task<IActionResult> My(int? userId)
        {
            // If you have real auth: get from claims; this param is only for your current manual testing
            var uid = userId ?? 0;
            if (uid == 0) return BadRequest("Missing userId.");

            var now = DateTime.UtcNow;

            var files = await _db.Set<FileShareAccess>()
                .Where(a => a.UserID == uid &&
                            a.StartDate <= now &&
                            (a.EndDate == null || a.EndDate >= now))
                .Select(a => a.File)
                .OrderByDescending(f => f.UploadedAt)
                .ToListAsync();

            return View("MyFiles", files); // create a simple list view if you want; not required for functionality
        }

        // GET /files/download/42?userId=123
        [HttpGet("download/{id:int}")]
        public async Task<IActionResult> Download(int id, int? userId)
        {
            var uid = userId ?? 0;
            if (uid == 0) return BadRequest("Missing userId.");

            var now = DateTime.UtcNow;

            var file = await _db.Set<AppFile>()
                .Include(f => f.FileAccesses)
                .FirstOrDefaultAsync(f => f.FileID == id);

            if (file is null) return NotFound();

            var allowed = file.FileAccesses.Any(a =>
                a.UserID == uid && a.StartDate <= now && (a.EndDate == null || a.EndDate >= now));

            if (!allowed) return Forbid();

            var stream = await _fs.DownloadAsync(file.StorageKeyOrUrl ?? file.FileName);
            var name = string.IsNullOrWhiteSpace(file.FileName) ? "download.bin" : file.FileName;
            var type = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;

            return File(stream, type, name);
        }

        // --- ADMIN SIDE (optional; move to AdminFilesController later) ---

        // POST /files/admin/upload
        // form: file=(file), adminId=1, allowedUserIds=1&allowedUserIds=2
        [HttpPost("admin/upload")]
        [RequestSizeLimit(524_288_000)]
        public async Task<IActionResult> AdminUpload(IFormFile? file, int adminId, [FromForm] int[]? allowedUserIds)
        {
            if (file is null || file.Length == 0) return BadRequest("No file.");
            using var s = file.OpenReadStream();
            var storagePath = await _fs.UploadAsync(s, file.Length, file.FileName); // root of share

            var appFile = new AppFile
            {
                AdminID = adminId,
                FileName = file.FileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                StorageKeyOrUrl = storagePath,
                SizeBytes = file.Length,
                UploadedAt = DateTime.UtcNow
            };

            var now = DateTime.UtcNow;
            foreach (var uid in (allowedUserIds ?? Array.Empty<int>()).Distinct())
                appFile.FileAccesses.Add(new FileShareAccess { UserID = uid, StartDate = now, EndDate = null });

            _db.Add(appFile);
            await _db.SaveChangesAsync();

            return Ok(new { appFile.FileID, appFile.FileName, appFile.StorageKeyOrUrl });
        }

        // POST /files/admin/delete/42
        [HttpPost("admin/delete/{id:int}")]
        public async Task<IActionResult> AdminDelete(int id)
        {
            var file = await _db.Set<AppFile>().FirstOrDefaultAsync(f => f.FileID == id);
            if (file is null) return NotFound();

            if (!string.IsNullOrWhiteSpace(file.StorageKeyOrUrl))
            {
                try { await _fs.DeleteAsync(file.StorageKeyOrUrl); } catch { /* ignore for now */ }
            }

            _db.Remove(file);
            await _db.SaveChangesAsync();
            return Ok(new { deleted = id });
        }
    }
}
