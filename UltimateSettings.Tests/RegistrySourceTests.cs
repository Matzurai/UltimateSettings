using UltimateSettings.Sources;

namespace UltimateSettings.Tests;

public sealed class RegistrySourceTests
{
    private const string KeyPath = @"HKEY_CURRENT_USER\Software\UltimateSettingsTests";

    [Fact]
    public void RegistrySource_ReadsAndWritesTypedValues()
    {
        var source = new RegistrySource("Registry", KeyPath, backend: new InMemoryRegistryBackend());

        source.Write("FontSize", 16);

        Assert.True(source.TryRead("FontSize", typeof(int), out var value));
        Assert.Equal(16, value);
    }

    [Fact]
    public void RegistrySource_ReadsArrays()
    {
        var source = new RegistrySource("Registry", KeyPath, backend: new InMemoryRegistryBackend());

        var methods = new[] { "AES-256", "RSA-4096" };
        source.Write("AllowedEncryptionMethods", methods);

        Assert.True(source.TryRead("AllowedEncryptionMethods", typeof(string[]), out var value));
        Assert.Equal(methods, (string[])value!);
    }

    [Fact]
    public void RegistrySource_RoundTripsBooleans()
    {
        var source = new RegistrySource("Registry", KeyPath, backend: new InMemoryRegistryBackend());

        source.Write("CanOverride", true);

        Assert.True(source.TryRead("CanOverride", typeof(bool), out var value));
        Assert.Equal(true, value);
    }

    [Fact]
    public void RegistrySource_RoundTripsEnums()
    {
        var source = new RegistrySource("Registry", KeyPath, backend: new InMemoryRegistryBackend());

        source.Write("Level", LogLevel.Warning);

        Assert.True(source.TryRead("Level", typeof(LogLevel), out var value));
        Assert.Equal(LogLevel.Warning, value);
    }

    [Fact]
    public void RegistrySource_FallsBackToJsonForComplexLeafValues()
    {
        var source = new RegistrySource("Registry", KeyPath, backend: new InMemoryRegistryBackend());

        source.Write("Point", new PointSettings { X = 1, Y = 2 });

        Assert.True(source.TryRead("Point", typeof(PointSettings), out var value));
        var point = Assert.IsType<PointSettings>(value);
        Assert.Equal(1, point.X);
        Assert.Equal(2, point.Y);
    }

    [Fact]
    public void RegistrySource_WriteProtected_ThrowsOnWrite()
    {
        var source = new RegistrySource("Registry", KeyPath, canWrite: false, backend: new InMemoryRegistryBackend());

        Assert.Throws<InvalidOperationException>(() => source.Write("FontSize", 14));
    }

    [Fact]
    public void RegistrySource_MissingKey_ReturnsFalse()
    {
        var source = new RegistrySource("Registry", KeyPath, backend: new InMemoryRegistryBackend());

        Assert.False(source.TryRead("DoesNotExist", typeof(int), out var value));
        Assert.Null(value);
    }

    [Fact]
    public void RegistrySource_NestedSettingsObjects_AreStoredAsSubkeysAndResolvedCorrectly()
    {
        var user = new RegistrySource("User", KeyPath + @"\User", backend: new InMemoryRegistryBackend());
        var machine = new RegistrySource("Machine", KeyPath + @"\Machine", backend: new InMemoryRegistryBackend());

        user.Write(nameof(HierarchicalSettingsA.AValue), "user-a");
        user.Write(nameof(HierarchicalSettingsA.B), new HierarchicalSettingsB { BValue = "user-b" });
        machine.Write(nameof(HierarchicalSettingsA.AValue), "machine-a");
        machine.Write(nameof(HierarchicalSettingsA.B), new HierarchicalSettingsB { BValue = "machine-b" });

        var manager = new SettingsManagerBuilder<HierarchicalSettingsA>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .WithDefaultOrder("User", "Machine")
            .Build();

        Assert.Equal("user-a", manager.Current.AValue);
        Assert.NotNull(manager.Current.B);
        Assert.Equal("machine-b", manager.Current.B.BValue);
    }
}

public enum LogLevel
{
    Info,
    Warning,
    Error
}

public sealed class PointSettings
{
    public int X { get; set; }

    public int Y { get; set; }
}
