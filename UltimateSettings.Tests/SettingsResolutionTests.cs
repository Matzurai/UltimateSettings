using UltimateSettings;
using UltimateSettings.Attributes;
using UltimateSettings.Sources;

namespace UltimateSettings.Tests;

[SourceOrder("User", "Machine")]
public sealed class SampleSettings : SettingsBase
{
    public int FontSize { get; set; } = 12;

    [SourceOrder("Machine", "GroupPolicy")]
    public string[] AllowedEncryptionMethods { get; set; } = Array.Empty<string>();

    [SourceOrder("GroupPolicy", "Machine")]
    public bool CanOverrideSettingXY { get; set; }

    [SourceOrder("GroupPolicy", "Machine")]
    [SourceOrderIf(nameof(CanOverrideSettingXY), "User", "GroupPolicy", "Machine")]
    public string SettingXY { get; set; } = string.Empty;
}

public sealed class SettingsResolutionTests
{
    [Fact]
    public void ClassLevelOrder_IsUsed_WhenNoPropertyOrderDeclared()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");
        var groupPolicy = new InMemorySettingsSource("GroupPolicy");

        user.Seed(nameof(SampleSettings.FontSize), 20);
        machine.Seed(nameof(SampleSettings.FontSize), 16);

        var manager = new SettingsManagerBuilder<SampleSettings>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .AddSource("GroupPolicy", groupPolicy)
            .WithDefaultOrder("User", "Machine", "GroupPolicy")
            .Build();

        Assert.Equal(20, manager.Current.FontSize);
    }

    [Fact]
    public void ExplicitDefaultOrder_OverridesClassLevelOrder()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");
        var groupPolicy = new InMemorySettingsSource("GroupPolicy");

        // Class-level order is "User", "Machine", but the explicit default order below puts Machine first.
        machine.Seed(nameof(SampleSettings.FontSize), 16);
        user.Seed(nameof(SampleSettings.FontSize), 20);

        var manager = new SettingsManagerBuilder<SampleSettings>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .AddSource("GroupPolicy", groupPolicy)
            .WithDefaultOrder("Machine", "User", "GroupPolicy")
            .Build();

        Assert.Equal(16, manager.Current.FontSize);
    }

    [Fact]
    public void PropertyLevelOrder_OverridesClassLevelOrder()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");
        var groupPolicy = new InMemorySettingsSource("GroupPolicy");

        user.Seed(nameof(SampleSettings.AllowedEncryptionMethods), new[] { "DES" });
        machine.Seed(nameof(SampleSettings.AllowedEncryptionMethods), new[] { "AES-256" });

        var manager = new SettingsManagerBuilder<SampleSettings>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .AddSource("GroupPolicy", groupPolicy)
            .WithDefaultOrder("User", "Machine", "GroupPolicy")
            .Build();

        Assert.Equal(new[] { "AES-256" }, manager.Current.AllowedEncryptionMethods);
    }

    [Fact]
    public void ConditionalOrder_IsIgnored_WhenConditionFalse()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");
        var groupPolicy = new InMemorySettingsSource("GroupPolicy");

        groupPolicy.Seed(nameof(SampleSettings.CanOverrideSettingXY), false);
        user.Seed(nameof(SampleSettings.SettingXY), "user-value");
        machine.Seed(nameof(SampleSettings.SettingXY), "machine-value");

        var manager = new SettingsManagerBuilder<SampleSettings>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .AddSource("GroupPolicy", groupPolicy)
            .WithDefaultOrder("User", "Machine", "GroupPolicy")
            .Build();

        Assert.Equal("machine-value", manager.Current.SettingXY);
    }

    [Fact]
    public void ConditionalOrder_TakesPrecedence_WhenConditionTrue()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");
        var groupPolicy = new InMemorySettingsSource("GroupPolicy");

        groupPolicy.Seed(nameof(SampleSettings.CanOverrideSettingXY), true);
        user.Seed(nameof(SampleSettings.SettingXY), "user-value");
        machine.Seed(nameof(SampleSettings.SettingXY), "machine-value");

        var manager = new SettingsManagerBuilder<SampleSettings>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .AddSource("GroupPolicy", groupPolicy)
            .WithDefaultOrder("User", "Machine", "GroupPolicy")
            .Build();

        Assert.Equal("user-value", manager.Current.SettingXY);
    }

    [Fact]
    public void MissingValueInAllSources_FallsBackToSchemaDefault()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");
        var groupPolicy = new InMemorySettingsSource("GroupPolicy");

        var manager = new SettingsManagerBuilder<SampleSettings>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .AddSource("GroupPolicy", groupPolicy)
            .WithDefaultOrder("User", "Machine", "GroupPolicy")
            .Build();

        Assert.Equal(12, manager.Current.FontSize);
    }

    [Fact]
    public void Save_WritesToTargetSource_AndRefreshesCurrent()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");
        var groupPolicy = new InMemorySettingsSource("GroupPolicy");

        var manager = new SettingsManagerBuilder<SampleSettings>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .AddSource("GroupPolicy", groupPolicy)
            .WithDefaultOrder("User", "Machine", "GroupPolicy")
            .Build();

        manager.Save(s => s.FontSize, 18, "Machine");

        Assert.True(machine.TryRead(nameof(SampleSettings.FontSize), out var raw));
        Assert.Equal(18, raw);
        Assert.Equal(18, manager.Current.FontSize);
    }

    [Fact]
    public void Save_ToWriteProtectedSource_Throws()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine", canWrite: false);
        var groupPolicy = new InMemorySettingsSource("GroupPolicy");

        var manager = new SettingsManagerBuilder<SampleSettings>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .AddSource("GroupPolicy", groupPolicy)
            .WithDefaultOrder("User", "Machine", "GroupPolicy")
            .Build();

        Assert.Throws<InvalidOperationException>(() => manager.Save(s => s.FontSize, 18, "Machine"));
    }

    [Fact]
    public void Save_ToUnknownSource_Throws()
    {
        var user = new InMemorySettingsSource("User");

        var manager = new SettingsManagerBuilder<SampleSettings>()
            .AddSource("User", user)
            .AddSource("Machine", new InMemorySettingsSource("Machine"))
            .AddSource("GroupPolicy", new InMemorySettingsSource("GroupPolicy"))
            .WithDefaultOrder("User", "Machine", "GroupPolicy")
            .Build();

        Assert.Throws<ArgumentException>(() => manager.Save(s => s.FontSize, 18, "DoesNotExist"));
    }

    [Fact]
    public void Build_WithMissingReferencedSource_Throws()
    {
        var builder = new SettingsManagerBuilder<SampleSettings>()
            .AddSource("User", new InMemorySettingsSource("User"))
            .AddSource("Machine", new InMemorySettingsSource("Machine"))
            .WithDefaultOrder("User", "Machine");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }
}

public sealed class CyclicSettingsA : SettingsBase
{
    [SourceOrderIf(nameof(B), "User")]
    public bool A { get; set; }

    [SourceOrderIf(nameof(A), "User")]
    public bool B { get; set; }
}

public sealed class SettingsValidationTests
{
    [Fact]
    public void Build_WithCyclicSourceOrderIf_Throws()
    {
        var builder = new SettingsManagerBuilder<CyclicSettingsA>()
            .AddSource("User", new InMemorySettingsSource("User"))
            .WithDefaultOrder("User");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }
}
