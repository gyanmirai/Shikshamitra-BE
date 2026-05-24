using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.ProfessionalManagement;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/professional-management")]
[Authorize]
public class ProfessionalManagementController : AbpControllerBase
{
	private readonly IProfessionalManagementAppService _appService;

	public ProfessionalManagementController(IProfessionalManagementAppService appService)
	{
		_appService = appService;
	}

	[HttpGet("review-cycles")]
	[ProducesResponseType(typeof(ProfessionalReviewCycleListResponse), StatusCodes.Status200OK)]
	public Task<ProfessionalReviewCycleListResponse> GetReviewCyclesAsync(
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(1, 100)] int pageSize = 20,
		[FromQuery] string? search = null)
	{
		return _appService.GetReviewCyclesAsync(page, pageSize, search);
	}

	[HttpPost("review-cycles")]
	[ProducesResponseType(typeof(ProfessionalReviewCycleDto), StatusCodes.Status201Created)]
	public Task<ProfessionalReviewCycleDto> CreateReviewCycleAsync([FromBody] ProfessionalReviewCycleRequest input)
	{
		return _appService.CreateReviewCycleAsync(input);
	}

	[HttpPut("review-cycles/{id}")]
	[ProducesResponseType(typeof(ProfessionalReviewCycleDto), StatusCodes.Status200OK)]
	public Task<ProfessionalReviewCycleDto> UpdateReviewCycleAsync(Guid id, [FromBody] ProfessionalReviewCycleRequest input)
	{
		return _appService.UpdateReviewCycleAsync(id, input);
	}

	[HttpGet("review-cycles/{id}/reviews")]
	[ProducesResponseType(typeof(ProfessionalReviewListResponse), StatusCodes.Status200OK)]
	public Task<ProfessionalReviewListResponse> GetCycleReviewsAsync(Guid id, [FromQuery] string? search = null)
	{
		return _appService.GetCycleReviewsAsync(id, search);
	}

	[HttpPut("reviews/{id}/draft")]
	[ProducesResponseType(typeof(ProfessionalReviewDto), StatusCodes.Status200OK)]
	public Task<ProfessionalReviewDto> SaveDraftAsync(Guid id, [FromBody] ProfessionalReviewSaveRequest input)
	{
		return _appService.SaveDraftAsync(id, input);
	}

	[HttpPost("reviews/{id}/submit")]
	[ProducesResponseType(typeof(ProfessionalReviewDto), StatusCodes.Status200OK)]
	public Task<ProfessionalReviewDto> SubmitAsync(Guid id, [FromBody] ProfessionalReviewSaveRequest input)
	{
		return _appService.SubmitAsync(id, input);
	}

	[HttpGet("employees/{employeeId}/review-history")]
	[ProducesResponseType(typeof(ProfessionalReviewListResponse), StatusCodes.Status200OK)]
	public Task<ProfessionalReviewListResponse> GetEmployeeReviewHistoryAsync(Guid employeeId)
	{
		return _appService.GetEmployeeReviewHistoryAsync(employeeId);
	}
}
