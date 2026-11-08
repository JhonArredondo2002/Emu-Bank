using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PokeBank.Core;

namespace PokeBank.App;

/// <summary>
/// Sprites de PokeAPI (github.com/PokeAPI/sprites). Se descargan la primera vez y quedan
/// guardados en %AppData%\PokeBank\Sprites para que funcionen sin internet después.
/// </summary>
public static class SpriteCache
{
    private const string BaseUrl = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/";

    private static readonly HttpClient Http = CreateClient();
    private static readonly ConcurrentDictionary<string, Task<Bitmap?>> Cache = new();
    private static readonly string Folder = Path.Combine(BankStore.DefaultRoot, "Sprites");

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PokeBank/0.2");
        return client;
    }

    private static Bitmap? _egg;

    /// <summary>Ícono de huevo incluido en la app (Assets/huevo.png).</summary>
    public static Bitmap? Egg
    {
        get
        {
            if (_egg is not null)
                return _egg;
            try
            {
                using var stream = AssetLoader.Open(new Uri("avares://PokeBank/Assets/huevo.png"));
                _egg = new Bitmap(stream);
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex, "Sprite huevo");
            }
            return _egg;
        }
    }

    public static Task<Bitmap?> GetAsync(ushort species, bool shiny)
    {
        if (species == 0)
            return Task.FromResult<Bitmap?>(null);
        var key = shiny ? $"{species}_shiny" : $"{species}";
        return Cache.GetOrAdd(key, _ => LoadAsync(species, shiny, key));
    }

    private static async Task<Bitmap?> LoadAsync(ushort species, bool shiny, string key)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var file = Path.Combine(Folder, key + ".png");
            if (!File.Exists(file))
            {
                var url = BaseUrl + (shiny ? "shiny/" : string.Empty) + species + ".png";
                var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
                await File.WriteAllBytesAsync(file, bytes).ConfigureAwait(false);
            }
            return new Bitmap(file);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, $"Sprite {key}");
            Cache.TryRemove(key, out _); // permitir reintentar más tarde (p. ej. si no había internet)
            return null;
        }
    }
}
