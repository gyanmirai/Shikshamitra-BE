using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Domain.Finance.Fees.Settings;

namespace ShikshaMitra.Finance.Fees.ConcessionRequests;

/// <summary>
/// Read-only projections for student-centric and per-request document views.
/// </summary>
[RemoteService(false)]
public class ConcessionRequestQueryAppService(
	IConcessionRequestRepository concessionRequestRepository,
	IRepository<ConcessionType, Guid> concessionTypeRepository,
	IRepository<Student, Guid> studentRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<EducationMedium, Guid> educationMediumRepository,
	IRepository<EducationBoard, Guid> educationBoardRepository)
	: ShikshaMitraAppService, IConcessionRequestQueryAppService
{
	// -------------------------------------------------------------------------
	// Student Request List
	// -------------------------------------------------------------------------

	/// <summary>
	/// Returns a paginated list of concession requests for a specific student,
	/// enriched with the student's basic profile and unfiltered application statistics.
	/// </summary>
	public virtual async Task<StudentConcessionRequestsDto> GetStudentConcessionRequestsAsync(
		Guid studentId,
		GetStudentConcessionRequestsInput input)
	{
		var student = await studentRepository.GetAsync(studentId);

		var domainStatus = input.Status.HasValue
			? MapApiStatusToDomain(input.Status.Value)
			: (ConcessionRequestStatus?)null;

		// Resolve search to a set of matching concession-type IDs to keep the
		// repository free of cross-aggregate joins.
		IReadOnlyList<Guid>? filteredTypeIds = null;
		if (!string.IsNullOrWhiteSpace(input.Search) && input.Search.Length >= 2)
		{
			var search = input.Search.Trim();
			var matchingTypes = await concessionTypeRepository
				.GetListAsync(ct => ct.Name.Contains(search));

			filteredTypeIds = matchingTypes.Select(ct => ct.Id).ToList();

			if (filteredTypeIds.Count == 0)
			{
				var emptyStats = await BuildConcessionBasicDetailAsync(studentId);
				var emptyStudentDetails = await BuildStudentBasicDetailsAsync(student);

				return new StudentConcessionRequestsDto
				{
					StudentDetails        = emptyStudentDetails,
					ConcessionBasicDetail = emptyStats,
					TotalCount            = 0,
					CurrentPage           = input.Page,
					PageSize              = input.PageSize,
					TotalPages            = 0,
					Items                 = new List<StudentConcessionRequestItemDto>(),
				};
			}
		}

		var totalCount = await concessionRequestRepository.GetStudentRequestsCountAsync(
			studentId,
			status: domainStatus,
			concessionTypeIds: filteredTypeIds);

		int skipCount = (input.Page - 1) * input.PageSize;
		var requests = await concessionRequestRepository.GetStudentRequestsWithDetailsAsync(
			studentId,
			skipCount: skipCount,
			maxResultCount: input.PageSize,
			status: domainStatus,
			concessionTypeIds: filteredTypeIds);

		var concessionStats = await BuildConcessionBasicDetailAsync(studentId);

		var concessionTypeIds = requests.Select(r => r.ConcessionTypeId).Distinct().ToList();
		var concessionTypes = concessionTypeIds.Count > 0
			? await concessionTypeRepository.GetListAsync(ct => concessionTypeIds.Contains(ct.Id))
			: new List<ConcessionType>();
		var concessionTypeById = concessionTypes.ToDictionary(ct => ct.Id);

		var feeHeadIds = requests
			.SelectMany(r => r.ConcessionFeeHeadMappings)
			.Select(m => m.FeeHeadId)
			.Distinct()
			.ToList();
		var feeHeadById = feeHeadIds.Count > 0
			? (await feeHeadRepository.GetListAsync(f => feeHeadIds.Contains(f.Id)))
			  .ToDictionary(f => f.Id)
			: new Dictionary<Guid, FeeHead>();

		var studentDetails = await BuildStudentBasicDetailsAsync(student);

		var items = requests
			.Select(r => MapToStudentConcessionRequestItemDto(r, concessionTypeById, feeHeadById))
			.ToList();

		int totalPages = totalCount == 0
			? 0
			: (int)Math.Ceiling((double)totalCount / input.PageSize);

		return new StudentConcessionRequestsDto
		{
			StudentDetails        = studentDetails,
			ConcessionBasicDetail = concessionStats,
			TotalCount            = totalCount,
			CurrentPage           = input.Page,
			PageSize              = input.PageSize,
			TotalPages            = totalPages,
			Items                 = items,
		};
	}

	// -------------------------------------------------------------------------
	// Per-Request Document List
	// -------------------------------------------------------------------------

	/// <summary>
	/// Returns a paginated flat list of document slots for a single concession request,
	/// merged with previously saved upload state. Optionally filtered by upload status.
	/// </summary>
	public virtual async Task<ConcessionDocumentsDto> GetConcessionDocumentsAsync(
		Guid studentConcessionTypeRequestId,
		GetConcessionDocumentsInput input)
	{
		var request = await concessionRequestRepository
			.GetWithDocumentsAsync(studentConcessionTypeRequestId);

		var concessionType = await concessionTypeRepository
			.FindAsync(request.ConcessionTypeId);

		var templates = concessionType?.ConcessionTypeDocuments
		                ?? Enumerable.Empty<ConcessionTypeDocument>();

		var uploadByDocumentId = request.ConcessionRequestDocuments
			.ToDictionary(d => d.DocumentId);

		var allItems = templates
			.Select(template =>
			{
				uploadByDocumentId.TryGetValue(template.Id, out var upload);

				return new ConcessionDocumentItemDto
				{
					DocumentId   = template.Id,
					DocumentName = template.DocumentName,
					Url          = upload?.Url,
					Status       = upload is not null ? (int)upload.Status : 0,
					UploadedAt   = upload is not null
					               ? upload.LastModificationTime ?? upload.CreationTime
					               : null,
				};
			})
			.ToList();

		if (input.Status.HasValue)
			allItems = allItems.Where(d => d.Status == input.Status.Value).ToList();

		long totalCount = allItems.Count;
		int skipCount   = (input.Page - 1) * input.PageSize;

		var pageItems = allItems
			.Skip(skipCount)
			.Take(input.PageSize)
			.ToList();

		int totalPages = totalCount == 0
			? 0
			: (int)Math.Ceiling((double)totalCount / input.PageSize);

		return new ConcessionDocumentsDto
		{
			Success                        = true,
			StudentConcessionTypeRequestId = studentConcessionTypeRequestId,
			TotalCount                     = totalCount,
			CurrentPage                    = input.Page,
			PageSize                       = input.PageSize,
			TotalPages                     = totalPages,
			Items                          = pageItems,
		};
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

	/// <summary>
	/// Builds unfiltered concession-application statistics for a student
	/// (counts always cover all statuses regardless of active query filters).
	/// </summary>
	private async Task<ApplicationConcessionBasicDetailDto> BuildConcessionBasicDetailAsync(Guid studentId)
	{
		var pending  = (int)await concessionRequestRepository.GetStudentRequestsCountAsync(studentId, ConcessionRequestStatus.Pending);
		var approved = (int)await concessionRequestRepository.GetStudentRequestsCountAsync(studentId, ConcessionRequestStatus.Approved);
		var rejected = (int)await concessionRequestRepository.GetStudentRequestsCountAsync(studentId, ConcessionRequestStatus.Rejected);

		return new ApplicationConcessionBasicDetailDto
		{
			PendingApplications  = pending,
			ApprovedApplications = approved,
			RejectedApplications = rejected,
			TotalApplications    = pending + approved + rejected,
		};
	}

	/// <summary>
	/// Constructs <see cref="StudentBasicDetailsDto"/> by loading the student's
	/// Standard, Division, EducationMedium, and EducationBoard records.
	/// </summary>
	private async Task<StudentBasicDetailsDto> BuildStudentBasicDetailsAsync(Student student)
	{
		var standardName = student.StandardId.HasValue
			? (await standardRepository.FindAsync(student.StandardId.Value))?.Name ?? string.Empty
			: string.Empty;

		var divisionName = student.DivisionId.HasValue
			? (await divisionRepository.FindAsync(student.DivisionId.Value))?.Name ?? string.Empty
			: string.Empty;

		var mediumName = (await educationMediumRepository.FindAsync(student.AcademicMediumId))?.Name
		                 ?? string.Empty;

		var boardName = student.AcademicBoardId.HasValue
			? (await educationBoardRepository.FindAsync(student.AcademicBoardId.Value))?.Type.ToString()
			  ?? string.Empty
			: string.Empty;

		var fullName = string.Join(" ",
			    new[] { student.FirstName, student.MiddleName, student.LastName }
			        .Where(part => !string.IsNullOrWhiteSpace(part)))
			.Trim();

		return new StudentBasicDetailsDto
		{
			StudentId           = student.Id,
			Name                = fullName,
			RollNo              = student.RollNo?.ToString() ?? string.Empty,
			GrNumber            = student.GrNo?.ToString() ?? string.Empty,
			StandardName        = standardName,
			DivisionName        = divisionName,
			BoardName           = boardName,
			EducationMediumName = mediumName,
		};
	}

	/// <summary>
	/// Maps a <see cref="ConcessionRequest"/> (with eagerly loaded mappings and documents)
	/// to <see cref="StudentConcessionRequestItemDto"/>.
	/// </summary>
	private static StudentConcessionRequestItemDto MapToStudentConcessionRequestItemDto(
		ConcessionRequest request,
		Dictionary<Guid, ConcessionType> concessionTypeById,
		Dictionary<Guid, FeeHead> feeHeadById)
	{
		concessionTypeById.TryGetValue(request.ConcessionTypeId, out var concessionType);

		var feeHeadItems = request.ConcessionFeeHeadMappings
			.Select(m =>
			{
				feeHeadById.TryGetValue(m.FeeHeadId, out var feeHead);
				return new ConcessionRequestFeeHeadItemDto
				{
					FeeHeadId     = m.FeeHeadId,
					FeeHeadName   = feeHead?.Name ?? string.Empty,
					Amount        = m.PreConcessionFeeHeadAmount,
					PaidAmount    = m.PostConcessionFeeHeadAmount,
					PendingAmount = m.FeeHeadConcessionAmount,
					Description   = feeHead?.Description ?? string.Empty,
					Status        = 0,
				};
			})
			.ToList();

		var documentItems = BuildDocumentItems(request, concessionType);

		int requiredDocCount  = concessionType?.ConcessionTypeDocuments.Count ?? 0;
		bool isDocumentUploaded = requiredDocCount > 0
		                          && request.ConcessionRequestDocuments.Count >= requiredDocCount;

		decimal totalAmount = request.ConcessionFeeHeadMappings.Sum(m => m.FeeHeadConcessionAmount);

		DateTime? approvedAt = request.Status == ConcessionRequestStatus.Approved
			? request.LastModificationTime
			: null;

		return new StudentConcessionRequestItemDto
		{
			StudentConcessionTypeRequestId = request.Id,
			ConcessionTypeId               = request.ConcessionTypeId,
			ConcessionTypeName             = concessionType?.Name ?? string.Empty,
			Status                         = ConcessionRequestDetailDto.MapStatus(request.Status),
			TotalAmount                    = totalAmount,
			FeeHeads                       = feeHeadItems,
			SubmittedDocuments             = documentItems,
			IsDocumentUploaded             = isDocumentUploaded,
			CreatedAt                      = request.CreationTime,
			UpdatedAt                      = request.LastModificationTime ?? request.CreationTime,
			Message                        = request.RejectionReason,
			ApprovedAt                     = approvedAt,
		};
	}

	/// <summary>
	/// Merges the concession-type document template slots with the student's uploaded
	/// documents for a single request.
	/// </summary>
	private static List<ConcessionRequestDocumentItemDto> BuildDocumentItems(
		ConcessionRequest request,
		ConcessionType? concessionType)
	{
		if (concessionType is null || concessionType.ConcessionTypeDocuments.Count == 0)
			return new List<ConcessionRequestDocumentItemDto>();

		var uploadByDocumentId = request.ConcessionRequestDocuments
			.ToDictionary(d => d.DocumentId);

		return concessionType.ConcessionTypeDocuments
			.Select(template =>
			{
				uploadByDocumentId.TryGetValue(template.Id, out var upload);
				return new ConcessionRequestDocumentItemDto
				{
					DocumentId   = template.Id,
					DocumentName = template.DocumentName,
					Url          = upload?.Url,
					UploadedAt   = upload is not null
					               ? upload.LastModificationTime ?? upload.CreationTime
					               : null,
					Status       = upload is not null ? (int)upload.Status : 0,
				};
			})
			.ToList();
	}
}
