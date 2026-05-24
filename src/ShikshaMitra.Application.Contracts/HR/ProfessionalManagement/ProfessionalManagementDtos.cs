using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalReviewCycleDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public DateTime PeriodStart { get; set; }
	public DateTime PeriodEnd { get; set; }
	public ProfessionalReviewCycleStatus Status { get; set; }
}

public class ProfessionalReviewCycleRequest : IValidatableObject
{
	[Required]
	[StringLength(120, MinimumLength = 2)]
	public string Name { get; set; } = string.Empty;

	[Required]
	public DateTime PeriodStart { get; set; }

	[Required]
	public DateTime PeriodEnd { get; set; }

	public ProfessionalReviewCycleStatus Status { get; set; } = ProfessionalReviewCycleStatus.Draft;

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (PeriodEnd.Date < PeriodStart.Date)
		{
			yield return new ValidationResult("Period end must be on or after period start.", new[] { nameof(PeriodEnd) });
		}
	}
}

public class ProfessionalReviewCycleListResponse
{
	public int TotalCount { get; set; }
	public List<ProfessionalReviewCycleDto> Items { get; set; } = new();
}

public class ProfessionalReviewDto
{
	public Guid Id { get; set; }
	public Guid CycleId { get; set; }
	public string CycleName { get; set; } = string.Empty;
	public Guid EmployeeId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string? EmployeeIdentifier { get; set; }
	public string? EmployeeRole { get; set; }
	public Guid? ReviewerUserId { get; set; }
	public int CommunicationScore { get; set; }
	public int ProblemSolvingScore { get; set; }
	public string? Comments { get; set; }
	public string? Feedback { get; set; }
	public ProfessionalReviewStatus Status { get; set; }
	public DateTime? SubmittedAt { get; set; }
}

public class ProfessionalReviewListResponse
{
	public int TotalCount { get; set; }
	public List<ProfessionalReviewDto> Items { get; set; } = new();
}

public class ProfessionalReviewSaveRequest
{
	[Range(0, 5)]
	public int CommunicationScore { get; set; }

	[Range(0, 5)]
	public int ProblemSolvingScore { get; set; }

	[StringLength(2000)]
	public string? Comments { get; set; }

	[StringLength(2000)]
	public string? Feedback { get; set; }
}

public interface IProfessionalManagementAppService : IApplicationService
{
	Task<ProfessionalReviewCycleListResponse> GetReviewCyclesAsync(int page = 1, int pageSize = 20, string? search = null);
	Task<ProfessionalReviewCycleDto> CreateReviewCycleAsync(ProfessionalReviewCycleRequest input);
	Task<ProfessionalReviewCycleDto> UpdateReviewCycleAsync(Guid id, ProfessionalReviewCycleRequest input);
	Task<ProfessionalReviewListResponse> GetCycleReviewsAsync(Guid id, string? search = null);
	Task<ProfessionalReviewDto> SaveDraftAsync(Guid id, ProfessionalReviewSaveRequest input);
	Task<ProfessionalReviewDto> SubmitAsync(Guid id, ProfessionalReviewSaveRequest input);
	Task<ProfessionalReviewListResponse> GetEmployeeReviewHistoryAsync(Guid employeeId);
}
