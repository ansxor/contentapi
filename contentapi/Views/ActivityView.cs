using System;
using System.Collections.Generic;

namespace contentapi.Views
{
    public class ActivityView : BaseView
    {
        public DateTime date {get;set;}

        public long userId {get;set;}
        public long contentId {get;set;}

        public string type {get;set;}
        public string contentType {get;set;}
        public string action {get;set;}
        public string extra {get;set;}
    }

    public class ActivityAggregateView : StandardAggregateData, IIdView
    {
        public long id {get;set;} //This is PARENT id
        //public int count {get;set;}
        //public DateTime? firstDate {get;set;}
        //public DateTime? lastDate {get;set;}
        //public long lastId {get;set;}
        //public List<long> userIds {get;set;}
        //public Dictionary<string,string> userActions {get;set;}
    }
}