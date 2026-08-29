using System.Text.Json;
using UltimateSettings.Sources;

namespace UltimateSettings.Tests;

public sealed class JsonFileSourceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _jsonPath;

    public JsonFileSourceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UltimateSettingsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _jsonPath = Path.Combine(_tempDirectory, "settings.json");
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
    public void JsonFileSource_ReadsAndWritesTypedValues()
    {
        using var source = new JsonFileSource("Json", _jsonPath);

        source.Write("FontSize", 16);

        Assert.True(source.TryRead("FontSize", typeof(int), out var value));
        Assert.Equal(16, value);
    }

    [Fact]
    public void JsonFileSource_ReadsArraysAndComplexTypes()
    {
        using var source = new JsonFileSource("Json", _jsonPath);

        var methods = new[] { "AES-256", "RSA-4096" };
        source.Write("AllowedEncryptionMethods", methods);

        Assert.True(source.TryRead("AllowedEncryptionMethods", typeof(string[]), out var value));
        Assert.NotNull(value);
        Assert.Equal(methods, (string[])value);
    }

    [Fact]
    public void JsonFileSource_WriteProtected_ThrowsOnWrite()
    {
        using var source = new JsonFileSource("Json", _jsonPath, canWrite: false);

        Assert.Throws<InvalidOperationException>(() => source.Write("FontSize", 14));
    }

    [Fact]
    public void JsonFileSource_FileWatcher_FiresSourceChangedOnExternalEdit()
    {
        File.WriteAllText(_jsonPath, "{\"FontSize\": 12}");

        using var source = new JsonFileSource("Json", _jsonPath, watchForChanges: true);

        var eventRaised = new ManualResetEventSlim(false);
        source.SourceChanged += (s, e) => eventRaised.Set();

        File.WriteAllText(_jsonPath, "{\"FontSize\": 20}");

        var fired = eventRaised.Wait(TimeSpan.FromSeconds(3));
        Assert.True(fired);

        Assert.True(source.TryRead("FontSize", typeof(int), out var value));
        Assert.Equal(20, value);
    }
}
