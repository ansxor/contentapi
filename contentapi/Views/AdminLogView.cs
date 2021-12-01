
using contentapi.Search;

namespace contentapi.Views;

[FromDb(typeof(Db.AdminLog))]
[FromRequest(RequestType.adminlog)]
public class AdminLog
{
    [Searchable]
    public long id {get;set;}
    [Searchable]
    public string type {get;set;} = "";
    [Searchable]
    public string? text {get;set;}
    [Searchable]
    public DateTime createDate {get;set;}
    [Searchable]
    public long initiator {get;set;}
    [Searchable]
    public long target {get;set;}
}