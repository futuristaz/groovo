using Groovo.Models;

// Atmintyje (RAM) laikoma suomenų bazė (tik at runtime) singleton principu, kad Controllers tais pačiais duomenim dalintūsi
// Vėliau bus pakeistas normalia duomenų baze
namespace Groovo.Data
{
    public sealed class InMemoryDatabase
    {
        private static readonly Lazy<InMemoryDatabase> _instance =
            new(() => new InMemoryDatabase());

        public static InMemoryDatabase Instance => _instance.Value;

        public List<Song> Songs { get; } = new();
        public List<Playlist> Playlists { get; } = new();

        private InMemoryDatabase() { }
    }
}
