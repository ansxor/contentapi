using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

[ResultFor(RequestType.content)]
[SelectFrom("content AS main")]
[WriteAs(typeof(Db.Content))]
public class ContentView : IIdView
{
    public const string NaturalCommentQuery = "deleted = 0 and module IS NULL";

    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.Preserve)]
    public long id { get; set; }

    //Entirely not writable
    [FieldSelect]
    public bool deleted { get; set; }

    [FieldSelect]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long createUserId { get; set; }

    [FieldSelect]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    //Entirely not writable
    [FieldSelect]
    public InternalContentType internalType {get;set;}

    [FieldSelect]
    [Writable]
    public string name { get; set; } = "";

    [FieldSelect]
    [Writable]
    public long parentId { get; set; }


    //NOtice the lack of "FieldSelect": this does NOT come from the standard query
    [NoQuery]
    [Writable]
    [Expensive(2)]
    public Dictionary<long, string> permissions {get;set;} = new Dictionary<long, string>();

    //NOTE: values will have some content-specific things!
    [NoQuery]
    [Writable]
    [Expensive(2)]
    public Dictionary<string, string> values {get;set;} = new Dictionary<string, string>();

    //Although these are NOT searchable with a standard search system, they do at least have 
    //macros to let you search. Essentially, any field that is a "list" or something else
    //will not be searchable.
    [NoQuery]
    [Writable]
    [Expensive(2)]
    public List<string> keywords {get;set;} = new List<string>();

    [NoQuery]
    [Expensive(2)]
    public Dictionary<VoteType, int> votes {get;set;} = new Dictionary<VoteType, int>();

    [Expensive(1)]
    [FieldSelect("select createDate from comments where main.id = contentId and " + NaturalCommentQuery + " order by id desc limit 1")]
    public DateTime lastCommentDate {get;set;}

    [Expensive(1)]
    [FieldSelect("select id from comments where main.id = contentId and " + NaturalCommentQuery + " order by id desc limit 1")]
    public long lastCommentId {get;set;}

    [Expensive(1)]
    [FieldSelect("select count(*) from comments where main.id = contentId and " + NaturalCommentQuery)]
    public int commentCount {get;set;}

    [Expensive(1)]
    [FieldSelect("select count(*) from content_watches where main.id = contentId")]
    public int watchCount {get;set;}

    [Expensive(1)]
    [FieldSelect("select createDate from content_history where main.id = contentId order by id desc limit 1")]
    public DateTime lastRevisionDate {get;set;}

    [Expensive(1)]
    [FieldSelect("select id from content_history where main.id = contentId order by id desc limit 1")]
    public long lastRevisionId {get;set;}
}