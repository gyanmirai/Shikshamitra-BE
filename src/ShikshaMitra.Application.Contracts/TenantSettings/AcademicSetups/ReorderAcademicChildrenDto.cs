using System;
using System.Collections.Generic;
using FluentValidation;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

public class ReorderAcademicChildrenDto
{
	public List<Guid> Ids { get; set; } = [];
}

public class ReorderAcademicChildrenDtoValidator : AbstractValidator<ReorderAcademicChildrenDto>
{
	public ReorderAcademicChildrenDtoValidator()
	{
		RuleFor(x => x.Ids).NotNull().NotEmpty();
		RuleForEach(x => x.Ids).NotEmpty();
	}
}
