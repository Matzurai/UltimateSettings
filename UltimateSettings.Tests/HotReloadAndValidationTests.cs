using UltimateSettings.Attributes;
using UltimateSettings.Sources;

namespace UltimateSettings.Tests;

public sealed class ValidatableSettings : SettingsBase
{
    public int FontSize { get; set; } = 12;

    public string Theme { get; set; } = "Light";
}

public sealed class HotReloadAndValidationTests
{
    [Fact]
    public void HotReload_TriggersSettingsChangedEvent_WhenSourceFiresChange()
    {
        var userSource = new InMemorySettingsSource("User");
        userSource.Seed(nameof(ValidatableSettings.FontSize), 12);

        using var manager = new SettingsManagerBuilder<ValidatableSettings>()
            .AddSource("User", userSource, watchForChanges: true)
            .WithDefaultOrder("User")
            .Build();

        SettingsChangedEventArgs<ValidatableSettings>? receivedArgs = null;
        manager.SettingsChanged += (s, args) => receivedArgs = args;

        userSource.Seed(nameof(ValidatableSettings.FontSize), 18);
        userSource.TriggerSourceChanged();

        Assert.NotNull(receivedArgs);
        Assert.Equal(12, receivedArgs.Previous.FontSize);
        Assert.Equal(18, receivedArgs.Current.FontSize);
        Assert.Equal(18, manager.Current.FontSize);
    }

    [Fact]
    public void Validation_RejectsInvalidCandidate_AndPreservesCurrentSettings()
    {
        var userSource = new InMemorySettingsSource("User");
        userSource.Seed(nameof(ValidatableSettings.FontSize), 14);

        using var manager = new SettingsManagerBuilder<ValidatableSettings>()
            .AddSource("User", userSource, watchForChanges: true)
            .WithDefaultOrder("User")
            .WithValidator((ValidatableSettings candidate, out string? error) =>
            {
                if (candidate.FontSize <= 0)
                {
                    error = "Font size must be positive.";
                    return false;
                }

                error = null;
                return true;
            })
            .Build();

        SettingsReloadRejectedEventArgs? rejectedArgs = null;
        manager.SettingsReloadRejected += (s, args) => rejectedArgs = args;

        userSource.Seed(nameof(ValidatableSettings.FontSize), -5);
        userSource.TriggerSourceChanged();

        Assert.NotNull(rejectedArgs);
        Assert.Equal("Font size must be positive.", rejectedArgs.Error);

        // Current settings snapshot must remain untouched at 14.
        Assert.Equal(14, manager.Current.FontSize);
    }

    [Fact]
    public void Build_Throws_WhenInitialSettingsFailValidation()
    {
        var userSource = new InMemorySettingsSource("User");
        userSource.Seed(nameof(ValidatableSettings.FontSize), -1);

        var builder = new SettingsManagerBuilder<ValidatableSettings>()
            .AddSource("User", userSource)
            .WithDefaultOrder("User")
            .WithValidator((ValidatableSettings candidate, out string? error) =>
            {
                if (candidate.FontSize <= 0)
                {
                    error = "Font size must be positive.";
                    return false;
                }

                error = null;
                return true;
            });

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("Font size must be positive", ex.Message);
    }

    [Fact]
    public void AddSource_Throws_WhenWatchForChangesTrueOnNonObservableSource()
    {
        var nonObservableSource = new NonObservableDummySource("Dummy");

        var builder = new SettingsManagerBuilder<ValidatableSettings>();

        Assert.Throws<InvalidOperationException>(() => builder.AddSource("Dummy", nonObservableSource, watchForChanges: true));
    }

    private sealed class NonObservableDummySource : ISettingsSource
    {
        public NonObservableDummySource(string id) => Id = id;

        public string Id { get; }

        public bool CanRead => true;

        public bool CanWrite => false;

        public bool TryRead(string key, out object? value)
        {
            value = null;
            return false;
        }

        public bool TryRead(string key, Type targetType, out object? value)
        {
            value = null;
            return false;
        }

        public void Write(string key, object? value) => throw new NotSupportedException();
    }
}
