using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace contentapi.Views
{
    public class CategoryView : BasePermissionView, IEditView, IPermissionView, IValueVlue
    {
        public long parentId { get; set; }

        public DateTime createDate { get; set;}
        public DateTime editDate { get;set;}
        public long createUserId { get;set;} 
        public long editUserId { get;set;}

        public string myPerms { get; set; }

        [Required]
        [StringLength(128, MinimumLength = 1)]
        public string name {get;set;}

        [StringLength(2048)]
        public string description {get;set;}

        public List<long> localSupers {get;set;} = new List<long>();

        protected override bool EqualsSelf(object obj)
        {
            var o = (CategoryView)obj;
            return base.EqualsSelf(obj) && o.localSupers.OrderBy(x => x).SequenceEqual(localSupers.OrderBy(x => x));
        }
    }
}