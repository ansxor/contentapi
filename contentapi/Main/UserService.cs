using System.Collections.Concurrent;
using System.Data;
using System.Text.RegularExpressions;
using contentapi.Db;
using contentapi.Search;
using contentapi.Security;
using contentapi.data.Views;
using Dapper;
using Dapper.Contrib.Extensions;
using contentapi.data;

namespace contentapi.Main;

public class UserServiceConfig
{
    public TimeSpan PasswordExpire {get;set;} = TimeSpan.Zero; //Zero means never expire, UNLESS the lastPasswordDate field is empty!
    public TimeSpan TemporaryPasswordExpire {get;set;} = TimeSpan.FromMinutes(20);
    public string UsernameRegex {get;set;} = "^[a-zA-Z0-9_]+$";
    public int MinUsernameLength {get;set;} = 2;
    public int MaxUsernameLength {get;set;} = 16;
    public int MinPasswordLength {get;set;} = 8;
    public int MaxPasswordLength {get;set;} = 32;
}

public class TemporaryPassword
{
    public DateTime ExpireDate {get;set;}
    public string Key {get;set;} = Guid.NewGuid().ToString();
}

public class UserService : IUserService
{
    protected ILogger logger;
    protected IHashService hashService;
    protected IAuthTokenService<long> authTokenService;
    protected UserServiceConfig config;
    protected IViewTypeInfoService typeInfoService;
    protected IDbServicesFactory dbFactory;

    protected string? userTable = null;
    public ConcurrentDictionary<long, string> RegistrationLog = new ConcurrentDictionary<long, string>();
    public ConcurrentDictionary<long, TemporaryPassword> TempPasswordSet = new ConcurrentDictionary<long, TemporaryPassword>();

    public UserService(ILogger<UserService> logger, IHashService hashService, IAuthTokenService<long> authTokenService,
        UserServiceConfig config, IDbServicesFactory factory, IViewTypeInfoService typeInfoService)
    {
        this.logger = logger;
        this.hashService = hashService;
        this.authTokenService = authTokenService;
        this.config = config;
        this.dbFactory = factory;
        this.typeInfoService = typeInfoService;
        userTable = typeInfoService.GetDatabaseForType<UserView>();
    }

    public async Task<User> GetUserByWhatever(string whereClause, object parameters)
    {
        using var dbcon = dbFactory.CreateRaw();
        var user = (await dbcon.QueryAsync<User>($"select * from {userTable} {whereClause}", parameters)).FirstOrDefault();
        if (user == null || user.deleted)
            throw new NotFoundException($"User not found");
        if (user.type != UserType.user)
            throw new ForbiddenException("Can only perform user session actions with real users!");
        return user;
    }

    public Task<User> GetUserById(long userId) => GetUserByWhatever($"where {nameof(User.id)} = @userId", new { userId = userId });
    public Task<User> GetUserByEmail(string email) => GetUserByWhatever($"where {nameof(User.email)} = @email COLLATE NOCASE", new { email = email });
    public Task<User> GetUserByUsername(string username) => GetUserByWhatever($"where {nameof(User.username)} = @username COLLATE NOCASE", new { username = username });
    public async Task<long> GetUserIdFromEmailAsync(string email) => (await GetUserByEmail(email)).id;
    public async Task<long> GetUserIdFromUsernameAsync(string username) => (await GetUserByUsername(username)).id;

    public async Task CheckValidUsernameAsync(string username, long existingUserId = 0)
    {
        using var dbcon = dbFactory.CreateRaw();

        if(string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username can't be null or whitespace only!");
        if(username.Length < config.MinUsernameLength)
            throw new ArgumentException($"Username too short! Min length: {config.MinUsernameLength}");
        if(username.Length > config.MaxUsernameLength)
            throw new ArgumentException($"Username too long! Max length: {config.MaxUsernameLength}");
        if(!Regex.IsMatch(username, config.UsernameRegex))
            throw new ArgumentException($"Username has invalid characters, must match regex: {config.UsernameRegex}");

        var existing = await dbcon.ExecuteScalarAsync<int>($"select count(*) from {userTable} where {nameof(User.username)} = @user and {nameof(User.id)} <> @id", new { user = username, id = existingUserId });

        if(existing > 0)
            throw new ArgumentException($"Username '{username}' already taken!");
    }

    public async Task CheckValidEmailAsync(string email)
    {
        using var dbcon = dbFactory.CreateRaw();

        if(string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Must provide email to create new user!");

        var existing = await dbcon.ExecuteScalarAsync<int>($"select count(*) from {userTable} where {nameof(User.email)} = @user", new { user = email });

        if(existing > 0)
            throw new ArgumentException($"Duplicate email in system: '{email}'");
    }

    public void CheckValidPassword(string password)
    {
        if(string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password can't be null or just whitespace!");
        if(password.Length < config.MinPasswordLength)
            throw new ArgumentException($"Password too short! Must be at least {config.MinPasswordLength} characters");
        if(password.Length > config.MaxPasswordLength)
            throw new ArgumentException($"Password too long! Must be at most {config.MaxPasswordLength} characters");
    }

    public bool IsPasswordExpired(User user)
    {
        return (user.lastPasswordDate.Ticks == 0 || (config.PasswordExpire.Ticks > 0 && (user.lastPasswordDate + config.PasswordExpire) < DateTime.UtcNow));
    }

    public async Task<bool> IsPasswordExpired(long userId)
    {
        return IsPasswordExpired(await GetUserById(userId));
    }
    
    /// <summary>
    /// Item1 is salt, Item2 is password, both are ready to be stored in a user
    /// </summary>
    /// <param name="password"></param>
    /// <returns></returns>
    public Tuple<string,string> GenerateNewPasswordData(string password)
    {
        var salt = hashService.GetSalt();
        return Tuple.Create(Convert.ToBase64String(salt), Convert.ToBase64String(hashService.GetHash(password, salt)));
    }

    public async Task ExpirePasswordNow(long userId)
    {
        using var dbcon = dbFactory.CreateRaw();

        var zero = new DateTime(0, DateTimeKind.Utc);

        var count = await dbcon.ExecuteAsync($"update {userTable} set lastPasswordDate=@date where id = @id", new {
            date = zero, id = userId
        });

        if(count != 1)
            throw new NotFoundException($"Couldn't find user {userId}");
    }

    public async Task<long> CreateNewUser(string username, string password, string email)
    {
        await CheckValidUsernameAsync(username);
        await CheckValidEmailAsync(email);
        CheckValidPassword(password);

        var user = new Db.User {
            username = username,
            email = email,
            createDate = DateTime.UtcNow,
            registrationKey = Guid.NewGuid().ToString(),
            lastPasswordDate = DateTime.UtcNow,
            type = UserType.user
        };

        var passwordData = GenerateNewPasswordData(password);
        user.salt = passwordData.Item1;
        user.password = passwordData.Item2;

        long id;

        using var dbcon = dbFactory.CreateRaw();
        using (var tsx = dbcon.BeginTransaction())
        {
            id = await dbcon.InsertAsync(user, tsx);

            if (id <= 0)
                throw new InvalidOperationException("For some reason, writing the user to the database failed!");

            tsx.Commit();
        }

        RegistrationLog.TryAdd(user.id, user.registrationKey);

        await WriteAdminLog(new AdminLog()
        {
            type = AdminLogType.user_create,
            initiator = id, 
            target = id, 
            text = $"User '{username}'({id}) created an account"
        }, dbcon);

        return id;
    }
    
    public string GetNewTokenForUser(long uid, TimeSpan? expireOverride = null)
    {
        var data = new Dictionary<string, string>();
        return authTokenService.GetNewToken(uid, data, expireOverride);
    }

    public async Task<string?> GetRegistrationKeyRawAsync(long userId)
    {
        //Go get the registration key
        var userRegistration = await GetUserById(userId); 
        return (string?)userRegistration.registrationKey;
    }

    public async Task<string> GetRegistrationKeyAsync(long userId)
    {
        return await GetRegistrationKeyRawAsync(userId) ?? 
            throw new RequestException($"No registration key for user {userId}!");
    }

    public async Task<string> CompleteRegistration(long userId, string registrationKey)
    {
        string? realKey = await GetRegistrationKeyAsync(userId); 

        if(string.IsNullOrWhiteSpace(realKey))
            throw new RequestException($"User {userId} seems to already be registered!");

        if(realKey != registrationKey)       
            throw new RequestException($"Invalid registration key!");
        
        using var dbcon = dbFactory.CreateRaw();
        var count = await dbcon.ExecuteAsync($"update {userTable} set {nameof(User.registrationKey)} = NULL where {nameof(User.id)} = @id", new { id = userId });

        if(count <= 0)
            throw new InvalidOperationException("Couldn't update user record to complete registration!");

        RegistrationLog.TryRemove(userId, out _);

        var user = await GetUserById(userId);

        await WriteAdminLog(new AdminLog()
        {
            type = AdminLogType.user_register,
            initiator = userId,
            target = userId,
            text = $"User '{user.username}'({user.id}) completed account registration"
        }, dbcon);

        return GetNewTokenForUser(userId);
   }

    protected async Task<string> LoginGeneric(string fieldname, object value, string password, TimeSpan? expireOverride)
    {
        //First, find the user they're even talking about. 
        using var dbcon = dbFactory.CreateRaw();

        //TODO: Consider making a special user view specifically for working with real user data, so we don't have to keep doing raw database
        //stuff when it comes to private user data. For instance, you have to check if "salt" is non-empty here, but that's very dependent on
        //how we mark if users are deleted or not. We ASSUME we don't want the salt (or the password) kept on deletion, BUT...

        User user;

        //Next, get the LEGITIMATE data from the database
        try
        {
            user = await GetUserByWhatever($"where {fieldname} = @user COLLATE NOCASE", new { user = value });
        }
        catch(NotFoundException)
        {
            await WriteAdminLog(new AdminLog()
            {
                type = AdminLogType.login_failure,
                initiator = 0,
                target = 0,
                text = $"Failed login attempt for {value}: user not found!"
            }, dbcon);

            throw;
        }

        //So for registration, it's SPECIFICALLY null which makes you registered! This is IMPORTANT!
        if(user.registrationKey != null)
            throw new ForbiddenException("User not registered! Can't log in!");
        
        if(TemporaryPasswordMatches(user.id, password))
        {
            //This is a SUCCESS! there's no throw
            await WriteAdminLog(new AdminLog()
            {
                type = AdminLogType.login_temporary,
                initiator = user.id,
                target = user.id,
                text = $"User '{user.username}'({user.id}) logged in with a temporary password"
            }, dbcon);

            ExpireTemporaryPassword(user.id);
        }
        //Check for expiration first so users don't know if it was right or wrong
        else if(IsPasswordExpired(user))
        {
            //This is a FAILURE!
            await WriteAdminLog(new AdminLog()
            {
                type = AdminLogType.login_passwordexpired,
                initiator = user.id,
                target = user.id,
                text = $"Aborted login attempt for '{user.username}'({user.id}): password expired"
            }, dbcon);

            throw new TokenException("Your password is expired, please send the recovery password to your email associated with this account");
        }
        //Compare password hashes; if bad, throw error
        else if(!hashService.VerifyText(password, Convert.FromBase64String(user.password), Convert.FromBase64String(user.salt)))
        {
            //This is a FAILURE!
            await WriteAdminLog(new AdminLog()
            {
                type = AdminLogType.login_failure,
                initiator = user.id,
                target = user.id,
                text = $"Failed login attempt for '{user.username}'({user.id}): incorrect password"
            }, dbcon);

            throw new RequestException("Password incorrect!");
        }

        //If everything above passes, we're good to go
        return GetNewTokenForUser (user.id, expireOverride);
    }

    public Task<string> LoginEmailAsync(string email, string password, TimeSpan? expireOverride = null) =>
        LoginGeneric(nameof(User.email), email, password, expireOverride);

    public Task<string> LoginUsernameAsync(string username, string password, TimeSpan? expireOverride = null) =>
        LoginGeneric(nameof(User.username), username, password, expireOverride);

    public async Task VerifyPasswordAsync(long userId, string password)
    {
        //The login will tell us all the exceptions we need to know
        await LoginGeneric(nameof(User.id), userId, password, null);
    }

    public void InvalidateAllTokens(long userId)
    {
        authTokenService.InvalidateAllTokens(userId);
    }

    public async Task<UserGetPrivateData> GetPrivateData(long userId)
    {
        var result = new UserGetPrivateData();
        var user = await GetUserById(userId);

        //WARN: this technically lets you get private data for groups!
        
        //This could also be done with an auto-mapper but
        result.email = user.email;

        return result;
    }

    public async Task SetPrivateData(long userId, UserSetPrivateData data)
    {
        var sets = new Dictionary<string,object>();

        //Go find the data to add
        if(!string.IsNullOrWhiteSpace(data.password)) // != null)
        {
            CheckValidPassword(data.password);
            var pwdata = GenerateNewPasswordData(data.password);
            sets.Add(nameof(User.salt), pwdata.Item1);
            sets.Add(nameof(User.password), pwdata.Item2);
            sets.Add(nameof(User.lastPasswordDate), DateTime.UtcNow);
        }

        if(!string.IsNullOrWhiteSpace(data.email)) // != null)
        {
            await CheckValidEmailAsync(data.email);
            sets.Add(nameof(User.email), data.email);
        }

        //They didn't appear to add any data!
        if(sets.Count == 0)
            throw new ArgumentException($"No private data sent for user {userId}!");
        
        var parameters = new Dictionary<string, object> {{ "id" , userId }};

        foreach(var kp in sets)
            parameters.Add(kp.Key, kp.Value);
        
        //WARN: this technically lets you set private data for groups!

        using var dbcon = dbFactory.CreateRaw();
        var count = await dbcon.ExecuteAsync($"update {userTable} set {string.Join(",", sets.Select(x => $"{x.Key} = @{x.Key}"))} where id = @id", parameters);

        if(count != 1)
            throw new ArgumentException($"Couldn't find user {userId}");
    }

    public async Task SetSuperStatus(long userId, bool super)
    {
        using var dbcon = dbFactory.CreateRaw();
        var count = await dbcon.ExecuteAsync($"update {userTable} set super = @super where id = @id", new { super = super, id = userId});

        if(count != 1)
            throw new ArgumentException($"Couldn't find user {userId}");
    }

    //Just yet another admin log writer... probably need to fix this
    public async Task<AdminLog> WriteAdminLog(AdminLog log, IDbConnection dbcon)
    {
        logger.LogDebug($"Admin log: {log.text}");
        log.id = 0;
        log.createDate = DateTime.UtcNow;
        //Probably not necessary to reassign
        log.id = await dbcon.InsertAsync(log);
        return log;
    }

    public TemporaryPassword RefreshPassword(TemporaryPassword temporaryPassword)
    {
        temporaryPassword.ExpireDate = DateTime.Now + config.TemporaryPasswordExpire;
        temporaryPassword.Key = Guid.NewGuid().ToString();
        return temporaryPassword;
    }

    public TemporaryPassword GetTemporaryPassword(long uid)
    {
        var tempPassword = TempPasswordSet.GetOrAdd(uid, uid => RefreshPassword(new TemporaryPassword()));

        //Regen the password if it expired
        if(tempPassword.ExpireDate < DateTime.Now)
            RefreshPassword(tempPassword);

        return tempPassword;
    }

    public void ExpireTemporaryPassword(long uid)
    {
        var tp = GetTemporaryPassword(uid);
        tp.ExpireDate = new DateTime(0);
    }

    public bool TemporaryPasswordMatches(long uid, string key)
    {
        TemporaryPassword? temporaryPassword;

        if(TempPasswordSet.TryGetValue(uid, out temporaryPassword))
        {
            return temporaryPassword?.Key == key && temporaryPassword.ExpireDate >= DateTime.Now;
        }

        return false;
    }
}