using System;

namespace ShikshaMitra.Library.Shared;

public class NamedIdDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!; // The response mapper populates these members after construction.
}
