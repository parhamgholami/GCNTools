using System.Text;

namespace GCNTools;

public struct BannerMetadata
{
    private Encoding _encoding;

    public const int Size = 0x0140;
    public Language Language { get; set; }
    public string Name { get; set; }
    public string Maker { get; set; }
    public string FullTitle { get; set; }
    public string FullMaker { get; set; }
    public string Description { get; set; }

    public BannerMetadata(Language language, byte[] data)
    {
        Language = language;
        _encoding = Utils.EncodingSelector(language);
        Name = _encoding.GetString(data, 0, 0x0020).Replace("\0", string.Empty);
        Maker = _encoding.GetString(data, 0x0020, 0x0020).Replace("\0", string.Empty);
        FullTitle = _encoding.GetString(data, 0x0040, 0x0040).Replace("\0", string.Empty);
        FullMaker = _encoding.GetString(data, 0x0080, 0x0040).Replace("\0", string.Empty);
        Description = _encoding.GetString(data, 0x00C0, data.Length < 320 ? 0x007F : 0x0080).Replace("\0", string.Empty);
    }

    public byte[] ToByteArray()
    {
        byte[] data = new byte[Size];
        _encoding.GetBytes(Name).CopyTo(data, 0);
        _encoding.GetBytes(Maker).CopyTo(data, 0x0020);
        _encoding.GetBytes(FullTitle).CopyTo(data, 0x0040);
        _encoding.GetBytes(FullMaker).CopyTo(data, 0x0080);
        _encoding.GetBytes(Description).CopyTo(data, 0x00C0);
        return data;
    }
}