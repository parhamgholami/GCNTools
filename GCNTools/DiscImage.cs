﻿using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;

namespace GCNTools;

public class DiscImage : IDisposable
{
    private const int DiscHeaderSize = 0x0440;
    private const int DolOffsetInfoLocation = 0x0420;
    private const int FstOffsetInfoLocation = 0x0424;
    private const int FstSizeInfoLocation = 0x0428;
    private const int FstSizeMaxInfoLocation = 0x0430;
    private const int ApploaderOffset = 0x2440;
    private readonly FileStream _fileStream;
    private readonly MemoryStream? _memoryStream;
    private readonly FileSystemTableEntry[] _fstEntries;
    private readonly bool _streamKeptInMemory;
    private string _title;
    private bool _disposed;
    public long FileSize { get; }
    
    // Header Information
    /// <summary>The game's gamecode</summary>
    /// <remarks>
    /// The gamecode can be broken down as follows:
    /// <list type="bullet">
    ///     <item> First character is the system.</item>
    ///     <item> The next two characters are the game's ID.</item>
    ///     <item> The final character is the region code.</item>
    /// </list>
    /// </remarks>
    public GameCode GameCode { get; set; }
    public Region Region { get; set; }
    /// <summary>The ID associated with the game's publisher</summary>
    public string MakerCode { get; set; }
    public int DiscId { get; set; }
    /// <summary>The game's version number</summary>
    public int Version { get; set; }
    public byte[] MagicWord { get; set; }
    public string Title
    {
        get => _title;
        set
        {
            // 0x03e0 / 992 is the character limit for the title
            if (value.Length > 0x03e0)
            {
                value = value.Substring(0, 0x03e0);
            }

            _title = value;
        }
    }
    /// <summary>Offset of main executable DOL (boot file)</summary>
    public uint DolOffset { get; }
    /// <summary>Offset of the file system table ("fst.bin")</summary>
    public uint FstOffset { get; }
    /// <summary> Total size of file system table</summary>
    public uint FstSize { get; }
    /// <summary> Maximum size of file system table</summary>
    public uint FstSizeMax { get; }

    public DateTime ApploaderDate;

    /// <summary> All the entries in the game's file system table</summary>
    public ReadOnlyCollection<FileSystemTableEntry> FstEntries => _fstEntries.AsReadOnly();
    public Banner? Banner { get; set; }

    [Obsolete("This constructor performs a read (I/O) operation which can fail. Use DiscImage(FileStream) instead and " +
              "handle file operations before constructing the object. Later releases will remove this constructor.")]
    public DiscImage(string filePath) : this(File.OpenRead(filePath)) {}

    /// <summary>Disc Image Constructor</summary>
    /// <param name="fileStream">The source disc image's filestream</param>
    /// <param name="keepStreamInMemory">If true, the disc image's filestream is copied into memory, allowing the data to persist
    /// after the filestream was disposed.</param>
    public DiscImage(FileStream fileStream, bool keepStreamInMemory) : this(fileStream)
    {
        _streamKeptInMemory = keepStreamInMemory;
        if (!keepStreamInMemory) return;
        _memoryStream = new MemoryStream();
        fileStream.CopyTo(_memoryStream);
    }
    
    /// <summary>Disc Image Constructor</summary>
    /// <remarks>This constructor reads the disc image directly from the filestream. The filestream must remain open
    /// for the lifetime of the DiscImage unless constructed with keepStreamInMemory set as "true" (see other constructor).</remarks>
    /// <param name="fileStream">The source disc image's filestream</param>
    public DiscImage(FileStream fileStream)
    {
        _fileStream = fileStream;
        FileSize = _fileStream.Length;
        
        byte[] imageHeaderData = new byte[DiscHeaderSize];
        
        // Read disc header for information
        _fileStream.Seek(0, SeekOrigin.Begin);
        _fileStream.ReadExactly(imageHeaderData, 0, DiscHeaderSize);
        
        string codeInformation = Encoding.ASCII.GetString(imageHeaderData, 0, 4);
        GameCode = new GameCode(codeInformation);
        MakerCode = Encoding.ASCII.GetString(imageHeaderData, 0x0004, 0x0002).Replace("\0", string.Empty);
        DiscId = imageHeaderData[0x0006];
        Version = imageHeaderData[0x0007];
        MagicWord = new byte[0x0004];
        Buffer.BlockCopy(imageHeaderData, 0x001c, MagicWord, 0, MagicWord.Length);
        
        Region = GameCode.CountryCode switch
        {
            'J' => Region.NTSC_J,
            'E' => Region.NTSC_U,
            'P' => Region.PAL,
            'U' => Region.PAL,      // Apparently the European version of OoT uses this ID?
            _   => Region.UNKNOWN   // Should never happen based on previous check, but keeps Rider/VS quiet.
        };
        
        _title = Encoding.ASCII.GetString(imageHeaderData, 0x0020, 0x03e0).Replace("\0", string.Empty);
        
        // Checking sizes and offsets. Data is big-endian.
        DolOffset   = ReadBigEndianAsUInt32(imageHeaderData, DolOffsetInfoLocation);
        FstOffset   = ReadBigEndianAsUInt32(imageHeaderData, FstOffsetInfoLocation);
        FstSize     = ReadBigEndianAsUInt32(imageHeaderData, FstSizeInfoLocation);
        FstSizeMax  = ReadBigEndianAsUInt32(imageHeaderData, FstSizeMaxInfoLocation);
        
        // Build File System Table
        _fileStream.Seek(FstOffset, SeekOrigin.Begin);
        _fstEntries = ReadFileSystemTable(_fileStream);

        FileSystemTableEntry? bannerEntry = _fstEntries.FirstOrDefault(x => x.FileName == "opening.bnr");
        if (bannerEntry != null)
        {
            byte[] bannerData = new byte[bannerEntry.FileSize];
            _fileStream.Seek(bannerEntry.FileOffset, SeekOrigin.Begin);
            _fileStream.ReadExactly(bannerData, 0, (int)bannerEntry.FileSize);
            Banner = new Banner(Region, bannerData);
        }
        
        // Get Apploader Date
        _fileStream.Seek(ApploaderOffset, SeekOrigin.Begin);
        byte[] apploaderDateBuffer = new byte[0x0010];
        _fileStream.ReadExactly(apploaderDateBuffer, 0, apploaderDateBuffer.Length);
        ApploaderDate = DateTime.Parse(Encoding.ASCII.GetString(apploaderDateBuffer).Replace("\0", string.Empty));
    }
    
    /// <summary>Extracts the disc image to a directory.</summary>
    /// <param name="destinationDirectory">The destination directory</param>
    /// <param name="extractionType">Optionally, choose whether you would like to export only the system data, game files,
    /// or both</param>
    public void ExtractToDirectory(string destinationDirectory, ExtractionType extractionType = ExtractionType.ALL)
    {
        ThrowIfDisposed();
        
        Stream? stream =  _streamKeptInMemory ? _memoryStream : _fileStream;
        if(stream == null) throw new ObjectDisposedException(nameof(stream));
        stream.Seek(0, SeekOrigin.Begin);
        
        if (Directory.Exists(destinationDirectory))
        {
            Directory.Delete(destinationDirectory, true);
        }
        
        Directory.CreateDirectory(destinationDirectory);
        
        if (extractionType is ExtractionType.ALL or ExtractionType.SYSTEM_DATA_ONLY)
        {
            ExtractSystemFiles(stream, destinationDirectory);
        }
        
        if (extractionType is ExtractionType.ALL or ExtractionType.FILES_ONLY)
        {
            // Starting with root entry information (this is a recursive function)
            CreateDirectoryFromEntry(stream, destinationDirectory, _fstEntries);
        }
    }

    private void ExtractSystemFiles(Stream imageStream, string destinationDirectory)
    {
        Directory.CreateDirectory(Path.Combine(destinationDirectory, "sys"));
        // Building binary files
        byte[] bootBin = new byte[0x440];
        imageStream.Seek(0x0, SeekOrigin.Begin);
        imageStream.ReadExactly(bootBin, 0, bootBin.Length);
        File.WriteAllBytes(Path.Combine(destinationDirectory, "sys", "boot.bin"), bootBin);

        byte[] bi2Bin = new byte[0x2000];
        imageStream.Seek(0x440, SeekOrigin.Begin);
        imageStream.ReadExactly(bi2Bin, 0, bi2Bin.Length);
        File.WriteAllBytes(Path.Combine(destinationDirectory, "sys", "bi2.bin"), bi2Bin);

        byte[] appLoaderSizeInfo = new byte[4];
        byte[] appLoaderTrailerSizeInfo = new byte[4];
        
        imageStream.Seek(0x2440 + 0x0014, SeekOrigin.Begin);
        imageStream.ReadExactly(appLoaderSizeInfo, 0, appLoaderSizeInfo.Length);

        imageStream.Seek(0x2440 + 0x0018, SeekOrigin.Begin);
        imageStream.ReadExactly(appLoaderTrailerSizeInfo, 0, appLoaderTrailerSizeInfo.Length);
        
        uint appLoaderSize = ReadBigEndianAsUInt32(appLoaderSizeInfo, 0);

        uint appLoaderTrailerSize = ReadBigEndianAsUInt32(appLoaderTrailerSizeInfo, 0);
        uint padding = 24; // Date (version) of the apploader in ASCII + padding + Apploader entrypoint
        long appLoaderTotalSize = appLoaderSize + appLoaderTrailerSize + appLoaderSizeInfo.Length +
                                  appLoaderTrailerSizeInfo.Length + padding;
        byte[] apploader = new byte[appLoaderTotalSize];
        imageStream.Seek(0x2440, SeekOrigin.Begin);
        imageStream.ReadExactly(apploader, 0, apploader.Length);
        File.WriteAllBytes(Path.Combine(destinationDirectory, "sys", "apploader.img"), apploader);
        
        byte[] dolData = new byte[FstOffset - DolOffset];
        imageStream.Seek(DolOffset, SeekOrigin.Begin);
        imageStream.ReadExactly(dolData, 0, dolData.Length);
        File.WriteAllBytes(Path.Combine(destinationDirectory, "sys", "main.dol"), dolData);

        byte[] fstData = new byte[FstSize];
        imageStream.Seek(FstOffset, SeekOrigin.Begin);
        imageStream.ReadExactly(fstData, 0, fstData.Length);
        File.WriteAllBytes(Path.Combine(destinationDirectory, "sys", "fst.bin"), fstData);
    }

    public void SaveToFile(string destinationPath)
    {
        ThrowIfDisposed();
        
        const int bufferSize = 81920;
        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize);
        _fileStream.Seek(0, SeekOrigin.Begin);
        
        byte[] buffer = new byte[bufferSize];
        int bytesRead = 0;
        while ((bytesRead = _fileStream.Read(buffer, 0, bufferSize)) > 0)
        {
            fileStream.Write(buffer, 0, bytesRead);
        }
        
        // Update header information. Move to start of stream.
        fileStream.Seek(0, SeekOrigin.Begin);
        
        byte[] gameCodeBytes = Encoding.ASCII.GetBytes(GameCode.ToString());
        fileStream.Write(gameCodeBytes, 0, Math.Min(gameCodeBytes.Length, 4));
        
        fileStream.Seek(0x0006, SeekOrigin.Begin);
        fileStream.WriteByte((byte)DiscId);
        fileStream.WriteByte((byte)Version);
    
        fileStream.Seek(0x001c, SeekOrigin.Begin);
        fileStream.Write(MagicWord, 0, MagicWord.Length);
    
        byte[] titleBytes = Encoding.ASCII.GetBytes(Title);
        fileStream.Seek(0x0020, SeekOrigin.Begin);
        fileStream.Write(titleBytes, 0, Math.Min(titleBytes.Length, 0x03e0));
    
        byte[] dateBytes = Encoding.ASCII.GetBytes(ApploaderDate.ToString("yyyy/MM/dd"));
        fileStream.Seek(ApploaderOffset, SeekOrigin.Begin);
        fileStream.Write(dateBytes, 0, Math.Min(dateBytes.Length, 0x0009));
        
        FileSystemTableEntry? bannerEntry = _fstEntries.FirstOrDefault(x => x.FileName == "opening.bnr");
        if (Banner != null && bannerEntry != null)
        {
            byte[] bannerData = Banner.ToByteArray();
            fileStream.Seek(bannerEntry.FileOffset, SeekOrigin.Begin);
            fileStream.Write(bannerData, 0, bannerData.Length);
        }
        
        fileStream.Close();
    }

    private FileSystemTableEntry[] ReadFileSystemTable(Stream fstStream)
    {
        byte[] fstEntryData = new byte[FileSystemTableEntry.Size];
        
        fstStream.ReadExactly(fstEntryData, 0, fstEntryData.Length);
        FileSystemTableEntry rootEntry = ParseFstEntry(fstEntryData);
            
        uint stringTableOffset = FstOffset + (rootEntry.FileSize * FileSystemTableEntry.Size);
        rootEntry.FileName = "files";
        rootEntry.Index = 0;
            
        FileSystemTableEntry[] fstEntries = new FileSystemTableEntry[rootEntry.FileSize];
        fstEntries[0] = rootEntry;
        for (int i = 1; i < rootEntry.FileSize; i++)  // Start at 1 since we already read root
        {
            byte[] entryData = new byte[FileSystemTableEntry.Size];
            fstStream.ReadExactly(entryData, 0, FileSystemTableEntry.Size);
            FileSystemTableEntry entry = ParseFstEntry(entryData);
                
            long currentPosition = fstStream.Position;
            entry.FileName = GetFileName(fstStream, stringTableOffset, entry.FileNameOffset);
            entry.Index = i;
                
            fstStream.Seek(currentPosition, SeekOrigin.Begin);
            fstEntries[i] = entry;
        }

        return fstEntries;
    }
    
    private static FileSystemTableEntry ParseFstEntry(byte[] entryData)
    {
        FileSystemTableEntry entry = new FileSystemTableEntry
        {
            Flag = entryData[0],
            // For name offset, we need the next 3 bytes after flag in big-endian order
            FileNameOffset = ((uint)entryData[1] << 16) |
                             ((uint)entryData[2] << 8) |
                             (entryData[3]),
            // For offset, read 4 bytes in big-endian order
            FileOffset = ReadBigEndianAsUInt32(entryData, 4),
            // For size, read 4 bytes in big-endian order
            FileSize = ReadBigEndianAsUInt32(entryData, 8)
        };

        return entry;
    }
    
    private static string GetFileName(Stream imageStream, uint stringTableBase, uint fileNameOffset)
    {
        // Seek to the actual string position (base + offset)
        imageStream.Seek(stringTableBase + fileNameOffset, SeekOrigin.Begin);
    
        // Read until null terminator
        List<byte> fileNameBytes = new List<byte>();
        int currentByte;
        while ((currentByte = imageStream.ReadByte()) != 0 && currentByte != -1)
        {
            fileNameBytes.Add((byte)currentByte);
        }
    
        return Encoding.ASCII.GetString(fileNameBytes.ToArray());
    }

    private void CreateDirectoryFromEntry(Stream stream, string parentDirectory, FileSystemTableEntry[] fstEntries)
    {
        for(int i = 0; i < fstEntries.Length; i++)
        {
            if (fstEntries[i].Flag == 0)
            {
                CreateFileFromEntry(stream, parentDirectory, fstEntries[i]);
            }
            else
            {
                string fileName = fstEntries[i].FileName ?? throw new ArgumentNullException(nameof(FileSystemTableEntry.FileName));
                DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(parentDirectory, fileName));
                
                FileSystemTableEntry[] childEntries = fstEntries.Skip(i + 1).Take((int)fstEntries[i].FileSize - fstEntries[i].Index - 1).ToArray();
                CreateDirectoryFromEntry(stream, directory.FullName, childEntries);
                i += childEntries.Length;
            }
        }
    }

    private static void CreateFileFromEntry(Stream stream, string parentDirectory, FileSystemTableEntry entry)
    {
        if (entry.FileName == null) throw new ArgumentNullException(nameof(entry.FileName));
        
        const int bufferSize = 81920;
        byte[] buffer = new byte[bufferSize];
        
        using FileStream fileStream = new FileStream(Path.Combine(parentDirectory, entry.FileName), FileMode.Create, FileAccess.Write, FileShare.None, bufferSize);
        long currentPosition = stream.Position;
        stream.Seek(entry.FileOffset, SeekOrigin.Begin);
        
        int remainingBytes = (int)entry.FileSize;
        while (remainingBytes > 0)
        {
            int bytesToRead = Math.Min(remainingBytes, bufferSize);
            int bytesRead = stream.Read(buffer, 0, bytesToRead);

            if (bytesRead == 0) break;
            fileStream.Write(buffer, 0, bytesRead);
            remainingBytes -= bytesRead;
        }
        
        fileStream.Close();
        stream.Seek(currentPosition, SeekOrigin.Begin);
    }

    /// <summary>Create a disc image file from system files and a data directory.</summary>
    /// <remarks>
    /// The data directory should only include files and folders for the game. Do not keep files like
    /// boot.bin in the data directory unless you need them to be accessible within the game.
    /// </remarks>
    /// <param name="bootBinPath">The path to boot.bin</param>
    /// <param name="bi2BinPath">The path to b2.bin</param>
    /// <param name="apploaderPath">The path to apploader.img</param>
    /// <param name="dolPath">The path to the .dol file</param>
    /// <param name="dataDirectory">The path to the data directory</param>
    /// <param name="destinationPath">The path for the final disc image file.</param>
    public static void CreateFile(string bootBinPath, string bi2BinPath, string apploaderPath, string dolPath,
        string dataDirectory, string destinationPath)
    {
        // Time to start writing
        using FileStream imageStream = new FileStream(destinationPath, FileMode.OpenOrCreate, FileAccess.Write);
        byte[] bootBin = File.ReadAllBytes(bootBinPath);
        byte[] bi2Bin = File.ReadAllBytes(bi2BinPath);
        byte[] apploader = File.ReadAllBytes(apploaderPath);
        byte[] mainDol = File.ReadAllBytes(dolPath);
        
        imageStream.Write(bootBin, 0, bootBin.Length);
        imageStream.Write(bi2Bin, 0, bi2Bin.Length);
        imageStream.Write(apploader, 0, apploader.Length);
            
        uint dolOffset = ReadBigEndianAsUInt32(bootBin, DolOffsetInfoLocation);
        
        imageStream.Seek(dolOffset, SeekOrigin.Begin);
        imageStream.Write(mainDol, 0, mainDol.Length);
        
        uint fstOffset = ReadBigEndianAsUInt32(bootBin, FstOffsetInfoLocation);
        uint fstSize = ReadBigEndianAsUInt32(bootBin, FstSizeInfoLocation);
        
        imageStream.Seek(fstOffset, SeekOrigin.Begin);
        
        // Build file system table
        List<FileSystemTableEntry> fstEntries = new List<FileSystemTableEntry>();
        List<string> stringTable = new List<string>();
        uint dataOffset = fstOffset + fstSize;
        BuildFileSystemTableEntries(dataDirectory, fstEntries, stringTable, dataOffset, 0, startingFromRoot: true);

        fstEntries[0].FileName = "Game";
        
        foreach (FileSystemTableEntry entry in fstEntries)
        {
            WriteFileSystemEntryToStream(imageStream, entry);
        }
        
        foreach (string fileName in stringTable)
        {
            byte[] strBytes = Encoding.ASCII.GetBytes(fileName);
            imageStream.Write(strBytes, 0, strBytes.Length);
            imageStream.WriteByte(0); // Null terminator
        }
        
        foreach (FileSystemTableEntry entry in fstEntries.Where(e => e.Flag == 0))
        {
            if (!File.Exists(entry.FullPath)) continue;
            byte[] fileData = File.ReadAllBytes(entry.FullPath);
            imageStream.Seek(entry.FileOffset, SeekOrigin.Begin);
            imageStream.Write(fileData, 0, fileData.Length);
        }
        
        imageStream.Close();
    }
    
    /// <summary>Create a disc image file from a directory.</summary>
    /// <remarks>
    /// This method assumes the game's directory is structured similarly to how GCNToolKit and Dolphin
    /// extract an image. All the system related files are in a subdirectory called "sys" and the game data is in a
    /// subdirectory called "files".
    /// </remarks>
    /// <param name="inputDirectory">The game's directory.</param>
    /// <param name="destinationPath">The path to boot.bin</param>
    public static void CreateFile(string inputDirectory, string destinationPath)
    {
        if (!Directory.Exists(inputDirectory))
        {
            throw new Exception("Input directory not found.");
        }
        
        string systemDirectory = Path.Combine(inputDirectory, "sys");
        string dataDirectory = Path.Combine(inputDirectory, "files");

        CreateFile(
            Path.Combine(systemDirectory, "boot.bin"),
            Path.Combine(systemDirectory, "bi2.bin"),
            Path.Combine(systemDirectory, "apploader.img"),
            Path.Combine(systemDirectory, "main.dol"), 
            dataDirectory, 
            destinationPath
        );
    }

    private static void BuildFileSystemTableEntries(string filePath, List<FileSystemTableEntry> fstEntries, List<string> stringTable, uint initialOffset, uint parentDirectory, bool startingFromRoot = false)
    {
        if (File.GetAttributes(filePath).HasFlag(FileAttributes.Directory))
        {
            string[] paths = Directory.GetFileSystemEntries(filePath, "*", SearchOption.TopDirectoryOnly).OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase).ToArray();
            FileSystemTableEntry directoryEntry = new FileSystemTableEntry
            {
                FileName = Path.GetFileName(filePath),
                Flag = 1,
                FileNameOffset = startingFromRoot ? 0 : UpdateStringTable(stringTable, Path.GetFileName(filePath)),
                FileOffset = parentDirectory
            };
            
            fstEntries.Add(directoryEntry);
            
            foreach (string path in paths)
            {
                BuildFileSystemTableEntries(path, fstEntries, stringTable, initialOffset, (uint)fstEntries.IndexOf(directoryEntry));
            }
            
            directoryEntry.FileSize = (uint) fstEntries.Count;
        }
        else
        {
            fstEntries.Add(new FileSystemTableEntry
            {
                FullPath = filePath,
                FileName = Path.GetFileName(filePath),
                Flag = 0,
                FileNameOffset = UpdateStringTable(stringTable, Path.GetFileName(filePath)),
                FileSize = (uint) new FileInfo(filePath).Length,
                FileOffset = fstEntries.Any(x => x.Flag == 0) ? 
                    AlignTo32Kb(fstEntries.Last(x => x.Flag == 0).FileOffset + fstEntries.Last(x => x.Flag == 0).FileSize) :
                    AlignTo32Kb(initialOffset)
            });
        }
    }

    private static uint UpdateStringTable(List<string> stringTable, string filePath)
    {
        int offset = 0;
        foreach (var existing in stringTable)
        {
            offset += existing.Length + 1; // +1 for null terminator
        }
        stringTable.Add(filePath);
        return (uint) offset;
    }

    private static uint AlignTo32Kb(uint value)
    {
        return (value + 0x7FFF) & ~0x7FFFu;
    }
    
    private static void WriteFileSystemEntryToStream(FileStream stream, FileSystemTableEntry entry)
    {
        stream.WriteByte(entry.Flag);
        WriteUint24AsBigEndian(stream, entry.FileNameOffset);
        WriteUint32AsBigEndian(stream, entry.FileOffset);
        WriteUint32AsBigEndian(stream, entry.FileSize);
    }
    
    private static void WriteUint32AsBigEndian(Stream stream, uint value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }
    
    private static void WriteUint24AsBigEndian(Stream stream, uint value)
    {
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }
    
    private static uint ReadBigEndianAsUInt32(byte[] data, int offset) 
    {
        return ((uint)data[offset]      << 24)  |
               ((uint)data[offset + 1]  << 16)  |
               ((uint)data[offset + 2]  << 8)   |
               data[offset + 3];
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        
        if (disposing)
        {
            _fileStream?.Dispose();
            _memoryStream?.Dispose();
            Array.Clear(_fstEntries);
        }
        
        _disposed = true;
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    private void ThrowIfDisposed()
    {
        if (!_disposed && (_fileStream.CanRead || _streamKeptInMemory)) return;
        throw new ObjectDisposedException(nameof(DiscImage));
    }
}