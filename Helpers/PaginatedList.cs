namespace SignalR_Demo.Helpers;

public sealed class PaginatedList<T>
{
    public List<T> Items{get;set;} = [];
    public int Page {get;set;}
    public int PageSize {get;set;}
    public int TotalCount {get;set;}
    public bool HasNextPage {get;set;}
}
