using Microsoft.AspNetCore.Mvc;
using J_Tutors_Web_Platform.Services.Storage;

namespace J_Tutors_Web_Platform.Controllers
{
    public class TestBlobController : Controller
    {
        private readonly BlobStorageService _blobs;
        public TestBlobController(BlobStorageService blobs) => _blobs = blobs;

        // GET /TestBlob/Gallery
        [HttpGet]
        public async Task<IActionResult> Gallery()
        {
            var names = await _blobs.ListAsync();
            return View("~/Views/Test/BlobGallery.cshtml", names);

        }

        // POST /TestBlob/Upload
        [HttpPost]
        [RequestSizeLimit(104_857_600)] // 100MB demo cap
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file is null || file.Length == 0) return RedirectToAction(nameof(Gallery));
            using var s = file.OpenReadStream();
            await _blobs.UploadImageAsync(s, file.FileName, file.ContentType);
            return RedirectToAction(nameof(Gallery));
        }

        // GET /TestBlob/Image?name=foo.png   (streams the image)
        [HttpGet]
        public async Task<IActionResult> Image(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return BadRequest();
            var (stream, contentType) = await _blobs.DownloadAsync(name);
            return File(stream, contentType);
        }

        // POST /TestBlob/Delete
        [HttpPost]
        public async Task<IActionResult> Delete(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                await _blobs.DeleteAsync(name);
            return RedirectToAction(nameof(Gallery));
        }
    }
}
