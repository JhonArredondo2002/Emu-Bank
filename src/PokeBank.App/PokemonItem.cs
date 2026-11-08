using System.ComponentModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PKHeX.Core;
using PokeBank.Core;

namespace PokeBank.App;

/// <summary>Elemento de lista con sprite + texto. El sprite llega de forma asíncrona.</summary>
public class PokemonItem : INotifyPropertyChanged
{
    private Bitmap? _sprite;

    public PokemonItem(PKM entity, string text)
    {
        Entity = entity;
        Text = text;
        if (entity.Species == 0)
            return;
        if (entity.IsEgg)
            _sprite = SpriteCache.Egg;
        else
            _ = LoadSpriteAsync();
    }

    public PKM Entity { get; }
    public string Text { get; }

    public Bitmap? Sprite
    {
        get => _sprite;
        private set
        {
            _sprite = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Sprite)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task LoadSpriteAsync()
    {
        var bmp = await SpriteCache.GetAsync(Entity.Species, Entity.IsShiny);
        Dispatcher.UIThread.Post(() => Sprite = bmp);
    }

    public override string ToString() => Text;
}

/// <summary>Un espacio de caja de una partida.</summary>
public sealed class SlotItem(int box, int slot, PKM entity)
    : PokemonItem(entity, $"{slot + 1:00}. {Names.Describe(entity)}")
{
    public int Box { get; } = box;
    public int Slot { get; } = slot;
    public bool IsEmpty => SaveSession.IsEmpty(Entity);
}

/// <summary>Un Pokémon del banco.</summary>
public sealed class BankItem(BankEntry entry) : PokemonItem(entry.Entity, entry.Text)
{
    public BankEntry Entry { get; } = entry;
}
