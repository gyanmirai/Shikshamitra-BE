using System;
using System.Collections.Generic;
using FluentValidation;

namespace ShikshaMitra.HR.Reports;

public class HrReportScheduleDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public Guid? TemplateId { get; set; }
	public string Frequency { get; set; } = HrReportScheduleFrequency.Monthly;
	public string DeliveryMode { get; set; } = HrReportDeliveryMode.InApp;
	public string Status { get; set; } = HrReportScheduleStatus.Active;
	public List<Guid> RecipientUserIds { get; set; } = new();
	public HrReportRequestDto Definition { get; set; } = new();
	public DateTime NextRunAt { get; set; }
	public DateTime? LastRunAt { get; set; }
	public Guid OwnerUserId { get; set; }
	public DateTime CreationTime { get; set; }
	public DateTime? LastModificationTime { get; set; }
}

public class HrReportScheduleRequestDto
{
	public string Name { get; set; } = string.Empty;
	public Guid? TemplateId { get; set; }
	public string Frequency { get; set; } = HrReportScheduleFrequency.Monthly;
	public string DeliveryMode { get; set; } = HrReportDeliveryMode.InApp;
	public List<Guid> RecipientUserIds { get; set; } = new();
	public HrReportRequestDto Definition { get; set; } = new();
	public DateTime? FirstRunAt { get; set; }
}

public class HrReportScheduleListDto
{
	public List<HrReportScheduleDto> Items { get; set; } = new();
}

public class HrReportScheduleRunResponseDto
{
	public Guid ScheduleId { get; set; }
	public Guid JobId { get; set; }
	public DateTime NextRunAt { get; set; }
}

public class HrReportScheduleRequestDtoValidator : AbstractValidator<HrReportScheduleRequestDto>
{
	public HrReportScheduleRequestDtoValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
		RuleFor(x => x.Frequency).Must(x =>
			x == HrReportScheduleFrequency.Daily ||
			x == HrReportScheduleFrequency.Weekly ||
			x == HrReportScheduleFrequency.Monthly);
		RuleFor(x => x.DeliveryMode).Must(x =>
			x == HrReportDeliveryMode.InApp ||
			x == HrReportDeliveryMode.InAppAndEmail);
		RuleFor(x => x.Definition.ReportType).NotEmpty();
	}
}
