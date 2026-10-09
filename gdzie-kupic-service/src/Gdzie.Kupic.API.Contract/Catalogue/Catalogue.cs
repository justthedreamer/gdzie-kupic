namespace Gdzie.Kupic.Service.API.Contract.Catalogue;

public sealed record NameRequest(string Name);

public sealed record TagResponse(Guid Id, string Name, bool IsDisabled);

public sealed record CategoryResponse(Guid Id, string Name, bool IsDisabled, IReadOnlyList<TagResponse> Tags);