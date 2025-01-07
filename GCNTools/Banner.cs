using System.Text;

namespace GCNTools;

public class Banner
{
    private byte[] _graphicalData;
    public string MagicWord { get; set; }
    public List<BannerMetadata> Metadata { get; set; } = new();
    public byte[] GraphicalData
    {
        get => _graphicalData;
        set
        {
            if(value.Length > 0x1800) throw new ArgumentException("Graphical data is too large");
            _graphicalData = value;
        }
    }

    public Banner(Region region, string path) : this(region, File.ReadAllBytes(path))
    { }
    
    public Banner(Region region, byte[] data)
    {
        _graphicalData = new byte[0x1800];
        MagicWord = Encoding.ASCII.GetString(data, 0x0000, 0x0004).Replace("\0", string.Empty);
        Buffer.BlockCopy(data, 0x0020, GraphicalData, 0, 0x1800);

        Language language = region == Region.NTSC_J ? Language.JAPANESE : Language.ENGLISH;
        int sourceOffset = 0x1820;
        int languagePosition = 0;
        while (sourceOffset < data.Length)
        {
            byte[] bannerData = new byte[BannerMetadata.Size];
            Buffer.BlockCopy(data, sourceOffset, bannerData, 0, BannerMetadata.Size);

            language = MagicWord.Last() == '2' ? (Language)(++languagePosition) : language;
            Metadata.Add(new BannerMetadata(language, bannerData));
            sourceOffset += BannerMetadata.Size;
        }
    }

    public byte[] ToByteArray()
    {
        byte[] bannerData = new byte[0x1820 + Metadata.Count * 0x0140];
        string magicWord = MagicWord.Remove(MagicWord.Length - 1, 1) + (Metadata.Count > 1 ? "2" : "1");
       
        Encoding.ASCII.GetBytes(magicWord).CopyTo(bannerData, 0);
        _graphicalData.CopyTo(bannerData, 0x0020);

        for (int i = 0; i < Metadata.Count; i++)
        {
            byte[] metaByte = Metadata[i].ToByteArray();
            metaByte.CopyTo(bannerData, 0x1820 * (i+1));
        }

        return bannerData;
    }

    public void Save(string path)
    {
        File.WriteAllBytes(path, ToByteArray());
    }
}

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

    public BannerMetadata(Language language, string path) : this(language, File.ReadAllBytes(path))
    { }
    
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