using System;
using System.Collections.Generic;
using FluentValidation;

namespace ShikshaMitra.HR.Reports;

public class HrReportTemplateDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public string DataSource { get; set; } = string.Empty;
	public string Visibility { get; set; } = HrReportTemplateVisibility.Private;
	public string Status { get; set; } = HrReportTemplateStatus.Draft;
	public Guid OwnerUserId { get; set; }
	public HrReportRequestDto Definition { get; set; } = new();
	public DateTime CreationTime { get; set; }
	public DateTime? LastModificationTime { get; set; }
}

public class HrReportTemplateListDto
{
	public List<HrReportTemplateDto> Items { get; set; } = new();
}

public class HrReportTemplateRequestDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string Category { get; set; } = string.Empty;
	public string DataSource { get; set; } = string.Empty;
	public string Visibility { get; set; } = HrReportTemplateVisibility.Private;
	public string Status { get; set; } = HrReportTemplateStatus.Draft;
	public HrReportRequestDto Definition { get; set; } = new();
}

public class HrReportTemplateRequestDtoValidator : AbstractValidator<HrReportTemplateRequestDto>
{
	public HrReportTemplateRequestDtoValidator()
	{
		RuleFor(x => x.Name).NotEmpty();
		RuleFor(x => x.Category).NotEmpty();
		RuleFor(x => x.DataSource).NotEmpty();
		RuleFor(x => x.Definition).NotNull().SetValidator(new HrReportRequestDtoValidator());
	}
}
