using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
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
public class FinanceAuditTrailAppService(
	IRepository<FinanceAuditEvent, Guid> repository,
	IRepository<Student, Guid> studentRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository)
	: ApplicationService, IFinanceAuditTrailAppService
{
	public async Task<PagedResultDto<FinanceAuditEventDto>> GetListAsync(FinanceAuditEventListRequestDto input)
	{
		await EnsureCanViewFinanceAuditAsync();
		var query = await repository.GetQueryableAsync();
		query = ApplyFilters(query, input);
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(ApplySorting(query, input.Sorting)
			.Skip(input.SkipCount)
			.Take(NormalizeMaxResultCount(input.MaxResultCount)));
		return new PagedResultDto<FinanceAuditEventDto>(total, rows.Select(FinanceAuditTrailProjection.ToDto).ToList());
	}

	public async Task<PagedResultDto<FinanceAuditEventDto>> GetStudentEventsAsync(
		Guid studentId,
		FinanceAuditEventListRequestDto input)
	{
		input.StudentId = studentId;
		return await GetListAsync(input);
	}

	public async Task<PagedResultDto<FinanceAuditEventDto>> GetEntityEventsAsync(
		string entityType,
		Guid entityId,
		FinanceAuditEventListRequestDto input)
	{
		input.EntityType = entityType;
		input.EntityId = entityId;
		return await GetListAsync(input);
	}

	public async Task<FinanceAuditCorrelationDto> GetByCorrelationAsync(
		string correlationId,
		PagedAndSortedResultRequestDto input)
	{
		await EnsureCanViewFinanceAuditAsync();
		if (string.IsNullOrWhiteSpace(correlationId))
		{
			throw new BusinessException("FinanceAudit:CorrelationIdRequired");
		}

		var query = await repository.GetQueryableAsync();
		var normalized = correlationId.Trim();
		query = query.Where(x => x.CorrelationId == normalized);
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id)
				.Skip(input.SkipCount)
				.Take(NormalizeMaxResultCount(input.MaxResultCount)));

		var dtos = rows.Select(FinanceAuditTrailProjection.ToDto).ToList();
		return new FinanceAuditCorrelationDto
		{
			CorrelationId = normalized,
			StudentId = rows.FirstOrDefault()?.StudentId,
			StudentNameSnapshot = rows.FirstOrDefault()?.StudentNameSnapshot,
			OccurredAtFirst = rows.Count > 0 ? rows.Min(x => x.OccurredAt) : null,
			OccurredAtLast = rows.Count > 0 ? rows.Max(x => x.OccurredAt) : null,
			EventCount = total,
			Events = dtos
		};
	}

	public async Task<FinanceAuditEventDto> GetAsync(Guid id)
	{
		await EnsureCanViewFinanceAuditAsync();
		var entity = await repository.GetAsync(id);
		return FinanceAuditTrailProjection.ToDto(entity);
	}

	public async Task<ListResultDto<FinanceAuditStudentLookupDto>> LookupStudentsForAuditAsync(
		FinanceAuditStudentLookupRequestDto input)
	{
		await EnsureCanViewFinanceAuditAsync();

		var search = input.Search?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(search))
		{
			return new ListResultDto<FinanceAuditStudentLookupDto>([]);
		}

		if (!Guid.TryParse(search, out _) && search.Length < 2)
		{
			return new ListResultDto<FinanceAuditStudentLookupDto>([]);
		}

		var max = input.MaxResultCount <= 0 ? 15 : Math.Min(input.MaxResultCount, 30);
		var term = search.ToLowerInvariant();
		var grDigits = ExtractGrDigits(search);

		var studentQ = await studentRepository.GetQueryableAsync();
		studentQ = studentQ.Where(s =>
			s.AcademicYearId == input.AcademicYearId &&
			s.Status == StudentLifeCycleStatusEnum.Enrolled);

		if (Guid.TryParse(search, out var studentId))
		{
			studentQ = studentQ.Where(s => s.Id == studentId);
		}
		else
		{
			studentQ = studentQ.Where(s =>
				(s.FirstName != null && s.FirstName.ToLower().Contains(term)) ||
				(s.MiddleName != null && s.MiddleName.ToLower().Contains(term)) ||
				(s.LastName != null && s.LastName.ToLower().Contains(term)) ||
				((s.FirstName ?? string.Empty) + " " + (s.MiddleName ?? string.Empty) + " " + (s.LastName ?? string.Empty))
					.ToLower().Contains(term) ||
				(s.RollNo != null && s.RollNo.ToString()!.Contains(term)) ||
				(s.GrNo != null && s.GrNo.ToString()!.Contains(term)) ||
				(grDigits.Length > 0 && s.GrNo != null && s.GrNo.ToString()!.Contains(grDigits)));
		}

		var students = await AsyncExecuter.ToListAsync(
			studentQ
				.OrderBy(s => s.FirstName)
				.ThenBy(s => s.LastName)
				.Take(max)
				.Select(s => new
				{
					s.Id,
					s.FirstName,
					s.MiddleName,
					s.LastName,
					s.GrNo,
					s.RollNo,
					s.StandardId,
					s.DivisionId,
				}));

		if (students.Count == 0)
		{
			return new ListResultDto<FinanceAuditStudentLookupDto>([]);
		}

		var standardIds = students
			.Where(s => s.StandardId.HasValue)
			.Select(s => s.StandardId!.Value)
			.Distinct()
			.ToList();
		var divisionIds = students
			.Where(s => s.DivisionId.HasValue)
			.Select(s => s.DivisionId!.Value)
			.Distinct()
			.ToList();

		var standardQ = await standardRepository.GetQueryableAsync();
		var divisionQ = await divisionRepository.GetQueryableAsync();

		var standardDict = standardIds.Count == 0
			? new Dictionary<Guid, string>()
			: (await AsyncExecuter.ToListAsync(
				standardQ.Where(s => standardIds.Contains(s.Id)).Select(s => new { s.Id, s.Name })))
			.ToDictionary(s => s.Id, s => s.Name);

		var divisionDict = divisionIds.Count == 0
			? new Dictionary<Guid, string>()
			: (await AsyncExecuter.ToListAsync(
				divisionQ.Where(d => divisionIds.Contains(d.Id)).Select(d => new { d.Id, d.Name })))
			.ToDictionary(d => d.Id, d => d.Name);

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
				StudentId = s.Id,
				DisplayName = displayName,
				GrNo = grNo,
				RollNo = rollNo,
				StandardDivision = standardDivision,
				Label = BuildStudentLookupLabel(displayName, grNo, rollNo, standardDivision),
			};
		}).ToList();

		return new ListResultDto<FinanceAuditStudentLookupDto>(items);
	}

	/// <summary>
	/// Finance audit reads accept Auditing.View (primary), legacy AuditTrail.View, or fee collection view for student drawer.
	/// </summary>
	private async Task EnsureCanViewFinanceAuditAsync()
	{
		if (await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Auditing.View)
		    || await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.AuditTrail.View)
		    || await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View))
		{
			return;
		}

		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Auditing.View);
	}

	private static IQueryable<FinanceAuditEvent> ApplyFilters(
		IQueryable<FinanceAuditEvent> query,
		FinanceAuditEventListRequestDto input)
	{
		if (input.AcademicYearId.HasValue)
		{
			query = query.Where(x => x.AcademicYearId == input.AcademicYearId.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.SubModuleCode))
		{
			var code = Normalize(input.SubModuleCode);
			query = query.Where(x => x.SubModuleCode == code);
		}

		if (!string.IsNullOrWhiteSpace(input.EntityType))
		{
			var entityType = Normalize(input.EntityType);
			query = query.Where(x => x.EntityType == entityType);
		}

		if (input.EntityId.HasValue)
		{
			query = query.Where(x => x.EntityId == input.EntityId.Value);
		}

		if (input.StudentId.HasValue)
		{
			query = query.Where(x => x.StudentId == input.StudentId.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.Action))
		{
			var action = Normalize(input.Action);
			query = query.Where(x => x.Action == action);
		}

		if (input.ActorUserId.HasValue)
		{
			query = query.Where(x => x.ActorUserId == input.ActorUserId.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.CorrelationId))
		{
			var correlationId = input.CorrelationId.Trim();
			query = query.Where(x => x.CorrelationId == correlationId);
		}

		if (input.FromDate.HasValue)
		{
			query = query.Where(x => x.OccurredAt >= input.FromDate.Value);
		}

		if (input.ToDate.HasValue)
		{
			query = query.Where(x => x.OccurredAt <= input.ToDate.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			var grDigits = ExtractGrDigits(input.Search);
			query = query.Where(x =>
				x.EntityName.ToLower().Contains(search) ||
				x.Summary.ToLower().Contains(search) ||
				(x.ActorNameSnapshot != null && x.ActorNameSnapshot.ToLower().Contains(search)) ||
				(x.StudentNameSnapshot != null && x.StudentNameSnapshot.ToLower().Contains(search)) ||
				(x.StudentGrNoSnapshot != null && x.StudentGrNoSnapshot.ToLower().Contains(search)) ||
				(x.StudentRollNoSnapshot != null && x.StudentRollNoSnapshot.ToLower().Contains(search)) ||
				(grDigits.Length > 0 && x.StudentGrNoSnapshot != null &&
				 x.StudentGrNoSnapshot.ToLower().Contains(grDigits)) ||
				(x.CorrelationId != null && x.CorrelationId.ToLower().Contains(search)) ||
				(x.ContextJson != null && x.ContextJson.ToLower().Contains(search)));
		}

		return query;
	}

	private static IOrderedQueryable<FinanceAuditEvent> ApplySorting(IQueryable<FinanceAuditEvent> query, string? sorting)
	{
		var sort = ParseSorting(sorting);
		return sort.Field switch
		{
			"action" => sort.Descending
				? query.OrderByDescending(x => x.Action).ThenByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
				: query.OrderBy(x => x.Action).ThenByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id),
			"actorNameSnapshot" => sort.Descending
				? query.OrderByDescending(x => x.ActorNameSnapshot).ThenByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
				: query.OrderBy(x => x.ActorNameSnapshot).ThenByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id),
			_ => sort.Descending
				? query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
				: query.OrderBy(x => x.OccurredAt).ThenByDescending(x => x.Id)
		};
	}

	private static (string Field, bool Descending) ParseSorting(string? sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
		{
			return ("occurredAt", true);
		}

		var parts = sorting.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var requestedField = parts.Length > 0 ? parts[0] : "occurredAt";
		var field = requestedField switch
		{
			var name when name.Equals("occurredAt", StringComparison.OrdinalIgnoreCase) => "occurredAt",
			var name when name.Equals("action", StringComparison.OrdinalIgnoreCase) => "action",
			var name when name.Equals("actorNameSnapshot", StringComparison.OrdinalIgnoreCase) => "actorNameSnapshot",
			_ => "occurredAt"
		};
		var descending = parts.Length < 2 || !parts[1].Equals("asc", StringComparison.OrdinalIgnoreCase);
		return (field, descending);
	}

	private static int NormalizeMaxResultCount(int maxResultCount) => maxResultCount <= 0 ? 25 : Math.Min(maxResultCount, 100);

	private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace("-", "_");

	private static string ExtractGrDigits(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		var trimmed = value.Trim();
		var withoutPrefix = Regex.Replace(trimmed, @"^(?i)gr[\s\-]*", string.Empty);
		var digits = Regex.Replace(withoutPrefix, @"[^\d]", string.Empty);
		return digits.TrimStart('0');
	}

	private static string BuildStudentDisplayName(string? first, string? middle, string? last)
	{
		var parts = new[] { first, middle, last }
			.Where(p => !string.IsNullOrWhiteSpace(p))
			.Select(p => p!.Trim());
		return string.Join(" ", parts);
	}

	private static string? BuildStandardDivision(string? standard, string? division)
	{
		if (string.IsNullOrWhiteSpace(standard) && string.IsNullOrWhiteSpace(division))
		{
			return null;
		}

		if (string.IsNullOrWhiteSpace(division))
		{
			return standard;
		}

		if (string.IsNullOrWhiteSpace(standard))
		{
			return division;
		}

		return $"{standard}-{division}";
	}

	private static string BuildStudentLookupLabel(
		string displayName,
		string? grNo,
		string? rollNo,
		string? standardDivision)
	{
		var parts = new List<string> { displayName };
		if (!string.IsNullOrWhiteSpace(grNo))
		{
			parts.Add($"GR {grNo}");
		}

		if (!string.IsNullOrWhiteSpace(rollNo))
		{
			parts.Add($"Roll {rollNo}");
		}

		if (!string.IsNullOrWhiteSpace(standardDivision))
		{
			parts.Add(standardDivision);
		}

		return string.Join(" · ", parts);
	}
}
