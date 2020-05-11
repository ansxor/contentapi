using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System;

namespace contentapi.Views
{
    public class UserViewBasic : BaseView
    {
        public string username { get; set; }
        public long avatar {get;set;}
    }

    //This is the user as we give them out
    public class UserView : BaseEntityView 
    {
        public string username { get; set; }
        public long avatar {get;set;}

        public string email { get; set; } //This field SHOULDN'T be set unless the user is ourselves.

        //This is actually GET only, don't use it during compare.
        public bool super { get;set; }

        protected override bool EqualsSelf(object obj)
        {
            var o = (UserView)obj;
            return base.EqualsSelf(obj) && o.username == username && o.avatar == avatar && o.email == email;
        }
    }

    public class UserViewFull : UserView
    {
        public string password {get;set;}
        public string salt {get;set;}
        public string registrationKey {get;set;}

        protected override bool EqualsSelf(object obj)
        {
            var o = (UserViewFull)obj;
            return base.EqualsSelf(obj) && o.password == password && o.salt == salt && o.registrationKey == registrationKey;
        }
    }
}