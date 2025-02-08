using System.Text.RegularExpressions;

namespace GCNTools;

public partial struct GameCode
{
    [GeneratedRegex(@"^[GDU][A-Z0-9]{2}[JEPU]$", RegexOptions.None)]
    private static partial Regex FullCodePattern();
    
    [GeneratedRegex(@"[GDU]", RegexOptions.None)]
    private static partial Regex ConsoleIdPattern();
    
    [GeneratedRegex(@"[A-Z0-9]{2}", RegexOptions.None)]
    private static partial Regex GameCodePattern();
    
    [GeneratedRegex(@"[JEPU]", RegexOptions.None)]
    private static partial Regex CountryPattern();
    
    public char ConsoleId;
    public string Code;
    public char CountryCode;

    public GameCode(string code) : this(code[0], code.Substring(1, 2), code[3])
    {
        if (!FullCodePattern().IsMatch(code))
        {
            throw new FormatException("Invalid game code format!");
        }
    }

    public GameCode(char consoleId, string gameCode, Region region) : this (consoleId, gameCode)
    {
        CountryCode = region switch
        {
            Region.NTSC_J   => 'J',
            Region.NTSC_U   => 'E',
            Region.PAL      => 'P',
            _               => throw new ArgumentOutOfRangeException(nameof(region), region, null)
        };
    }
    
    public GameCode(char consoleId, string gameCode, char countryCode) : this (consoleId, gameCode)
    {
        if (!CountryPattern().IsMatch(countryCode.ToString()))
        {
            throw new FormatException("Invalid country code!");
        }
        CountryCode = countryCode;
    }

    private GameCode(char consoleId, string gameCode)
    {
        if (ConsoleIdPattern().IsMatch(consoleId.ToString()))
        {
            throw new FormatException("Invalid console ID!");
        }

        if (!GameCodePattern().IsMatch(gameCode))
        {
            throw new FormatException("Invalid game code!");
        }
        
        ConsoleId = consoleId;
        Code = gameCode;
    }
    
    public override string ToString()
    {
        return $"{ConsoleId}{Code}{CountryCode}";
    }
}