using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public class UserSearch : BaseHistorySearch
    {
        public string UsernameLike {get;set;}
        public List<string> Usernames {get;set;} = new List<string>();
    }

    public class UserViewSourceProfile : Profile
    {
        public UserViewSourceProfile()
        {
            CreateMap<UserSearch, EntitySearch>()
                .ForMember(x => x.NameLike, o => o.MapFrom(s => s.UsernameLike))
                .ForMember(x => x.Names, o => o.MapFrom(s => s.Usernames));
        }
    }

    public class UserViewSource : BaseEntityViewSource<UserViewFull, EntityPackage, EntityGroup, UserSearch>
    {
        protected IPermissionService service;
        protected BanViewSource banSource;
        //protected 

        public override string EntityType => Keys.UserType;

        public UserViewSource(ILogger<UserViewSource> logger, BaseViewSourceServices services, IPermissionService service, BanViewSource banSource) 
            : base(logger, services)
        { 
            this.service = service;
            this.banSource = banSource;
        }

        public override UserViewFull ToView(EntityPackage user)
        {
            var result = new UserViewFull() 
            { 
                username = user.Entity.name, 
                email = user.GetValue(Keys.EmailKey).value, 
                super = service.IsSuper(user.Entity.id),
                password = user.GetValue(Keys.PasswordHashKey).value,
                salt = user.GetValue(Keys.PasswordSaltKey).value
            };

            this.ApplyToEditView(user, result);

            if(user.HasValue(Keys.UserSpecialKey))
                result.special = user.GetValue(Keys.UserSpecialKey).value;
            if(user.HasValue(Keys.AvatarKey))
                result.avatar = long.Parse(user.GetValue(Keys.AvatarKey).value);
            if(user.HasValue(Keys.RegistrationCodeKey))
                result.registrationKey = user.GetValue(Keys.RegistrationCodeKey).value;
            if(user.HasValue(Keys.UserHideKey))
                result.hidelist = user.GetValue(Keys.UserHideKey).value.Split(",".ToCharArray(),StringSplitOptions.RemoveEmptyEntries).Select(x => long.Parse(x)).ToList();
            
            //Doesn't matter that there are two fields because nobody can set these anyway
            result.ban = banSource.GetCurrentBan(user.Relations);
            result.banned = result.ban != null;

            result.registered = string.IsNullOrWhiteSpace(result.registrationKey);

            return result;
        }

        public void SetAvatar(EntityPackage package, UserViewFull user)
        {
            package.SetGenericValue(Keys.AvatarKey, user.avatar.ToString());
        }

        public void SetSpecial(EntityPackage package, UserViewFull user)
        {                
            package.SetGenericValue(Keys.UserSpecialKey, user.special);
        }

        public void SetEmail(EntityPackage package, UserViewFull user)
        {                
            package.SetGenericValue(Keys.EmailKey, user.email);
        }

        public void SetPassword(EntityPackage package, UserViewFull user)
        {                
            package.SetGenericValue(Keys.PasswordSaltKey, user.salt);
            package.SetGenericValue(Keys.PasswordHashKey, user.password);
        }

        public void SetHidelist(EntityPackage package, UserViewFull user)
        {
            package.SetGenericValue(Keys.UserHideKey, string.Join(",", user.hidelist));
        }

        public override EntityPackage FromView(UserViewFull user)
        {
            var NewValue = new Func<string, string, EntityValue>((k,v) => new EntityValue()
            {
                entityId = user.id,
                createDate = null,
                key = k,
                value = v
            });

            var newUser = this.NewEntity(user.username);
            this.ApplyFromEditView(user, newUser, EntityType);

            //SetUsername(newUser, user);
            SetAvatar(newUser, user);
            SetSpecial(newUser, user);
            SetHidelist(newUser, user);
            SetEmail(newUser, user);
            SetPassword(newUser, user);

            //Can't do anything about super
            //Also ignore the ban lol that's not how it works.
            
            if(!string.IsNullOrWhiteSpace(user.registrationKey))
                newUser.Add(NewValue(Keys.RegistrationCodeKey, user.registrationKey));

            return newUser;
        }
    }
}