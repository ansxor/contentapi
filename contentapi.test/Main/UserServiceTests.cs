using System;
using System.Threading.Tasks;
using contentapi.data;
using contentapi.data.Views;
using contentapi.Db;
using contentapi.Main;
using contentapi.Search;
using contentapi.Security;
using contentapi.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

//[Collection("PremadeDatabase")]
public class UserServiceTests : UnitTestBase, IClassFixture<DbUnitTestBase>
{
    protected DbUnitTestBase fixture;
    protected UserService service;
    protected UserServiceConfig config;
    protected IGenericSearch searcher;

    public UserServiceTests(DbUnitTestBase fixture)
    {
        config = new UserServiceConfig {
            MinPasswordLength = 4,
            MaxPasswordLength = 8,
            MinUsernameLength = 4,
            MaxUsernameLength = 8,
            UsernameRegex = "^[a-zA-Z0-9]+$"
        };

        searcher = fixture.GetService<IGenericSearch>();
        this.fixture = fixture;

        this.service = new UserService(fixture.GetService<ILogger<UserService>>(), fixture.GetService<IHashService>(), 
            fixture.GetService<IAuthTokenService<long>>(), config, fixture.GetService<ContentApiDbConnection>(),
            fixture.GetService<IViewTypeInfoService>()); //, fixture.GetService<IDbWriter>());

        //Always want a fresh database!
        fixture.ResetDatabase();
    }

    [Fact]
    public async Task CreateNewUser_Basic()
    {
        var userId = await service.CreateNewUser("hello", "short", "email@email.com");
        var user = await searcher.GetById<UserView>(userId);
        AssertDateClose(user.createDate);
        Assert.True(user.id > 0);
        Assert.False(user.registered);
        Assert.Equal("hello", user.username);
        Assert.Equal(UserType.user, user.type);
    }

    [Fact]
    public async Task CreateNewUser_LoginBeforeRegisterFail()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");
        await Assert.ThrowsAnyAsync<ForbiddenException>(async () => {
            var token = await service.LoginUsernameAsync("hello", "short");
        });
        //Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task CreateNewUser_GetRegistration()
    {
        var userId = await service.CreateNewUser("hello", "short", "email@email.com");
        var registration = await service.GetRegistrationKeyAsync(userId);
        Assert.Equal(service.RegistrationLog[userId], registration);
        //Retrieving the registration shouldn't REGISTER them
        var completedUser = await searcher.GetById<UserView>(userId);
        Assert.False(completedUser.registered);
    }

    [Fact]
    public async Task CreateNewUser_GetUserIdFromEmail()
    {
        var userId = await service.CreateNewUser("hello", "short", "email@email.com");
        var id = await service.GetUserIdFromEmailAsync("email@email.com");
        Assert.Equal(userId, id);
    }

    [Fact]
    public async Task CreateNewUser_GetUserIdFromEmail_Fail()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");

        await Assert.ThrowsAnyAsync<NotFoundException>(async () =>
        {
            var id = await service.GetUserIdFromEmailAsync("notemail@email.com");
        });
    }

    [Fact]
    public async Task CreateNewUser_Register()
    {
        var userId = await service.CreateNewUser("hello", "short", "email@email.com");
        var token = await service.CompleteRegistration(userId, service.RegistrationLog[userId]);
        Assert.False(string.IsNullOrWhiteSpace(token));
        var completedUser = await searcher.GetById<UserView>(userId);
        Assert.True(completedUser.registered);
    }

    [Fact]
    public async Task CreateNewUser_Register_UnknownUser()
    {
        var userId = await service.CreateNewUser("hello", "short", "email@email.com");

        await Assert.ThrowsAnyAsync<NotFoundException>(async () => {
            var token = await service.CompleteRegistration(99, service.RegistrationLog[userId]);
        });
    }

    [Fact]
    public async Task CreateNewUser_Register_BadRegistrationKey()
    {
        var userId = await service.CreateNewUser("hello", "short", "email@email.com");

        await Assert.ThrowsAnyAsync<RequestException>(async () => {
            var token = await service.CompleteRegistration(userId, service.RegistrationLog[userId] + "A");
        });
    }

    [Fact]
    public async Task CreateNewUser_LoginUsername()
    {
        var userId = await service.CreateNewUser("hello", "short", "email@email.com");
        var token = await service.CompleteRegistration(userId, service.RegistrationLog[userId]);
        var loginToken = await service.LoginUsernameAsync("hello", "short");
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.False(string.IsNullOrWhiteSpace(loginToken));
    }

    [Fact]
    public async Task CreateNewUser_LoginEmail()
    {
        var userId = await service.CreateNewUser("hello", "short", "email@email.com");
        var token = await service.CompleteRegistration(userId, service.RegistrationLog[userId]);
        var loginToken = await service.LoginEmailAsync("email@email.com", "short");
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.False(string.IsNullOrWhiteSpace(loginToken));
    }

    [Fact]
    public async Task CreateNewUser_VerifyPassword()
    {
        var userId = await service.CreateNewUser("hello", "short", "email@email.com");
        var token = await service.CompleteRegistration(userId, service.RegistrationLog[userId]);
        await service.VerifyPasswordAsync(userId, "short");

        await Assert.ThrowsAnyAsync<Exception>(() => service.VerifyPasswordAsync(userId, "shorts"));
    }

    [Fact]
    public async Task CreateNewUser_Login_BadPassword()
    {
        var userId = await service.CreateNewUser("hello", "short", "email@email.com");
        var token = await service.CompleteRegistration(userId, service.RegistrationLog[userId]);
        await Assert.ThrowsAnyAsync<RequestException>(async () => {
            var loginToken = await service.LoginUsernameAsync("hello", "shorts");
        });
    }

    [Fact]
    public async Task CreateNewUser_UsernameCollision()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");

        await Assert.ThrowsAnyAsync<ArgumentException>(async () => {
            var user2 = await service.CreateNewUser("hello", "short2", "email2@email.com");
        });
    }

    [Fact]
    public async Task CreateNewUser_EmailCollision()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");

        await Assert.ThrowsAnyAsync<ArgumentException>(async () => {
            var user2 = await service.CreateNewUser("hello2", "short2", "email@email.com");
        });
    }

    [Theory]
    [InlineData("ab", "short")]  //Username too short
    [InlineData("abcdefgt9", "short")] //Username too long
    [InlineData("hello", "sho")] //Password too short
    [InlineData("hello", "shortlong")] //Password too long
    [InlineData("!hello", "short")] //bad char in username
    public async Task CreateNewUser_InputErrors(string username, string password)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(async () => {
            var user = await service.CreateNewUser(username, password, "email@email.com");
        });
    }

    [Fact]
    public async Task GetPrivateData()
    {
        //Assume this goes OK
        var userId = await service.CreateNewUser("hello", "short", "email@email.com");
        var token = await service.CompleteRegistration(userId, service.RegistrationLog[userId]);

        //Now go get the email and hidelist. Hidelist should just be an empty array
        var privateData = await service.GetPrivateData(userId);

        Assert.Equal("email@email.com", privateData.email);
        //Assert.NotNull(privateData.hideList);
        //Assert.Empty(privateData.hideList);
    }

    //Set some data
    [Theory]
    [InlineData("newemail@junk.com", true)]
    [InlineData("email@email.com", false)]
    [InlineData("", false)]
    public async Task SetPrivateData_Email(string email, bool success)
    {
        //Assume this goes OK
        var userId = await service.CreateNewUser("hello", "short", "email@email.com");
        var token = await service.CompleteRegistration(userId, service.RegistrationLog[userId]);

        //Now go update the email
        var setData = new Func<Task>(() => service.SetPrivateData(userId, new UserSetPrivateData() { email = email }));

        if(success)
        {
            await setData();
            var loginToken = await service.LoginEmailAsync(email, "short");
            Assert.False(string.IsNullOrWhiteSpace(loginToken));
        }
        else
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(setData);
        }
    }

    [Theory]
    [InlineData("fine", true)]
    [InlineData("alsofine", true)]
    [InlineData("NO", false)]
    [InlineData("", false)]
    public async Task SetPrivateData_Password(string password, bool success)
    {
        //Assume this goes OK
        var userId = await service.CreateNewUser("hello", "short", "email@email.com");
        var token = await service.CompleteRegistration(userId, service.RegistrationLog[userId]);

        //Now go update the password
        var setData = new Func<Task>(() => service.SetPrivateData(userId, new UserSetPrivateData() { password = password }));

        if(success)
        {
            await setData();
            var loginToken = await service.LoginUsernameAsync("hello", password);
            Assert.False(string.IsNullOrWhiteSpace(loginToken));
        }
        else
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(setData);
        }
    }

    //[Fact]
    //public async Task SetPrivateData_Hidelist()
    //{
    //    //Assume this goes OK
    //    var user = await service.CreateNewUser("hello", "short", "email@email.com");
    //    var token = await service.CompleteRegistration(user.id, service.RegistrationLog[user.id]);

    //    var newHidelist = new List<long> {5, 10} ;

    //    //Now go update the hidelist
    //    await service.SetPrivateData(user.id, new UserSetPrivateData() { hideList = newHidelist });
    //    var privateData = await service.GetPrivateData(user.id);

    //    Assert.True(newHidelist.SequenceEqual(privateData.hideList!));
    //}

    protected async Task GeneralNewUserTest(Func<string, string, long, string, Task> test)
    {
        const string username = "hello";
        const string password = "short";

        //Assume this goes OK
        var userId = await service.CreateNewUser(username, password, "email@email.com");
        var token = await service.CompleteRegistration(userId, service.RegistrationLog[userId]);

        //This should be fine
        var loginToken = await service.LoginUsernameAsync(username, password);

        await test(username, password, userId, loginToken);
    }

    [Fact]
    public Task Login_ZeroTimeNoPasswordExpire() => GeneralNewUserTest(async (username, password, userId, loginToken) =>
    {
        config.PasswordExpire = TimeSpan.FromTicks(0); //The expiration is only looked at if it's non-zero

        //This should be fine, ticks 0 means "no expiration"
        loginToken = await service.LoginUsernameAsync(username, password);
    });

    [Fact]
    public Task Login_NaturalPasswordExpire() => GeneralNewUserTest(async (username, password, userId, loginToken) =>
    {
        //IMMEDIATE password expire
        config.PasswordExpire = TimeSpan.FromTicks(1); //The expiration is only looked at if it's non-zero

        //Now that expiration is instant, this should fail
        await Assert.ThrowsAnyAsync<TokenException>(() => service.LoginUsernameAsync(username, password));
    });

    [Fact]
    public Task Login_ForcedPasswordExpire() => GeneralNewUserTest(async (username, password, userId, loginToken) =>
    {
        await service.ExpirePasswordNow(userId);

        //Now that expiration is instant, this should fail
        await Assert.ThrowsAnyAsync<TokenException>(() => service.LoginUsernameAsync(username, password));
    });

    [Fact]
    public Task Login_ForcedPasswordExpire_UserIdNotFound() => GeneralNewUserTest(async (username, password, userId, loginToken) =>
    {
        await Assert.ThrowsAnyAsync<NotFoundException>(() => service.ExpirePasswordNow(userId + 1));
    });

    [Fact]
    public Task Login_TempPassword() => GeneralNewUserTest(async (username, password, userId, loginToken) =>
    {
        //Generate a temp password
        var tempPassword = service.GetTemporaryPassword(userId);

        //Should be able to login with that password now
        var token = await service.LoginUsernameAsync(username, tempPassword.Key);

        Assert.False(String.IsNullOrWhiteSpace(token));
    });

    [Fact]
    public Task Login_TempPassword_NoRandoms() => GeneralNewUserTest(async (username, password, userId, loginToken) =>
    {
        //Generate a temp password
        var tempPassword = service.GetTemporaryPassword(userId);

        //Should not be able to login with some random password now
        await Assert.ThrowsAnyAsync<RequestException>(() => service.LoginUsernameAsync(username, tempPassword.Key + "a"));
    });

    [Fact]
    public Task Login_TempPassword_Expire() => GeneralNewUserTest(async (username, password, userId, loginToken) =>
    {
        config.TemporaryPasswordExpire = TimeSpan.FromMilliseconds(50);

        //Generate a temp password
        var tempPassword = service.GetTemporaryPassword(userId);

        //Should be able to login with that password for now
        var token = await service.LoginUsernameAsync(username, tempPassword.Key);

        //Wait a bit
        await Task.Delay(60);

        //Should not be able to login with some random password now
        await Assert.ThrowsAnyAsync<RequestException>(() => service.LoginUsernameAsync(username, tempPassword.Key));
    });
}