namespace contentapi.oldsbs;

public class Categories
{
    public long cid {get;set;} //primary key
    public long? pcid {get;set;} //category parent (hierarchy)
    public string name {get;set;} = "";
    public string? description {get;set;}   //Note: all empty
    public long permissions {get;set;}      //Seemingly not used (all 0)
    public bool alwaysavailable {get;set;}  //Same (all 0)
}