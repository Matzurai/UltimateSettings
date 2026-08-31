using UltimateSettings.Sources;

namespace UltimateSettings.Tests;

public sealed class XmlFileSourceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _xmlPath;

    public XmlFileSourceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UltimateSettingsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _xmlPath = Path.Combine(_tempDirectory, "settings.xml");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Cleanup best effort.
            }
        }
    }

    [Fact]
    public void XmlFileSource_ReadsAndWritesTypedValues()
    {
        using var source = new XmlFileSource("Xml", _xmlPath);

        source.Write("FontSize", 16);

        Assert.True(source.TryRead("FontSize", typeof(int), out var value));
        Assert.Equal(16, value);
    }

    [Fact]
    public void XmlFileSource_ReadsArraysAndComplexTypes()
    {
        using var source = new XmlFileSource("Xml", _xmlPath);

        var methods = new[] { "AES-256", "RSA-4096" };
        source.Write("AllowedEncryptionMethods", methods);

        Assert.True(source.TryRead("AllowedEncryptionMethods", typeof(string[]), out var value));
        Assert.NotNull(value);
        Assert.Equal(methods, (string[])value);
    }

    [Fact]
    public void XmlFileSource_WriteProtected_ThrowsOnWrite()
    {
        using var source = new XmlFileSource("Xml", _xmlPath, canWrite: false);

        Assert.Throws<InvalidOperationException>(() => source.Write("FontSize", 14));
    }

    [Fact]
    public void XmlFileSource_FileWatcher_FiresSourceChangedOnExternalEdit()
    {
        File.WriteAllText(_xmlPath, "<Settings><FontSize>12</FontSize></Settings>");

        using var source = new XmlFileSource("Xml", _xmlPath, watchForChanges: true);

        var eventRaised = new ManualResetEventSlim(false);
        source.SourceChanged += (s, e) => eventRaised.Set();

        File.WriteAllText(_xmlPath, "<Settings><FontSize>20</FontSize></Settings>");

        var fired = eventRaised.Wait(TimeSpan.FromSeconds(3));
        Assert.True(fired);

        Assert.True(source.TryRead("FontSize", typeof(int), out var value));
        Assert.Equal(20, value);
    }

    [Fact]
    public void XmlFileSource_PersistsAcrossReload()
    {
        using (var source = new XmlFileSource("Xml", _xmlPath))
        {
            source.Write("FontSize", 18);
        }

        using var reloaded = new XmlFileSource("Xml", _xmlPath);
        Assert.True(reloaded.TryRead("FontSize", typeof(int), out var value));
        Assert.Equal(18, value);
    }

    [Fact]
    public void XmlFileSource_MissingKey_ReturnsFalse()
    {
        using var source = new XmlFileSource("Xml", _xmlPath);

        Assert.False(source.TryRead("DoesNotExist", typeof(int), out var value));
        Assert.Null(value);
    }
}
