namespace RadioServer;

using System.Diagnostics.CodeAnalysis;
using System.Threading;

/// <summary>
/// Информация о станции для отдачи через /stations.
/// </summary>
public sealed class StationInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Listeners { get; set; }
    public string? CurrentTrack { get; set; }
}

/// <summary>
/// Одна радиостанция: плейлист, общий индекс текущего трека, счётчик слушателей.
/// Индекс продвигается таймером — все клиенты станции слышат один и тот же трек.
/// </summary>
public sealed class Station
{
    public string Id { get; }
    public string Name { get; }
    public string Folder { get; }
    public List<string> Playlist { get; } = new();

    /// <summary>Общий индекс текущего трека для всех клиентов станции.</summary>
    public int PlaylistIndex { get; private set; }

    private int _listeners;
    public int Listeners => Volatile.Read(ref _listeners);

    public Station(string id, string name, string folder)
    {
        Id = id;
        Name = name;
        Folder = folder;
    }

    public void AddListener() => Interlocked.Increment(ref _listeners);
    public void RemoveListener() => Interlocked.Decrement(ref _listeners);

    /// <summary>Вернуть текущий трек, не продвигая индекс.</summary>
    public string GetCurrentTrack()
    {
        lock (Playlist)
        {
            if (Playlist.Count == 0) return string.Empty;
            return Playlist[PlaylistIndex % Playlist.Count];
        }
    }

    /// <summary>Переключиться на следующий трек (вызывается таймером станции).</summary>
    public void AdvanceTrack()
    {
        lock (Playlist)
        {
            if (Playlist.Count == 0) return;
            PlaylistIndex = (PlaylistIndex + 1) % Playlist.Count;
        }
    }
}

/// <summary>
/// Управляет станциями: сканирует папку с аудио, хранит общий индекс плейлиста
/// и счётчики слушателей. Потокобезопасно.
/// </summary>
public sealed class StationManager : IDisposable
{
    private readonly Dictionary<string, Station> _stations = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<StationManager> _logger;
    private readonly List<Timer> _timers = new();

    public StationManager(ILogger<StationManager> logger, string audioRoot)
    {
        _logger = logger;

        if (!Directory.Exists(audioRoot))
        {
            _logger.LogWarning("Audio root {Root} does not exist", audioRoot);
            return;
        }

        foreach (var dir in Directory.GetDirectories(audioRoot).OrderBy(d => d))
        {
            var id = Path.GetFileName(dir);
            if (id is null) continue;

            var mp3Files = Directory.GetFiles(dir, "*.mp3", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(Path.GetFileName)
                .Where(f => f is not null)
                .Cast<string>()
                .ToList();

            if (mp3Files.Count == 0)
            {
                _logger.LogWarning("Station {Id} has no MP3 files, skipping", id);
                continue;
            }

            var station = new Station(id, id, dir);
            station.Playlist.AddRange(mp3Files);
            _stations[id] = station;

            // Оценочная длительность трека из самого крупного файла (128 кбит/с = 16 KB/с).
            var maxSize = mp3Files.Select(f => new FileInfo(Path.Combine(dir, f)).Length).Max();
            var seconds = Math.Max(5, (int)(maxSize / 16000.0));
            var timer = new Timer(_ => station.AdvanceTrack(), null,
                TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(seconds));
            _timers.Add(timer);

            _logger.LogInformation("Station '{Id}' loaded with {Count} track(s), track switch every {Sec}s",
                id, mp3Files.Count, seconds);
        }

        if (_stations.Count == 0)
        {
            _logger.LogWarning("No radio stations were loaded from {Root}", audioRoot);
        }
    }

    public IEnumerable<StationInfo> GetStationsInfo()
    {
        lock (_stations)
        {
            foreach (var station in _stations.Values)
            {
                yield return new StationInfo
                {
                    Id = station.Id,
                    Name = station.Name,
                    Listeners = station.Listeners,
                    CurrentTrack = station.GetCurrentTrack()
                };
            }
        }
    }

    public bool TryGetStation(string id, [NotNullWhen(true)] out Station? station)
    {
        lock (_stations)
        {
            return _stations.TryGetValue(id, out station);
        }
    }

    public string GetTrackPath(string stationId, string trackFileName)
    {
        lock (_stations)
        {
            if (!_stations.TryGetValue(stationId, out var station))
                throw new FileNotFoundException($"Station '{stationId}' not found");

            return Path.Combine(station.Folder, trackFileName);
        }
    }

    public void Dispose()
    {
        foreach (var timer in _timers) timer.Dispose();
    }
}
