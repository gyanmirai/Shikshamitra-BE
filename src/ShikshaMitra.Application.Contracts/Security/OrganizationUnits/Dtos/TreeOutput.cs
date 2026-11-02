using System;
using System.Collections.Generic;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class TreeOutput
{
    public string Title { get; set; } = null!; // The response mapper populates these members after construction.

    public Guid Key { get; set; }

    public List<TreeOutput> Children { get; set; } = null!;
}