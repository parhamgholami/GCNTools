namespace GCNTools;

// Must maintain this order because banners are packed in this order for PAL releases (excluding Japanese)
public enum Language
{
    JAPANESE,
    ENGLISH,
    GERMAN,
    FRENCH,
    SPANISH,
    ITALIAN,
    DUTCH
}

public enum Region
{
    UNKNOWN,
    NTSC_J,
    NTSC_U,
    PAL
}

public enum ExtractionType
{
    ALL,
    SYSTEM_DATA_ONLY,
    FILES_ONLY
}