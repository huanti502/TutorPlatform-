using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Linq;
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

        // Chỉ các định dạng ảnh mới upload kiểu image; còn lại (pdf, doc, docx, ppt, pptx, xls, xlsx, txt...)
        // upload kiểu raw để Cloudinary giữ nguyên file gốc và trả về URL tải được.
        var imageExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

        if (imageExts.Contains(ext))
        {
            var imgParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = $"tutorplatform/{folder}"
            };
            var result = await _cloudinary.UploadAsync(imgParams);
            return result.SecureUrl?.ToString();
        }
        else
        {
            var rawParams = new RawUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = $"tutorplatform/{folder}"
            };
            var result = await _cloudinary.UploadAsync(rawParams);
            return result.SecureUrl?.ToString();
        }
    }

    /// <summary>
    /// Tạo URL tải file CÓ CHỮ KÝ (signed) từ URL Cloudinary đã lưu trong DB.
    ///
    /// Lý do: Tài khoản Cloudinary đời mới mặc định bật "Restricted media types",
    /// chặn phân phối công khai file PDF / raw → truy cập URL gốc bị HTTP 401.
    /// File loại bị hạn chế chỉ được phân phối khi URL có chữ ký hợp lệ
    /// (đoạn "s--xxxxx--"). Hàm này phân tích URL gốc rồi dựng lại URL có ký.
    ///
    /// Trả về chính URL gốc nếu không phải file Cloudinary hoặc nếu không cần ký
    /// (ví dụ ảnh image/upload vẫn tải bình thường).
    /// </summary>
    public string GetSignedDeliveryUrl(string storedUrl)
    {
        if (string.IsNullOrWhiteSpace(storedUrl)) return storedUrl;
        if (!storedUrl.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
            return storedUrl;

        try
        {
            var uri = new Uri(storedUrl);
            // Bỏ "/" đầu/cuối rồi tách: [cloud, resourceType, type, (version?), ...publicId]
            var segs = uri.AbsolutePath.Trim('/').Split('/');
            if (segs.Length < 4) return storedUrl;

            var resourceType = segs[1];   // raw | image | video
            var type = segs[2];           // upload | authenticated | private...

            int idx = 3;
            string? version = null;
            // Đoạn version có dạng "v1780665271"
            if (segs[idx].Length > 1 && segs[idx][0] == 'v'
                && segs[idx].Skip(1).All(char.IsDigit))
            {
                version = segs[idx].Substring(1);
                idx++;
            }

            var publicId = string.Join('/', segs.Skip(idx));
            publicId = Uri.UnescapeDataString(publicId);

            // raw: public_id GIỮ phần mở rộng (.pdf...).
            // image/video: public_id BỎ phần mở rộng.
            if (!resourceType.Equals("raw", StringComparison.OrdinalIgnoreCase))
            {
                var dot = publicId.LastIndexOf('.');
                if (dot > 0) publicId = publicId.Substring(0, dot);
            }

            var builder = _cloudinary.Api.Url
                .Secure(true)
                .ResourceType(resourceType)
                .Action(type)
                .Signed(true);

            if (version != null)
                builder = builder.Version(version);

            return builder.BuildUrl(publicId);
        }
        catch
        {
            // Nếu phân tích lỗi thì dùng URL gốc để không chặn người dùng.
            return storedUrl;
        }
    }
}