namespace PhantomPulse.Foundation.Dtos.Responses;

public sealed record LoginResponse(
    string                Token,
    string                RefreshToken,
    Guid                  UserId,
    string                Email,
    string                Name,
    string                Role,
    IReadOnlyList<string> Permissions);
