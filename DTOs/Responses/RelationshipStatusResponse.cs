namespace Groovo.DTOs.Responses;

public record RelationshipStatusResponse
{
    public bool IFollowThem { get; init; }
    public bool TheyFollowMe { get; init; }
    public bool AreFriends { get; init; }
}