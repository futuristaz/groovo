using System.ComponentModel;

namespace Groovo.DTOs;

/// <summary>
/// Enum representing pop-up emoji reactions that can be sent during playlist playback
/// </summary>
public enum EmojiReaction
{
    [Description("Heart")]
    Heart = 0,
    
    [Description("Fire")]
    Fire = 1,
    
    [Description("Laughing")]
    Laughing = 2,
    
    [Description("Crying")]
    Crying = 3,
    
    [Description("Star Eyes")]
    StarEyes = 4,
    
    [Description("Clapping")]
    Clapping = 5,
    
    [Description("Thumbs Up")]
    ThumbsUp = 6,
    
    [Description("Party Popper")]
    PartyPopper = 7,
    
    [Description("Musical Note")]
    MusicalNote = 8,
    
    [Description("Rocket")]
    Rocket = 9
}
