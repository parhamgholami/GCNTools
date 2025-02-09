using System.Diagnostics;

namespace GCNTools.Test;

public class DiscImageTests
{
    private DiscImage? _discImage;
    private readonly string _usIsoPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../samples/swiss_ntsc-u.iso");

    [Test]
    public void OpenAmericanDiscImage()
    {
        using FileStream imageStream = new(_usIsoPath, FileMode.Open, FileAccess.Read);
        _discImage = new(imageStream);
        Assert.That(_discImage.FileSize, Is.EqualTo(1085440));
    }

    [Test]
    public void ReadAmericanDiscImage()
    {
        Assert.That(_discImage, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(_discImage.Title, Is.EqualTo("SWISS FOR THE NINTENDO GAMECUBE"));
            Assert.That(_discImage.DiscId, Is.EqualTo(0));
            Assert.That(_discImage.Version, Is.EqualTo(0));
            Assert.That(_discImage.ApploaderDate, Is.EqualTo(new DateTime(2021, 10, 06)));
            Assert.That(_discImage.GameCode.ToString(), Is.EqualTo("GSWE"));
            Assert.That(_discImage.MakerCode, Is.EqualTo("GL"));
            Assert.That(_discImage.Region, Is.EqualTo(Region.NTSC_U));
        });
    }

    [Test]
    public void ReadAmericanBanner()
    {
        Assert.That(_discImage, Is.Not.Null);
        Banner? discBanner = _discImage.Banner;
        
        Assert.Multiple(() =>
        {
            Assert.That(discBanner, Is.Not.EqualTo(null));
            Assert.That(discBanner!.Metadata.Count, Is.EqualTo(1));
        });
        
        BannerMetadata metadata = discBanner.Metadata[0];
        
        Assert.Multiple(() =>
        {
            Assert.That(discBanner.MagicWord.Last(), Is.EqualTo('1'));
            Assert.That(metadata.Name, Is.EqualTo("Swiss for GC"));
            Assert.That(metadata.Maker, Is.EqualTo("emu_kidid & Extrems"));
            Assert.That(metadata.Description, Is.EqualTo("Finally the complete tool for " +
                                                         "GameCube!\n\nhttp://www.gc-forever.com for more!"));
            Assert.That(metadata.FullTitle, Is.EqualTo("Swiss for the Nintendo GameCube"));
            Assert.That(metadata.FullMaker, Is.EqualTo("emu_kidid & Extrems"));
        });
    }
}