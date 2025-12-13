namespace Groovo.DTOs.Responses;

public class EmojiReactionResponse
{
    public required EmojiReaction Reaction { get; set; }
    public required string Username { get; set; }
}
