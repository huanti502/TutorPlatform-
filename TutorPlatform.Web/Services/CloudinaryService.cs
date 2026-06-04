using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading.Tasks;

namespace TutorPlatform.Web.Services;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration config)
    {
        var account = new Account(
            config["Cloudinary:CloudName"],
            config["Cloudinary:ApiKey"],
            config["Cloudinary:ApiSecret"]
        );
        _cloudinary = new Cloudinary(account);
    }

    /// <summary>
    /// Upload hình ảnh (Avatar, Ảnh khuôn mặt...)
    /// Cắt ảnh tự động thành hình vuông 400x400 tập trung vào khuôn mặt
    /// </summary>
    public async Task<string?> UploadImageAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0) return null;

        using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = $"tutorplatform/{folder}",
            // Tối ưu ảnh đại diện: cắt vuông, tập trung vào mặt
            Transformation = new Transformation().Width(400).Height(400).Crop("fill").Gravity("face")
        };

        var result = await _cloudinary.UploadAsync(uploadParams);
        return result.SecureUrl?.ToString();
    }

    /// <summary>
    /// Upload tài liệu hoặc chứng chỉ (PDF, JPG, PNG...)
    /// Không cắt xén, giữ nguyên bản gốc
    /// </summary>
    public async Task<string?> UploadFileAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0) return null;

        using var stream = file.OpenReadStream();
        var ext = Path.GetExtension(file.FileName).ToLower();

        if (ext == ".pdf")
        {
            var rawParams = new RawUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = $"tutorplatform/{folder}"
            };
            var result = await _cloudinary.UploadAsync(rawParams);
            return result.SecureUrl?.ToString();
        }
        else
        {
            var imgParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = $"tutorplatform/{folder}"
            };
            var result = await _cloudinary.UploadAsync(imgParams);
            return result.SecureUrl?.ToString();
        }
    }
}