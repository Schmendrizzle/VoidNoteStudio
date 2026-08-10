using System.Formats.Tar;
using System.IO.Compression;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: VoidNote.Packaging <source-directory> <output.tar.gz>");
    return 2;
}

var sourceDirectory = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);
if (!Directory.Exists(sourceDirectory))
    throw new DirectoryNotFoundException(sourceDirectory);
if (Path.GetDirectoryName(outputPath) is { } outputDirectory)
    Directory.CreateDirectory(outputDirectory);

await using (var output = File.Create(outputPath))
await using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize))
using (var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: false))
{
    foreach (var path in Directory.EnumerateFileSystemEntries(sourceDirectory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
    {
        var entryName = Path.GetRelativePath(sourceDirectory, path).Replace(Path.DirectorySeparatorChar, '/');
        var isDirectory = Directory.Exists(path);
        var entry = new PaxTarEntry(isDirectory ? TarEntryType.Directory : TarEntryType.RegularFile, entryName)
        {
            Mode = isDirectory || entryName == "VoidNote.App"
                ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                  UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                  UnixFileMode.OtherRead | UnixFileMode.OtherExecute
                : UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead,
            ModificationTime = isDirectory ? Directory.GetLastWriteTimeUtc(path) : File.GetLastWriteTimeUtc(path)
        };
        if (!isDirectory)
            entry.DataStream = File.OpenRead(path);
        await writer.WriteEntryAsync(entry);
        entry.DataStream?.Dispose();
    }
}

await using (var archive = File.OpenRead(outputPath))
await using (var gzip = new GZipStream(archive, CompressionMode.Decompress))
using (var reader = new TarReader(gzip))
{
    TarEntry? entry;
    var appHostIsExecutable = false;
    while ((entry = await reader.GetNextEntryAsync()) is not null)
    {
        if (entry.Name == "VoidNote.App")
            appHostIsExecutable = (entry.Mode & UnixFileMode.UserExecute) != 0;
    }
    if (!appHostIsExecutable)
        throw new InvalidDataException("Linux archive does not contain an executable VoidNote.App host.");
}

Console.WriteLine($"Linux portable archive created with executable app host: {outputPath}");
return 0;
