using Microsoft.AspNetCore.Identity;
using TestAPILayer.Dtos.Users;

namespace TestAPILayer.Contracts
{
    public interface IAuthManager
    {
        Task<IEnumerable<IdentityError>> Register(ApiUserDto userDto);
        Task<IEnumerable<IdentityError>> RegisterAdmin(ApiUserDto userDto);       
        Task<bool> ResetPassword(ResetPasswordDto resetPasswordDto);
        Task<string> PasswordRecovery(PasswordRecoveryDto passwordRecoverDto);
        Task<string> CreateRefreshToken();
        Task<AuthResponseDto> VerifyRefreshToken(AuthResponseDto request);
    }
}
