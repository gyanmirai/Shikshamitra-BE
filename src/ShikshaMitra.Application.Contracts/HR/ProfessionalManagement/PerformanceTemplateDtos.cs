using System;
using System.Collections.Generic;
using FluentValidation;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceTemplateDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public PerformanceStaffCategory StaffCategory { get; set; }
	public string? RoleName { get; set; }
	public int RatingScaleMax { get; set; } = 5;
	public bool AllowNotApplicable { get; set; } = true;
	public PerformanceTemplateStatus Status { get; set; }
	public string? Description { get; set; }
	public List<PerformanceTemplateSectionDto> Sections { get; set; } = new();
}

public class PerformanceTemplateSectionDto
{
	public Guid Id { get; set; }
	public Guid TemplateId { get; set; }
	public string Name { get; set; } = string.Empty;
	public decimal Weight { get; set; }
	public int DisplayOrder { get; set; }
	public List<PerformanceTemplateCriterionDto> Criteria { get; set; } = new();
}

public class PerformanceTemplateCriterionDto
{
	public Guid Id { get; set; }
	public Guid TemplateId { get; set; }
	public Guid SectionId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public decimal Weight { get; set; }
	public bool EvidenceRequired { get; set; }
	public int DisplayOrder { get; set; }
}

public class PerformanceTemplateRequest
{
	public string Name { get; set; } = string.Empty;
	public PerformanceStaffCategory StaffCategory { get; set; }
	public string? RoleName { get; set; }
	public int RatingScaleMax { get; set; } = 5;
	public bool AllowNotApplicable { get; set; } = true;
	public string? Description { get; set; }
	public List<PerformanceTemplateSectionRequest> Sections { get; set; } = new();
}

public class PerformanceTemplateRequestValidator : AbstractValidator<PerformanceTemplateRequest>
{
	public PerformanceTemplateRequestValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.Length(2, 160);
		RuleFor(x => x.RoleName)
			.MaximumLength(120);
		RuleFor(x => x.RatingScaleMax)
			.InclusiveBetween(1, 10);
		RuleFor(x => x.Description)
			.MaximumLength(1000);
		RuleForEach(x => x.Sections)
			.SetValidator(new PerformanceTemplateSectionRequestValidator());
	}
}

public class PerformanceTemplateSectionRequest
{
	public Guid? Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public decimal Weight { get; set; }
	public int DisplayOrder { get; set; }
	public List<PerformanceTemplateCriterionRequest> Criteria { get; set; } = new();
}

public class PerformanceTemplateSectionRequestValidator : AbstractValidator<PerformanceTemplateSectionRequest>
{
	public PerformanceTemplateSectionRequestValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.Length(2, 160);
		RuleFor(x => x.Weight)
			.InclusiveBetween(0, 100);
		RuleForEach(x => x.Criteria)
			.SetValidator(new PerformanceTemplateCriterionRequestValidator());
	}
}

public class PerformanceTemplateCriterionRequest
{
	public Guid? Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public decimal Weight { get; set; }
	public bool EvidenceRequired { get; set; }
	public int DisplayOrder { get; set; }
}

public class PerformanceTemplateCriterionRequestValidator : AbstractValidator<PerformanceTemplateCriterionRequest>
{
	public PerformanceTemplateCriterionRequestValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.Length(2, 200);
		RuleFor(x => x.Description)
			.MaximumLength(1000);
		RuleFor(x => x.Weight)
			.InclusiveBetween(0, 100);
	}
}

public class PerformanceTemplateListResponse
{
	public int TotalCount { get; set; }
	public List<PerformanceTemplateDto> Items { get; set; } = new();
}

public class DefaultPerformanceTemplatesResponse
{
	public List<PerformanceTemplateRequest> Items { get; set; } = new();
}
