#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using J_Tutors_Web_Platform.Services.Storage;
using J_Tutors_Web_Platform.Data;

// Adjust this to your actual Event namespace/type
using J_Tutors_Web_Platform.Models.Events; // assumes class Event { int EventID; string? ImageURL; }

namespace J_Tutors_Web_Platform.Controllers
{
    /// <summary>
    /// Blob-backed images for Events.
    /// - GET  /events/{eventId}/image            -> streams current image
    /// - POST /events/{eventId}/image            -> upload/replace image for event
    /// - POST /events/{eventId}/image/delete     -> delete image
    /// </summary>
    [Route("events/{eventId:int}/image")]
    public class EventImagesController : Controller
    {
        private readonly BlobStorageService _blobs;
        private readonly AppDbContext _db;

        public EventImagesController(BlobStorageService blobs, AppDbContext db)
        {
            _blobs = blobs;
            _db = db;
        }

        // GET /events/{eventId}/image
        [HttpGet]
        public async Task<IActionResult> Get(int eventId)
        {
            var ev = await _db.Set<Event>().FirstOrDefaultAsync(e => e.EventID == eventId);
            if (ev is null || string.IsNullOrWhiteSpace(ev.ImageURL))
                return NotFound(); // no image

            var (stream, contentType) = await _blobs.DownloadAsync(ev.ImageURL);
            return File(stream, contentType);
        }

        // POST /events/{eventId}/image
        // form-data: file=(image)
        [HttpPost]
        [RequestSizeLimit(20_000_000)] // 20MB demo cap; raise if needed
        public async Task<IActionResult> Upload(int eventId, IFormFile? file)
        {
            if (file is null || file.Length == 0) return BadRequest("No image uploaded.");

            var ev = await _db.Set<Event>().FirstOrDefaultAsync(e => e.EventID == eventId);
            if (ev is null) return NotFound("Event not found.");

            // If replacing, delete previous blob (optional)
            if (!string.IsNullOrWhiteSpace(ev.ImageURL))
            {
                try { await _blobs.DeleteAsync(ev.ImageURL); } catch { /* ignore */ }
            }

            using var s = file.OpenReadStream();
            var blobName = await _blobs.UploadImageAsync(s, file.FileName, file.ContentType);

            // Save blob pointer on the event (adjust property name if different)
            ev.ImageURL = blobName;
            await _db.SaveChangesAsync();

            // Redirect to wherever you show the event
            return RedirectToAction("Details", "Home", new { id = eventId });
        }

        // POST /events/{eventId}/image/delete
        [HttpPost("delete")]
        public async Task<IActionResult> Delete(int eventId)
        {
            var ev = await _db.Set<Event>().FirstOrDefaultAsync(e => e.EventID == eventId);
            if (ev is null) return NotFound("Event not found.");

            if (!string.IsNullOrWhiteSpace(ev.ImageURL))
            {
                try { await _blobs.DeleteAsync(ev.ImageURL); } catch { /* ignore */ }
                ev.ImageURL = null;
                await _db.SaveChangesAsync();
            }

            return RedirectToAction("Details", "Home", new { id = eventId });
        }
    }
}
