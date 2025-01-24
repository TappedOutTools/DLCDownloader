using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace DLCDownloader;

class Program
{
    private const string BaseUrl = "https://oct2018-4-35-0-uam5h44a.tstodlc.eamobile.com/netstorage/gameasset/direct/simpsons/";
    private const string LocalPath = "DOWNLOADS";

    static async Task Main(string[] args)
    {
        Directory.CreateDirectory(LocalPath);

        await Http.Download(Path.Combine(BaseUrl, "dlc", "DLCIndex.zip"), Path.Combine(LocalPath, "dlc", "DLCIndex.zip"));
        var dlcIndex = await Http.DownloadXmlFromZip(Path.Combine(BaseUrl, "dlc", "DLCIndex.zip"), "DLCIndex.xml");

        foreach (var indexFileEl in dlcIndex.Descendants("IndexFile"))
        {
            string? indexAttr = indexFileEl.Attribute("index")?.Value;
            if (string.IsNullOrEmpty(indexAttr))
            {
                Console.WriteLine("Skipping <IndexFile> with no 'index' attribute.");
                continue;
            }

            var indexPath = indexAttr.Split(":");
            var localPath = Path.Combine([LocalPath, ..indexPath]);

            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

            if (File.Exists(localPath))
            {
                Console.WriteLine($"{localPath} already exists.");
            }
            else
            {
                try
                {
                    await Http.Download(Path.Combine([BaseUrl, ..indexPath]), localPath);
                    Console.WriteLine("Downloaded: " + localPath);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to download: " + localPath);
                    Console.WriteLine(e);
                }
            }

            var index = await GetFirstXmlFromZip(localPath);
            await ProcessIndex(index);
        }
    }

    private static async Task ProcessIndex(XDocument index)
    {
        foreach (var package in index.Descendants("Package"))
        {
            string fileName = package.Element("FileName")?.Attribute("val")?.Value;
            string crcString = package.Element("IndexFileCRC")?.Attribute("val")?.Value;

            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(crcString))
            {
                Console.WriteLine("Skipping a package due to missing FileName or IndexFileCRC.");
                continue;
            }

            if (!uint.TryParse(crcString, out uint expectedCrc))
            {
                Console.WriteLine($"Package has invalid CRC: {crcString}");
                continue;
            }

            var filePath = fileName.Split(":");
            var localPath = Path.Combine([LocalPath, ..filePath]);
            var remotePath = Path.Combine([BaseUrl, ..filePath]);

            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

            if (!File.Exists(localPath))
            {
                await Http.Download(remotePath, localPath);
                Console.WriteLine("Downloaded: " + localPath);
            }

            await using var localStream = File.Open(localPath, FileMode.Open);
            var crc = GetIndexZipCRC(localStream);
            if (crc != expectedCrc)
            {
                throw new Exception($"{localPath} already exists and has an incorrect CRC.");
            }
        }
    }

    private static async Task<XDocument> GetFirstXmlFromZip(string zip)
    {
        await using var zipStream = File.OpenRead(zip);
        using ZipArchive zipArchive = new(zipStream, ZipArchiveMode.Read);
        var entry = zipArchive.GetEntry(zipArchive.Entries[0].Name);
        var ms = new MemoryStream();
        await using var entryStream = entry.Open();
        await entryStream.CopyToAsync(ms);
        ms.Position = 0;
        return XDocument.Load(ms);
    }

    public static uint GetIndexZipCRC(Stream zipFileStream)
    {
        using var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.GetEntry("0");
        if (entry == null)
        {
            throw new FileNotFoundException("No file named '0' found inside the zip archive.");
        }

        using var entryStream = entry.Open();
        using MemoryStream ms = new MemoryStream();
        entryStream.CopyTo(ms);
        byte[] hashBytes = System.IO.Hashing.Crc32.Hash(ms.ToArray());
        return BitConverter.ToUInt32(hashBytes, 0);
    }
}
