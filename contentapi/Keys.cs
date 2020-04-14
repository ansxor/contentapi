using System;
using System.Linq;

namespace contentapi
{
    public class Keys
    {
        public string UserIdentifier => "uid";
        public string ActiveIdentifier => "a";

        //General stuff
        public string CreatorRelation => "rc";
        public string ParentRelation => "rp";
        public string StandInRelation => ">";

        //Access stuff (I hate that these are individual, hopefully this won't impact performance too bad...)
        public string CreateAccess => "ac";
        public string ReadAccess => "ar";
        public string UpdateAccess => "au";
        public string DeleteAccess => "ad";

        //Overall types of entities
        public string UserType => "tu";
        public string CategoryType => "tc";
        public string ContentType => "tp"; //p for page/post
        public string CommentType => "ta"; //a for addendum or annotation
        public string StandInType => "ts";

        //Historic keys
        public string HistoryKey => "_";

        //User stuff 
        public string EmailKey => "se";
        public string PasswordHashKey => "sph";
        public string PasswordSaltKey => "sps";
        public string RegistrationCodeKey => "srk";

        //Category stuff

        public void EnsureAllUnique()
        {
            var properties = GetType().GetProperties();
            var values = properties.Select(x => (string)x.GetValue(this));

            if(values.Distinct().Count() != values.Count())
                throw new InvalidOperationException("There is a duplicate key!");
        }
    }
}