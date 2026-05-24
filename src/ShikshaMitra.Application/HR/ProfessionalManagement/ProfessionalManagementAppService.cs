using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.ProfessionalManagement;

[Authorize(ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View)]
public class ProfessionalManagementAppService : ApplicationService, IProfessionalManagementAppService
{
	private const int MaxPageSize = 100;

	private readonly IRepository<ProfessionalReviewCycle, Guid> _cycleRepository;
	private readonly IRepository<ProfessionalReview, Guid> _reviewRepository;
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly IGuidGenerator _guidGenerator;

	public ProfessionalManagementAppService(
		IRepository<ProfessionalReviewCycle, Guid> cycleRepository,
		IRepository<ProfessionalReview, Guid> reviewRepository,
		IRepository<Employee, Guid> employeeRepository,
		IGuidGenerator guidGenerator)
	{
		_cycleRepository = cycleRepository;
		_reviewRepository = reviewRepository;
		_employeeRepository = employeeRepository;
		_guidGenerator = guidGenerator;
	}

	public async Task<ProfessionalReviewCycleListResponse> GetReviewCyclesAsync(int page = 1, int pageSize = 20, string? search = null)
	{
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

		var query = await _cycleRepository.GetQueryableAsync();
		query = query.WhereIf(!string.IsNullOrWhiteSpace(search), x => x.Name.Contains(search!));

		var totalCount = await AsyncExecuter.CountAsync(query);
		var cycles = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.PeriodStart)
				.Skip((page - 1) * pageSize)
				.Take(pageSize));

		return new ProfessionalReviewCycleListResponse
		{
			TotalCount = totalCount,
			Items = cycles.Select(MapCycle).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add)]
	public async Task<ProfessionalReviewCycleDto> CreateReviewCycleAsync(ProfessionalReviewCycleRequest input)
	{
		ValidateCycle(input);

		var cycle = new ProfessionalReviewCycle(
			_guidGenerator.Create(),
			CurrentTenant.Id,
			input.Name,
			input.PeriodStart,
			input.PeriodEnd,
			input.Status);

		await _cycleRepository.InsertAsync(cycle, autoSave: true);
		await EnsureCycleReviewsAsync(cycle.Id);
		return MapCycle(cycle);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add)]
	public async Task<ProfessionalReviewCycleDto> UpdateReviewCycleAsync(Guid id, ProfessionalReviewCycleRequest input)
	{
		ValidateCycle(input);

		var cycle = await _cycleRepository.GetAsync(id);
		cycle.Update(input.Name, input.PeriodStart, input.PeriodEnd, input.Status);
		await _cycleRepository.UpdateAsync(cycle, autoSave: true);
		await EnsureCycleReviewsAsync(cycle.Id);
		return MapCycle(cycle);
	}

	public async Task<ProfessionalReviewListResponse> GetCycleReviewsAsync(Guid id, string? search = null)
	{
		var cycle = await _cycleRepository.GetAsync(id);
		await EnsureCycleReviewsAsync(id);

		var reviews = await GetReviewsForCycleAsync(id);
		var employeeIds = reviews.Select(x => x.EmployeeId).Distinct().ToList();
		var employees = await GetEmployeesByIdsAsync(employeeIds);

		var mapped = reviews.Select(x => MapReview(x, cycle, employees.GetValueOrDefault(x.EmployeeId))).ToList();
		if (!string.IsNullOrWhiteSpace(search))
		{
			mapped = mapped
				.Where(x =>
					x.EmployeeName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
					(x.EmployeeIdentifier?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
				.ToList();
		}

		return new ProfessionalReviewListResponse
		{
			TotalCount = mapped.Count,
			Items = mapped
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add)]
	public async Task<ProfessionalReviewDto> SaveDraftAsync(Guid id, ProfessionalReviewSaveRequest input)
	{
		var review = await _reviewRepository.GetAsync(id);
		review.SaveDraft(input.CommunicationScore, input.ProblemSolvingScore, input.Comments, input.Feedback, CurrentUser.Id);
		await _reviewRepository.UpdateAsync(review, autoSave: true);
		return await MapReviewWithCycleAsync(review);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add)]
	public async Task<ProfessionalReviewDto> SubmitAsync(Guid id, ProfessionalReviewSaveRequest input)
	{
		var review = await _reviewRepository.GetAsync(id);
		review.Submit(input.CommunicationScore, input.ProblemSolvingScore, input.Comments, input.Feedback, CurrentUser.Id, Clock.Now);
		await _reviewRepository.UpdateAsync(review, autoSave: true);
		return await MapReviewWithCycleAsync(review);
	}

	public async Task<ProfessionalReviewListResponse> GetEmployeeReviewHistoryAsync(Guid employeeId)
	{
		var reviewQuery = await _reviewRepository.GetQueryableAsync();
		var reviews = await AsyncExecuter.ToListAsync(
			reviewQuery
				.Where(x => x.EmployeeId == employeeId)
				.OrderByDescending(x => x.SubmittedAt ?? x.LastModificationTime ?? x.CreationTime));

		var cycleIds = reviews.Select(x => x.CycleId).Distinct().ToList();
		var cycleQuery = await _cycleRepository.GetQueryableAsync();
		var cycles = await AsyncExecuter.ToListAsync(cycleQuery.Where(x => cycleIds.Contains(x.Id)));
		var cycleMap = cycles.ToDictionary(x => x.Id);
		var employee = await _employeeRepository.FindAsync(employeeId);

		return new ProfessionalReviewListResponse
		{
			TotalCount = reviews.Count,
			Items = reviews
				.Where(x => cycleMap.ContainsKey(x.CycleId))
				.Select(x => MapReview(x, cycleMap[x.CycleId], employee))
				.ToList()
		};
	}

	private async Task EnsureCycleReviewsAsync(Guid cycleId)
	{
		var reviewQuery = await _reviewRepository.GetQueryableAsync();
		var existingEmployeeIds = await AsyncExecuter.ToListAsync(
			reviewQuery.Where(x => x.CycleId == cycleId).Select(x => x.EmployeeId));

		var employeeQuery = await _employeeRepository.GetQueryableAsync();
		var employees = await AsyncExecuter.ToListAsync(employeeQuery.OrderBy(x => x.FirstName).ThenBy(x => x.LastName));
		var missing = employees.Where(x => !existingEmployeeIds.Contains(x.Id)).ToList();

		foreach (var employee in missing)
		{
			await _reviewRepository.InsertAsync(
				new ProfessionalReview(_guidGenerator.Create(), CurrentTenant.Id, cycleId, employee.Id, CurrentUser.Id),
				autoSave: true);
		}
	}

	private async Task<List<ProfessionalReview>> GetReviewsForCycleAsync(Guid cycleId)
	{
		var query = await _reviewRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(query.Where(x => x.CycleId == cycleId).OrderBy(x => x.CreationTime));
	}

	private async Task<Dictionary<Guid, Employee>> GetEmployeesByIdsAsync(List<Guid> employeeIds)
	{
		var query = await _employeeRepository.GetQueryableAsync();
		var employees = await AsyncExecuter.ToListAsync(query.Where(x => employeeIds.Contains(x.Id)));
		return employees.ToDictionary(x => x.Id);
	}

	private async Task<ProfessionalReviewDto> MapReviewWithCycleAsync(ProfessionalReview review)
	{
		var cycle = await _cycleRepository.GetAsync(review.CycleId);
		var employee = await _employeeRepository.FindAsync(review.EmployeeId);
		return MapReview(review, cycle, employee);
	}

	private static void ValidateCycle(ProfessionalReviewCycleRequest input)
	{
		if (input.PeriodEnd.Date < input.PeriodStart.Date)
		{
			throw new BusinessException(HRErrorCodes.ProfessionalReviewCycleInvalidPeriod);
		}
	}

	private static ProfessionalReviewCycleDto MapCycle(ProfessionalReviewCycle cycle)
	{
		return new ProfessionalReviewCycleDto
		{
			Id = cycle.Id,
			Name = cycle.Name,
			PeriodStart = cycle.PeriodStart,
			PeriodEnd = cycle.PeriodEnd,
			Status = cycle.Status
		};
	}

	private static ProfessionalReviewDto MapReview(ProfessionalReview review, ProfessionalReviewCycle cycle, Employee? employee)
	{
		return new ProfessionalReviewDto
		{
			Id = review.Id,
			CycleId = review.CycleId,
			CycleName = cycle.Name,
			EmployeeId = review.EmployeeId,
			EmployeeName = employee?.EmployeeName ?? "Unknown employee",
			EmployeeIdentifier = employee?.EmployeeCode,
			EmployeeRole = employee?.Role?.Name,
			ReviewerUserId = review.ReviewerUserId,
			CommunicationScore = review.CommunicationScore,
			ProblemSolvingScore = review.ProblemSolvingScore,
			Comments = review.Comments,
			Feedback = review.Feedback,
			Status = review.Status,
			SubmittedAt = review.SubmittedAt
		};
	}
}
