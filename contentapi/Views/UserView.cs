using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.User))]
[ForRequest(RequestType.user)]
public class UserView : IIdView
{
    [Searchable]
    public long id {get;set;}

    [Searchable]
    public string username {get;set;} = "";

    [Searchable]
    public long avatar {get;set;}

    public string? special {get;set;}

    [Searchable]
    public string type {get;set;} = "";

    [Searchable]
    public DateTime createDate {get;set;}

    [Searchable]
    public bool super {get;set;}

    [Searchable]
    [FromField("")] //Not a field you can select
    public bool registered {get;set;}

    [FromField("")]
    public List<long> groups {get;set;} = new List<long>();
}