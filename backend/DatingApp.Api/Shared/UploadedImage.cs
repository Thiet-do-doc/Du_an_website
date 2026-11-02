using ImageMagick;

namespace DatingApp.Api.Shared;

public sealed record UploadedImage(byte[] Content, string ContentType)
{
    public const int MaxBytes = 5 * 1024 * 1024;

    static UploadedImage()
    {
        ResourceLimits.Width = 8000;
        ResourceLimits.Height = 8000;
        ResourceLimits.Memory = 256 * 1024 * 1024;
        ResourceLimits.Disk = 512 * 1024 * 1024;
    }

    public static async Task<UploadedImage> ReadAsync(Stream stream, string? contentType, CancellationToken ct)
    {
        using var input = new MemoryStream();
        await stream.CopyToAsync(input, ct);
        if (input.Length is 0 or > MaxBytes) throw new PhotoValidationException("Ảnh phải có dung lượng từ 1 byte đến 5 MB.");
        input.Position = 0;
        var info = new MagickImageInfo(input);
        var identifiedFormat = info.Format switch { MagickFormat.Jpeg => "image/jpeg", MagickFormat.Png => "image/png", MagickFormat.WebP => "image/webp", _ => null };
        if (identifiedFormat is null || identifiedFormat != contentType)
            throw new PhotoValidationException("Chỉ nhận ảnh JPEG, PNG hoặc WebP đúng định dạng.");
        if (info.Width > 8000 || info.Height > 8000 || (long)info.Width * info.Height > 40_000_000)
            throw new PhotoValidationException("Kích thước ảnh vượt giới hạn cho phép.");
        input.Position = 0;
        using var image = new MagickImage(input);
        if (image.Width > 8000 || image.Height > 8000 || (long)image.Width * image.Height > 40_000_000)
            throw new PhotoValidationException("Kích thước ảnh vượt giới hạn cho phép.");
        var format = image.Format switch { MagickFormat.Jpeg => "image/jpeg", MagickFormat.Png => "image/png", MagickFormat.WebP => "image/webp", _ => null };
        if (format is null || contentType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new PhotoValidationException("Chỉ nhận ảnh JPEG, PNG hoặc WebP hợp lệ.");
        if (format != contentType) throw new PhotoValidationException("Định dạng nội dung ảnh không khớp.");
        image.AutoOrient();
        image.Strip();
        using var output = new MemoryStream();
        var outputFormat = image.Format;
        image.Quality = 88;
        image.Write(output, outputFormat);
        if (output.Length > MaxBytes) throw new PhotoValidationException("Ảnh sau xử lý vượt quá 5 MB.");
        return new UploadedImage(output.ToArray(), format);
    }
}

public sealed class PhotoValidationException(string message) : Exception(message);
