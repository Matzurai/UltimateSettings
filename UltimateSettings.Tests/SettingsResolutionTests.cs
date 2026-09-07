using UltimateSettings;
using UltimateSettings.Attributes;
using UltimateSettings.Sources;
using System.Diagnostics;

namespace UltimateSettings.Tests;

[SourceOrder("User", "Machine")]
public sealed class SampleSettings : SettingsBase
{
    public int FontSize { get; init; } = 12;

    [SourceOrder("Machine", "GroupPolicy")]
    public string[] AllowedEncryptionMethods { get; init; } = Array.Empty<string>();

    [SourceOrder("GroupPolicy", "Machine")]
    public bool CanOverrideSettingXY { get; init; }

    [SourceOrder("GroupPolicy", "Machine")]
    [SourceOrderIf(nameof(CanOverrideSettingXY), "User", "GroupPolicy", "Machine")]
    public string SettingXY { get; init; } = string.Empty;
}


[SourceOrder("User", "Machine")]
public sealed class HierarchicalSettingsA : SettingsBase
{
    public HierarchicalSettingsB? B { get; init; }
    public string? AValue { get; init; }
}

public sealed class HierarchicalSettingsB : SettingsBase
{
    [SourceOrder("Machine", "User")]
    public string? BValue { get; init; }
}

public sealed class NestedClassOrderA : SettingsBase
{
    public NestedClassOrderB? B { get; init; }
}

[SourceOrder("Machine", "User")]
public sealed class NestedClassOrderB : SettingsBase
{
    public string? Value { get; init; }
}

public sealed class ContainingPropertyOrderA : SettingsBase
{
    [SourceOrder("Machine", "User")]
    public ContainingPropertyOrderB? B { get; init; }
}

public sealed class ContainingPropertyOrderB : SettingsBase
{
    public string? Value { get; init; }
}

public sealed class LeafPropertyOrderA : SettingsBase
{
    [SourceOrder("Machine", "User")]
    public LeafPropertyOrderB? B { get; init; }
}

public sealed class LeafPropertyOrderB : SettingsBase
{
    [SourceOrder("User", "Machine")]
    public string? Value { get; init; }
}

public sealed class NestedConditionalA : SettingsBase
{
    public NestedConditionalB? B { get; init; }
}

public sealed class NestedConditionalB : SettingsBase
{
    public bool CanOverride { get; init; }

    [SourceOrder("Machine", "User")]
    [SourceOrderIf(nameof(CanOverride), "User", "Machine")]
    public string? Value { get; init; }
}

public sealed class DeepNestingA : SettingsBase
{
    public DeepNestingB? B { get; init; }
}

public sealed class DeepNestingB : SettingsBase
{
    public DeepNestingC? C { get; init; }
}

[SourceOrder("Machine", "User")]
public sealed class DeepNestingC : SettingsBase
{
    public string? Value { get; init; }
}

public sealed class ArrayOfNestedSettingsA : SettingsBase
{
    public HierarchicalSettingsB[]? Items { get; init; }
}

public sealed class SuppressedArrayOfNestedSettingsA : SettingsBase
{
    [SuppressArrayMergeWarning]
    public HierarchicalSettingsB[]? Items { get; init; }
}

/// <summary>Captures Trace warnings raised during a test so they can be asserted without polluting global listeners.</summary>
internal sealed class CapturingTraceListener : TraceListener
{
    public List<string> Messages { get; } = new();

    public override void Write(string? message) { }

    public override void WriteLine(string? message)
    {
        if (message is not null)
        {
            Messages.Add(message);
        }
    }
}

public sealed class NestedMissingSourceA : SettingsBase
{
    public NestedMissingSourceB? B { get; init; }
}

[SourceOrder("DoesNotExist", "User")]
public sealed class NestedMissingSourceB : SettingsBase
{
    public string? Value { get; init; }
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
    public void Edit_WritesToTargetSource_AndRefreshesCurrent()
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

        manager.Edit("Machine", edit => edit.Set(s => s.FontSize, 18));

        Assert.True(machine.TryRead(nameof(SampleSettings.FontSize), out var raw));
        Assert.Equal(18, raw);
        Assert.Equal(18, manager.Current.FontSize);
    }

    [Fact]
    public void Edit_ToWriteProtectedSource_Throws()
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

        Assert.Throws<InvalidOperationException>(() => manager.Edit("Machine", edit => edit.Set(s => s.FontSize, 18)));
    }

    [Fact]
    public void Edit_ToUnknownSource_Throws()
    {
        var user = new InMemorySettingsSource("User");

        var manager = new SettingsManagerBuilder<SampleSettings>()
            .AddSource("User", user)
            .AddSource("Machine", new InMemorySettingsSource("Machine"))
            .AddSource("GroupPolicy", new InMemorySettingsSource("GroupPolicy"))
            .WithDefaultOrder("User", "Machine", "GroupPolicy")
            .Build();

        Assert.Throws<ArgumentException>(() => manager.Edit("DoesNotExist", edit => edit.Set(s => s.FontSize, 18)));
    }

    [Fact]
    public void Edit_AppliesMultipleChanges_AndRefreshesCurrentOnce()
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
        var changeCount = 0;
        manager.SettingsChanged += (_, _) => changeCount++;

        manager.Edit("Machine", edit =>
        {
            edit.Set(s => s.FontSize, 18);
            edit.Set(s => s.SettingXY, "AES-256");
        });

        Assert.Equal(18, manager.Current.FontSize);
        Assert.Equal("AES-256", manager.Current.SettingXY);
        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void Edit_WhenCallbackThrows_DoesNotWriteChanges()
    {
        var user = new InMemorySettingsSource("User");
        var manager = new SettingsManagerBuilder<SampleSettings>()
            .AddSource("User", user)
            .AddSource("Machine", new InMemorySettingsSource("Machine"))
            .AddSource("GroupPolicy", new InMemorySettingsSource("GroupPolicy"))
            .WithDefaultOrder("User", "Machine", "GroupPolicy")
            .Build();

        Assert.Throws<InvalidOperationException>(() => manager.Edit("User", edit =>
        {
            edit.Set(s => s.FontSize, 18);
            throw new InvalidOperationException("edit failed");
        }));

        Assert.False(user.TryRead(nameof(SampleSettings.FontSize), out _));
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

    [Fact]
    public void HierarchicalSettings_AreResolvedCorrectly()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");

        user.Seed(nameof(HierarchicalSettingsA.AValue), "user-a");
        user.Seed(nameof(HierarchicalSettingsA.B), new HierarchicalSettingsB { BValue = "user-b" });
        machine.Seed(nameof(HierarchicalSettingsA.AValue), "machine-a");
        machine.Seed(nameof(HierarchicalSettingsA.B), new HierarchicalSettingsB { BValue = "machine-b" });

        var manager = new SettingsManagerBuilder<HierarchicalSettingsA>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .WithDefaultOrder("User", "Machine")
            .Build();

        Assert.Equal("user-a", manager.Current.AValue);
        Assert.NotNull(manager.Current.B);
        Assert.Equal("machine-b", manager.Current.B.BValue);
    }

    [Fact]
    public void HierarchicalSettings_AreResolvedCorrectly_WithXmlFileSource()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "UltimateSettingsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            using var user = new XmlFileSource("User", Path.Combine(tempDirectory, "user.xml"));
            using var machine = new XmlFileSource("Machine", Path.Combine(tempDirectory, "machine.xml"));

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
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void NestedClassOrder_IsUsed_WhenNoPropertyOrderOrDefaultOrder()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");

        user.Seed(nameof(NestedClassOrderA.B), new NestedClassOrderB { Value = "user-value" });
        machine.Seed(nameof(NestedClassOrderA.B), new NestedClassOrderB { Value = "machine-value" });

        var manager = new SettingsManagerBuilder<NestedClassOrderA>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .Build();

        Assert.Equal("machine-value", manager.Current.B?.Value);
    }

    [Fact]
    public void ContainingPropertyOrder_OverridesDefaultOrder_ForNestedField()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");

        user.Seed(nameof(ContainingPropertyOrderA.B), new ContainingPropertyOrderB { Value = "user-value" });
        machine.Seed(nameof(ContainingPropertyOrderA.B), new ContainingPropertyOrderB { Value = "machine-value" });

        var manager = new SettingsManagerBuilder<ContainingPropertyOrderA>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .WithDefaultOrder("User", "Machine")
            .Build();

        Assert.Equal("machine-value", manager.Current.B?.Value);
    }

    [Fact]
    public void LeafPropertyOrder_OverridesContainingPropertyOrder_ForNestedField()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");

        user.Seed(nameof(LeafPropertyOrderA.B), new LeafPropertyOrderB { Value = "user-value" });
        machine.Seed(nameof(LeafPropertyOrderA.B), new LeafPropertyOrderB { Value = "machine-value" });

        var manager = new SettingsManagerBuilder<LeafPropertyOrderA>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .Build();

        // B carries a Machine-first order, but Value's own order (User-first) wins for the leaf field.
        Assert.Equal("user-value", manager.Current.B?.Value);
    }

    [Fact]
    public void NestedConditionalOrder_UsesSiblingConditionWithinNestedType()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");

        user.Seed(nameof(NestedConditionalA.B), new NestedConditionalB { CanOverride = true, Value = "user-value" });
        machine.Seed(nameof(NestedConditionalA.B), new NestedConditionalB { CanOverride = false, Value = "machine-value" });

        var manager = new SettingsManagerBuilder<NestedConditionalA>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .WithDefaultOrder("User", "Machine")
            .Build();

        // CanOverride resolves to true from User (first in default order), so the SourceOrderIf override applies.
        Assert.Equal("user-value", manager.Current.B?.Value);
    }

    [Fact]
    public void DeeplyNestedSettings_AreResolvedAcrossMultipleLevels()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");

        user.Seed(nameof(DeepNestingA.B), new DeepNestingB { C = new DeepNestingC { Value = "user-value" } });
        machine.Seed(nameof(DeepNestingA.B), new DeepNestingB { C = new DeepNestingC { Value = "machine-value" } });

        var manager = new SettingsManagerBuilder<DeepNestingA>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .Build();

        Assert.NotNull(manager.Current.B);
        Assert.NotNull(manager.Current.B.C);
        Assert.Equal("machine-value", manager.Current.B.C.Value);
    }

    [Fact]
    public void ArrayOfNestedSettingsObjects_IsReplacedWholesale_NotMerged()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");

        var userItems = new[] { new HierarchicalSettingsB { BValue = "u1" }, new HierarchicalSettingsB { BValue = "u2" }, new HierarchicalSettingsB { BValue = "u3" } };
        var machineItems = new[] { new HierarchicalSettingsB { BValue = "m1" } };

        user.Seed(nameof(ArrayOfNestedSettingsA.Items), userItems);
        machine.Seed(nameof(ArrayOfNestedSettingsA.Items), machineItems);

        var manager = new SettingsManagerBuilder<ArrayOfNestedSettingsA>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .WithDefaultOrder("Machine", "User")
            .Build();

        // Arrays cannot be element-wise merged, so the whole array from the winning source is used as-is.
        Assert.Same(machineItems, manager.Current.Items);
    }

    [Fact]
    public void Build_WithCollectionOfNestedSettings_EmitsArrayMergeWarning()
    {
        var listener = new CapturingTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            new SettingsManagerBuilder<ArrayOfNestedSettingsA>()
                .AddSource("User", new InMemorySettingsSource("User"))
                .Build();
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        Assert.Contains(listener.Messages, m => m.Contains("Items") && m.Contains(nameof(HierarchicalSettingsB)));
    }

    [Fact]
    public void Build_WithSuppressArrayMergeWarningAttribute_NoWarningEmitted()
    {
        var listener = new CapturingTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            new SettingsManagerBuilder<SuppressedArrayOfNestedSettingsA>()
                .AddSource("User", new InMemorySettingsSource("User"))
                .Build();
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        Assert.Empty(listener.Messages);
    }

    [Fact]
    public void NestedField_FallsBackToNextSource_WhenMissingInHigherPrecedenceSource()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");

        machine.Seed(nameof(HierarchicalSettingsA.B), new HierarchicalSettingsB { BValue = "machine-b" });

        var manager = new SettingsManagerBuilder<HierarchicalSettingsA>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .WithDefaultOrder("User", "Machine")
            .Build();

        Assert.NotNull(manager.Current.B);
        Assert.Equal("machine-b", manager.Current.B.BValue);
    }

    [Fact]
    public void NestedField_FallsBackToSchemaDefault_WhenMissingInAllSources()
    {
        var user = new InMemorySettingsSource("User");
        var machine = new InMemorySettingsSource("Machine");

        var manager = new SettingsManagerBuilder<HierarchicalSettingsA>()
            .AddSource("User", user)
            .AddSource("Machine", machine)
            .WithDefaultOrder("User", "Machine")
            .Build();

        Assert.NotNull(manager.Current.B);
        Assert.Null(manager.Current.B.BValue);
    }

    [Fact]
    public void Build_WithMissingReferencedSource_InNestedType_Throws()
    {
        var builder = new SettingsManagerBuilder<NestedMissingSourceA>()
            .AddSource("User", new InMemorySettingsSource("User"))
            .WithDefaultOrder("User");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

}

public sealed class CyclicSettingsA : SettingsBase
{
    [SourceOrderIf(nameof(B), "User")]
    public bool A { get; init; }

    [SourceOrderIf(nameof(A), "User")]
    public bool B { get; init; }
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
