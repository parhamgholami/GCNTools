namespace GCNTools;

public struct GameCode
{
    public char ConsoleId;
    public string Code;
    public char CountryCode;

    public GameCode(string code) : this(code[0], code.Substring(1, 2), code[3]) { }

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
        CountryCode = countryCode;
    }

    private GameCode(char consoleId, string gameCode)
    {
        ConsoleId = consoleId;
        Code = gameCode;
    }
    
    public override string ToString()
    {
        return $"{ConsoleId}{Code}{CountryCode}";
    }
}