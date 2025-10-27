using Microsoft.Extensions.Configuration;
using System.Text;

namespace SecureHumanLoopCaptcha.Worker.Services;

public class SnapshotStorage
{
    private readonly string _rootPath;

    public SnapshotStorage(IConfiguration configuration)
    {
        _rootPath = configuration["Storage:Path"] ?? "/data";
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveScreenshotAsync(byte[] content, string prefix, CancellationToken cancellationToken)
    {
        var fileName = $"{prefix}_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.png";
        var path = Path.Combine(_rootPath, fileName);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        return path;
    }

    public async Task<string> SaveHtmlAsync(string html, string prefix, CancellationToken cancellationToken)
    {
        var fileName = $"{prefix}_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.html";
        var path = Path.Combine(_rootPath, fileName);
        await File.WriteAllTextAsync(path, html, Encoding.UTF8, cancellationToken);
        return path;
    }
}
