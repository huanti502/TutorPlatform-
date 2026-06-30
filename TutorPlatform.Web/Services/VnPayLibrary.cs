using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace TutorPlatform.Web.Services;

// Thư viện hỗ trợ VNPay: tạo URL thanh toán và kiểm tra chữ ký phản hồi.
public class VnPayLibrary
{
    private readonly SortedList<string, string> _requestData = new(new VnPayCompare());
    private readonly SortedList<string, string> _responseData = new(new VnPayCompare());

    public void AddRequestData(string key, string? value)
    {
        if (!string.IsNullOrEmpty(value)) _requestData[key] = value;
    }

    public void AddResponseData(string key, string? value)
    {
        if (!string.IsNullOrEmpty(value)) _responseData[key] = value;
    }

    public string? GetResponseData(string key)
        => _responseData.TryGetValue(key, out var v) ? v : null;

    // Tạo URL chuyển hướng sang cổng thanh toán VNPay.
    public string CreateRequestUrl(string baseUrl, string hashSecret)
    {
        var data = new StringBuilder();
        foreach (var (key, value) in _requestData)
        {
            if (!string.IsNullOrEmpty(value))
                data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
        }

        var queryString = data.ToString();
        if (queryString.Length > 0)
            queryString = queryString.Remove(queryString.Length - 1, 1);

        var secureHash = HmacSha512(hashSecret, queryString);
        return baseUrl + "?" + queryString + "&vnp_SecureHash=" + secureHash;
    }

    // Kiểm tra chữ ký trên dữ liệu VNPay trả về.
    public bool ValidateSignature(string inputHash, string hashSecret)
    {
        var raw = GetResponseRaw();
        var myChecksum = HmacSha512(hashSecret, raw);
        return myChecksum.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
    }

    private string GetResponseRaw()
    {
        _responseData.Remove("vnp_SecureHashType");
        _responseData.Remove("vnp_SecureHash");

        var data = new StringBuilder();
        foreach (var (key, value) in _responseData)
        {
            if (!string.IsNullOrEmpty(value))
                data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
        }

        var raw = data.ToString();
        if (raw.Length > 0)
            raw = raw.Remove(raw.Length - 1, 1);
        return raw;
    }

    private static string HmacSha512(string key, string data)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        var sb = new StringBuilder();
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}

// VNPay yêu cầu sắp xếp tham số theo thứ tự ordinal.
public class VnPayCompare : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;
        return string.CompareOrdinal(x, y);
    }
}
