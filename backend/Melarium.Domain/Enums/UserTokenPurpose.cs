namespace Melarium.Domain.Enums;

/// <summary>What a <see cref="Entities.UserToken"/> authorises. A token is only ever valid for its own purpose.</summary>
public enum UserTokenPurpose
{
    PasswordReset = 1,
    EmailVerification = 2,
}
