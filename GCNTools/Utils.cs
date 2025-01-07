using System.Text;

namespace GCNTools;

public static class Utils
{
    public static Encoding EncodingSelector(Language language)
    {
        if (language != Language.JAPANESE)
        {
            return Encoding.ASCII;
        }
        
        // Check if encoding already exists. If it doesn't, load it.
        if (Encoding.GetEncodings().All(e => e.CodePage != 932))
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        
        return Encoding.GetEncoding(932);
    }
}