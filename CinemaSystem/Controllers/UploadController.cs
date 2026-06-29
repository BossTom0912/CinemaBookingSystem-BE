using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Common;
using Microsoft.AspNetCore.Hosting;

namespace CinemaSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Admin)]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        public UploadController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpPost("image")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<object>.Fail("Không có file nào được tải lên.", "FILE_EMPTY"));
            }

            var extension = Path.GetExtension(file.FileName).ToLower();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (Array.IndexOf(allowedExtensions, extension) == -1)
            {
                return BadRequest(ApiResponse<object>.Fail("Chỉ hỗ trợ định dạng .jpg, .jpeg, .png, .gif, .webp", "INVALID_EXTENSION"));
            }

            var newFileName = Guid.NewGuid().ToString() + extension;
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "images");
            
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var filePath = Path.Combine(uploadsFolder, newFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var fileUrl = $"/images/{newFileName}";

            return Ok(ApiResponse<string>.Ok(fileUrl, "Tải ảnh lên thành công."));
        }
    }
}
