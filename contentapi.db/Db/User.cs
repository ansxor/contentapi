using System;
using System.Collections.Generic;
using System.Linq;
using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("users")]
    public class User
    {
        [Key]
        public long id { get; set; }
        public string username { get; set; } = "";
        public long avatar { get; set; }
        public string special { get; set; } //Special is nullable!
        public bool super { get; set; }
        public UserType type { get; set; }
        public DateTime createDate { get; set; }
        public DateTime editDate { get; set; }
        public string email { get; set; } = "";
        public string hidelist { get; set; } //Hidelist is nullable!
        public string password { get; set; } = ""; //Don't worry, just the salted hash
        public string salt { get; set; } = "";
        public string registrationKey { get; set; } //Registration key is nullable!

        [Write(false)]
        public List<long> hideListParsed
        {
            get
            {
                if (hidelist == null) return new List<long>();
                return hidelist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(x => long.Parse(x.Trim())).ToList();
            }
        }
    }
}
