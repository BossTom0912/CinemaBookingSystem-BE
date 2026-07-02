using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using CinemaSystem.Contracts.Common;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Controllers
{
    /// <summary>
    /// Điểm vào HTTP để Manager/Admin tải ảnh lên thư mục static của API.
    /// </summary>
    /// <remarks>
    /// Controller xử lý trực tiếp vì đây là adapter file đơn giản: kiểm tra
    /// extension/kích thước rồi ghi vào <c>CinemaSystem/wwwroot/images</c>.
    /// URL trả về được MovieController/MovieService lưu vào trường posterUrl.
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Admin)]
    public class UploadController : ControllerBase
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly FileStorageSettings _settings;

        public UploadController(
            IFileStorageService fileStorageService,
            IOptions<FileStorageSettings> options)
        {
            _fileStorageService = fileStorageService;
            _settings = options.Value;
        }

        [HttpPost("image")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<object>.Fail("Không có file nào được tải lên.", "FILE_EMPTY"));
            }

            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!_settings.AllowedImageExtensions.Contains(
                    extension,
                    StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse<object>.Fail(
                    $"Chỉ hỗ trợ định dạng {string.Join(", ", _settings.AllowedImageExtensions)}",
                    "INVALID_EXTENSION"));
            }

            await using var stream = file.OpenReadStream();
            var fileUrl = await _fileStorageService.SaveFileAsync(
                stream,
                file.FileName,
                _settings.GeneralImageFolder,
                HttpContext.RequestAborted);

            return Ok(ApiResponse<string>.Ok(fileUrl, "Tải ảnh lên thành công."));
        }
    }
}
