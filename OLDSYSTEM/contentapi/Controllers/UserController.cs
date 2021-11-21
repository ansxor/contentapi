using contentapi.Views;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using contentapi.Services;
using AutoMapper;
using contentapi.Services.Implementations;
using contentapi.Services.Constants;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;

namespace contentapi.Controllers
{
    public class UserControllerConfig
    {
        public int NameChangesPerTime {get;set;} //= 3;
        public TimeSpan NameChangeRange {get;set;}
        public TimeSpan PasswordResetExpire {get;set;}
    }


    public class UserController : BaseSimpleController
    {
        public class PasswordReset
        {
            public long UserId {get;set;}
            public string Key {get;set;}
            public bool Valid {get;set;} = true;
        }

        protected IHashService hashService;
        protected ITokenService tokenService;
        protected ILanguageService languageService;
        protected IEmailService emailService;
        protected IMapper mapper;
        protected UserViewService service;
        protected UserViewSource source;
        protected IDecayer<PasswordReset> passwordResets;
        protected ITempTokenService<long> tempTokenService;

        protected UserControllerConfig config;


        public UserController(BaseSimpleControllerServices services, IHashService hashService,
            ITokenService tokenService, ILanguageService languageService, IEmailService emailService,
            UserControllerConfig config, UserViewService service, IMapper mapper, IDecayer<PasswordReset> passwordResets,
            ITempTokenService<long> tempTokenService,
            UserViewSource source)
            :base(services)
        { 
            this.hashService = hashService;
            this.tokenService = tokenService;
            this.languageService = languageService;
            this.emailService = emailService;
            this.config = config;
            this.service = service;
            this.mapper = mapper;
            this.passwordResets = passwordResets;
            this.tempTokenService = tempTokenService;
            this.source = source;
        }

        protected async Task<UserViewFull> GetCurrentUser()
        {
            var requester = GetRequesterNoFail();
            var user = await service.FindByIdAsync(requester.userId, requester);

            //A VERY SPECIFIC glitch you really only get in development 
            if (user == null)
                throw new UnauthorizedAccessException($"No user with uid {requester.userId}");
            
            return user;
        }

        [HttpGet]
        public Task<ActionResult<IList<UserViewBasic>>> GetAsync([FromQuery]UserSearch search)
        {
            return ThrowToAction<IList<UserViewBasic>>(async () =>
            {
                return (await service.SearchAsync(search, GetRequesterNoFail())).Select(x => mapper.Map<UserViewBasic>(x)).ToList();
            });
        }

        [HttpGet("history")]
        public Task<ActionResult<IList<UserView>>> GetHistoryAsync()
        {
            return ThrowToAction<IList<UserView>>(async () =>
            {
                var requester = GetRequesterNoFail();
                var historicUsers = await source.GetRevisions(requester.userId);
                return historicUsers.Select(x => mapper.Map<UserView>(x)).ToList();
            });
        }

        [HttpGet("me")]
        [Authorize]
        public Task<ActionResult<UserView>> Me()
        {
            return ThrowToAction<UserView>(async () => 
            {
                return mapper.Map<UserView>(await GetCurrentUser());
            }); 
        }

        public class UserBasicPost
        {
            public long? avatar {get;set;}

            [MaxLength(256)]
            public string special {get;set;} = null;

            public List<long> hidelist {get;set;} = null;
        }

        protected Task<UserViewFull> SpecialWrite(UserViewFull original, UserViewFull updated, Requester requester)
        {
            //All of these force a public update, which is honestly simpler
            if(original.avatar != updated.avatar || original.username != updated.username || 
                original.special != updated.special)
            {
                return service.WriteAsync(updated, requester);
            }
            else
            {
                return service.WriteSpecialAsync(original.id, requester, p =>
                {
                    //Regardless of if these were updated or not, set them anyway. Shouldn't do any harm...
                    service.Source.SetEmail(p, updated);
                    service.Source.SetHidelist(p, updated);
                    service.Source.SetPassword(p, updated);
                });
            }
        }

        [HttpPut("basic")]
        [Authorize]
        public Task<ActionResult<UserView>> PutBasicAsync([FromBody]UserBasicPost data)
        {
            return ThrowToAction<UserView>(async () => 
            {
                var original = await GetCurrentUser();
                var userView = mapper.Map<UserViewFull>(original);
                var requester = GetRequesterNoFail();

                if(data.avatar != null)
                    userView.avatar = (long)data.avatar;
                if(data.special != null)
                    userView.special = data.special;
                if(data.hidelist != null)
                    userView.hidelist = data.hidelist;

                return mapper.Map<UserView>(await SpecialWrite(original, userView, requester));
            }); 
        }

        protected string GetToken(long id, TimeSpan? expireOverride = null)
        {
            return tokenService.GetToken(new Dictionary<string, string>()
            {
                { Keys.UserIdentifier, id.ToString() },
                { Keys.UserValidate, userValidation.GetUserValidationToken(id) }
            }, expireOverride);
        }

        [HttpPost("authenticate")]
        public async Task<ActionResult<string>> Authenticate([FromBody]UserAuthenticate user)
        {
            UserViewFull userView = null;
            var requester = GetRequesterNoFail();

            if(user.username != null)
                userView = await service.FindByUsernameAsync(user.username, requester);
            else if (user.email != null)
                userView = await service.FindByEmailAsync(user.email, requester);

            //Should this be the same as bad password? eeeehhhh
            if(userView == null)
                return BadRequest("Must provide a valid username or email!");
            
            if(!string.IsNullOrWhiteSpace(userView.registrationKey)) //There's a registration code pending
                return BadRequest("You must confirm your email first");

            if(!Verify(userView, user.password))
                return BadRequest("Password incorrect!");

            TimeSpan? expireOverride = null;

            //Note: this allows users to create ultimate super long tokens for use like... forever. Until we get
            //the token expirer set up, this will be SCARY
            if(user.ExpireSeconds > 0)
                expireOverride = TimeSpan.FromSeconds(user.ExpireSeconds);

            return GetToken(userView.id, expireOverride);
        }

        protected virtual async Task SendEmailAsync(string subjectKey, string bodyKey, string recipient, Dictionary<string, object> replacements)
        {
            var subject = languageService.GetString(subjectKey, "en");
            var body = languageService.GetString(bodyKey, "en", replacements);
            await emailService.SendEmailAsync(new EmailMessage(recipient, subject, body));
        }

        protected bool ValidUsername(string username)
        {
            if(Regex.IsMatch(username, @"[\s,|%*]"))
                return false;
            
            return true;
        }

        //You 'Create' a new user by posting ONLY 'credentials'. This is different than most other types of things...
        //passwords and emails and such shouldn't be included in every view unlike regular models where every field is there.
        [HttpPost("register")]
        public async Task<ActionResult<UserView>> Register([FromBody]UserCredential user)
        {
            //One day, fix these so they're the "standard" bad object request from model validation!!
            //Perhaps do custom validation!
            if(user.username == null)
                return BadRequest("Must provide a username!");
            if(user.email == null)
                return BadRequest("Must provide an email!");
            if(string.IsNullOrWhiteSpace(user.password))
                return BadRequest("Must provide a password!");
            
            if(!ValidUsername(user.username))
                return BadRequest("Bad username: no spaces!");

            var requester = GetRequesterNoFail();

            if(await service.FindByUsernameAsync(user.username, requester) != null || await service.FindByEmailAsync(user.email, requester) != null)
                return BadRequest("This user already seems to exist!");
            
            var fullUser = mapper.Map<UserViewFull>(user);

            SetPassword(fullUser, fullUser.password);
            fullUser.registrationKey = Guid.NewGuid().ToString();

            return await ThrowToAction(async() => mapper.Map<UserView>(await service.WriteAsync(fullUser, requester)));
        }

        public class EmailPost
        {
            public string email {get;set;}
        }

        public class EmailKeyPost
        {
            public string confirmationKey {get;set;}
        }


        [HttpPost("register/sendemail")]
        public async Task<ActionResult> SendRegistrationEmail([FromBody]EmailPost post)
        {
            var requester = GetRequesterNoFail();
            var foundUser = await service.FindByEmailAsync(post.email, requester);

            if(foundUser == null)
                return BadRequest("No user with that email");

            //Now look up the registration code (that's all we need from user)
            var registrationCode = foundUser.registrationKey;

            if(string.IsNullOrWhiteSpace(registrationCode))
                return BadRequest("Nothing to do for user");

            await SendEmailAsync("ConfirmEmailSubject", "ConfirmEmailBody", post.email, new Dictionary<string, object>() {{"confirmCode", foundUser.registrationKey}});

            return Ok("Email sent");
        }

        [HttpPost("register/confirm")]
        public async Task<ActionResult<string>> ConfirmEmail([FromBody]EmailKeyPost post)
        {
            if(string.IsNullOrEmpty(post.confirmationKey))
                return BadRequest("Must provide a confirmation key in the body");

            var requester = GetRequesterNoFail();

            var unconfirmedUser = await service.FindByRegistration(post.confirmationKey, requester);

            if(unconfirmedUser == null)
                return BadRequest("No user found with confirmation key");

            unconfirmedUser.registrationKey = null;

            //Clear out the registration. This is probably not good? These are implementation details, shouldn't be here
            var confirmedUser = await service.WriteAsync(unconfirmedUser, requester);

            return GetToken(confirmedUser.id);
        }

        [HttpPost("passwordreset/sendemail")]
        public async Task<ActionResult> SendPasswordResetEmailAsync([FromBody]EmailPost post)
        {
            var requester = GetRequesterNoFail();
            var foundUser = await service.FindByEmailAsync(post.email, requester);

            if(foundUser == null)
                return BadRequest("No user with that email");

            //Now, add a new password reset code or something.
            var code = new PasswordReset() { UserId = foundUser.id, Key = Guid.NewGuid().ToString() };
            passwordResets.UpdateList(new[] { code });

            await SendEmailAsync("PasswordResetSubject", "PasswordResetBody", post.email, new Dictionary<string, object>() 
            {
                {"resetCode", code.Key},
                {"resetTime", config.PasswordResetExpire}
            });

            return Ok("Email sent");
        }

        public class PasswordResetPost : UserCredential
        {
            public string resetKey {get;set;}
        }

        [HttpPost("passwordreset")]
        public async Task<ActionResult<string>> PasswordResetAsync([FromBody]PasswordResetPost post)
        {
            if(string.IsNullOrEmpty(post.resetKey))
                return BadRequest("Must provide a reset key in the body");

            var reset = passwordResets.DecayList(config.PasswordResetExpire).Where(x => x.Key == post.resetKey && x.Valid).SingleOrDefault();

            if(reset == null)
                return BadRequest("Invalid password reset key");

            var self = new Requester() { userId = reset.UserId };
            var original = await service.FindByIdAsync(reset.UserId, self);
            var user = mapper.Map<UserViewFull>(original);

            if(user == null)
                return BadRequest("No user found for password reset, this SHOULD NOT HAPPEN!");

            SetPassword(user, post.password);
            await SpecialWrite(original, user, self); //Not using return, don't need to map

            reset.Valid = false;
            passwordResets.UpdateList(new[] { reset });

            return GetToken(user.id);
        }

        protected bool Verify(UserViewFull user, string password)
        {
            //Get hash for given password using old hash to authenticate
            var hash = hashService.GetHash(password, Convert.FromBase64String(user.salt));
            return hash.SequenceEqual(Convert.FromBase64String(user.password));
        }

        protected void SetPassword(UserViewFull user, string newPassword)
        {
            var salt = hashService.GetSalt();
            user.salt = Convert.ToBase64String(salt);
            user.password = Convert.ToBase64String(hashService.GetHash(newPassword, salt));
        }

        [HttpPost("sensitive")]
        [Authorize]
        public async Task<ActionResult> SensitiveAsync([FromBody]SensitiveUserChange change)
        {
            var original = await GetCurrentUser();
            var fullUser = mapper.Map<UserViewFull>(original); //await GetCurrentUser();

            var output = new List<string>();

            var requester = GetRequesterNoFail();

            if(!Verify(fullUser, change.oldPassword))
                return BadRequest("Old password incorrect!");

            if(!string.IsNullOrWhiteSpace(change.password))
            {
                SetPassword(fullUser, change.password);
                output.Add("Changed password");
            }

            if(!string.IsNullOrWhiteSpace(change.email))
            {
                if(await service.FindByEmailAsync(change.email, requester) != null)
                    return BadRequest("This email is already taken!");

                fullUser.email = change.email;
                output.Add("Changed email");
            }

            if(!string.IsNullOrWhiteSpace(change.username))
            {
                if(change.username == fullUser.username)
                    return BadRequest("That's your current username!");

                if(!ValidUsername(change.username))
                    return BadRequest("Bad username: no spaces!");

                //If two users come in at the same time and do this without locking, the world will crumble.
                if(await service.FindByUsernameAsync(change.username, requester) != null)
                    return BadRequest("Username already taken!");

                var beginning = DateTime.Now - config.NameChangeRange;

                //Need historic users 
                var historicUsers = (await source.GetRevisions(fullUser.id)).Where(x => x.editDate > beginning);
                var usernames = historicUsers.Select(x => x.username).Append(fullUser.username).Append(change.username).Distinct();

                if(usernames.Count() > config.NameChangesPerTime)
                    return BadRequest($"Too many username changes in the given time: allowed {config.NameChangesPerTime} per {config.NameChangeRange}");
                
                fullUser.username = change.username;
                output.Add("Changed username");
            }

            await SpecialWrite(original, fullUser, requester); //Not using return, don't need to map

            return Ok(string.Join(", ", output));
        }

        [HttpPost("invalidatealltokens")]
        [Authorize]
        public ActionResult InvalidateAllTokens()
        {
            var requester = GetRequesterNoFail();
            userValidation.NewValidation(requester.userId);
            tempTokenService.InvalidateTokens(requester.userId);
            return Ok("All login tokens invalidated, you will need to login again");
        }
    }
}