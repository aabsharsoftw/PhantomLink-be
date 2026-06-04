namespace PhantomPulse.Foundation.Dtos.Requests;

public sealed record LoginRequest(string Email, string Password);
public sealed record SignupRequest(string Name, string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
