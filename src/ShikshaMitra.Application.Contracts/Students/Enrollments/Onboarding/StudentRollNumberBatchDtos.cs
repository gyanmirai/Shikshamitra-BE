using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class StudentRollScopeInput
{
	public Guid AcademicYearId { get; set; }
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
}
public class PreviewStudentRollBatchInput : StudentRollScopeInput
{
	public string Operation { get; set; } = "assign-missing";
	public List<Guid>? CaseIds { get; set; }
}
public class SubmitStudentRollBatchInput
{
	public string ConcurrencyStamp { get; set; } = "";
	public string IdempotencyKey { get; set; } = "";
	public string Reason { get; set; } = "";
}
public class DecideStudentRollBatchInput
{
	public string ConcurrencyStamp { get; set; } = "";
	public bool Approve { get; set; }
	public string Reason { get; set; } = "";
}
public class StudentRollBatchRowDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = "";
	public int? CurrentRollNumber { get; set; }
	public int? ProposedRollNumber { get; set; }
	public string? Issue { get; set; }
}
public class StudentRollNumberBatchDto : EntityDto<Guid>
{
	public DateTime CreationTime { get; set; }
	public string ScopeLabel { get; set; } = "";
	public string ConcurrencyStamp { get; set; } = "";
	public Guid AcademicYearId { get; set; }
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
	public string Operation { get; set; } = "";
	public string Status { get; set; } = "";
	public string Reason { get; set; } = "";
	public string Grouping { get; set; } = "mixed";
	public bool ReasonRequired { get; set; }
	public IReadOnlyList<Guid>? CaseIds { get; set; }
	public bool ApprovalRequired { get; set; }
	public IReadOnlyList<IReadOnlyList<string>> ApprovalStages { get; set; } = [];
	public Guid? PolicyVersionId { get; set; }
	public string? IdempotencyKey { get; set; }
	public string SortBy { get; set; } = "studentName";
	public string NameFormat { get; set; } = "first-name-first";
	public string SortDirection { get; set; } = "ascending";
	public int TotalCount { get; set; }
	public int BlockedCount { get; set; }
	public bool CanSubmit { get; set; }
	public bool CanDecide { get; set; }
	public bool CanApply { get; set; }
	public DateTimeOffset? AppliedAt { get; set; }
	public IReadOnlyList<StudentRollBatchRowDto> Items { get; set; } = [];
}
public class StudentRollBatchContextDto
{
	public string ScopeLabel { get; set; } = "";
	public int UnassignedCount { get; set; }
	public bool CanAssignMissing { get; set; }
	public bool CanRenumber { get; set; }
	public string? BlockedReason { get; set; }
	public IReadOnlyList<StudentRollNumberBatchDto> Batches { get; set; } = [];
}
public class PreviewStudentRollBatchInputValidator : AbstractValidator<PreviewStudentRollBatchInput>
{
	public PreviewStudentRollBatchInputValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty(); RuleFor(x => x.StandardId).NotEmpty(); RuleFor(x => x.DivisionId).NotEmpty();
		RuleFor(x => x.Operation).Must(x => x is "assign-missing" or "renumber");
		RuleFor(x => x.CaseIds).Must(x => x == null || x.Count is > 0 and <= 100 && x.All(id => id != Guid.Empty) && x.Distinct().Count() == x.Count);
		RuleFor(x => x.Operation).Equal("assign-missing").When(x => x.CaseIds != null);
	}
}
public class SubmitStudentRollBatchInputValidator : AbstractValidator<SubmitStudentRollBatchInput>
{
	public SubmitStudentRollBatchInputValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty().MaximumLength(100);
		RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
		RuleFor(x => x.Reason).NotNull().MaximumLength(1000);
	}
}
public class DecideStudentRollBatchInputValidator : AbstractValidator<DecideStudentRollBatchInput>
{
	public DecideStudentRollBatchInputValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty().MaximumLength(100); RuleFor(x => x.Reason).NotNull().MaximumLength(1000);
	}
}
public interface IStudentRollNumberBatchAppService : IApplicationService
{
	Task<StudentRollBatchContextDto> GetContextAsync(StudentRollScopeInput input);
	Task<StudentRollNumberBatchDto> PreviewAsync(PreviewStudentRollBatchInput input);
	Task<StudentRollNumberBatchDto> GetAsync(Guid id, PagedResultRequestDto input);
	Task<StudentRollNumberBatchDto> SubmitAsync(Guid id, SubmitStudentRollBatchInput input);
	Task<StudentRollNumberBatchDto> DecideAsync(Guid id, DecideStudentRollBatchInput input);
	Task<StudentRollNumberBatchDto> ApplyAsync(Guid id, SubmitStudentRollBatchInput input);
}
