namespace ThriftMediaService.Models;

public class Media
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MediaType MediaType { get; set; }
    public string Url { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
