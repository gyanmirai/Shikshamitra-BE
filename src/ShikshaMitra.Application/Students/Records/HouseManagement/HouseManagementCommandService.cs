using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records.Settings;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records.HouseManagement;

[RemoteService(false)]
public partial class HouseManagementCommandService(
	HouseManagementAccessService access,
	HouseManagementQueryService queries,
	IRepository<HouseSettings, Guid> houses,
	IRepository<StudentEnrollment, Guid> enrollments,
	IRepository<Division, Guid> divisions,
	IRepository<Standard, Guid> standards,
	IRepository<EducationMediumStage, Guid> stages,
	HouseDeletionGuard deletion,
	StudentScopeAccessService scope,
	IUnitOfWorkManager units)
	: ApplicationService, ITransientDependency
{
	public async Task<HouseManagementHouseDto> SaveHouseAsync(Guid? id, SaveHouseRequest input)
	{
		return await TransactionAsync(async () =>
		{
			await access.GetAsync(input.AcademicMediumId, manage: true);
			if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 200 ||
				!HouseManagementInputRules.IsColor(input.Color) || !HouseManagementInputRules.IsLogoUrl(input.LogoUrl) ||
				input.Description?.Length > 1000 || input.LogoUrl?.Length > 500)
				throw new BusinessException(HouseManagementErrorCodes.InvalidInput);
			var normalized = string.Join(" ", input.Name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
			var existing = await houses.GetListAsync(h => h.TenantId == CurrentTenant.Id && h.AcademicMediumId == input.AcademicMediumId && h.Id != id);
			if (existing.Any(h => string.Join(" ", h.Name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant() == normalized))
				throw new BusinessException(StudentErrorCodes.HouseSettingsAlreadyExists);
			HouseSettings house;
			if (id.HasValue)
			{
				house = await HouseAsync(id.Value, input.AcademicMediumId);
				house.EnsureVersion(input.ConcurrencyStamp ?? "");
				house.UpdateDetails(input.Name.Trim(), input.Color, input.LogoUrl, input.Description);
				await houses.UpdateAsync(house, autoSave: true);
			}
			else
			{
				house = new HouseSettings(GuidGenerator.Create(), input.Name.Trim(), input.Color, input.AcademicMediumId, input.LogoUrl, input.Description)
				{
					TenantId = CurrentTenant.Id
				};
				await houses.InsertAsync(house, autoSave: true);
			}

			var roster = await queries.StudentsAsync(input.AcademicMediumId);
			return new HouseManagementHouseDto
			{
				Id = house.Id,
				Name = house.Name,
				Color = house.Color,
				Description = house.Description,
				LogoUrl = house.LogoUrl,
				ConcurrencyStamp = house.ConcurrencyStamp,
				CanManage = true,
				MemberCount = await AsyncExecuter.CountAsync(roster.Where(s => s.HouseId == house.Id)),
				HouseLeaderCount = await AsyncExecuter.CountAsync(roster.Where(s => s.HouseId == house.Id && s.IsHouseLeader)),
				Leader = await AsyncExecuter.SingleOrDefaultAsync(roster.Where(s => s.HouseId == house.Id && s.IsHouseLeader))
			};
		});
	}

	public Task DeleteHouseAsync(Guid id, Guid mediumId, string stamp) => TransactionAsync(async () =>
	{
		await access.GetAsync(mediumId, manage: true);
		var house = await HouseAsync(id, mediumId);
		house.EnsureVersion(stamp);
		await deletion.EnsureEmptyAsync(id);
		house.TouchMembership();
		await houses.DeleteAsync(house, autoSave: true);
		return true;
	});

	public async Task<HouseAssignmentPreviewDto> PreviewAsync(HouseAssignmentRequest input)
	{
		var state = await AssignmentStateAsync(input);
		return state.Preview;
	}

	public Task CommitAsync(HouseAssignmentRequest input) => TransactionAsync(async () =>
	{
		var state = await AssignmentStateAsync(input, committing: true);
		if (string.IsNullOrWhiteSpace(input.PreviewToken) || input.PreviewToken != state.Preview.PreviewToken)
			throw new BusinessException(HouseManagementErrorCodes.StalePreview);
		foreach (var enrollment in state.Enrollments.Where(e => e.HouseId != input.HouseId))
		{
			if (enrollment.HouseId.HasValue)
				state.Houses.Single(h => h.Id == enrollment.HouseId).MemberDeparted(enrollment.Id);
			enrollment.AssignHouse(input.HouseId);
		}

		if (input.HouseId.HasValue)
			state.Houses.Single(h => h.Id == input.HouseId).TouchMembership();
		await enrollments.UpdateManyAsync(state.Enrollments);
		await houses.UpdateManyAsync(state.Houses);
		return true;
	});

	private async Task<(HouseAssignmentPreviewDto Preview, List<StudentEnrollment> Enrollments, List<HouseSettings> Houses)> AssignmentStateAsync(HouseAssignmentRequest input, bool committing = false)
	{
		await access.GetAsync(input.AcademicMediumId);
		if (input.DivisionId == Guid.Empty || input.HouseId == Guid.Empty ||
			input.TargetScope is not (null or HouseAssignmentRequest.DivisionScope))
			throw new BusinessException(HouseManagementErrorCodes.InvalidInput);
		var targetsDivision = input.TargetScope == HouseAssignmentRequest.DivisionScope;
		var selected = targetsDivision
			? await DivisionAssignmentStudentsAsync(input, committing)
			: await SelectedAssignmentStudentsAsync(input);
		var ids = selected.Select(s => s.EnrollmentId).ToArray();
		var rows = await enrollments.GetListAsync(e => e.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, e.Id));
		if (rows.Count != ids.Length || rows.Any(e => e.DivisionId != input.DivisionId))
			throw new BusinessException(HouseManagementErrorCodes.StalePreview);
		var houseIds = rows.Where(e => e.HouseId.HasValue).Select(e => e.HouseId!.Value)
			.Concat(input.HouseId.HasValue ? new[] { input.HouseId.Value } : Array.Empty<Guid>()).Distinct().ToArray();
		var involved = await houses.GetListAsync(h => h.TenantId == CurrentTenant.Id &&
			h.AcademicMediumId == input.AcademicMediumId && Enumerable.Contains(houseIds, h.Id));
		if (involved.Count != houseIds.Length)
			throw new BusinessException(HouseManagementErrorCodes.StalePreview);
		var clearedLeaders = selected.Where(s => s.IsHouseLeader && s.HouseId != input.HouseId).ToList();
		var signature = string.Join("|",
			new[] { CurrentTenant.Id.ToString(), CurrentUser.Id.ToString(), input.AcademicMediumId.ToString(), input.DivisionId.ToString(), input.HouseId.ToString() }
				.Concat(targetsDivision ? new[] { HouseAssignmentRequest.DivisionScope, input.IncludeAssigned.ToString() } : Array.Empty<string>())
				.Concat(rows.OrderBy(e => e.Id).Select(e => $"{e.Id}:{e.ConcurrencyStamp}:{e.HouseId}"))
				.Concat(involved.OrderBy(h => h.Id).Select(h => $"{h.Id}:{h.ConcurrencyStamp}:{h.MembershipRevision}"))
				.Concat(clearedLeaders.OrderBy(s => s.EnrollmentId).Select(s => $"{s.HouseId}:{s.EnrollmentId}")));
		return (new HouseAssignmentPreviewDto
		{
			Students = selected,
			TargetHouseId = input.HouseId,
			TargetHouseName = involved.SingleOrDefault(h => h.Id == input.HouseId)?.Name,
			ClearedHouseLeaders = clearedLeaders,
			PreviewToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)))
		}, rows, involved);
	}

	private async Task<List<HouseManagementStudentDto>> SelectedAssignmentStudentsAsync(HouseAssignmentRequest input)
	{
		if (input.IncludeAssigned || input.EnrollmentIds == null ||
			input.EnrollmentIds.Count is < 1 or > HouseAssignmentRequest.MaxEnrollmentIds ||
			input.EnrollmentIds.Contains(Guid.Empty) || input.EnrollmentIds.Distinct().Count() != input.EnrollmentIds.Count)
			throw new BusinessException(HouseManagementErrorCodes.InvalidInput);
		var ids = input.EnrollmentIds.ToArray();
		var roster = await queries.StudentsAsync(input.AcademicMediumId);
		var selected = await AsyncExecuter.ToListAsync(roster.Where(s => Enumerable.Contains(ids, s.EnrollmentId)));
		if (selected.Count != ids.Length)
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		if (selected.Any(s => s.DivisionId != input.DivisionId))
			throw new BusinessException(HouseManagementErrorCodes.AssignmentDivisionMismatch);
		return selected;
	}

	private async Task<List<HouseManagementStudentDto>> DivisionAssignmentStudentsAsync(HouseAssignmentRequest input, bool committing)
	{
		if (!input.HouseId.HasValue || input.EnrollmentIds?.Count != 0)
			throw new BusinessException(HouseManagementErrorCodes.InvalidInput);
		await ScopedDivisionAsync(input.DivisionId, input.AcademicMediumId, "HouseManagement.DivisionAssignment");

		var roster = await queries.StudentsAsync(input.AcademicMediumId);
		var divisionStudents = roster.Where(s => s.DivisionId == input.DivisionId);
		var affected = input.IncludeAssigned
			? divisionStudents.Where(s => s.HouseId != input.HouseId)
			: divisionStudents.Where(s => s.HouseId == null);
		var selected = await AsyncExecuter.ToListAsync(affected.OrderBy(s => s.StudentName).ThenBy(s => s.EnrollmentId)
			.Take(HouseAssignmentRequest.MaxEnrollmentIds + 1));
		if (selected.Count == 0)
			throw new BusinessException(committing ? HouseManagementErrorCodes.StalePreview : HouseManagementErrorCodes.NoEligibleStudents);
		if (selected.Count > HouseAssignmentRequest.MaxEnrollmentIds)
			throw new BusinessException(committing ? HouseManagementErrorCodes.StalePreview : HouseManagementErrorCodes.AssignmentLimitExceeded);
		return selected;
	}

	private async Task<Division> ScopedDivisionAsync(Guid divisionId, Guid mediumId, string operation)
	{
		await scope.EnsureCanAccessDivisionAsync(divisionId, operation);
		var query =
			from d in await divisions.GetQueryableAsync()
			join c in await standards.GetQueryableAsync() on d.StandardId equals c.Id
			join stage in await stages.GetQueryableAsync() on c.EducationMediumStageId equals stage.Id
			where d.Id == divisionId && d.TenantId == CurrentTenant.Id && c.TenantId == CurrentTenant.Id &&
				stage.TenantId == CurrentTenant.Id && stage.EducationMediumId == mediumId
			select d;
		return await AsyncExecuter.SingleOrDefaultAsync(query) ?? throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}

	private async Task<HouseSettings> HouseAsync(Guid id, Guid mediumId)
	{
		var house = await houses.FindAsync(h => h.Id == id && h.TenantId == CurrentTenant.Id && h.AcademicMediumId == mediumId);
		return house ?? throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}

	private async Task<T> TransactionAsync<T>(Func<Task<T>> action)
	{
		try
		{
			using var unit = units.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.Serializable);
			var result = await action();
			await unit.CompleteAsync();
			return result;
		}
		catch (Exception exception) when (IsConflict(exception))
		{
			throw new BusinessException(HouseManagementErrorCodes.StalePreview, innerException: exception);
		}
	}

	private static bool IsConflict(Exception exception)
	{
		for (Exception? e = exception; e != null; e = e.InnerException)
			if (e is AbpDbConcurrencyException || e is DbException { SqlState: "40001" or "40P01" or "23505" })
				return true;
		return false;
	}
}
