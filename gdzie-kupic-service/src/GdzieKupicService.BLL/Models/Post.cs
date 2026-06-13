namespace GdzieKupicService.Core.Models;

internal class Post
{
    public Guid Id { get; }
    public Guid OwnerId { get; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public string ProductName { get; set; }
    public Location Location { get; set; }

    public Post(Guid id, Guid ownerId, string title, string description, string productName, Location location)
    {
        this.Id = id;
        this.OwnerId = ownerId;
        this.Title = title;
        this.Description = description;
        this.ProductName = productName;
        this.Location = location;
    }

    public Post(Guid id, Guid ownerId)
    {
        this.Id = id;
        this.OwnerId = ownerId;
    }
}