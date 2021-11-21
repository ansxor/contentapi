using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace contentapi.Views
{
    //public class AggregateVoteData
    //{
    //    public Dictionary<string, int> all {get;set;}
    //    public List<VoteView> @public {get;set;}
    //}

    //public class ContentViewFull : ContentView
    //{
    //    public List<VoteData> rawVotes {get;set;}
    //}

    public class AboutView
    {
        public SimpleAggregateData comments {get;set;} = new SimpleAggregateData();
        public SimpleAggregateData watches {get;set;} = new SimpleAggregateData();
        public Dictionary<string, SimpleAggregateData> votes {get;set;} = new Dictionary<string, SimpleAggregateData>();

        public bool watching {get;set;}
        public string myVote {get;set;}
    }

    public class ContentView : StandardView
    {
        [Required]
        [StringLength(128, MinimumLength=1)]
        public string name {get;set;}

        [Required]
        [StringLength(65536, MinimumLength = 2)]
        public string content {get;set;}

        public string type {get;set;}

        protected override bool EqualsSelf(object obj)
        {
            var o = (ContentView)obj;
            return base.EqualsSelf(obj) && o.keywords.OrderBy(x => x).SequenceEqual(keywords.OrderBy(x => x));
        }

        [IgnoreCompare]
        public AboutView about {get;set;} = new AboutView();
        //public AggregateVoteData votes {get;set;} = new AggregateVoteData(); //Always have at least an empty vote data
    }
}