using System.IO.Compression;
using System.Xml.Linq;

namespace DLCDownloader;

public class Http
{
    private static HttpClient _httpClient = new();

    public static async Task Download(string downloadUrl, string destFile)
    {
        await Download(downloadUrl, () =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            return File.Create(destFile);
        });
    }

    public static async Task Download(string downloadUrl, Stream destStream)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(downloadUrl);
        response.EnsureSuccessStatusCode();
        await response.Content.CopyToAsync(destStream);
    }

    public static async Task Download(string downloadUrl, Func<Stream> destStream)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(downloadUrl);
        response.EnsureSuccessStatusCode();
        var stream = destStream();
        await response.Content.CopyToAsync(stream);
        await stream.DisposeAsync();
    }

    public static async Task DownloadFromZip(string downloadUrl, string file, Stream destStream)
    {
        var zipStream = new MemoryStream();
        await Download(downloadUrl, zipStream);
        zipStream.Position = 0;
        using ZipArchive zipArchive = new(zipStream, ZipArchiveMode.Read);
        var entry = zipArchive.GetEntry(file);
        if (entry == null)
        {
            throw new FileNotFoundException($"No file named '{file}' found inside the zip archive.");
        }
        await using Stream entryStream = entry.Open();
        await entryStream.CopyToAsync(destStream);
    }

    public static async Task<XDocument> DownloadXmlFromZip(string downloadUrl, string file)
    {
        var ms = new MemoryStream();
        await DownloadFromZip(downloadUrl, file, ms);
        ms.Position = 0;
        return XDocument.Load(ms);
    }
}