using contentapi.Views;

namespace contentapi.Main;

public interface IUserService
{
    //Task<UserView?> GetByUsernameAsync(string username);
    //Task<UserView?> GetByEmailAsync(string email);

    void InvalidateAllTokens(long userId);
    Task<string> LoginUsernameAsync(string username, string password, TimeSpan? expireOverride = null);
    Task<string> LoginEmailAsync(string email, string password, TimeSpan? expireOverride = null);
    Task<UserView> CreateNewUser(string username, string password, string email);

    Task<string> GetRegistrationKeyAsync(long userId);

    /// <summary>
    /// Since email is not publicly searchable, this is how you currently do it.
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    Task<long> GetUserIdFromEmailAsync(string email);

    Task<string> CompleteRegistration(long userId, string registrationKey);
}