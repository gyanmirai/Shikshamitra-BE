using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Transport;

[Authorize(ShikshaMitraSubModulePermissions.ApplicationManagement)]
[ExposeServices(typeof(ITransportMemberAppService), typeof(ITransportApplicationAppService), typeof(TransportMemberApplicationAppService))]
[RemoteService(false)]
public class TransportMemberApplicationAppService(
	IRepository<TransportMember, Guid> memberRepository,
	IRepository<TransportApplication, Guid> applicationRepository,
	IRepository<TransportMemberRouteAssignment, Guid> memberAssignmentRepository,
	IRepository<TransportRoute, Guid> routeRepository,
	IRepository<TransportStop, Guid> stopRepository,
	IRepository<TransportRouteStop, Guid> routeStopRepository,
	IRepository<TransportRouteAssignment, Guid> routeVehicleAssignmentRepository,
	IRepository<TransportVehicle, Guid> vehicleRepository,
	IStudentRepository studentRepository,
	IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
	IRepository<StudentContactDetail, Guid> studentContactDetailRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<EmployeeAdditionalDetails, Guid> employeeAdditionalDetailsRepository,
	IRepository<EducationMedium, Guid> academicMediumRepository,
	StudentScopeAccessService studentScopeAccessService
) : ApplicationService, ITransportMemberAppService, ITransportApplicationAppService
{
	private static readonly TransportApplicationStatus[] OpenApplicationStatuses =
		[TransportApplicationStatus.Pending, TransportApplicationStatus.Hold];

	public async Task<PagedResultDto<TransportMemberCandidateDto>> GetCandidatesAsync(TransportMemberCandidateListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetCandidatesAsync));

		if (input.MemberType == TransportMemberType.Student)
		{
			var result = await GetStudentCandidatesAsync(input, input.SkipCount, input.NormalizedPageSize);
			return new PagedResultDto<TransportMemberCandidateDto>(result.TotalCount, result.Rows);
		}

		if (input.MemberType == TransportMemberType.Employee)
		{
			var result = await GetEmployeeCandidatesAsync(input, input.SkipCount, input.NormalizedPageSize);
			return new PagedResultDto<TransportMemberCandidateDto>(result.TotalCount, result.Rows);
		}

		var combinedTake = input.SkipCount + input.NormalizedPageSize;
		var studentCandidates = await GetStudentCandidatesAsync(input, 0, combinedTake);
		var employeeCandidates = await GetEmployeeCandidatesAsync(input, 0, combinedTake);
		var rows = studentCandidates.Rows
			.Concat(employeeCandidates.Rows)
			.OrderBy(candidate => candidate.Name)
			.Skip(input.SkipCount)
			.Take(input.NormalizedPageSize)
			.ToList();

		return new PagedResultDto<TransportMemberCandidateDto>(studentCandidates.TotalCount + employeeCandidates.TotalCount, rows);
	}

	public async Task<PagedResultDto<TransportMemberDto>> GetListAsync(TransportMemberListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetListAsync));

		var query = await memberRepository.GetQueryableAsync();
		query = query.Where(member => member.AcademicMediumId == input.AcademicMediumId);

		if (input.MemberType.HasValue)
		{
			query = query.Where(member => member.MemberType == input.MemberType.Value);
		}

		if (input.MemberStatus.HasValue)
		{
			query = query.Where(member => member.Status == input.MemberStatus.Value);
		}

		if (input.RouteId.HasValue)
		{
			query = query.Where(member => member.CurrentRouteId == input.RouteId.Value);
		}

		if (input.StopId.HasValue)
		{
			query = query.Where(member => member.CurrentStopId == input.StopId.Value);
		}

		var members = await AsyncExecuter.ToListAsync(query);
		var search = NormalizeSearch(input.Search);
		if (!string.IsNullOrWhiteSpace(search))
		{
			members = members
				.Where(member => MatchesMemberSearch(member, search))
				.ToList();
		}

		var totalCount = members.Count;
		var rows = members
			.OrderBy(member => member.DisplayName)
			.Skip(input.SkipCount)
			.Take(input.NormalizedPageSize)
			.ToList();

		return new PagedResultDto<TransportMemberDto>(
			totalCount,
			await MapMembersAsync(rows));
	}

	async Task<TransportMemberDto> ITransportMemberAppService.GetAsync(Guid memberId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(ITransportMemberAppService.GetAsync));
		var member = await GetMemberForMediumAsync(memberId, academicMediumId);
		return (await MapMembersAsync([member])).Single();
	}

	public async Task<PagedResultDto<TransportMemberRouteAssignmentDto>> GetAssignmentsAsync(Guid memberId, TransportMemberAssignmentListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetAssignmentsAsync));
		await GetMemberForMediumAsync(memberId, input.AcademicMediumId);

		var query = await memberAssignmentRepository.GetQueryableAsync();
		query = query.Where(assignment =>
			assignment.AcademicMediumId == input.AcademicMediumId &&
			assignment.MemberId == memberId);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var assignments = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(assignment => assignment.EffectiveFrom)
				.Skip(input.SkipCount)
				.Take(input.NormalizedPageSize));

		return new PagedResultDto<TransportMemberRouteAssignmentDto>(
			totalCount,
			await MapMemberAssignmentsAsync(assignments));
	}

	public async Task<TransportMemberDto> ChangeStatusAsync(Guid memberId, Guid academicMediumId, ChangeTransportMemberStatusDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(ChangeStatusAsync));
		var member = await GetMemberForMediumAsync(memberId, academicMediumId);
		var effectiveDate = input.EffectiveDate ?? DateTime.UtcNow;

		if (input.Status == TransportMemberStatus.Inactive)
		{
			await CloseCurrentMemberAssignmentAsync(member.Id, academicMediumId, effectiveDate, input.Remarks);
			member.Deactivate(effectiveDate, input.Remarks);
			await SyncSourceTransportFlagAsync(member.MemberType, member.SourceId, false);
		}
		else
		{
			var latestAssignment = await GetLatestMemberAssignmentAsync(member.Id, academicMediumId);
			if (latestAssignment == null)
			{
				throw new BusinessException(TransportErrorCodes.ApplicationRouteStopInvalid)
					.WithData("MemberId", member.Id);
			}

			var validation = await ValidateRouteStopAssignmentAsync(
				academicMediumId,
				latestAssignment.RouteId,
				latestAssignment.StopId,
				member.Id);
			if (member.Status != TransportMemberStatus.Active || member.CurrentRouteId != validation.Route.Id || member.CurrentStopId != validation.Stop.Id)
			{
				var assignment = new TransportMemberRouteAssignment(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					academicMediumId,
					member.Id,
					validation.Route.Id,
					validation.Stop.Id,
					null,
					effectiveDate,
					validation.Stop.Amount,
					input.Remarks);
				await memberAssignmentRepository.InsertAsync(assignment, autoSave: false);
			}

			member.Activate(
				validation.Route.Id,
				validation.Stop.Id,
				effectiveDate,
				input.Remarks);
			await SyncSourceTransportFlagAsync(member.MemberType, member.SourceId, true);
		}

		await memberRepository.UpdateAsync(member, autoSave: true);
		return (await MapMembersAsync([member])).Single();
	}

	public async Task<PagedResultDto<TransportApplicationDto>> GetListAsync(TransportApplicationListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetListAsync));

		var query = await applicationRepository.GetQueryableAsync();
		query = query.Where(application => application.AcademicMediumId == input.AcademicMediumId);

		if (input.MemberType.HasValue)
		{
			query = query.Where(application => application.MemberType == input.MemberType.Value);
		}

		if (input.ApplicationStatus.HasValue)
		{
			query = query.Where(application => application.Status == input.ApplicationStatus.Value);
		}

		if (input.ApplicationType.HasValue)
		{
			query = query.Where(application => application.ApplicationType == input.ApplicationType.Value);
		}

		var applications = await AsyncExecuter.ToListAsync(query);
		var search = NormalizeSearch(input.Search);
		if (!string.IsNullOrWhiteSpace(search))
		{
			applications = applications
				.Where(application => MatchesApplicationSearch(application, search))
				.ToList();
		}

		var totalCount = applications.Count;
		var rows = applications
			.OrderByDescending(application => application.CreationTime)
			.Skip(input.SkipCount)
			.Take(input.NormalizedPageSize)
			.ToList();

		return new PagedResultDto<TransportApplicationDto>(
			totalCount,
			await MapApplicationsAsync(rows));
	}

	async Task<TransportApplicationDto> ITransportApplicationAppService.GetAsync(Guid applicationId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(ITransportApplicationAppService.GetAsync));
		var application = await GetApplicationForMediumAsync(applicationId, academicMediumId);
		return (await MapApplicationsAsync([application])).Single();
	}

	public async Task<TransportApplicationDto> CreateAsync(Guid academicMediumId, CreateUpdateTransportApplicationDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(CreateAsync));
		NormalizeInput(input);

		TransportMember? existingMember = null;
		if (input.ApplicationType == TransportApplicationType.StopChange)
		{
			existingMember = await ResolveExistingMemberForStopChangeAsync(academicMediumId, input);
			input.MemberType = existingMember.MemberType;
			input.MemberId = existingMember.Id;
			input.StudentId = existingMember.StudentId;
			input.EmployeeId = existingMember.EmployeeId;
		}

		var snapshot = await GetSourceSnapshotAsync(academicMediumId, input.MemberType, input.StudentId, input.EmployeeId);
		await EnsureApplicationCanBeCreatedAsync(academicMediumId, input, snapshot.SourceId, existingMember?.Id);
		if (input.RequestedStopId.HasValue)
		{
			await GetActiveStopForMediumAsync(input.RequestedStopId.Value, academicMediumId);
		}

		var application = new TransportApplication(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicMediumId,
			input.ApplicationType,
			input.MemberType,
			input.MemberId,
			input.StudentId,
			input.EmployeeId,
			input.RequestedStopId,
			input.PickupType,
			input.Landmark,
			input.AreaLocality,
			input.AddressLine1,
			input.Description,
			snapshot.Name,
			snapshot.MemberCode,
			snapshot.Role,
			snapshot.ClassName,
			snapshot.DivisionName,
			snapshot.PhoneNo,
			snapshot.PhotoUrl);

		await applicationRepository.InsertAsync(application, autoSave: true);
		return (await MapApplicationsAsync([application])).Single();
	}

	public async Task<TransportApplicationDto> UpdateAsync(Guid applicationId, Guid academicMediumId, CreateUpdateTransportApplicationDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(UpdateAsync));
		NormalizeInput(input);

		var application = await GetApplicationForMediumAsync(applicationId, academicMediumId);
		EnsureApplicationEditable(application);

		if (application.ApplicationType == TransportApplicationType.StopChange && application.MemberId.HasValue)
		{
			var member = await GetMemberForMediumAsync(application.MemberId.Value, academicMediumId);
			input.MemberType = member.MemberType;
			input.StudentId = member.StudentId;
			input.EmployeeId = member.EmployeeId;
			input.MemberId = member.Id;
		}

		var snapshot = await GetSourceSnapshotAsync(academicMediumId, input.MemberType, input.StudentId, input.EmployeeId);
		if (input.RequestedStopId.HasValue)
		{
			await GetActiveStopForMediumAsync(input.RequestedStopId.Value, academicMediumId);
		}

		application.UpdateRequest(input.RequestedStopId, input.PickupType, input.Landmark, input.AreaLocality, input.AddressLine1, input.Description);
		application.UpdateApplicantSnapshot(
			snapshot.Name,
			snapshot.MemberCode,
			snapshot.Role,
			snapshot.ClassName,
			snapshot.DivisionName,
			snapshot.PhoneNo,
			snapshot.PhotoUrl);

		await applicationRepository.UpdateAsync(application, autoSave: true);
		return (await MapApplicationsAsync([application])).Single();
	}

	public async Task<List<TransportSuggestedRouteDto>> GetSuggestedRoutesAsync(Guid applicationId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetSuggestedRoutesAsync));
		var application = await GetApplicationForMediumAsync(applicationId, academicMediumId);

		var routeQuery = await routeRepository.GetQueryableAsync();
		var routeStopQuery = await routeStopRepository.GetQueryableAsync();
		var stopQuery = await stopRepository.GetQueryableAsync();

		var rows = await AsyncExecuter.ToListAsync(
			from route in routeQuery
			join routeStop in routeStopQuery on route.Id equals routeStop.RouteId
			join stop in stopQuery on routeStop.StopId equals stop.Id
			where route.AcademicMediumId == academicMediumId &&
				route.Status == TransportRecordStatus.Active &&
				stop.AcademicMediumId == academicMediumId &&
				stop.Status == TransportRecordStatus.Active &&
				(!application.RequestedStopId.HasValue || routeStop.StopId == application.RequestedStopId.Value)
			select route);

		var routeIds = rows.Select(route => route.Id).Distinct().ToList();
		var routes = await routeRepository.GetListAsync(route => routeIds.Contains(route.Id));
		var routeStops = await GetRouteStopsForRoutesAsync(routeIds, academicMediumId);
		var capacities = await GetRouteCapacityRowsAsync(routeIds, academicMediumId);

		return routes
			.OrderBy(route => route.RouteName)
			.Select(route =>
			{
				capacities.TryGetValue(route.Id, out var capacity);
				capacity ??= new RouteCapacityRow(0, 0);
				routeStops.TryGetValue(route.Id, out var stops);
				return new TransportSuggestedRouteDto
				{
					RouteId = route.Id,
					RouteName = route.RouteName,
					RouteCode = route.RouteCode,
					StartPoint = route.StartPoint,
					EndPoint = route.EndPoint,
					Capacity = capacity.Capacity,
					OccupiedSeats = capacity.OccupiedSeats,
					RemainingSeats = capacity.Capacity > 0 ? Math.Max(capacity.Capacity - capacity.OccupiedSeats, 0) : 0,
					Status = route.Status.ToString(),
					Stops = stops ?? new List<TransportRouteStopDto>()
				};
			})
			.ToList();
	}

	public async Task<TransportApplicationDto> AssignRouteStopAsync(Guid applicationId, Guid academicMediumId, AssignTransportApplicationRouteStopDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(AssignRouteStopAsync));
		var application = await GetApplicationForMediumAsync(applicationId, academicMediumId);
		EnsureApplicationEditable(application);

		var validation = await ValidateRouteStopAssignmentAsync(academicMediumId, input.RouteId, input.StopId, null);
		application.AssignRouteStop(input.RouteId, input.StopId, validation.Stop.Amount);

		await applicationRepository.UpdateAsync(application, autoSave: true);
		return (await MapApplicationsAsync([application])).Single();
	}

	public async Task<TransportApplicationDto> ChangeStatusAsync(Guid applicationId, Guid academicMediumId, ChangeTransportApplicationStatusDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(ChangeStatusAsync));
		var application = await GetApplicationForMediumAsync(applicationId, academicMediumId);

		if (application.Status == TransportApplicationStatus.Approved ||
			application.Status == TransportApplicationStatus.Rejected ||
			application.Status == TransportApplicationStatus.Cancelled)
		{
			throw new BusinessException(TransportErrorCodes.ApplicationInvalidStatus)
				.WithData("ApplicationId", applicationId)
				.WithData("Status", application.Status.ToString());
		}

		if (input.Status == TransportApplicationStatus.Approved)
		{
			await ApproveApplicationAsync(application, input);
		}
		else if (input.Status is TransportApplicationStatus.Rejected or TransportApplicationStatus.Hold or TransportApplicationStatus.Cancelled)
		{
			application.ChangeStatus(input.Status, CurrentUser.Id, input.Remarks);
		}
		else
		{
			throw new BusinessException(TransportErrorCodes.ApplicationInvalidStatus)
				.WithData("Status", input.Status.ToString());
		}

		await applicationRepository.UpdateAsync(application, autoSave: true);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return (await MapApplicationsAsync([application])).Single();
	}

	private async Task ApproveApplicationAsync(TransportApplication application, ChangeTransportApplicationStatusDto input)
	{
		if (!application.AssignedRouteId.HasValue || !application.AssignedStopId.HasValue)
		{
			throw new BusinessException(TransportErrorCodes.ApplicationRouteStopInvalid)
				.WithData("ApplicationId", application.Id);
		}

		var member = await FindMemberBySourceAsync(application.AcademicMediumId, application.MemberType, application.SourceId);
		if (application.ApplicationType == TransportApplicationType.NewEnrollment &&
			member is { Status: TransportMemberStatus.Active })
		{
			throw new BusinessException(TransportErrorCodes.MemberAlreadyExists)
				.WithData("SourceId", application.SourceId);
		}

		if (application.ApplicationType == TransportApplicationType.StopChange)
		{
			member = application.MemberId.HasValue
				? await GetMemberForMediumAsync(application.MemberId.Value, application.AcademicMediumId)
				: member;

			if (member == null || member.Status != TransportMemberStatus.Active)
			{
				throw new BusinessException(TransportErrorCodes.MemberNotFound)
					.WithData("SourceId", application.SourceId);
			}
		}

		var validation = await ValidateRouteStopAssignmentAsync(
			application.AcademicMediumId,
			application.AssignedRouteId.Value,
			application.AssignedStopId.Value,
			member?.Id);

		var effectiveDate = input.EffectiveDate ?? DateTime.UtcNow;
		var snapshot = await GetSourceSnapshotAsync(application.AcademicMediumId, application.MemberType, application.StudentId, application.EmployeeId);

		if (member == null)
		{
			member = new TransportMember(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				application.AcademicMediumId,
				application.MemberType,
				application.StudentId,
				application.EmployeeId,
				snapshot.Name,
				snapshot.MemberCode,
				snapshot.Role,
				snapshot.ClassName,
				snapshot.DivisionName,
				snapshot.PhoneNo,
				snapshot.PhotoUrl,
				input.Remarks);
			await memberRepository.InsertAsync(member, autoSave: false);
		}
		else
		{
			member.UpdateSnapshot(snapshot.Name, snapshot.MemberCode, snapshot.Role, snapshot.ClassName, snapshot.DivisionName, snapshot.PhoneNo, snapshot.PhotoUrl);
			await CloseCurrentMemberAssignmentAsync(member.Id, application.AcademicMediumId, effectiveDate, input.Remarks ?? "Transport reassigned");
		}

		member.Activate(validation.Route.Id, validation.Stop.Id, effectiveDate, input.Remarks);
		await memberRepository.UpdateAsync(member, autoSave: false);

		var assignment = new TransportMemberRouteAssignment(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			application.AcademicMediumId,
			member.Id,
			validation.Route.Id,
			validation.Stop.Id,
			application.Id,
			effectiveDate,
			validation.Stop.Amount,
			input.Remarks);

		await memberAssignmentRepository.InsertAsync(assignment, autoSave: false);
		await SyncSourceTransportFlagAsync(member.MemberType, member.SourceId, true);

		application.LinkMember(member.Id);
		application.AssignRouteStop(validation.Route.Id, validation.Stop.Id, validation.Stop.Amount);
		application.ChangeStatus(TransportApplicationStatus.Approved, CurrentUser.Id, input.Remarks);
	}

	private async Task<TransportMember> ResolveExistingMemberForStopChangeAsync(Guid academicMediumId, CreateUpdateTransportApplicationDto input)
	{
		TransportMember? member = null;
		if (input.MemberId.HasValue)
		{
			member = await GetMemberForMediumAsync(input.MemberId.Value, academicMediumId);
		}
		else
		{
			var sourceId = input.MemberType == TransportMemberType.Student ? input.StudentId : input.EmployeeId;
			if (sourceId.HasValue)
			{
				member = await FindMemberBySourceAsync(academicMediumId, input.MemberType, sourceId.Value);
			}
		}

		if (member == null || member.Status != TransportMemberStatus.Active)
		{
			throw new BusinessException(TransportErrorCodes.MemberNotFound);
		}

		return member;
	}

	private async Task EnsureApplicationCanBeCreatedAsync(
		Guid academicMediumId,
		CreateUpdateTransportApplicationDto input,
		Guid sourceId,
		Guid? existingMemberId)
	{
		var existingMember = existingMemberId.HasValue
			? await memberRepository.FindAsync(existingMemberId.Value)
			: await FindMemberBySourceAsync(academicMediumId, input.MemberType, sourceId);

		if (input.ApplicationType == TransportApplicationType.NewEnrollment &&
			existingMember is { Status: TransportMemberStatus.Active })
		{
			throw new BusinessException(TransportErrorCodes.MemberAlreadyExists)
				.WithData("SourceId", sourceId);
		}

		var query = await applicationRepository.GetQueryableAsync();
		var hasOpenApplication = await AsyncExecuter.AnyAsync(query.Where(application =>
			application.AcademicMediumId == academicMediumId &&
			application.MemberType == input.MemberType &&
			application.ApplicationType == input.ApplicationType &&
			OpenApplicationStatuses.Contains(application.Status) &&
			(input.MemberType == TransportMemberType.Student
				? application.StudentId == sourceId
				: application.EmployeeId == sourceId)));

		if (hasOpenApplication)
		{
			throw new BusinessException(TransportErrorCodes.ApplicationDuplicate)
				.WithData("SourceId", sourceId);
		}
	}

	private async Task<RouteStopValidation> ValidateRouteStopAssignmentAsync(
		Guid academicMediumId,
		Guid routeId,
		Guid stopId,
		Guid? excludingMemberId)
	{
		var route = await GetActiveRouteForMediumAsync(routeId, academicMediumId);
		var stop = await GetActiveStopForMediumAsync(stopId, academicMediumId);
		var routeStop = await routeStopRepository.FirstOrDefaultAsync(row => row.RouteId == routeId && row.StopId == stopId);
		if (routeStop == null)
		{
			throw new BusinessException(TransportErrorCodes.ApplicationRouteStopInvalid)
				.WithData("RouteId", routeId)
				.WithData("StopId", stopId);
		}

		var capacity = await GetRouteCapacityAsync(routeId, academicMediumId, excludingMemberId);
		if (capacity.Capacity > 0 && capacity.OccupiedSeats >= capacity.Capacity)
		{
			throw new BusinessException(TransportErrorCodes.ApplicationCapacityExceeded)
				.WithData("RouteId", routeId)
				.WithData("Capacity", capacity.Capacity);
		}

		return new RouteStopValidation(route, stop);
	}

	private async Task<TransportRoute> GetActiveRouteForMediumAsync(Guid routeId, Guid academicMediumId)
	{
		var route = await routeRepository.FindAsync(routeId);
		if (route == null || route.AcademicMediumId != academicMediumId || route.Status != TransportRecordStatus.Active)
		{
			throw new BusinessException(TransportErrorCodes.ApplicationRouteStopInvalid)
				.WithData("RouteId", routeId);
		}

		return route;
	}

	private async Task<TransportStop> GetActiveStopForMediumAsync(Guid stopId, Guid academicMediumId)
	{
		var stop = await stopRepository.FindAsync(stopId);
		if (stop == null || stop.AcademicMediumId != academicMediumId || stop.Status != TransportRecordStatus.Active)
		{
			throw new BusinessException(TransportErrorCodes.ApplicationRouteStopInvalid)
				.WithData("StopId", stopId);
		}

		return stop;
	}

	private async Task<TransportMember> GetMemberForMediumAsync(Guid memberId, Guid academicMediumId)
	{
		var member = await memberRepository.FindAsync(memberId);
		if (member == null || member.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportMember), memberId);
		}

		return member;
	}

	private async Task<TransportApplication> GetApplicationForMediumAsync(Guid applicationId, Guid academicMediumId)
	{
		var application = await applicationRepository.FindAsync(applicationId);
		if (application == null || application.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportApplication), applicationId);
		}

		return application;
	}

	private async Task<TransportMember?> FindMemberBySourceAsync(Guid academicMediumId, TransportMemberType memberType, Guid sourceId)
	{
		var query = await memberRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(member =>
			member.AcademicMediumId == academicMediumId &&
			member.MemberType == memberType &&
			(memberType == TransportMemberType.Student
				? member.StudentId == sourceId
				: member.EmployeeId == sourceId)));
	}

	private async Task CloseCurrentMemberAssignmentAsync(Guid memberId, Guid academicMediumId, DateTime effectiveTo, string? remarks)
	{
		var query = await memberAssignmentRepository.GetQueryableAsync();
		var current = await AsyncExecuter.ToListAsync(query.Where(assignment =>
			assignment.AcademicMediumId == academicMediumId &&
			assignment.MemberId == memberId &&
			assignment.EffectiveTo == null));

		foreach (var assignment in current)
		{
			assignment.Close(effectiveTo, remarks);
			await memberAssignmentRepository.UpdateAsync(assignment, autoSave: false);
		}
	}

	private async Task<TransportMemberRouteAssignment?> GetLatestMemberAssignmentAsync(Guid memberId, Guid academicMediumId)
	{
		var query = await memberAssignmentRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(
			query
				.Where(assignment =>
					assignment.AcademicMediumId == academicMediumId &&
					assignment.MemberId == memberId)
				.OrderByDescending(assignment => assignment.EffectiveFrom));
	}

	private async Task SyncSourceTransportFlagAsync(TransportMemberType memberType, Guid sourceId, bool isTransportMember)
	{
		if (memberType == TransportMemberType.Student)
		{
			var additionalDetail = await studentAdditionalDetailRepository.FindAsync(sourceId);
			if (additionalDetail != null)
			{
				additionalDetail.IsTransportMember = isTransportMember;
				await studentAdditionalDetailRepository.UpdateAsync(additionalDetail, autoSave: false);
			}

			return;
		}

		var detailsQuery = await employeeAdditionalDetailsRepository.GetQueryableAsync();
		var details = await AsyncExecuter.FirstOrDefaultAsync(detailsQuery.Where(row => row.EmployeeId == sourceId));
		if (details == null)
		{
			details = new EmployeeAdditionalDetails(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				sourceId,
				null,
				false,
				null,
				false,
				isTransportMember);
			await employeeAdditionalDetailsRepository.InsertAsync(details, autoSave: false);
			return;
		}

		details.Patch(null, null, null, null, isTransportMember);
		await employeeAdditionalDetailsRepository.UpdateAsync(details, autoSave: false);
	}

	private async Task<MemberSourceSnapshot> GetSourceSnapshotAsync(
		Guid academicMediumId,
		TransportMemberType memberType,
		Guid? studentId,
		Guid? employeeId)
	{
		if (memberType == TransportMemberType.Student)
		{
			if (!studentId.HasValue)
			{
				throw new BusinessException(TransportErrorCodes.MemberSourceInvalid);
			}

			var student = await studentScopeAccessService.GetStudentAsync(studentId.Value, nameof(GetSourceSnapshotAsync));
			if (student.AcademicMediumId != academicMediumId || student.Status is StudentLifeCycleStatusEnum.InActive or StudentLifeCycleStatusEnum.Left)
			{
				throw new BusinessException(TransportErrorCodes.MemberSourceInvalid)
					.WithData("StudentId", studentId.Value);
			}

			return new MemberSourceSnapshot(
				TransportMemberType.Student,
				student.Id,
				GetStudentName(student),
				student.GrNo?.ToString(),
				"Student",
				student.StandardId?.ToString(),
				student.DivisionId?.ToString(),
				student.ContactDetail?.PrimaryContactNo,
				student.PhotoBlobName);
		}

		if (!employeeId.HasValue)
		{
			throw new BusinessException(TransportErrorCodes.MemberSourceInvalid);
		}

		var employee = await employeeRepository.FindAsync(employeeId.Value);
		if (employee == null ||
			(employee.EmployeeStatusType.HasValue && employee.EmployeeStatusType != EmployeeStatusType.Active))
		{
			throw new BusinessException(TransportErrorCodes.MemberSourceInvalid)
				.WithData("EmployeeId", employeeId.Value);
		}

		return new MemberSourceSnapshot(
			TransportMemberType.Employee,
			employee.Id,
			GetEmployeeName(employee),
			employee.EmployeeCode,
			string.IsNullOrWhiteSpace(employee.Designation) ? "Employee" : employee.Designation,
			null,
			null,
			employee.MobileNo,
			employee.ProfilePictureBlobName);
	}

	private async Task<CandidateQueryResult> GetStudentCandidatesAsync(TransportMemberCandidateListRequestDto input, int skipCount, int pageSize)
	{
		var search = NormalizeSearch(input.Search);
		var activeSourceIds = await GetSourceIdsWithActiveMembershipAsync(input.AcademicMediumId, TransportMemberType.Student);
		var openSourceIds = await GetSourceIdsWithOpenApplicationAsync(input.AcademicMediumId, TransportMemberType.Student);
		var query = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		query = query.Where(student =>
			student.AcademicMediumId == input.AcademicMediumId &&
			student.Status != StudentLifeCycleStatusEnum.InActive &&
			student.Status != StudentLifeCycleStatusEnum.Left);

		if (!string.IsNullOrWhiteSpace(search))
		{
			var searchTerm = GetPrimarySearchTerm(search);
			var hasGrNoSearch = int.TryParse(searchTerm, out var grNoSearch);
			query = query.Where(student =>
				(student.FirstName != null && student.FirstName.ToLower().Contains(searchTerm)) ||
				(student.MiddleName != null && student.MiddleName.ToLower().Contains(searchTerm)) ||
				(student.LastName != null && student.LastName.ToLower().Contains(searchTerm)) ||
				(hasGrNoSearch && student.GrNo == grNoSearch));
		}

		if (input.ExcludeExisting)
		{
			query = query.Where(student =>
				!activeSourceIds.Contains(student.Id) &&
				!openSourceIds.Contains(student.Id));
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var students = await AsyncExecuter.ToListAsync(query
			.OrderBy(student => student.FirstName)
			.ThenBy(student => student.MiddleName)
			.ThenBy(student => student.LastName)
			.Skip(skipCount)
			.Take(pageSize));
		var phoneByStudentId = await GetStudentPhoneMapAsync(students.Select(student => student.Id).ToList());

		var rows = students.Select(student => new TransportMemberCandidateDto
		{
			SourceId = student.Id,
			MemberType = TransportMemberType.Student,
			Name = GetStudentName(student),
			MemberCode = student.GrNo?.ToString(),
			Role = "Student",
			ClassName = student.StandardId?.ToString(),
			DivisionName = student.DivisionId?.ToString(),
			PhoneNo = phoneByStudentId.TryGetValue(student.Id, out var phoneNo) ? phoneNo : null,
			PhotoUrl = student.PhotoBlobName,
			HasActiveMembership = activeSourceIds.Contains(student.Id),
			HasOpenApplication = openSourceIds.Contains(student.Id)
		}).ToList();

		return new CandidateQueryResult(totalCount, rows);
	}

	private async Task<CandidateQueryResult> GetEmployeeCandidatesAsync(TransportMemberCandidateListRequestDto input, int skipCount, int pageSize)
	{
		var search = NormalizeSearch(input.Search);
		var activeSourceIds = await GetSourceIdsWithActiveMembershipAsync(input.AcademicMediumId, TransportMemberType.Employee);
		var openSourceIds = await GetSourceIdsWithOpenApplicationAsync(input.AcademicMediumId, TransportMemberType.Employee);
		var query = await employeeRepository.GetQueryableAsync();
		query = query.Where(employee =>
			!employee.EmployeeStatusType.HasValue ||
			employee.EmployeeStatusType == EmployeeStatusType.Active);

		if (!string.IsNullOrWhiteSpace(search))
		{
			var searchTerm = GetPrimarySearchTerm(search);
			query = query.Where(employee =>
				(employee.FirstName != null && employee.FirstName.ToLower().Contains(searchTerm)) ||
				(employee.MiddleName != null && employee.MiddleName.ToLower().Contains(searchTerm)) ||
				(employee.LastName != null && employee.LastName.ToLower().Contains(searchTerm)) ||
				(employee.EmployeeCode != null && employee.EmployeeCode.ToLower().Contains(searchTerm)) ||
				(employee.Designation != null && employee.Designation.ToLower().Contains(searchTerm)) ||
				(employee.MobileNo != null && employee.MobileNo.ToLower().Contains(searchTerm)));
		}

		if (input.ExcludeExisting)
		{
			query = query.Where(employee =>
				!activeSourceIds.Contains(employee.Id) &&
				!openSourceIds.Contains(employee.Id));
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var employees = await AsyncExecuter.ToListAsync(query
			.OrderBy(employee => employee.FirstName)
			.ThenBy(employee => employee.MiddleName)
			.ThenBy(employee => employee.LastName)
			.Skip(skipCount)
			.Take(pageSize));

		var rows = employees.Select(employee => new TransportMemberCandidateDto
		{
			SourceId = employee.Id,
			MemberType = TransportMemberType.Employee,
			Name = GetEmployeeName(employee),
			MemberCode = employee.EmployeeCode,
			Role = string.IsNullOrWhiteSpace(employee.Designation) ? "Employee" : employee.Designation,
			PhoneNo = employee.MobileNo,
			PhotoUrl = employee.ProfilePictureBlobName,
			HasActiveMembership = activeSourceIds.Contains(employee.Id),
			HasOpenApplication = openSourceIds.Contains(employee.Id)
		}).ToList();

		return new CandidateQueryResult(totalCount, rows);
	}

	private async Task<Dictionary<Guid, string>> GetStudentPhoneMapAsync(List<Guid> studentIds)
	{
		if (studentIds.Count == 0)
		{
			return new Dictionary<Guid, string>();
		}

		var contactQuery = await studentContactDetailRepository.GetQueryableAsync();
		var contacts = await AsyncExecuter.ToListAsync(contactQuery
			.Where(contact => studentIds.Contains(contact.Id))
			.Select(contact => new { contact.Id, contact.PrimaryContactNo }));

		return contacts.ToDictionary(contact => contact.Id, contact => contact.PrimaryContactNo);
	}

	private async Task<HashSet<Guid>> GetSourceIdsWithActiveMembershipAsync(Guid academicMediumId, TransportMemberType memberType)
	{
		var query = await memberRepository.GetQueryableAsync();
		var sourceIds = await AsyncExecuter.ToListAsync(query
			.Where(member =>
				member.AcademicMediumId == academicMediumId &&
				member.MemberType == memberType &&
				member.Status == TransportMemberStatus.Active)
			.Select(member => member.SourceId));

		return sourceIds.ToHashSet();
	}

	private async Task<HashSet<Guid>> GetSourceIdsWithOpenApplicationAsync(Guid academicMediumId, TransportMemberType memberType)
	{
		var query = await applicationRepository.GetQueryableAsync();
		var sourceIds = await AsyncExecuter.ToListAsync(query
			.Where(application =>
				application.AcademicMediumId == academicMediumId &&
				application.MemberType == memberType &&
				OpenApplicationStatuses.Contains(application.Status))
			.Select(application => application.SourceId));

		return sourceIds.ToHashSet();
	}

	private async Task<RouteCapacityRow> GetRouteCapacityAsync(Guid routeId, Guid academicMediumId, Guid? excludingMemberId)
	{
		var capacityRows = await GetRouteCapacityRowsAsync([routeId], academicMediumId, excludingMemberId);
		return capacityRows.TryGetValue(routeId, out var row) ? row : new RouteCapacityRow(0, 0);
	}

	private async Task<IReadOnlyDictionary<Guid, RouteCapacityRow>> GetRouteCapacityRowsAsync(
		List<Guid> routeIds,
		Guid academicMediumId,
		Guid? excludingMemberId = null)
	{
		if (routeIds.Count == 0)
		{
			return new Dictionary<Guid, RouteCapacityRow>();
		}

		var assignmentQuery = await routeVehicleAssignmentRepository.GetQueryableAsync();
		var currentVehicleAssignments = await AsyncExecuter.ToListAsync(assignmentQuery.Where(assignment =>
			assignment.AcademicMediumId == academicMediumId &&
			assignment.EffectiveTo == null &&
			routeIds.Contains(assignment.RouteId)));

		var vehicleIds = currentVehicleAssignments.Select(assignment => assignment.VehicleId).Distinct().ToList();
		var vehicles = (await vehicleRepository.GetListAsync(vehicle => vehicleIds.Contains(vehicle.Id)))
			.ToDictionary(vehicle => vehicle.Id);

		var memberAssignmentQuery = await memberAssignmentRepository.GetQueryableAsync();
		var currentMemberAssignments = await AsyncExecuter.ToListAsync(memberAssignmentQuery.Where(assignment =>
			assignment.AcademicMediumId == academicMediumId &&
			assignment.EffectiveTo == null &&
			routeIds.Contains(assignment.RouteId) &&
			(!excludingMemberId.HasValue || assignment.MemberId != excludingMemberId.Value)));

		return routeIds.ToDictionary(
			routeId => routeId,
			routeId =>
			{
				var vehicleAssignment = currentVehicleAssignments.FirstOrDefault(assignment => assignment.RouteId == routeId);
				var capacity = vehicleAssignment != null && vehicles.TryGetValue(vehicleAssignment.VehicleId, out var vehicle)
					? vehicle.SeatingCapacity
					: 0;
				var occupied = currentMemberAssignments.Count(assignment => assignment.RouteId == routeId);
				return new RouteCapacityRow(capacity, occupied);
			});
	}

	private async Task<IReadOnlyDictionary<Guid, List<TransportRouteStopDto>>> GetRouteStopsForRoutesAsync(List<Guid> routeIds, Guid academicMediumId)
	{
		if (routeIds.Count == 0)
		{
			return new Dictionary<Guid, List<TransportRouteStopDto>>();
		}

		var routeStopQuery = await routeStopRepository.GetQueryableAsync();
		var stopQuery = await stopRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(
			from routeStop in routeStopQuery
			join stop in stopQuery on routeStop.StopId equals stop.Id
			where routeIds.Contains(routeStop.RouteId) && stop.AcademicMediumId == academicMediumId
			orderby routeStop.StopSequence
			select new { routeStop.RouteId, RouteStop = routeStop, Stop = stop });

		return rows
			.GroupBy(row => row.RouteId)
			.ToDictionary(
				group => group.Key,
				group => group.Select(row => new TransportRouteStopDto
				{
					Id = row.RouteStop.Id,
					StopId = row.RouteStop.StopId,
					StopName = row.Stop.StopName,
					Order = row.RouteStop.StopSequence,
					ExpectedArrivalTime = row.RouteStop.ExpectedArrivalTime,
					ExpectedDepartureTime = row.RouteStop.ExpectedDepartureTime,
					DistanceFromPrevious = row.RouteStop.DistanceFromPrevious,
					CreationTime = row.RouteStop.CreationTime,
					CreatorId = row.RouteStop.CreatorId,
					LastModificationTime = row.RouteStop.LastModificationTime,
					LastModifierId = row.RouteStop.LastModifierId
				}).ToList());
	}

	private async Task<List<TransportMemberDto>> MapMembersAsync(List<TransportMember> members)
	{
		if (members.Count == 0)
		{
			return new List<TransportMemberDto>();
		}

		var routeIds = members.Select(member => member.CurrentRouteId ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
		var stopIds = members.Select(member => member.CurrentStopId ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
		var routes = (await routeRepository.GetListAsync(route => routeIds.Contains(route.Id))).ToDictionary(route => route.Id);
		var stops = (await stopRepository.GetListAsync(stop => stopIds.Contains(stop.Id))).ToDictionary(stop => stop.Id);

		return members.Select(member =>
		{
			TransportRoute? route = null;
			if (member.CurrentRouteId.HasValue)
			{
				routes.TryGetValue(member.CurrentRouteId.Value, out route);
			}

			TransportStop? stop = null;
			if (member.CurrentStopId.HasValue)
			{
				stops.TryGetValue(member.CurrentStopId.Value, out stop);
			}

			return new TransportMemberDto
			{
				Id = member.Id,
				AcademicMediumId = member.AcademicMediumId,
				MemberType = member.MemberType,
				StudentId = member.StudentId,
				EmployeeId = member.EmployeeId,
				SourceId = member.SourceId,
				Status = member.Status,
				StatusName = member.Status.ToString(),
				CurrentRouteId = member.CurrentRouteId,
				CurrentRouteName = route?.RouteName,
				CurrentStopId = member.CurrentStopId,
				CurrentStopName = stop?.StopName,
				EffectiveFrom = member.EffectiveFrom,
				EffectiveTo = member.EffectiveTo,
				Name = member.DisplayName,
				MemberCode = member.MemberCode,
				Role = member.Role,
				ClassName = member.ClassName,
				DivisionName = member.DivisionName,
				PhoneNo = member.PhoneNo,
				PhotoUrl = member.PhotoUrl,
				Remarks = member.Remarks,
				CreationTime = member.CreationTime,
				CreatorId = member.CreatorId,
				LastModificationTime = member.LastModificationTime,
				LastModifierId = member.LastModifierId
			};
		}).ToList();
	}

	private async Task<List<TransportApplicationDto>> MapApplicationsAsync(List<TransportApplication> applications)
	{
		if (applications.Count == 0)
		{
			return new List<TransportApplicationDto>();
		}

		var routeIds = applications.Select(application => application.AssignedRouteId ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
		var stopIds = applications
			.SelectMany(application => new[] { application.RequestedStopId ?? Guid.Empty, application.AssignedStopId ?? Guid.Empty })
			.Where(id => id != Guid.Empty)
			.Distinct()
			.ToList();

		var routes = (await routeRepository.GetListAsync(route => routeIds.Contains(route.Id))).ToDictionary(route => route.Id);
		var stops = (await stopRepository.GetListAsync(stop => stopIds.Contains(stop.Id))).ToDictionary(stop => stop.Id);

		return applications.Select(application =>
		{
			TransportRoute? route = null;
			if (application.AssignedRouteId.HasValue)
			{
				routes.TryGetValue(application.AssignedRouteId.Value, out route);
			}

			TransportStop? requestedStop = null;
			if (application.RequestedStopId.HasValue)
			{
				stops.TryGetValue(application.RequestedStopId.Value, out requestedStop);
			}

			TransportStop? assignedStop = null;
			if (application.AssignedStopId.HasValue)
			{
				stops.TryGetValue(application.AssignedStopId.Value, out assignedStop);
			}

			return new TransportApplicationDto
			{
				Id = application.Id,
				AcademicMediumId = application.AcademicMediumId,
				ApplicationType = application.ApplicationType,
				ApplicationTypeName = application.ApplicationType.ToString(),
				Status = application.Status,
				StatusName = application.Status.ToString(),
				MemberType = application.MemberType,
				MemberId = application.MemberId,
				StudentId = application.StudentId,
				EmployeeId = application.EmployeeId,
				SourceId = application.SourceId,
				RequestedStopId = application.RequestedStopId,
				RequestedStopName = requestedStop?.StopName,
				AssignedRouteId = application.AssignedRouteId,
				AssignedRouteName = route?.RouteName,
				AssignedStopId = application.AssignedStopId,
				AssignedStopName = assignedStop?.StopName,
				PickupType = application.PickupType,
				Landmark = application.Landmark,
				AreaLocality = application.AreaLocality,
				AddressLine1 = application.AddressLine1,
				Description = application.Description,
				EstimatedFeeAmount = application.EstimatedFeeAmount,
				Name = application.ApplicantName,
				MemberCode = application.ApplicantCode,
				Role = application.ApplicantRole,
				ClassName = application.ApplicantClassName,
				DivisionName = application.ApplicantDivisionName,
				PhoneNo = application.ApplicantPhoneNo,
				PhotoUrl = application.ApplicantPhotoUrl,
				StatusRemarks = application.StatusRemarks,
				ReviewedAt = application.ReviewedAt,
				ReviewedById = application.ReviewedById,
				CreationTime = application.CreationTime,
				CreatorId = application.CreatorId,
				LastModificationTime = application.LastModificationTime,
				LastModifierId = application.LastModifierId
			};
		}).ToList();
	}

	private async Task<List<TransportMemberRouteAssignmentDto>> MapMemberAssignmentsAsync(List<TransportMemberRouteAssignment> assignments)
	{
		if (assignments.Count == 0)
		{
			return new List<TransportMemberRouteAssignmentDto>();
		}

		var routeIds = assignments.Select(assignment => assignment.RouteId).Distinct().ToList();
		var stopIds = assignments.Select(assignment => assignment.StopId).Distinct().ToList();
		var routes = (await routeRepository.GetListAsync(route => routeIds.Contains(route.Id))).ToDictionary(route => route.Id);
		var stops = (await stopRepository.GetListAsync(stop => stopIds.Contains(stop.Id))).ToDictionary(stop => stop.Id);

		return assignments.Select(assignment =>
		{
			routes.TryGetValue(assignment.RouteId, out var route);
			stops.TryGetValue(assignment.StopId, out var stop);
			return new TransportMemberRouteAssignmentDto
			{
				Id = assignment.Id,
				AcademicMediumId = assignment.AcademicMediumId,
				MemberId = assignment.MemberId,
				RouteId = assignment.RouteId,
				RouteName = route?.RouteName ?? string.Empty,
				RouteCode = route?.RouteCode ?? string.Empty,
				StopId = assignment.StopId,
				StopName = stop?.StopName ?? string.Empty,
				ApplicationId = assignment.ApplicationId,
				EffectiveFrom = assignment.EffectiveFrom,
				EffectiveTo = assignment.EffectiveTo,
				IsCurrent = assignment.IsCurrent,
				FeeAmountSnapshot = assignment.FeeAmountSnapshot,
				Remarks = assignment.Remarks,
				CreationTime = assignment.CreationTime,
				CreatorId = assignment.CreatorId,
				LastModificationTime = assignment.LastModificationTime,
				LastModifierId = assignment.LastModifierId
			};
		}).ToList();
	}

	private async Task EnsureCanUseMediumAsync(Guid academicMediumId, string operation)
	{
		await academicMediumRepository.GetAsync(academicMediumId);
		await studentScopeAccessService.EnsureCanAccessMediumAsync(academicMediumId, operation);
	}

	private static void EnsureApplicationEditable(TransportApplication application)
	{
		if (!OpenApplicationStatuses.Contains(application.Status))
		{
			throw new BusinessException(TransportErrorCodes.ApplicationInvalidStatus)
				.WithData("ApplicationId", application.Id)
				.WithData("Status", application.Status.ToString());
		}
	}

	private static void NormalizeInput(CreateUpdateTransportApplicationDto input)
	{
		input.RequestedStopId ??= input.PreferredStopId;
		if (input.StudentId == Guid.Empty)
		{
			input.StudentId = null;
		}

		if (input.EmployeeId == Guid.Empty)
		{
			input.EmployeeId = null;
		}

		if (input.MemberId == Guid.Empty)
		{
			input.MemberId = null;
		}
	}

	private static string? NormalizeSearch(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

	private static string GetPrimarySearchTerm(string search) =>
		search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? search;

	private static bool MatchesCandidateSearch(TransportMemberCandidateDto candidate, string search) =>
		Contains(candidate.Name, search) ||
		Contains(candidate.MemberCode, search) ||
		Contains(candidate.Role, search) ||
		Contains(candidate.ClassName, search) ||
		Contains(candidate.DivisionName, search) ||
		Contains(candidate.PhoneNo, search);

	private static bool MatchesMemberSearch(TransportMember member, string search) =>
		Contains(member.DisplayName, search) ||
		Contains(member.MemberCode, search) ||
		Contains(member.Role, search) ||
		Contains(member.ClassName, search) ||
		Contains(member.DivisionName, search) ||
		Contains(member.PhoneNo, search);

	private static bool MatchesApplicationSearch(TransportApplication application, string search) =>
		Contains(application.ApplicantName, search) ||
		Contains(application.ApplicantCode, search) ||
		Contains(application.ApplicantRole, search) ||
		Contains(application.ApplicantClassName, search) ||
		Contains(application.ApplicantDivisionName, search) ||
		Contains(application.ApplicantPhoneNo, search) ||
		Contains(application.AreaLocality, search) ||
		Contains(application.Landmark, search);

	private static bool Contains(string? value, string search) =>
		!string.IsNullOrWhiteSpace(value) && value.ToLowerInvariant().Contains(search);

	private static string GetStudentName(Student student)
	{
		var parts = new[] { student.FirstName, student.MiddleName, student.LastName }
			.Where(part => !string.IsNullOrWhiteSpace(part));
		var name = string.Join(" ", parts);
		return string.IsNullOrWhiteSpace(name) ? student.Id.ToString("N") : name;
	}

	private static string GetEmployeeName(Employee employee)
	{
		var name = employee.EmployeeName;
		return string.IsNullOrWhiteSpace(name) ? employee.EmployeeCode : name;
	}

	private sealed record MemberSourceSnapshot(
		TransportMemberType MemberType,
		Guid SourceId,
		string Name,
		string? MemberCode,
		string? Role,
		string? ClassName,
		string? DivisionName,
		string? PhoneNo,
		string? PhotoUrl);

	private sealed record CandidateQueryResult(int TotalCount, List<TransportMemberCandidateDto> Rows);

	private sealed record RouteCapacityRow(int Capacity, int OccupiedSeats);

	private sealed record RouteStopValidation(TransportRoute Route, TransportStop Stop);
}
