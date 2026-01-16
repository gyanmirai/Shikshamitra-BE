using System;
using System.Collections.Generic;

namespace ShikshaMitra.Groups;

public class AcademicRule
{
    public List<Guid> Boards { get; set; } = new();
    public List<Guid> Mediums { get; set; } = new();
    public List<Guid> Stages { get; set; } = new();
    public List<Guid> Classes { get; set; } = new();
    public List<Guid> Divisions { get; set; } = new();
}
