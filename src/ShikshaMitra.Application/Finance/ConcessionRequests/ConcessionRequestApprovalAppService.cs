using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;

namespace ShikshaMitra.Finance.ConcessionRequests;

/// <summary>
/// Handles the approval workflow: approve, reject, admin application listing,
/// and deletion of non-approved applications.
/// </summary>
[RemoteService(false)]
public class ConcessionRequestApprovalAppService(
	IConcessionRequestRepository concessionRequestRepository,
	IRepository<Student, Guid> studentRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	ConcessionRequestManager concessionRequestManager)
	: ShikshaMitraAppService, IConcessionRequestApprovalAppService
{
	// -------------------------------------------------------------------------
	// State Transitions
	// -------------------------------------------------------------------------

	/// <summary>
	/// Approves a Pending concession request (Pending → Approved).
	/// The approver's identity is captured before the state transition so it is
	/// certain regardless of when ABP flushes <c>LastModifierId</c>.
	/// </summary>
	public virtual async Task<ConcessionApprovalResponseDto> ApproveAsync(
		Guid studentConcessionTypeRequestId,
		ApproveConcessionRequestDto input)
	{
		var entity = await concessionRequestRepository.GetAsync(studentConcessionTypeRequestId);

		var approverId = CurrentUser.GetId();

		entity = await concessionRequestManager.ApproveAsync(entity, input.Message);

		return new ConcessionApprovalResponseDto
		{
			StudentConcessionTypeRequestId = entity.Id,
			Status          = ConcessionRequestDetailDto.MapStatus(entity.Status),
			StatusName      = nameof(ConcessionRequestStatus.Approved),
			ApprovedAt      = entity.LastModificationTime ?? Clock.Now,
			ApproverId      = approverId,
			ApproverMessage = entity.ApproverMessage,
			Message         = "Application approved successfully",
		};
	}

	/// <summary>
	/// Rejects a Pending concession request (Pending → Rejected).
	/// The rejector's identity is captured before the state transition.
	/// </summary>
	public virtual async Task<ConcessionRejectionResponseDto> RejectAsync(
		Guid studentConcessionTypeRequestId,
		RejectConcessionRequestDto input)
	{
		var entity = await concessionRequestRepository.GetAsync(studentConcessionTypeRequestId);

		var rejectorId = CurrentUser.GetId();

		entity = await concessionRequestManager.RejectAsync(entity, input.Message);

		return new ConcessionRejectionResponseDto
		{
			StudentConcessionTypeRequestId = entity.Id,
			Status          = ConcessionRequestDetailDto.MapStatus(entity.Status),
			StatusName      = nameof(ConcessionRequestStatus.Rejected),
			RejectedAt      = entity.LastModificationTime ?? Clock.Now,
			RejectorId      = rejectorId,
			RejectionReason = entity.RejectionReason ?? string.Empty,
			Message         = "Application rejected successfully",
		};
	}

	// -------------------------------------------------------------------------
	// Admin List
	// -------------------------------------------------------------------------

	/// <summary>
	/// Returns a paginated list of concession applications scoped to a board and
	/// education medium, ordered newest-first. Per-status counts are computed
	/// independently of the active status filter. Defaults to Draft when status
	/// is not specified.
	/// </summary>
	public virtual async Task<ConcessionApplicationsDto> GetConcessionApplicationsAsync(
		GetConcessionApplicationsInput input)
	{
		var domainStatus = input.Status.HasValue
			? MapApiStatusToDomain(input.Status.Value)
			: ConcessionRequestStatus.Draft;

		// Cross-aggregate join: ABP guarantees same DbContext within a request,
		// so EF Core composes this into a single SQL query.
		var requestQueryable = await concessionRequestRepository.GetQueryableAsync();
		var studentQueryable  = await studentRepository.GetQueryableAsync();

		var baseQuery =
			from r in requestQueryable
			join s in studentQueryable.Where(s =>
				    s.AcademicBoardId == input.BoardId &&
				    s.AcademicMediumId == input.EducationMediumId)
				on r.StudentId equals s.Id
			select new { Request = r, Student = s };

		if (!string.IsNullOrWhiteSpace(input.Search) && input.Search.Length >= 2)
		{
			var searchTerm = input.Search.Trim();
			int? rollNoSearch = int.TryParse(searchTerm, out var parsedRoll) ? parsedRoll : null;

			baseQuery = baseQuery.Where(x =>
				x.Student.FirstName.Contains(searchTerm) ||
				x.Student.MiddleName.Contains(searchTerm) ||
				x.Student.LastName.Contains(searchTerm) ||
				(rollNoSearch.HasValue && x.Student.RollNo == rollNoSearch) ||
				(x.Student.ContactDetail != null &&
				 x.Student.ContactDetail.Email != null &&
				 x.Student.ContactDetail.Email.Contains(searchTerm)));
		}

		// Per-status counts are independent of the active filter so all tabs stay accurate.
		var pendingCount  = await AsyncExecuter.CountAsync(
			baseQuery.Where(x => x.Request.Status == ConcessionRequestStatus.Pending));
		var approvedCount = await AsyncExecuter.CountAsync(
			baseQuery.Where(x => x.Request.Status == ConcessionRequestStatus.Approved));
		var rejectedCount = await AsyncExecuter.CountAsync(
			baseQuery.Where(x => x.Request.Status == ConcessionRequestStatus.Rejected));

		var filteredQuery = baseQuery.Where(x => x.Request.Status == domainStatus);
		var totalCount    = await AsyncExecuter.LongCountAsync(filteredQuery);

		int skipCount = (input.Page - 1) * input.PageSize;

		var pagedProjection = filteredQuery
			.OrderByDescending(x => x.Request.CreationTime)
			.Skip(skipCount)
			.Take(input.PageSize)
			.Select(x => new
			{
				RequestId     = x.Request.Id,
				RequestStatus = x.Request.Status,
				StudentId     = x.Student.Id,
			});

		var pagedRows = await AsyncExecuter.ToListAsync(pagedProjection);

		if (pagedRows.Count == 0)
		{
			return new ConcessionApplicationsDto
			{
				PendingCount  = pendingCount,
				ApprovedCount = approvedCount,
				RejectedCount = rejectedCount,
				TotalCount    = totalCount,
				CurrentPage   = input.Page,
				PageSize      = input.PageSize,
				TotalPages    = 0,
				Items         = new List<ConcessionApplicationItemDto>(),
			};
		}

		var studentIdsOnPage = pagedRows.Select(r => r.StudentId).Distinct().ToList();
		var students = await studentRepository.GetListAsync(s => studentIdsOnPage.Contains(s.Id));
		var studentsById = students.ToDictionary(s => s.Id);

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

		var standardsById = standardIds.Count > 0
			? (await standardRepository.GetListAsync(s => standardIds.Contains(s.Id)))
			  .ToDictionary(s => s.Id)
			: new Dictionary<Guid, Standard>();

		var divisionsById = divisionIds.Count > 0
			? (await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id)))
			  .ToDictionary(d => d.Id)
			: new Dictionary<Guid, Division>();

		var totalAppCountQuery = requestQueryable
			.Where(r => studentIdsOnPage.Contains(r.StudentId))
			.GroupBy(r => r.StudentId)
			.Select(g => new { StudentId = g.Key, Count = g.Count() });

		var totalAppCountList = await AsyncExecuter.ToListAsync(totalAppCountQuery);
		var totalAppCountById = totalAppCountList.ToDictionary(x => x.StudentId, x => x.Count);

		var items = pagedRows.Select(row =>
		{
			studentsById.TryGetValue(row.StudentId, out var student);

			var standardName = student?.StandardId.HasValue == true &&
			                   standardsById.TryGetValue(student.StandardId!.Value, out var std)
				? std.Name
				: string.Empty;

			var divisionName = student?.DivisionId.HasValue == true &&
			                   divisionsById.TryGetValue(student.DivisionId!.Value, out var div)
				? div.Name
				: string.Empty;

			var fullName = string.Join(" ",
				    new[] { student?.FirstName, student?.MiddleName, student?.LastName }
				        .Where(p => !string.IsNullOrWhiteSpace(p)))
				.Trim();

			totalAppCountById.TryGetValue(row.StudentId, out var totalApps);

			return new ConcessionApplicationItemDto
			{
				ConcessionRequestId        = row.RequestId,
				StudentId                  = row.StudentId,
				StudentName                = fullName,
				RollNo                     = student?.RollNo?.ToString() ?? string.Empty,
				GrNumber                   = student?.GrNo?.ToString(),
				StandardName               = standardName,
				DivisionName               = divisionName,
				Email                      = student?.ContactDetail?.Email ?? string.Empty,
				ContactNo                  = student?.ContactDetail?.PrimaryContactNo ?? string.Empty,
				Status                     = ConcessionRequestDetailDto.MapStatus(row.RequestStatus),
				TotalConcessionApplication = totalApps,
			};
		}).ToList();

		int totalPages = totalCount == 0
			? 0
			: (int)Math.Ceiling((double)totalCount / input.PageSize);

		return new ConcessionApplicationsDto
		{
			PendingCount  = pendingCount,
			ApprovedCount = approvedCount,
			RejectedCount = rejectedCount,
			TotalCount    = totalCount,
			CurrentPage   = input.Page,
			PageSize      = input.PageSize,
			TotalPages    = totalPages,
			Items         = items,
		};
	}

	// -------------------------------------------------------------------------
	// Delete Application
	// -------------------------------------------------------------------------

	/// <summary>
	/// Deletes a Pending or Rejected concession application.
	/// Uses <c>FindAsync</c> to make the operation idempotent — an already-deleted
	/// record returns null and the call succeeds silently.
	/// </summary>
	public virtual async Task DeleteConcessionApplicationAsync(Guid studentConcessionTypeRequestId)
	{
		var entity = await concessionRequestRepository.FindAsync(studentConcessionTypeRequestId);

		if (entity is null)
			return;

		if (entity.Status == ConcessionRequestStatus.Approved)
		{
			throw new BusinessException(ConcessionRequestErrorCodes.CannotDeleteApprovedApplication)
				.WithData("ConcessionRequestId", studentConcessionTypeRequestId)
				.WithData("Status", entity.Status.ToString());
		}

		await concessionRequestRepository.DeleteAsync(entity);
	}

	// -------------------------------------------------------------------------
	// Private helpers
	// -------------------------------------------------------------------------

	/// <summary>
	/// Maps the API-level status integer to the domain <see cref="ConcessionRequestStatus"/>.
	/// Convention: 0=Pending, 1=Approved, 2=Rejected, 3=Draft.
	/// </summary>
	private static ConcessionRequestStatus MapApiStatusToDomain(int apiStatus) => apiStatus switch
	{
		0 => ConcessionRequestStatus.Pending,
		1 => ConcessionRequestStatus.Approved,
		2 => ConcessionRequestStatus.Rejected,
		3 => ConcessionRequestStatus.Draft,
		_ => throw new BusinessException(ConcessionRequestErrorCodes.InvalidBasedOnValue)
		         .WithData("Status", apiStatus),
	};
}
