using System;

namespace ShikshaMitra.Academic.Curriculum.Settings;

public sealed record SubjectCreation(string Name, string Color, Guid EducationMediumId,
    string? DisplayName, string? Category, string? Icon, string? Description,
    SubjectMasterStatus Status, int? SortOrder);
