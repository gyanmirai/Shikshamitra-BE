using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Transport;

[Authorize]
[RemoteService(false)]
public class TransportBillingAppService(
	IRepository<TransportZoneArea, Guid> zoneAreaRepository,
	IRepository<TransportFeeConfiguration, Guid> feeConfigurationRepository,
	IRepository<TransportFeeRun, Guid> feeRunRepository,
	IRepository<TransportFeeDue, Guid> feeDueRepository,
	IRepository<TransportMember, Guid> memberRepository,
	IRepository<TransportMemberRouteAssignment, Guid> memberAssignmentRepository,
	IRepository<TransportRoute, Guid> routeRepository,
	IRepository<TransportStop, Guid> stopRepository,
	IRepository<EducationMedium, Guid> academicMediumRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IStudentRepository studentRepository,
	IRepository<ChargeCategory, Guid> chargeCategoryRepository,
	StudentScopeAccessService studentScopeAccessService,
	ChargeRequestManager chargeRequestManager
) : ApplicationService, ITransportBillingAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<List<TransportZoneAreaDto>> GetZoneAreasAsync(Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetZoneAreasAsync));
		var rows = await zoneAreaRepository.GetListAsync(row => row.AcademicMediumId == academicMediumId);
		return rows.OrderBy(row => row.Name).Select(MapZoneArea).ToList();
	}

	public async Task<TransportZoneAreaDto> CreateZoneAreaAsync(Guid academicMediumId, CreateUpdateTransportZoneAreaDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(CreateZoneAreaAsync));
		var name = NormalizeRequiredName(input.ZoneName ?? input.Name, "Zone/area name is required.");
		await EnsureZoneAreaNameUniqueAsync(academicMediumId, name);

		var row = new TransportZoneArea(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicMediumId,
			name,
			input.Description,
			input.IsActive);

		await zoneAreaRepository.InsertAsync(row, autoSave: true);
		return MapZoneArea(row);
	}

	public async Task<TransportZoneAreaDto> UpdateZoneAreaAsync(Guid zoneAreaId, Guid academicMediumId, CreateUpdateTransportZoneAreaDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(UpdateZoneAreaAsync));
		var name = NormalizeRequiredName(input.ZoneName ?? input.Name, "Zone/area name is required.");
		var row = await GetZoneAreaForMediumAsync(zoneAreaId, academicMediumId);
		await EnsureZoneAreaNameUniqueAsync(academicMediumId, name, zoneAreaId);

		row.UpdateDetails(name, input.Description, input.IsActive);
		await zoneAreaRepository.UpdateAsync(row, autoSave: true);
		return MapZoneArea(row);
	}

	public async Task DeleteZoneAreaAsync(Guid zoneAreaId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(DeleteZoneAreaAsync));
		await zoneAreaRepository.DeleteAsync(await GetZoneAreaForMediumAsync(zoneAreaId, academicMediumId), autoSave: true);
	}

	public Task<List<TransportOptionDto>> GetFeeFrequencyOptionsAsync()
	{
		return Task.FromResult(new List<TransportOptionDto>
		{
			ToOption(((int)TransportFeeFrequency.Monthly).ToString(), "Monthly"),
			ToOption(((int)TransportFeeFrequency.Quarterly).ToString(), "Quarterly"),
			ToOption(((int)TransportFeeFrequency.Yearly).ToString(), "Yearly")
		});
	}

	public async Task<TransportFeeConfigurationDto?> GetFeeConfigurationAsync(Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetFeeConfigurationAsync));
		var configuration = await GetActiveConfigurationOrNullAsync(academicMediumId);
		return configuration == null ? null : MapConfiguration(configuration);
	}

	public async Task<TransportFeeConfigurationDto> CreateFeeConfigurationAsync(Guid academicMediumId, CreateUpdateTransportFeeConfigurationDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(CreateFeeConfigurationAsync));
		var details = NormalizeConfigurationInput(input);
		await EnsureChargeCategoryExistsAsync(details.ChargeCategoryId);
		if (details.IsActive)
		{
			await EnsureNoOtherActiveConfigurationAsync(academicMediumId);
		}

		var configuration = new TransportFeeConfiguration(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicMediumId,
			details.ModelType,
			details.Frequency,
			details.RatePerKm,
			details.ChargeCategoryId,
			details.EffectiveFrom,
			details.IsActive,
			input.Remarks);

		await feeConfigurationRepository.InsertAsync(configuration, autoSave: true);
		return MapConfiguration(configuration);
	}

	public async Task<TransportFeeConfigurationDto> UpdateFeeConfigurationAsync(Guid configurationId, Guid academicMediumId, CreateUpdateTransportFeeConfigurationDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(UpdateFeeConfigurationAsync));
		var details = NormalizeConfigurationInput(input);
		await EnsureChargeCategoryExistsAsync(details.ChargeCategoryId);
		var configuration = await GetConfigurationForMediumAsync(configurationId, academicMediumId);
		if (details.IsActive)
		{
			await EnsureNoOtherActiveConfigurationAsync(academicMediumId, configurationId);
		}

		configuration.UpdateDetails(
			details.ModelType,
			details.Frequency,
			details.RatePerKm,
			details.ChargeCategoryId,
			details.EffectiveFrom,
			details.IsActive,
			input.Remarks);

		await feeConfigurationRepository.UpdateAsync(configuration, autoSave: true);
		return MapConfiguration(configuration);
	}

	public async Task<TransportFeeRunPreviewDto> PreviewRunAsync(Guid academicMediumId, TransportFeeRunRequestDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(PreviewRunAsync));
		var context = await BuildRunContextAsync(academicMediumId, input);
		var rows = await BuildPreviewRowsAsync(context);
		return MapPreview(context, rows);
	}

	public async Task<TransportFeeRunDto> CreateRunAsync(Guid academicMediumId, TransportFeeRunRequestDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(CreateRunAsync));
		var context = await BuildRunContextAsync(academicMediumId, input);
		await EnsureRunDoesNotExistAsync(context);

		var rows = await BuildPreviewRowsAsync(context);
		if (rows.Any(row => row.AlreadyGenerated))
		{
			throw new BusinessException(TransportErrorCodes.FeeDueAlreadyExists)
				.WithData("PeriodFrom", context.PeriodFrom)
				.WithData("PeriodTo", context.PeriodTo);
		}

		var run = new TransportFeeRun(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicMediumId,
			context.AcademicYearId,
			context.Configuration.Id,
			context.PeriodFrom,
			context.PeriodTo,
			context.DueDate,
			rows.Count,
			rows.Count(row => row.MemberType == TransportMemberType.Student),
			rows.Count(row => row.MemberType == TransportMemberType.Employee),
			rows.Sum(row => row.Amount),
			input.Remarks);

		await feeRunRepository.InsertAsync(run, autoSave: false);

		foreach (var row in rows)
		{
			await feeDueRepository.InsertAsync(new TransportFeeDue(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicMediumId,
				context.AcademicYearId,
				run.Id,
				context.Configuration.Id,
				context.Configuration.ChargeCategoryId,
				row.MemberId,
				row.MemberType,
				row.StudentId,
				row.EmployeeId,
				row.AssignmentId,
				row.RouteId,
				row.StopId,
				context.Configuration.ModelType,
				context.Configuration.FeeFrequency,
				context.PeriodFrom,
				context.PeriodTo,
				context.DueDate,
				row.Amount,
				row.MemberName,
				row.MemberCode,
				row.RouteName,
				row.RouteCode,
				row.StopName), autoSave: false);
		}

		await CurrentUnitOfWork!.SaveChangesAsync();
		return await GetRunAsync(run.Id, academicMediumId);
	}

	public async Task<PagedResultDto<TransportFeeRunDto>> GetRunsAsync(TransportFeeRunListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetRunsAsync));
		var query = await feeRunRepository.GetQueryableAsync();
		query = query.Where(run => run.AcademicMediumId == input.AcademicMediumId);

		if (input.AcademicYearId.HasValue)
		{
			query = query.Where(run => run.AcademicYearId == input.AcademicYearId.Value);
		}

		if (input.Status.HasValue)
		{
			query = query.Where(run => run.Status == input.Status.Value);
		}

		if (input.PeriodFrom.HasValue)
		{
			query = query.Where(run => run.PeriodFrom >= input.PeriodFrom.Value.Date);
		}

		if (input.PeriodTo.HasValue)
		{
			query = query.Where(run => run.PeriodTo <= input.PeriodTo.Value.Date);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(run => run.CreationTime)
				.Skip(input.SkipCount)
				.Take(input.NormalizedPageSize));

		return new PagedResultDto<TransportFeeRunDto>(
			totalCount,
			await MapRunsAsync(rows, includeDues: false));
	}

	public async Task<TransportFeeRunDto> GetRunAsync(Guid runId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetRunAsync));
		var run = await GetRunForMediumAsync(runId, academicMediumId);
		return (await MapRunsAsync([run], includeDues: true)).Single();
	}

	public async Task<TransportFeeFinancePostResultDto> PostRunToFinanceAsync(Guid runId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(PostRunToFinanceAsync));
		var run = await GetRunForMediumAsync(runId, academicMediumId);
		var duesQuery = await feeDueRepository.GetQueryableAsync();
		var allRunDues = await AsyncExecuter.ToListAsync(duesQuery.Where(due =>
			due.AcademicMediumId == academicMediumId &&
			due.FeeRunId == runId));

		if (allRunDues.Count == 0)
		{
			throw new BusinessException(TransportErrorCodes.FeeRunHasNoDues)
				.WithData("RunId", runId);
		}

		var studentDues = allRunDues
			.Where(due =>
				due.MemberType == TransportMemberType.Student &&
				due.StudentId.HasValue &&
				due.Status == TransportFeeDueStatus.Pending)
			.ToList();
		var skippedEmployeeDueCount = allRunDues.Count(due => due.MemberType == TransportMemberType.Employee);
		var chargeRequestCount = 0;
		var postedDueCount = 0;

		if (studentDues.Count > 0)
		{
			var studentIds = studentDues.Select(due => due.StudentId!.Value).Distinct().ToList();
			var studentQuery = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
			var students = await AsyncExecuter.ToListAsync(studentQuery.Where(student => studentIds.Contains(student.Id)));
			var studentMap = students.ToDictionary(student => student.Id);

			var groups = studentDues.GroupBy(due =>
			{
				var student = studentMap[due.StudentId!.Value];
				return new FinancePostGroupKey(
					due.Amount,
					due.DueDate.Date,
					due.ChargeCategoryId,
					student.AcademicBoardId ?? Guid.Empty,
					student.AcademicMediumId);
			});

			var requestorId = CurrentUser.Id ?? throw new BusinessException(TransportErrorCodes.FeeFinancePostInvalid)
				.WithData("Message", "A signed-in user is required to post transport dues to Finance.");

			foreach (var group in groups)
			{
				var groupDues = group.ToList();
				var groupStudents = groupDues
					.Select(due => studentMap[due.StudentId!.Value])
					.ToList();
				var classIds = groupStudents
					.Where(student => student.StandardId.HasValue)
					.Select(student => student.StandardId!.Value)
					.Distinct()
					.ToList();

				if (classIds.Count == 0)
				{
					throw new BusinessException(TransportErrorCodes.FeeFinancePostInvalid)
						.WithData("Message", "Student transport dues cannot be posted because no class scope was found.");
				}

				var studentIdList = groupDues.Select(due => due.StudentId!.Value).Distinct().ToList();
				var audienceDto = new ChargeAudienceDto
				{
					Scope = new AcademicScopeDto
					{
						BoardId = group.Key.BoardId,
						EducationMediumId = group.Key.EducationMediumId,
						ClassIds = classIds
					},
					Selection = new StudentSelectionDto
					{
						Mode = StudentSelectionMode.Explicit,
						StudentIds = studentIdList
					}
				};
				var audience = new ChargeAudience(
					new AcademicScope(group.Key.BoardId, group.Key.EducationMediumId, classIds),
					new StudentSelection(StudentSelectionMode.Explicit, studentIdList));

				var (request, lines) = await chargeRequestManager.CreateAsync(
					CurrentTenant.Id,
					run.AcademicYearId,
					BuildChargeName(run),
					group.Key.ChargeCategoryId,
					BuildChargePurpose(run),
					group.Key.Amount,
					group.Key.DueDate,
					ChargePriority.Medium,
					audience,
					JsonSerializer.Serialize(audienceDto, JsonOptions),
					requestorId,
					$"Generated from transport fee run {run.Id}");

				chargeRequestCount++;
				var lineMap = lines.ToDictionary(line => line.StudentId);
				foreach (var due in groupDues)
				{
					if (!lineMap.TryGetValue(due.StudentId!.Value, out var line))
					{
						continue;
					}

					due.MarkPostedToFinance(request.Id, line.Id);
					await feeDueRepository.UpdateAsync(due, autoSave: false);
					postedDueCount++;
				}
			}
		}

		var hasUnpostedStudentDues = allRunDues.Any(due =>
			due.MemberType == TransportMemberType.Student &&
			due.Status == TransportFeeDueStatus.Pending);
		run.MarkFinancePosted(chargeRequestCount, hasUnpostedStudentDues);
		await feeRunRepository.UpdateAsync(run, autoSave: false);
		await CurrentUnitOfWork!.SaveChangesAsync();

		return new TransportFeeFinancePostResultDto
		{
			Run = await GetRunAsync(run.Id, academicMediumId),
			PostedStudentDueCount = postedDueCount,
			SkippedEmployeeDueCount = skippedEmployeeDueCount,
			ChargeRequestCount = chargeRequestCount
		};
	}

	public async Task<PagedResultDto<TransportFeeDueDto>> GetDuesAsync(TransportFeeDueListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetDuesAsync));
		var query = await feeDueRepository.GetQueryableAsync();
		query = ApplyDueFilters(query.Where(due => due.AcademicMediumId == input.AcademicMediumId), input);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(due => due.CreationTime)
				.Skip(input.SkipCount)
				.Take(input.NormalizedPageSize));

		return new PagedResultDto<TransportFeeDueDto>(totalCount, rows.Select(MapDue).ToList());
	}

	public async Task<PagedResultDto<TransportFeeDueDto>> GetMemberDuesAsync(Guid memberId, TransportMemberDueListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetMemberDuesAsync));
		var member = await memberRepository.FindAsync(memberId);
		if (member == null || member.AcademicMediumId != input.AcademicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportMember), memberId);
		}

		var query = await feeDueRepository.GetQueryableAsync();
		query = query.Where(due => due.AcademicMediumId == input.AcademicMediumId && due.MemberId == memberId);
		var totalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(due => due.PeriodFrom)
				.Skip(input.SkipCount)
				.Take(input.NormalizedPageSize));

		return new PagedResultDto<TransportFeeDueDto>(totalCount, rows.Select(MapDue).ToList());
	}

	private IQueryable<TransportFeeDue> ApplyDueFilters(IQueryable<TransportFeeDue> query, TransportFeeDueListRequestDto input)
	{
		if (input.AcademicYearId.HasValue)
		{
			query = query.Where(due => due.AcademicYearId == input.AcademicYearId.Value);
		}

		if (input.MemberType.HasValue)
		{
			query = query.Where(due => due.MemberType == input.MemberType.Value);
		}

		if (input.Status.HasValue)
		{
			query = query.Where(due => due.Status == input.Status.Value);
		}

		if (input.RouteId.HasValue)
		{
			query = query.Where(due => due.RouteId == input.RouteId.Value);
		}

		if (input.StopId.HasValue)
		{
			query = query.Where(due => due.StopId == input.StopId.Value);
		}

		if (input.RunId.HasValue)
		{
			query = query.Where(due => due.FeeRunId == input.RunId.Value);
		}

		return query;
	}

	private async Task<RunContext> BuildRunContextAsync(Guid academicMediumId, TransportFeeRunRequestDto input)
	{
		if (input.AcademicYearId == Guid.Empty)
		{
			throw new UserFriendlyException("Academic year is required.");
		}

		await academicYearRepository.GetAsync(input.AcademicYearId);

		var periodFrom = input.PeriodFrom.Date;
		var periodTo = input.PeriodTo.Date;
		if (periodFrom == default || periodTo == default || periodFrom > periodTo)
		{
			throw new UserFriendlyException("A valid fee period is required.");
		}

		var dueDate = input.DueDate.Date == default ? periodTo : input.DueDate.Date;
		var configuration = input.FeeConfigurationId.HasValue
			? await GetConfigurationForMediumAsync(input.FeeConfigurationId.Value, academicMediumId)
			: await GetActiveConfigurationAsync(academicMediumId);

		if (!configuration.IsActive)
		{
			throw new BusinessException(TransportErrorCodes.FeeConfigurationNotFound)
				.WithData("ConfigurationId", configuration.Id);
		}

		return new RunContext(academicMediumId, input.AcademicYearId, configuration, periodFrom, periodTo, dueDate);
	}

	private async Task<List<TransportFeeDuePreviewRowDto>> BuildPreviewRowsAsync(RunContext context)
	{
		var periodEnd = context.PeriodTo.Date.AddDays(1).AddTicks(-1);
		var assignmentQuery = await memberAssignmentRepository.GetQueryableAsync();
		var assignments = await AsyncExecuter.ToListAsync(
			assignmentQuery.Where(assignment =>
				assignment.AcademicMediumId == context.AcademicMediumId &&
				assignment.EffectiveFrom <= periodEnd &&
				(!assignment.EffectiveTo.HasValue || assignment.EffectiveTo.Value >= context.PeriodFrom)));

		var latestAssignments = assignments
			.GroupBy(assignment => assignment.MemberId)
			.Select(group => group.OrderByDescending(assignment => assignment.EffectiveFrom).First())
			.ToList();

		if (latestAssignments.Count == 0)
		{
			return [];
		}

		var memberIds = latestAssignments.Select(assignment => assignment.MemberId).Distinct().ToList();
		var routeIds = latestAssignments.Select(assignment => assignment.RouteId).Distinct().ToList();
		var stopIds = latestAssignments.Select(assignment => assignment.StopId).Distinct().ToList();

		var members = (await memberRepository.GetListAsync(member => memberIds.Contains(member.Id))).ToDictionary(member => member.Id);
		var routes = (await routeRepository.GetListAsync(route => routeIds.Contains(route.Id))).ToDictionary(route => route.Id);
		var stops = (await stopRepository.GetListAsync(stop => stopIds.Contains(stop.Id))).ToDictionary(stop => stop.Id);
		var existingKeys = await GetExistingDueKeysAsync(context);
		var rows = new List<TransportFeeDuePreviewRowDto>();

		foreach (var assignment in latestAssignments)
		{
			if (!members.TryGetValue(assignment.MemberId, out var member) ||
			    !routes.TryGetValue(assignment.RouteId, out var route) ||
			    !stops.TryGetValue(assignment.StopId, out var stop))
			{
				continue;
			}

			var amount = CalculateDueAmount(context.Configuration, assignment, stop);
			rows.Add(new TransportFeeDuePreviewRowDto
			{
				MemberId = member.Id,
				MemberType = member.MemberType,
				StudentId = member.StudentId,
				EmployeeId = member.EmployeeId,
				AssignmentId = assignment.Id,
				RouteId = route.Id,
				StopId = stop.Id,
				MemberName = member.DisplayName,
				MemberCode = member.MemberCode,
				RouteName = route.RouteName,
				RouteCode = route.RouteCode,
				StopName = stop.StopName,
				Amount = amount,
				AlreadyGenerated = existingKeys.Contains(BuildDueKey(member.Id, assignment.Id))
			});
		}

		return rows.OrderBy(row => row.MemberName).ToList();
	}

	private async Task<HashSet<string>> GetExistingDueKeysAsync(RunContext context)
	{
		var query = await feeDueRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(query.Where(due =>
			due.AcademicMediumId == context.AcademicMediumId &&
			due.AcademicYearId == context.AcademicYearId &&
			due.FeeConfigurationId == context.Configuration.Id &&
			due.PeriodFrom == context.PeriodFrom &&
			due.PeriodTo == context.PeriodTo));

		return existing.Select(due => BuildDueKey(due.MemberId, due.AssignmentId)).ToHashSet();
	}

	private async Task EnsureRunDoesNotExistAsync(RunContext context)
	{
		if (await feeRunRepository.AnyAsync(run =>
			run.AcademicMediumId == context.AcademicMediumId &&
			run.AcademicYearId == context.AcademicYearId &&
			run.FeeConfigurationId == context.Configuration.Id &&
			run.PeriodFrom == context.PeriodFrom &&
			run.PeriodTo == context.PeriodTo))
		{
			throw new BusinessException(TransportErrorCodes.FeeRunAlreadyExists)
				.WithData("PeriodFrom", context.PeriodFrom)
				.WithData("PeriodTo", context.PeriodTo);
		}
	}

	private async Task<List<TransportFeeRunDto>> MapRunsAsync(List<TransportFeeRun> runs, bool includeDues)
	{
		if (runs.Count == 0)
		{
			return [];
		}

		var configurationIds = runs.Select(run => run.FeeConfigurationId).Distinct().ToList();
		var configurations = (await feeConfigurationRepository.GetListAsync(config => configurationIds.Contains(config.Id)))
			.ToDictionary(config => config.Id);
		var dueLookup = new Dictionary<Guid, List<TransportFeeDue>>();

		if (includeDues)
		{
			var runIds = runs.Select(run => run.Id).ToList();
			var dues = await feeDueRepository.GetListAsync(due => runIds.Contains(due.FeeRunId));
			dueLookup = dues
				.GroupBy(due => due.FeeRunId)
				.ToDictionary(group => group.Key, group => group.OrderBy(due => due.MemberName).ToList());
		}

		return runs.Select(run =>
		{
			configurations.TryGetValue(run.FeeConfigurationId, out var configuration);
			dueLookup.TryGetValue(run.Id, out var dues);
			return MapRun(run, configuration, dues ?? []);
		}).ToList();
	}

	private async Task<TransportFeeRun> GetRunForMediumAsync(Guid runId, Guid academicMediumId)
	{
		var run = await feeRunRepository.FindAsync(runId);
		if (run == null || run.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportFeeRun), runId);
		}

		return run;
	}

	private async Task<TransportFeeConfiguration> GetConfigurationForMediumAsync(Guid configurationId, Guid academicMediumId)
	{
		var configuration = await feeConfigurationRepository.FindAsync(configurationId);
		if (configuration == null || configuration.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportFeeConfiguration), configurationId);
		}

		return configuration;
	}

	private async Task<TransportFeeConfiguration> GetActiveConfigurationAsync(Guid academicMediumId)
	{
		return await GetActiveConfigurationOrNullAsync(academicMediumId)
			?? throw new BusinessException(TransportErrorCodes.FeeConfigurationNotFound)
				.WithData("AcademicMediumId", academicMediumId);
	}

	private async Task<TransportFeeConfiguration?> GetActiveConfigurationOrNullAsync(Guid academicMediumId)
	{
		var query = await feeConfigurationRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(
			query
				.Where(configuration => configuration.AcademicMediumId == academicMediumId && configuration.IsActive)
				.OrderByDescending(configuration => configuration.EffectiveFrom));
	}

	private async Task EnsureNoOtherActiveConfigurationAsync(Guid academicMediumId, Guid? excludingId = null)
	{
		if (await feeConfigurationRepository.AnyAsync(configuration =>
			configuration.AcademicMediumId == academicMediumId &&
			configuration.IsActive &&
			(!excludingId.HasValue || configuration.Id != excludingId.Value)))
		{
			throw new BusinessException(TransportErrorCodes.FeeConfigurationAlreadyExists)
				.WithData("AcademicMediumId", academicMediumId);
		}
	}

	private async Task EnsureChargeCategoryExistsAsync(Guid chargeCategoryId)
	{
		var category = await chargeCategoryRepository.FindAsync(chargeCategoryId);
		if (category == null || !category.IsActive)
		{
			throw new BusinessException(TransportErrorCodes.FeeConfigurationInvalid)
				.WithData("ChargeCategoryId", chargeCategoryId);
		}
	}

	private async Task<TransportZoneArea> GetZoneAreaForMediumAsync(Guid zoneAreaId, Guid academicMediumId)
	{
		var row = await zoneAreaRepository.FindAsync(zoneAreaId);
		if (row == null || row.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportZoneArea), zoneAreaId);
		}

		return row;
	}

	private async Task EnsureZoneAreaNameUniqueAsync(Guid academicMediumId, string name, Guid? excludingId = null)
	{
		var normalized = NormalizeName(name);
		var rows = await zoneAreaRepository.GetListAsync(row =>
			row.AcademicMediumId == academicMediumId &&
			(!excludingId.HasValue || row.Id != excludingId.Value));
		if (rows.Any(row => NormalizeName(row.Name) == normalized))
		{
			throw new BusinessException(TransportErrorCodes.ZoneAreaAlreadyExists)
				.WithData("Name", name);
		}
	}

	private async Task EnsureCanUseMediumAsync(Guid academicMediumId, string operation)
	{
		await academicMediumRepository.GetAsync(academicMediumId);
		await studentScopeAccessService.EnsureCanAccessMediumAsync(academicMediumId, operation);
	}

	private ConfigurationInput NormalizeConfigurationInput(CreateUpdateTransportFeeConfigurationDto input)
	{
		var model = input.ModelType ?? ParseModelType(input.Model ?? input.FeeModelType);
		var frequency = input.FeeFrequency ?? ParseFrequency(input.Frequency);
		var effectiveFrom = (input.EffectiveFrom ?? DateTime.UtcNow.Date).Date;
		return new ConfigurationInput(
			model,
			frequency,
			input.RatePerKm,
			input.ChargeCategoryId,
			effectiveFrom,
			input.IsActive);
	}

	private static TransportFeeModelType ParseModelType(string? value)
	{
		var normalized = NormalizeToken(value);
		return normalized switch
		{
			"stopbased" => TransportFeeModelType.StopBased,
			"distancebased" => TransportFeeModelType.DistanceBased,
			_ => throw new BusinessException(TransportErrorCodes.FeeConfigurationInvalid)
				.WithData("Model", value ?? string.Empty)
		};
	}

	private static TransportFeeFrequency ParseFrequency(string? value)
	{
		var normalized = NormalizeToken(value);
		return normalized switch
		{
			"monthly" => TransportFeeFrequency.Monthly,
			"quarterly" => TransportFeeFrequency.Quarterly,
			"yearly" => TransportFeeFrequency.Yearly,
			_ => throw new BusinessException(TransportErrorCodes.FeeConfigurationInvalid)
				.WithData("Frequency", value ?? string.Empty)
		};
	}

	private static decimal CalculateDueAmount(TransportFeeConfiguration configuration, TransportMemberRouteAssignment assignment, TransportStop stop)
	{
		return configuration.ModelType == TransportFeeModelType.DistanceBased
			? Math.Round((configuration.RatePerKm ?? 0) * stop.ApproxKmFromSchool, 2, MidpointRounding.AwayFromZero)
			: assignment.FeeAmountSnapshot;
	}

	private static TransportFeeRunPreviewDto MapPreview(RunContext context, List<TransportFeeDuePreviewRowDto> rows) => new()
	{
		AcademicMediumId = context.AcademicMediumId,
		AcademicYearId = context.AcademicYearId,
		FeeConfigurationId = context.Configuration.Id,
		PeriodFrom = context.PeriodFrom,
		PeriodTo = context.PeriodTo,
		DueDate = context.DueDate,
		GeneratedDueCount = rows.Count,
		StudentDueCount = rows.Count(row => row.MemberType == TransportMemberType.Student),
		EmployeeDueCount = rows.Count(row => row.MemberType == TransportMemberType.Employee),
		TotalAmount = rows.Sum(row => row.Amount),
		Items = rows
	};

	private static TransportZoneAreaDto MapZoneArea(TransportZoneArea row) => new()
	{
		Id = row.Id,
		AcademicMediumId = row.AcademicMediumId,
		Name = row.Name,
		ZoneName = row.Name,
		Description = row.Description,
		IsActive = row.IsActive,
		CreationTime = row.CreationTime,
		CreatorId = row.CreatorId,
		LastModificationTime = row.LastModificationTime,
		LastModifierId = row.LastModifierId
	};

	private static TransportFeeConfigurationDto MapConfiguration(TransportFeeConfiguration configuration) => new()
	{
		Id = configuration.Id,
		AcademicMediumId = configuration.AcademicMediumId,
		ModelType = configuration.ModelType,
		Model = ToModelWireValue(configuration.ModelType),
		FeeModelType = ToModelWireValue(configuration.ModelType),
		FeeFrequency = configuration.FeeFrequency,
		Frequency = configuration.FeeFrequency.ToString(),
		RatePerKm = configuration.RatePerKm,
		ChargeCategoryId = configuration.ChargeCategoryId,
		EffectiveFrom = configuration.EffectiveFrom,
		IsActive = configuration.IsActive,
		Remarks = configuration.Remarks,
		CreationTime = configuration.CreationTime,
		CreatorId = configuration.CreatorId,
		LastModificationTime = configuration.LastModificationTime,
		LastModifierId = configuration.LastModifierId
	};

	private static TransportFeeRunDto MapRun(TransportFeeRun run, TransportFeeConfiguration? configuration, List<TransportFeeDue> dues) => new()
	{
		Id = run.Id,
		AcademicMediumId = run.AcademicMediumId,
		AcademicYearId = run.AcademicYearId,
		FeeConfigurationId = run.FeeConfigurationId,
		Status = run.Status,
		StatusName = run.Status.ToString(),
		PeriodFrom = run.PeriodFrom,
		PeriodTo = run.PeriodTo,
		DueDate = run.DueDate,
		GeneratedDueCount = run.GeneratedDueCount,
		StudentDueCount = run.StudentDueCount,
		EmployeeDueCount = run.EmployeeDueCount,
		TotalAmount = run.TotalAmount,
		FinancePostedAt = run.FinancePostedAt,
		FinanceChargeRequestCount = run.FinanceChargeRequestCount,
		Remarks = run.Remarks,
		Configuration = configuration == null ? null : MapConfiguration(configuration),
		Dues = dues.Select(MapDue).ToList(),
		CreationTime = run.CreationTime,
		CreatorId = run.CreatorId,
		LastModificationTime = run.LastModificationTime,
		LastModifierId = run.LastModifierId
	};

	private static TransportFeeDueDto MapDue(TransportFeeDue due) => new()
	{
		Id = due.Id,
		AcademicMediumId = due.AcademicMediumId,
		AcademicYearId = due.AcademicYearId,
		FeeRunId = due.FeeRunId,
		FeeConfigurationId = due.FeeConfigurationId,
		ChargeCategoryId = due.ChargeCategoryId,
		MemberId = due.MemberId,
		MemberType = due.MemberType,
		StudentId = due.StudentId,
		EmployeeId = due.EmployeeId,
		AssignmentId = due.AssignmentId,
		RouteId = due.RouteId,
		StopId = due.StopId,
		FeeModelType = due.FeeModelType,
		FeeModelTypeName = due.FeeModelType.ToString(),
		FeeFrequency = due.FeeFrequency,
		Frequency = due.FeeFrequency.ToString(),
		PeriodFrom = due.PeriodFrom,
		PeriodTo = due.PeriodTo,
		DueDate = due.DueDate,
		Amount = due.Amount,
		Status = due.Status,
		StatusName = due.Status.ToString(),
		FinanceChargeRequestId = due.FinanceChargeRequestId,
		FinanceChargeLineId = due.FinanceChargeLineId,
		FinancePostedAt = due.FinancePostedAt,
		MemberName = due.MemberName,
		MemberCode = due.MemberCode,
		RouteName = due.RouteName,
		RouteCode = due.RouteCode,
		StopName = due.StopName,
		CreationTime = due.CreationTime,
		CreatorId = due.CreatorId,
		LastModificationTime = due.LastModificationTime,
		LastModifierId = due.LastModifierId
	};

	private static string BuildChargeName(TransportFeeRun run) =>
		$"Transport Fee {run.PeriodFrom:MMM yyyy}";

	private static string BuildChargePurpose(TransportFeeRun run) =>
		$"Transport fee for {run.PeriodFrom:dd MMM yyyy} to {run.PeriodTo:dd MMM yyyy}";

	private static string BuildDueKey(Guid memberId, Guid assignmentId) =>
		$"{memberId:N}:{assignmentId:N}";

	private static TransportOptionDto ToOption(string value, string label) => new()
	{
		Id = Guid.Empty,
		Value = value,
		Label = label,
		Name = label
	};

	private static string ToModelWireValue(TransportFeeModelType modelType) =>
		modelType == TransportFeeModelType.DistanceBased ? "distance-based" : "stop-based";

	private static string NormalizeRequiredName(string? value, string message)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new UserFriendlyException(message);
		}

		return string.Join(" ", value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
	}

	private static string NormalizeName(string value) =>
		string.Join(" ", value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

	private static string NormalizeToken(string? value) =>
		string.IsNullOrWhiteSpace(value)
			? string.Empty
			: new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

	private sealed record ConfigurationInput(
		TransportFeeModelType ModelType,
		TransportFeeFrequency Frequency,
		decimal? RatePerKm,
		Guid ChargeCategoryId,
		DateTime EffectiveFrom,
		bool IsActive);

	private sealed record RunContext(
		Guid AcademicMediumId,
		Guid AcademicYearId,
		TransportFeeConfiguration Configuration,
		DateTime PeriodFrom,
		DateTime PeriodTo,
		DateTime DueDate);

	private sealed record FinancePostGroupKey(
		decimal Amount,
		DateTime DueDate,
		Guid ChargeCategoryId,
		Guid BoardId,
		Guid EducationMediumId);
}
