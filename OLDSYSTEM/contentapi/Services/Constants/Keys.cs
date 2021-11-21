using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace contentapi.Services.Constants
{
    public static class Keys
    {
        public const string UserIdentifier = "uid";
        public const string UserValidate = "vl";


        //Symbolic key stuff (these are all prepended to something else)
        public const string AssociatedValueKey = "@";
        public const string VariableKey = "v:";
        public const string HistoryKey = "_";
        public const string ActivityKey = ".";
        public const string ModuleMessageKey = "M";
        public const string KeywordKey = "#";

        public const string BanKey = "B";
        public const string BanPublicKey = "P";
        //public const string PublicBanKey = "BP";

        //General Relation keys (just relations, no appending)
        //Creator meaning is twofold: entityid1 is the creator of this content and the value is the editor
        public const string CreatorRelation = "rc"; 
        public const string ParentRelation = "rp";
        public const string HistoryRelation = "rh";
        public const string SuperRelation = "rs";
        public const string WatchRelation = "rw";
        public const string VoteRelation = "rv";

        //I don't know what these are
        public const string WatchDelete = "uwd";
        public const string WatchUpdate = "uwu";  //owo



        //Access stuff (I hate that these are individual, hopefully this won't impact performance too bad...)
        //These are also relations
        public const string CreateAction = "!c";
        public const string ReadAction = "!r";
        public const string UpdateAction = "!u";
        public const string DeleteAction = "!d";


        //Overall types of entities (entity types)
        public const string UserType = "tu";
        public const string CategoryType = "tc";
        public const string ContentType = "tp"; //p for page/post
        public const string FileType = "tf";
        public const string ModuleType = "tm";

        public static Dictionary<string, string> TypeNames = new Dictionary<string, string>()
        {
            { Keys.ContentType, "content"},
            { Keys.CategoryType,  "category" },
            { Keys.UserType,  "user" },
            { Keys.FileType,  "file" },
            { Keys.ModuleType,  "module"}
        };

        //User stuff  (keys for entity values)
        public const string EmailKey = "se";
        public const string PasswordHashKey = "sph";
        public const string PasswordSaltKey = "sps";
        public const string RegistrationCodeKey = "srk";
        public const string AvatarKey = "sa";
        public const string UserSpecialKey = "sus";
        public const string UserHideKey ="suh";
        public const string ReadonlyKeyKey = "sro";
        public const string BucketKey = "sbu";
        public const string QuantizationKey = "sqn";


        public const string ModuleDescriptionKey = "md";


        //Awful hacks
        public const string CommentHack = "Zcc";
        public const string CommentHistoryHack ="Zcu";
        public const string CommentDeleteHack ="Zcd";
        public const string ModuleHack ="Zmm";


        //Chaining?
        public const string ChainCommentDelete = "commentdelete";
        public const string ChainWatchUpdate = "watchupdate";
        public const string ChainWatchDelete = "watchdelete";


        public static void EnsureAllUnique()
        {
            var type = typeof(Keys);
            var properties = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(fi => fi.IsLiteral && !fi.IsInitOnly).ToList();
            var values = properties.Select(x => (string)x.GetRawConstantValue());

            if(values.Count() <= 0)
                throw new InvalidOperationException("There are no values!");

            if(values.Distinct().Count() != values.Count())
                throw new InvalidOperationException("There is a duplicate key!");
        }
    }
}