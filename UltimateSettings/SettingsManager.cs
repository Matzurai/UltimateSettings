using System.Linq.Expressions;
using UltimateSettings.Internal;
using UltimateSettings.Sources;

namespace UltimateSettings;

internal sealed class SettingsManager<TSettings> : ISettingsManager<TSettings>
    where TSettings : SettingsBase, new()
{
    private readonly object _lock = new();
    private readonly IReadOnlyDictionary<string, ISettingsSource> _sources;
    private readonly IReadOnlyList<string> _defaultOrder;
    private readonly SettingsValidator<TSettings>? _validator;
    private readonly List<IObservableSettingsSource> _subscribedWatchedSources = new();
    private bool _isDisposed;

    public event EventHandler<SettingsChangedEventArgs<TSettings>>? SettingsChanged;

    public event EventHandler<SettingsReloadRejectedEventArgs>? SettingsReloadRejected;

    internal SettingsManager(
        IReadOnlyDictionary<string, ISettingsSource> sources,
        IReadOnlyList<string> defaultOrder,
        HashSet<string> watchedSourceIds,
        SettingsValidator<TSettings>? validator)
    {
        _sources = sources;
        _defaultOrder = defaultOrder;
        _validator = validator;

        Current = ResolveAndValidateCandidate(out var initialError);
        if (initialError is not null)
        {
            throw new InvalidOperationException($"Initial settings validation failed: {initialError}");
        }

        foreach (var id in watchedSourceIds)
        {
            if (_sources.TryGetValue(id, out var source) && source is IObservableSettingsSource observable)
            {
                observable.SourceChanged += OnSourceChanged;
                _subscribedWatchedSources.Add(observable);
            }
        }
    }

    public TSettings Current { get; private set; }

    public TSettings Load()
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(SettingsManager<TSettings>));
            }

            var candidate = SettingsResolver<TSettings>.Resolve(_sources, _defaultOrder);

            if (_validator is not null && !_validator(candidate, out var error))
            {
                SettingsReloadRejected?.Invoke(this, new SettingsReloadRejectedEventArgs(error));
                return Current;
            }

            var previous = Current;
            Current = candidate;
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs<TSettings>(previous, candidate));
            return Current;
        }
    }

    public void Edit(string sourceId, Action<ISettingsEditor<TSettings>> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);

        lock (_lock)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(SettingsManager<TSettings>));
            }

            if (!_sources.TryGetValue(sourceId, out var source))
            {
                throw new ArgumentException($"No source registered with id '{sourceId}'.", nameof(sourceId));
            }

            if (!source.CanWrite)
            {
                throw new InvalidOperationException($"Source '{sourceId}' is write-protected.");
            }

            var editor = new SettingsEditor<TSettings>();
            edit(editor);

            foreach (var change in editor.Changes)
            {
                source.Write(change.Property.Name, change.Value);
            }

            Load();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            foreach (var source in _subscribedWatchedSources)
            {
                source.SourceChanged -= OnSourceChanged;
            }

            _subscribedWatchedSources.Clear();
        }
    }

    private TSettings ResolveAndValidateCandidate(out string? validationError)
    {
        var candidate = SettingsResolver<TSettings>.Resolve(_sources, _defaultOrder);

        if (_validator is not null && !_validator(candidate, out var error))
        {
            validationError = error;
            return candidate;
        }

        validationError = null;
        return candidate;
    }

    private void OnSourceChanged(object? sender, EventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            Load();
        }
        catch (Exception ex)
        {
            SettingsReloadRejected?.Invoke(this, new SettingsReloadRejectedEventArgs(ex.Message, ex));
        }
    }
}
