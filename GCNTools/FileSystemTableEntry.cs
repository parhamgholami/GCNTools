namespace GCNTools;

public class FileSystemTableEntry
{
    public const int Size = 0x0c;
    public int Index;
    public string? FullPath { get; init; }
    public string? FileName { get; set; }
    /// <summary> Set whether entry is a file or directory.</summary>
    /// <remarks> 0 is a file, 1 is a directory</remarks>
    public byte Flag {get; init;}
    /// <summary> Offset of filename in string table.</summary>
    public uint FileNameOffset { get; init; }
    /// <summary> Offset of file in the game's data.</summary>
    public uint FileOffset { get; init; }
    public uint FileSize { get; set; }
}