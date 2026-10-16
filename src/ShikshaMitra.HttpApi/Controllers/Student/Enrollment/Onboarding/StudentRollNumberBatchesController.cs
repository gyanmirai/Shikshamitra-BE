using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students;

[ApiController]
[Authorize]
[Route("api/students/roll-number-batches")]
public class StudentRollNumberBatchesController(IStudentRollNumberBatchAppService service) : AbpController
{
	[HttpGet("context")]
	public async Task<Result<StudentRollBatchContextDto>> ContextAsync([FromQuery] StudentRollScopeInput input) => Result.Success(await service.GetContextAsync(input));
	[HttpPost("preview")]
	public async Task<Result<StudentRollNumberBatchDto>> PreviewAsync(PreviewStudentRollBatchInput input) => Result.Success(await service.PreviewAsync(input));
	[HttpGet("{id:guid}")]
	public async Task<Result<StudentRollNumberBatchDto>> GetAsync(Guid id, [FromQuery] PagedResultRequestDto input) => Result.Success(await service.GetAsync(id, input));
	[HttpPost("{id:guid}/submit")]
	public async Task<Result<StudentRollNumberBatchDto>> SubmitAsync(Guid id, SubmitStudentRollBatchInput input) => Result.Success(await service.SubmitAsync(id, input));
	[HttpPost("{id:guid}/decision")]
	public async Task<Result<StudentRollNumberBatchDto>> DecideAsync(Guid id, DecideStudentRollBatchInput input) => Result.Success(await service.DecideAsync(id, input));
	[HttpPost("{id:guid}/apply")]
	public async Task<Result<StudentRollNumberBatchDto>> ApplyAsync(Guid id, SubmitStudentRollBatchInput input) => Result.Success(await service.ApplyAsync(id, input));
}
