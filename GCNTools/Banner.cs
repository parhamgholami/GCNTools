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
    
    public Banner(Region region, byte[] data)
    {
        _graphicalData = new byte[0x1800];
        MagicWord = Encoding.ASCII.GetString(data, 0x0000, 0x0004).Replace("\0", string.Empty);
        Buffer.BlockCopy(data, 0x0020, GraphicalData, 0, 0x1800);

        Language language = region == Region.NTSC_J ? Language.JAPANESE : Language.ENGLISH;
        int sourceOffset = 0x1820;
        int languagePosition = (int)language;
        while (sourceOffset < data.Length)
        {
            byte[] bannerData = new byte[BannerMetadata.Size];
            Buffer.BlockCopy(data, sourceOffset, bannerData, 0, BannerMetadata.Size);

            language = MagicWord.Last() == '2' ? (Language)(languagePosition++) : language;
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