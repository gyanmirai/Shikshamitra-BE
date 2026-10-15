using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Audit;

[RemoteService(false)]
[Authorize]
public partial class FinanceAuditTrailAppService(
	IRepository<FinanceAuditEvent, Guid> repository,
	IRepository<Student, Guid> studentRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	FinanceScopeAccessService financeScopeAccessService,
	IRepository<PaymentTerm, Guid> paymentTermRepository,
	IUserScopeAccessService scopeAccess)
	: ApplicationService, IFinanceAuditTrailAppService
{
	public async Task<PagedResultDto<FinanceAuditEventDto>> GetListAsync(FinanceAuditEventListRequestDto input)
	{
		await EnsureCanViewFinanceAuditAsync();

		var query = await ApplyPayerScopeAsync(await ApplyPaymentTermScopeAsync(await repository.GetQueryableAsync()));
		query = ApplyFilters(query, input);

		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(ApplySorting(query, input.Sorting)
			.Skip(input.SkipCount).Take(NormalizeMaxResultCount(input.MaxResultCount)));
		return new PagedResultDto<FinanceAuditEventDto>(total, rows.Select(FinanceAuditTrailProjection.ToDto).ToList());
	}

	public async Task<PagedResultDto<FinanceAuditEventDto>> GetStudentEventsAsync(
		Guid studentId, FinanceAuditEventListRequestDto input)
	{
		input.StudentId = studentId;
		return await GetListAsync(input);
	}

	public async Task<PagedResultDto<FinanceAuditEventDto>> GetEntityEventsAsync(
		string entityType, Guid entityId, FinanceAuditEventListRequestDto input)
	{
		input.EntityType = entityType;
		input.EntityId = entityId;
		return await GetListAsync(input);
	}

	public async Task<FinanceAuditCorrelationDto> GetByCorrelationAsync(
		string correlationId, PagedAndSortedResultRequestDto input)
	{
		await EnsureCanViewFinanceAuditAsync();
		if (string.IsNullOrWhiteSpace(correlationId))
			throw new BusinessException("FinanceAudit:CorrelationIdRequired");

		var query = await ApplyPayerScopeAsync(await ApplyPaymentTermScopeAsync(await repository.GetQueryableAsync()));
		var normalized = correlationId.Trim();
		query = query.Where(x => x.CorrelationId == normalized);
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id)
				.Skip(input.SkipCount).Take(NormalizeMaxResultCount(input.MaxResultCount)));

		return new FinanceAuditCorrelationDto
		{
			CorrelationId = normalized,
			StudentId = rows.FirstOrDefault()?.StudentId,
			StudentNameSnapshot = rows.FirstOrDefault()?.StudentNameSnapshot,
			OccurredAtFirst = rows.Count > 0 ? rows.Min(x => x.OccurredAt) : null,
			OccurredAtLast = rows.Count > 0 ? rows.Max(x => x.OccurredAt) : null,
			EventCount = total,
			Events = rows.Select(FinanceAuditTrailProjection.ToDto).ToList()
		};
	}

	public async Task<FinanceAuditEventDto> GetAsync(Guid id)
	{
		await EnsureCanViewFinanceAuditAsync();
		var entity = await AsyncExecuter.SingleOrDefaultAsync((await ApplyPayerScopeAsync(await ApplyPaymentTermScopeAsync(await repository.GetQueryableAsync()))).Where(x => x.Id == id))
			?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(FinanceAuditEvent), id);

		return FinanceAuditTrailProjection.ToDto(entity);
	}

	private sealed class AuditClassScope : IUserScopedEntity
	{
		public Guid? AcademicYearId { get; set; }
		public Guid? EducationBoardId { get; set; }
		public Guid? EducationMediumId { get; set; }
		public Guid? EducationMediumStageId { get; set; }
		public Guid? StandardId { get; set; }
		public Guid? DivisionId { get; set; }
	}

	private async Task<IQueryable<FinanceAuditEvent>> ApplyPaymentTermScopeAsync(IQueryable<FinanceAuditEvent> query)
	{
		var scope = await scopeAccess.GetCurrentUserScopeAsync();
		if (scope.IsAdmin) return query;
		var classes = scopeAccess.ApplyScope((await standardRepository.GetQueryableAsync()).Select(c => new AuditClassScope {
			AcademicYearId = c.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId,
			EducationBoardId = c.EducationMediumStage.EducationMedium.EducationBoardId,
			EducationMediumId = c.EducationMediumStage.EducationMediumId,
			EducationMediumStageId = c.EducationMediumStageId, StandardId = c.Id
		}), scope);
		var boardIds = classes.Select(c => c.EducationBoardId);
		var terms = (await paymentTermRepository.GetQueryableAsync()).Where(t => boardIds.Contains(t.BoardId)).Select(t => t.Id);
		return query.Where(e => e.EntityType != FinanceAuditEntityTypes.PaymentTerm || terms.Contains(e.EntityId));
	}

	public async Task<ListResultDto<FinanceAuditStudentLookupDto>> LookupStudentsForAuditAsync(
		FinanceAuditStudentLookupRequestDto input)
	{
		await EnsureCanViewFinanceAuditAsync();
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(input.AcademicYearId, null, null, null, null);

		var search = input.Search?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(search) || (!Guid.TryParse(search, out _) && search.Length < 2))
			return new ListResultDto<FinanceAuditStudentLookupDto>([]);

		var max = input.MaxResultCount <= 0 ? 15 : Math.Min(input.MaxResultCount, 30);
		var term = search.ToLowerInvariant();
		var grDigits = ExtractGrDigits(search);

		var studentQ = await studentRepository.GetQueryableAsync();
		studentQ = studentQ.Where(s => s.AcademicYearId == input.AcademicYearId && s.Status == StudentLifeCycleStatusEnum.Enrolled);
		var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
		studentQ = studentQ.Where(s => scopedStudentIds.Contains(s.Id));

		if (Guid.TryParse(search, out var studentId))
			studentQ = studentQ.Where(s => s.Id == studentId);
		else
			studentQ = studentQ.Where(s =>
				(s.FirstName != null && s.FirstName.ToLower().Contains(term)) ||
				(s.MiddleName != null && s.MiddleName.ToLower().Contains(term)) ||
				(s.LastName != null && s.LastName.ToLower().Contains(term)) ||
				((s.FirstName ?? string.Empty) + " " + (s.MiddleName ?? string.Empty) + " " + (s.LastName ?? string.Empty)).ToLower().Contains(term) ||
				(s.RollNo != null && s.RollNo.ToString()!.Contains(term)) ||
				(s.GrNo != null && s.GrNo.ToString()!.Contains(term)) ||
				(grDigits.Length > 0 && s.GrNo != null && s.GrNo.ToString()!.Contains(grDigits)));

		var students = await AsyncExecuter.ToListAsync(
			studentQ.OrderBy(s => s.FirstName).ThenBy(s => s.LastName).Take(max)
				.Select(s => new { s.Id, s.FirstName, s.MiddleName, s.LastName, s.GrNo, s.RollNo, s.StandardId, s.DivisionId }));

		if (students.Count == 0) return new ListResultDto<FinanceAuditStudentLookupDto>([]);

		var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
		var divisionIds = students.Where(s => s.DivisionId.HasValue).Select(s => s.DivisionId!.Value).Distinct().ToList();
		var standardQ = await standardRepository.GetQueryableAsync();
		var divisionQ = await divisionRepository.GetQueryableAsync();
		var standardDict = standardIds.Count == 0 ? [] :
			(await AsyncExecuter.ToListAsync(standardQ.Where(s => standardIds.Contains(s.Id)).Select(s => new { s.Id, s.Name }))).ToDictionary(s => s.Id, s => s.Name);
		var divisionDict = divisionIds.Count == 0 ? [] :
			(await AsyncExecuter.ToListAsync(divisionQ.Where(d => divisionIds.Contains(d.Id)).Select(d => new { d.Id, d.Name }))).ToDictionary(d => d.Id, d => d.Name);

		var items = students.Select(s =>
		{
			var displayName = BuildStudentDisplayName(s.FirstName, s.MiddleName, s.LastName);
			var grNo = s.GrNo?.ToString();
			var rollNo = s.RollNo?.ToString();
			standardDict.TryGetValue(s.StandardId ?? Guid.Empty, out var standardName);
			divisionDict.TryGetValue(s.DivisionId ?? Guid.Empty, out var divisionName);
			var standardDivision = BuildStandardDivision(standardName, divisionName);
			return new FinanceAuditStudentLookupDto
			{
				StudentId = s.Id, DisplayName = displayName, GrNo = grNo, RollNo = rollNo,
				StandardDivision = standardDivision,
				Label = BuildStudentLookupLabel(displayName, grNo, rollNo, standardDivision),
			};
		}).ToList();

		return new ListResultDto<FinanceAuditStudentLookupDto>(items);
	}
}
