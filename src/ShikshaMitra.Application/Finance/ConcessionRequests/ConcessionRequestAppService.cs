using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Application.Contracts.Finance.ConcessionRequests;
using ShikshaMitra.Application.Contracts.Finance.ConcessionRequests.Dtos;
using ShikshaMitra.Finance.ConcessionRequests;
using DiscountBasedOn = ShikshaMitra.Finance.ConcessionRequests.DiscountBasedOn;
using ShikshaMitra.Finance.Setting;
using ShikshaMitra.Finance.Setting.ConcessionType;
using ShikshaMitra.Finance.Setting.FeeStructure;
using ShikshaMitra.Finance.StudentFeeAssignment;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;
using ShikshaMitra.Finance.StudentFeeAssignment;

namespace ShikshaMitra.Application.Finance.ConcessionRequests;

[Authorize]
[RemoteService(false)]
public class ConcessionRequestAppService : ApplicationService, IConcessionRequestAppService
{
	private readonly IRepository<ConcessionRequest, Guid> _concessionRequestRepository;
	private readonly IRepository<ConcessionRequestConcessionType, Guid> _concessionRequestConcessionTypeRepository;
	private readonly IRepository<ConcessionRequestFeeHeadMapping, Guid> _feeHeadMappingRepository;
	private readonly IRepository<ConcessionRequestFeeHeadValue, Guid> _feeHeadValueRepository;
	private readonly IRepository<ConcessionType, Guid> _concessionTypeRepository;
	private readonly IRepository<FeeHead, Guid> _feeHeadRepository;
	private readonly IRepository<FeeStructure, Guid> _feeStructureRepository;
	private readonly IRepository<FeeStructureStandard, Guid> _feeStructureStandardRepository;
	private readonly IRepository<FeeStructureDetail, Guid> _feeStructureDetailRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;
	private readonly IRepository<EducationMedium, Guid> _educationMediumRepository;
	private readonly IRepository<EducationBoard, Guid> _educationBoardRepository;
	private readonly IRepository<StudentFeeStructure, Guid> _studentFeeStructureRepository;
	private readonly IRepository<StudentFeeHead, Guid> _studentFeeHeadRepository;
	private readonly ICurrentUser _currentUser;

	public ConcessionRequestAppService(
		IRepository<ConcessionRequest, Guid> concessionRequestRepository,
		IRepository<ConcessionRequestConcessionType, Guid> concessionRequestConcessionTypeRepository,
		IRepository<ConcessionRequestFeeHeadMapping, Guid> feeHeadMappingRepository,
		IRepository<ConcessionRequestFeeHeadValue, Guid> feeHeadValueRepository,
		IRepository<ConcessionType, Guid> concessionTypeRepository,
		IRepository<FeeHead, Guid> feeHeadRepository,
		IRepository<FeeStructure, Guid> feeStructureRepository,
		IRepository<FeeStructureStandard, Guid> feeStructureStandardRepository,
		IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
		IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
		IRepository<Student, Guid> studentRepository,
		IRepository<StudentFeeHead, Guid> studentFeeHeadRepository,
		IRepository<Standard, Guid> standardRepository,
		IRepository<Division, Guid> divisionRepository,
		IRepository<EducationMedium, Guid> educationMediumRepository,
		IRepository<EducationBoard, Guid> educationBoardRepository,
		ICurrentUser currentUser)
	{
		_concessionRequestRepository = concessionRequestRepository;
		_concessionRequestConcessionTypeRepository = concessionRequestConcessionTypeRepository;
		_feeHeadMappingRepository = feeHeadMappingRepository;
		_feeHeadValueRepository = feeHeadValueRepository;
		_concessionTypeRepository = concessionTypeRepository;
		_feeHeadRepository = feeHeadRepository;
		_feeStructureRepository = feeStructureRepository;
		_feeStructureStandardRepository = feeStructureStandardRepository;
		_feeStructureDetailRepository = feeStructureDetailRepository;
		_studentRepository = studentRepository;
		_studentFeeStructureRepository = studentFeeStructureRepository;
		_studentFeeHeadRepository = studentFeeHeadRepository;
		_standardRepository = standardRepository;
		_divisionRepository = divisionRepository;
		_educationMediumRepository = educationMediumRepository;
		_educationBoardRepository = educationBoardRepository;
		_currentUser = currentUser;
	}

	// ─── Step 1 POST ─────────────────────────────────────────────────────────

	public async Task<CreateBasicDetailsResponse> CreateBasicDetailsAsync(CreateBasicDetailsRequest input)
	{
		// Validate student exists
		var student = await _studentRepository.GetAsync(input.StudentId);

		// Resolve fee structure by board + medium + standard
		var feeStructures = await _studentFeeStructureRepository.FirstOrDefaultAsync(
			x => x.StudentId == input.StudentId);

		if (feeStructures == null)
		{
			throw new BusinessException("Fee structure not found").WithData("Fee structure not found", input.StudentId);

		}

		Guid? feeStructureId = feeStructures.Id;


		// Determine academic year from current year
		var now = DateTime.UtcNow;
		var academicYear = now.Month >= 6
			? $"{now.Year % 100:D2}-{(now.Year + 1) % 100:D2}"
			: $"{(now.Year - 1) % 100:D2}-{now.Year % 100:D2}";

		var concessionRequest = new ConcessionRequest(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			input.BoardId,
			input.EducationMediumId,
			input.StandardId,
			input.DivisionId,
			input.StudentId,
			feeStructureId,
			academicYear);

		await _concessionRequestRepository.InsertAsync(concessionRequest);

		return new CreateBasicDetailsResponse
		{
			Success = true,
			Data = new CreateBasicDetailsData
			{
				ConcessionRequestId = concessionRequest.Id,
				BoardId = concessionRequest.BoardId,
				EducationMediumId = concessionRequest.EducationMediumId,
				StandardId = concessionRequest.StandardId,
				DivisionId = concessionRequest.DivisionId,
				StudentId = concessionRequest.StudentId,
				AcademicYear = concessionRequest.AcademicYear,
				Status = (int) concessionRequest.Status,
				CurrentStep = concessionRequest.CurrentStep,
				CompletedSteps = concessionRequest.GetCompletedSteps(),
				CreatedAt = concessionRequest.CreationTime
			}
		};
	}

	// ─── Step 1 GET ──────────────────────────────────────────────────────────

	public async Task<GetBasicDetailsResponse> GetBasicDetailsAsync(Guid concessionRequestId)
	{
		ConcessionRequest? cr = await _concessionRequestRepository.GetAsync(concessionRequestId);

		// Load related entities
		var board = await _educationBoardRepository.FindAsync(cr.BoardId);
		var medium = await _educationMediumRepository.FindAsync(cr.EducationMediumId);
		var standard = await _standardRepository.FindAsync(cr.StandardId);
		var division = await _divisionRepository.FindAsync(cr.DivisionId);
		var student = await _studentRepository.FindAsync(cr.StudentId);

		// Load fee structure details
		StudentFeeStructure? studentFeeStructure = await _studentFeeStructureRepository.FindAsync(cr.FeeStructureId.Value);
		string? feeStructureName = studentFeeStructure?.StructureName;
		var feeHeadDtos = new List<FeeHeadObjectDto>();
		decimal totalAmount = 0;

		if (cr.FeeStructureId.HasValue)
		{
			// Get fee structure standard for this standard
			// var fssQuery = await _feeStructureStandardRepository.GetListAsync(
			// 	x => x.FeeStructureId == cr.FeeStructureId.Value && x.StandardId == cr.StandardId);
			// var fss = fssQuery.FirstOrDefault();

			// if (fss != null)
			// {
			// var details = await _feeStructureDetailRepository.GetListAsync(
			// 	x => x.FeeStructureStandardId == fss.Id);
			// var feeHeadIds = details.Select(d => d.FeeHeadId).ToList();
			// var feeHeads = await _feeHeadRepository.GetListAsync(x => feeHeadIds.Contains(x.Id));
			// var feeHeadDict = feeHeads.ToDictionary(x => x.Id, x => x.Name);

			// feeHeadDtos = details.Select(d => new FeeHeadObjectDto
			// {
			// 	FeeHeadId = d.FeeHeadId,
			// 	FeeHeadName = feeHeadDict.GetValueOrDefault(d.FeeHeadId, ""),
			// 	Amount = d.Amount
			// }).ToList();

			// totalAmount = details.Sum(d => d.Amount);
			var feeHeads = await _studentFeeHeadRepository.GetListAsync(
				x => x.StudentFeeStructureId == studentFeeStructure.Id);
			var feeHeadIds = feeHeads.Select(d => d.FeeHeadId).ToList();
			// var feeHeads = await _feeHeadRepository.GetListAsync(x => feeHeadIds.Contains(x.Id));
			// var feeHeadDict = feeHeads.ToDictionary(x => x.Id, x => x.Name);

			feeHeadDtos = feeHeads.Select(d => new FeeHeadObjectDto
			{
				FeeHeadId = d.FeeHeadId,
				FeeHeadName = d.FeeHeadName,
				Amount = d.Amount
			}).ToList();

			totalAmount = feeHeads.Sum(d => d.Amount);
			// }
		}

		var studentName = string.Join(" ", new[] { student?.FirstName, student?.MiddleName, student?.LastName }
			.Where(s => !string.IsNullOrWhiteSpace(s)));

		return new GetBasicDetailsResponse
		{
			Success = true,
			Data = new GetBasicDetailsData
			{
				ConcessionRequestId = cr.Id,
				AcademicYear = cr.AcademicYear,
				BoardId = cr.BoardId,
				BoardName = board?.Type.ToString() ?? "",
				EducationMediumId = cr.EducationMediumId,
				EducationMediumName = medium?.Name ?? "",
				StandardId = cr.StandardId,
				StandardName = standard?.Name ?? "",
				DivisionId = cr.DivisionId,
				DivisionName = division?.Name ?? "",
				StudentId = cr.StudentId,
				StudentName = studentName ?? string.Empty,
				FeeStructureId = cr.FeeStructureId,
				FeeStructureName = feeStructureName ?? string.Empty,
				FeeHeads = feeHeadDtos,
				Totalamount = totalAmount,
				Status = (int) cr.Status,
				CurrentStep = cr.CurrentStep,
				CompletedSteps = cr.GetCompletedSteps(),
				CreatedAt = cr.CreationTime,
				UpdatedAt = cr.LastModificationTime
			}
		};
	}

	// ─── Step 2 PUT ──────────────────────────────────────────────────────────

	public async Task<SaveConcessionTypesResponse> SaveConcessionTypesAsync(Guid concessionRequestId, SaveConcessionTypesRequest input)
	{
		ConcessionRequest? cr = await _concessionRequestRepository.GetAsync(concessionRequestId);

		// Full replacement — delete existing
		var existing = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);
		await _concessionRequestConcessionTypeRepository.DeleteManyAsync(existing);

		// Also delete step 3 and step 4 data (cascade replacement)
		var existingMappings = await _feeHeadMappingRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);
		await _feeHeadMappingRepository.DeleteManyAsync(existingMappings);

		var existingValues = await _feeHeadValueRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);
		await _feeHeadValueRepository.DeleteManyAsync(existingValues);

		// Insert new
		var newLinks = input.ConcessionTypeIds.Select(ctId =>
			new ConcessionRequestConcessionType(GuidGenerator.Create(), concessionRequestId, ctId)).ToList();
		await _concessionRequestConcessionTypeRepository.InsertManyAsync(newLinks, autoSave: true);

		cr.AdvanceStep(2);
		await _concessionRequestRepository.UpdateAsync(cr);

		// Load concession type names for response
		var concessionTypes = await _concessionTypeRepository.GetListAsync(
			x => input.ConcessionTypeIds.Contains(x.Id));
		var ctDict = concessionTypes.ToDictionary(x => x.Id, x => x.Name);

		return new SaveConcessionTypesResponse
		{
			Success = true,
			Data = new SaveConcessionTypesData
			{
				ConcessionRequestId = concessionRequestId,
				SelectedConcessionTypes = input.ConcessionTypeIds.Select(ctId => new SelectedConcessionTypeDto
				{
					ConcessionTypeId = ctId,
					Name = ctDict.GetValueOrDefault(ctId, "")
				}).ToList()
			}
		};
	}

	// ─── Step 3 PUT ──────────────────────────────────────────────────────────

	public async Task<SaveFeeHeadsDataResponse> SaveFeeHeadsDataAsync(Guid concessionRequestId, SaveFeeHeadsDataRequest input)
	{
		var cr = await _concessionRequestRepository.GetAsync(concessionRequestId);

		// Full replacement
		var existing = await _feeHeadMappingRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);
		await _feeHeadMappingRepository.DeleteManyAsync(existing);

		// Also clear step 4 values
		var existingValues = await _feeHeadValueRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);
		await _feeHeadValueRepository.DeleteManyAsync(existingValues);

		var newMappings = new List<ConcessionRequestFeeHeadMapping>();
		foreach (var item in input.FeeHeadsData)
		{
			foreach (var feeHeadId in item.SelectedFeeHeadIds)
			{
				newMappings.Add(new ConcessionRequestFeeHeadMapping(
					GuidGenerator.Create(),
					concessionRequestId,
					item.ConcessionTypeId,
					feeHeadId));
			}
		}
		await _feeHeadMappingRepository.InsertManyAsync(newMappings);

		cr.AdvanceStep(3);
		await _concessionRequestRepository.UpdateAsync(cr);

		return new SaveFeeHeadsDataResponse { Success = true };
	}

	// ─── Step 4 PUT ──────────────────────────────────────────────────────────

	public async Task<SaveValueEntryResponse> SaveValueEntryAsync(Guid concessionRequestId, SaveValueEntryRequest input)
	{
		ConcessionRequest? cr = await _concessionRequestRepository.GetAsync(concessionRequestId);

		// Contract: request must be in DRAFT (0) or IN_PROGRESS (1) — 409 otherwise
		if (
			cr.Status == ConcessionRequestStatus.APPROVED ||
			cr.Status == ConcessionRequestStatus.REJECTED)
		{
			throw new UserFriendlyException(
				"Concession request has already been submitted and cannot be modified.",
				"CONFLICT");
		}

		// Load step 3 mappings to validate completeness
		List<ConcessionRequestFeeHeadMapping> mappings = await _feeHeadMappingRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);

		// Load student fee heads — these carry the actual annualBaseAmount per fee head
		Guid? studentFeeStructureId = cr.FeeStructureId;
		var studentFeeHeads = await _studentFeeHeadRepository.GetListAsync(
			x => x.StudentFeeStructureId == studentFeeStructureId);

		// annualBase keyed by FeeHeadId (the domain fee-head UUID, not the StudentFeeHead entity Id)
		var annualBaseDict = studentFeeHeads.ToDictionary(x => x.FeeHeadId, x => (double) x.Amount);

		// Fee head display names keyed by FeeHeadId
		var feeHeadNameDict = studentFeeHeads.ToDictionary(x => x.FeeHeadId, x => x.FeeHeadName);

		// Load concession type names
		var allConcessionTypeIds = mappings.Select(m => m.ConcessionTypeId).Distinct().ToList();
		var concessionTypes = await _concessionTypeRepository.GetListAsync(
			x => allConcessionTypeIds.Contains(x.Id));
		var ctDict = concessionTypes.ToDictionary(x => x.Id, x => x);

		// Validate: all fee heads mapped in Step 3 must be present in the payload
		foreach (var mapping in mappings)
		{
			var block = input.ConcessionTypeValues.FirstOrDefault(b => b.ConcessionTypeId == mapping.ConcessionTypeId);
			if (block == null || !block.FeeHeadValues.Any(v => v.FeeHeadId == mapping.FeeHeadId))
			{
				var fhName = feeHeadNameDict.GetValueOrDefault(mapping.FeeHeadId, mapping.FeeHeadId.ToString());
				var ctName = ctDict.ContainsKey(mapping.ConcessionTypeId)
					? ctDict[mapping.ConcessionTypeId].Name
					: mapping.ConcessionTypeId.ToString();
				throw new UserFriendlyException(
					$"All fee heads mapped in Step 3 must have a value entry.",
					"INCOMPLETE_VALUE_ENTRY");
			}
		}

		// Validate: submitted feeHeadIds must exist in the Step 3 mapping for that concession type
		foreach (var block in input.ConcessionTypeValues)
		{
			var mappedFeeHeadIds = mappings
				.Where(m => m.ConcessionTypeId == block.ConcessionTypeId)
				.Select(m => m.FeeHeadId)
				.ToHashSet();

			foreach (var entry in block.FeeHeadValues)
			{
				if (!mappedFeeHeadIds.Contains(entry.FeeHeadId))
					throw new UserFriendlyException(
						$"Fee head {entry.FeeHeadId} was not found in the mapping for concession type {block.ConcessionTypeId}.",
						"NOT_FOUND");
			}
		}

		// Full replacement of step 4 values
		var existingValues = await _feeHeadValueRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);
		await _feeHeadValueRepository.DeleteManyAsync(existingValues);

		var newValues = new List<ConcessionRequestFeeHeadValue>();
		var resultBlocks = new List<ConcessionTypeValueResult>();

		foreach (var block in input.ConcessionTypeValues)
		{
			var ct = ctDict.GetValueOrDefault(block.ConcessionTypeId);
			double blockTotalDiscount = 0;
			double blockTotalNet = 0;
			var feeHeadResults = new List<FeeHeadValueResult>();

			foreach (var entry in block.FeeHeadValues)
			{
				double annualBase = annualBaseDict.GetValueOrDefault(entry.FeeHeadId, 0);

				// Validate discount value per basedOn mode
				if (entry.BasedOn == DiscountBasedOn.PERCENTAGE)
				{
					if (entry.Amount < 0 || entry.Amount > 100)
						throw new UserFriendlyException(
							"Percentage discount cannot exceed 100%.",
							"INVALID_DISCOUNT_VALUE");
				}
				else
				{
					if (entry.Amount < 0 || entry.Amount > annualBase)
						throw new UserFriendlyException(
							"Flat discount amount cannot exceed the annual base amount.",
							"INVALID_DISCOUNT_VALUE");
				}

				// Compute discount and net amounts server-side
				double discountAmount = entry.BasedOn == DiscountBasedOn.PERCENTAGE
					? (entry.Amount / 100.0) * annualBase
					: entry.Amount;
				double netAmount = annualBase - discountAmount;

				// Validate client-submitted netAmount within ±0.01 rounding tolerance
				if (Math.Abs(entry.NetAmount - netAmount) > 0.01)
					throw new UserFriendlyException(
						$"Net amount mismatch for fee head {entry.FeeHeadId}. Expected {netAmount:F2}, got {entry.NetAmount:F2}.",
						"INVALID_NET_AMOUNT");

				newValues.Add(new ConcessionRequestFeeHeadValue(
					GuidGenerator.Create(),
					concessionRequestId,
					block.ConcessionTypeId,
					entry.FeeHeadId,
					entry.BasedOn,
					entry.Amount,
					discountAmount,
					netAmount));

				blockTotalDiscount += discountAmount;
				blockTotalNet += netAmount;

				feeHeadResults.Add(new FeeHeadValueResult
				{
					FeeHeadId = entry.FeeHeadId,
					FeeHeadName = feeHeadNameDict.GetValueOrDefault(entry.FeeHeadId, ""),
					AnnualBaseAmount = annualBase,
					BasedOn = (int) entry.BasedOn,
					Amount = entry.Amount,
					DiscountAmount = discountAmount,
					NetAmount = netAmount
				});
			}

			resultBlocks.Add(new ConcessionTypeValueResult
			{
				ConcessionTypeId = block.ConcessionTypeId,
				ConcessionTypeName = ct?.Name ?? "",
				//ConcessionType = ct != null ? (int) ct.BasedOn : 0,
				FeeHeadValues = feeHeadResults,
				TotalDiscountAmount = blockTotalDiscount,
				TotalNetAmount = blockTotalNet
			});
		}

		await _feeHeadValueRepository.InsertManyAsync(newValues);

		cr.AdvanceStep(4);
		await _concessionRequestRepository.UpdateAsync(cr);

		double grandTotalDiscount = resultBlocks.Sum(b => b.TotalDiscountAmount);
		double grandTotalNet = resultBlocks.Sum(b => b.TotalNetAmount);

		return new SaveValueEntryResponse
		{
			Success = true,
			Data = new SaveValueEntryData
			{
				ConcessionRequestId = concessionRequestId,
				RequestStatus = (int) cr.Status,
				ConcessionTypeValues = resultBlocks,
				GrandTotalDiscountAmount = grandTotalDiscount,
				GrandTotalNetAmount = grandTotalNet
			}
		};
	}

	// ─── Get Student Concession Requests ─────────────────────────────────────

	public async Task<ConcessionListResponse> GetStudentConcessionRequestsAsync(
		Guid studentId, int? status, int page, int pageSize, string? search)
	{
		var student = await _studentRepository.GetAsync(studentId);
		var standard = student.StandardId.HasValue
			? await _standardRepository.FindAsync(student.StandardId.Value)
			: null;
		var division = student.DivisionId.HasValue
			? await _divisionRepository.FindAsync(student.DivisionId.Value)
			: null;

		var studentName = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
			.Where(s => !string.IsNullOrWhiteSpace(s)));

		// Query concession requests for this student
		var query = await _concessionRequestRepository.GetQueryableAsync();
		query = query.Where(x => x.StudentId == studentId);

		if (status.HasValue)
			query = query.Where(x => (int) x.Status == status.Value);

		var totalCount = query.Count();
		var allRequests = query.OrderByDescending(x => x.CreationTime).ToList();

		// Summary counts (unfiltered by status)
		var allForStudent = await _concessionRequestRepository.GetListAsync(x => x.StudentId == studentId);
		var totalApplications = allForStudent.Count;
		var pendingApplications = allForStudent.Count(x => x.Status == ConcessionRequestStatus.PENDING);
		var approvedApplications = allForStudent.Count(x => x.Status == ConcessionRequestStatus.APPROVED);
		var rejectedApplications = allForStudent.Count(x => x.Status == ConcessionRequestStatus.REJECTED);

		var pagedRequests = allRequests.Skip((page - 1) * pageSize).Take(pageSize).ToList();

		// Build items — load concession type links
		var requestIds = pagedRequests.Select(r => r.Id).ToList();
		var concessionTypeLinks = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => requestIds.Contains(x.ConcessionRequestId));
		var ctIds = concessionTypeLinks.Select(x => x.ConcessionTypeId).Distinct().ToList();
		var concessionTypes = await _concessionTypeRepository.GetListAsync(x => ctIds.Contains(x.Id));
		var ctDict = concessionTypes.ToDictionary(x => x.Id, x => x);

		// Load fee head values
		var feeHeadValues = await _feeHeadValueRepository.GetListAsync(
			x => requestIds.Contains(x.ConcessionRequestId));
		var fhIds = feeHeadValues.Select(x => x.FeeHeadId).Distinct().ToList();
		var feeHeads = await _feeHeadRepository.GetListAsync(x => fhIds.Contains(x.Id));
		var fhDict = feeHeads.ToDictionary(x => x.Id, x => x);

		var items = new List<ConcessionRequestItemDto>();
		foreach (var cr in pagedRequests)
		{
			var crLinks = concessionTypeLinks.Where(x => x.ConcessionRequestId == cr.Id).ToList();
			// One item per concession type link
			foreach (var link in crLinks)
			{
				var ct = ctDict.GetValueOrDefault(link.ConcessionTypeId);
				var crValues = feeHeadValues.Where(v =>
					v.ConcessionRequestId == cr.Id && v.ConcessionTypeId == link.ConcessionTypeId).ToList();

				var feeHeadItems = crValues.Select(v => new FeeHeadItemDto
				{
					FeeHeadId = v.FeeHeadId,
					FeeHeadName = fhDict.ContainsKey(v.FeeHeadId) ? fhDict[v.FeeHeadId].Name : "",
					Amount = v.DiscountAmount,
					NetAmount = v.NetAmount,
					Description = fhDict.ContainsKey(v.FeeHeadId) ? fhDict[v.FeeHeadId].Description : null
				}).ToList();

				items.Add(new ConcessionRequestItemDto
				{
					StudentConcessionTypeRequestId = link.Id,
					//ConcessionType = ct != null ? (int) ct.BasedOn : 0,
					ConcessionTypeName = ct?.Name ?? "",
					Status = (int) cr.Status,
					TotalAmount = crValues.Sum(v => v.NetAmount),
					FeeHeads = feeHeadItems,
					SubmittedDocuments = new List<SubmittedDocumentDto>(),
					CreatedAt = cr.CreationTime,
					UpdatedAt = cr.LastModificationTime,
					Message = cr.ApproverMessage ?? cr.RejectionReason,
					ApprovedAt = cr.ApprovedAt
				});
			}
		}

		return new ConcessionListResponse
		{
			StudentDetails = new StudentBasicDetailsDto
			{
				StudentId = student.Id,
				Name = studentName,
				RollNo = student.RollNo?.ToString() ?? "",
				GrNumber = student.GrNo?.ToString() ?? "",
				StandardName = standard?.Name ?? "",
				DivisionName = division?.Name ?? ""
			},
			ConcessionBasicDetail = new ApplicationConcessionBasicDetail
			{
				TotalApplications = totalApplications,
				PendingApplications = pendingApplications,
				ApprovedApplications = approvedApplications,
				RejectedApplications = rejectedApplications
			},
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = (int) Math.Ceiling(totalCount / (double) pageSize),
			Items = items
		};
	}

	// ─── Approve ─────────────────────────────────────────────────────────────

	public async Task<ApprovalResponse> ApproveConcessionRequestAsync(
		Guid studentConcessionTypeRequestId, ApprovalRequest input)
	{
		// studentConcessionTypeRequestId is the ConcessionRequestConcessionType.Id
		var link = await _concessionRequestConcessionTypeRepository.GetAsync(studentConcessionTypeRequestId);
		var cr = await _concessionRequestRepository.GetAsync(link.ConcessionRequestId);

		if (cr.Status == ConcessionRequestStatus.APPROVED)
			throw new UserFriendlyException("Concession request is already approved.", "CONFLICT");

		if (cr.Status == ConcessionRequestStatus.REJECTED)
			throw new UserFriendlyException("Cannot approve a rejected concession request.", "CONFLICT");

		var approverId = _currentUser.Id ?? Guid.Empty;
		cr.Approve(approverId, input.Message);
		await _concessionRequestRepository.UpdateAsync(cr);

		return new ApprovalResponse
		{
			StudentConcessionTypeRequestId = studentConcessionTypeRequestId,
			Status = (int) ConcessionRequestStatus.APPROVED,
			StatusName = "Approved",
			ApprovedAt = cr.ApprovedAt!.Value,
			ApproverId = approverId,
			ApproverMessage = cr.ApproverMessage,
			Message = "Application approved successfully"
		};
	}

	// ─── Reject ──────────────────────────────────────────────────────────────

	public async Task<RejectionResponse> RejectConcessionRequestAsync(
		Guid studentConcessionTypeRequestId, RejectionRequest input)
	{
		var link = await _concessionRequestConcessionTypeRepository.GetAsync(studentConcessionTypeRequestId);
		var cr = await _concessionRequestRepository.GetAsync(link.ConcessionRequestId);

		if (cr.Status == ConcessionRequestStatus.REJECTED)
			throw new UserFriendlyException("Concession request is already rejected.", "CONFLICT");

		if (cr.Status == ConcessionRequestStatus.APPROVED)
			throw new UserFriendlyException("Cannot reject an approved concession request.", "CONFLICT");

		var rejectorId = _currentUser.Id ?? Guid.Empty;
		cr.Reject(rejectorId, input.Message);
		await _concessionRequestRepository.UpdateAsync(cr);

		return new RejectionResponse
		{
			StudentConcessionTypeRequestId = studentConcessionTypeRequestId,
			Status = (int) ConcessionRequestStatus.REJECTED,
			StatusName = "Rejected",
			RejectedAt = cr.RejectedAt!.Value,
			RejectorId = rejectorId,
			RejectionReason = cr.RejectionReason,
			Message = "Application rejected successfully"
		};
	}

	// ─── Get Concession Applications (admin list) ─────────────────────────────

	public async Task<ConcessionApplicationsResponse> GetConcessionApplicationsAsync(
		Guid boardId, Guid educationMediumId, int? status, int page, int pageSize, string? search)
	{
		var query = await _concessionRequestRepository.GetQueryableAsync();
		query = query.Where(x => x.BoardId == boardId && x.EducationMediumId == educationMediumId);

		if (status.HasValue)
			query = query.Where(x => (int) x.Status == status.Value);

		var allRequests = query.OrderByDescending(x => x.CreationTime).ToList();

		// Status counts (unfiltered)
		var allForFilter = (await _concessionRequestRepository.GetListAsync(
			x => x.BoardId == boardId && x.EducationMediumId == educationMediumId));
		int pendingCount = allForFilter.Count(x => x.Status == ConcessionRequestStatus.PENDING);
		int approvedCount = allForFilter.Count(x => x.Status == ConcessionRequestStatus.APPROVED);
		int rejectedCount = allForFilter.Count(x => x.Status == ConcessionRequestStatus.REJECTED);
		int totalCount = allRequests.Count;

		// Load student data for search + response
		var studentIds = allRequests.Select(r => r.StudentId).Distinct().ToList();
		var students = await _studentRepository.GetListAsync(x => studentIds.Contains(x.Id));
		var studentDict = students.ToDictionary(x => x.Id, x => x);

		// Apply search filter
		if (!string.IsNullOrWhiteSpace(search))
		{
			allRequests = allRequests.Where(r =>
			{
				if (!studentDict.TryGetValue(r.StudentId, out var s)) return false;
				var name = string.Join(" ", new[] { s.FirstName, s.MiddleName, s.LastName }
					.Where(x => !string.IsNullOrWhiteSpace(x)));
				return name.Contains(search, StringComparison.OrdinalIgnoreCase)
					|| s.RollNo?.ToString().Contains(search) == true
					|| s.GrNo?.ToString().Contains(search) == true;
			}).ToList();
			totalCount = allRequests.Count;
		}

		var pagedRequests = allRequests.Skip((page - 1) * pageSize).Take(pageSize).ToList();

		// Load standard/division names
		var standardIds = pagedRequests.Select(r => r.StandardId).Distinct().ToList();
		var divisionIds = pagedRequests.Select(r => r.DivisionId).Distinct().ToList();
		var standards = await _standardRepository.GetListAsync(x => standardIds.Contains(x.Id));
		var divisions = await _divisionRepository.GetListAsync(x => divisionIds.Contains(x.Id));
		var standardDict = standards.ToDictionary(x => x.Id, x => x.Name);
		var divisionDict = divisions.ToDictionary(x => x.Id, x => x.Name);

		// Count total concession requests per student
		var requestCountByStudent = allForFilter
			.GroupBy(x => x.StudentId)
			.ToDictionary(g => g.Key, g => g.Count());

		var items = pagedRequests.Select(r =>
		{
			studentDict.TryGetValue(r.StudentId, out var s);
			var name = s != null
				? string.Join(" ", new[] { s.FirstName, s.MiddleName, s.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)))
				: "";
			return new ConcessionApplicationDto
			{
				ConcessionRequestId = r.Id,
				StudentName = name,
				RollNo = s?.RollNo?.ToString() ?? "",
				GrNumber = s?.GrNo?.ToString(),
				StandardName = standardDict.GetValueOrDefault(r.StandardId, ""),
				DivisionName = divisionDict.GetValueOrDefault(r.DivisionId, ""),
				Email = string.Empty,
				ContactNo = string.Empty,
				TotalConcessionApplication = requestCountByStudent.GetValueOrDefault(r.StudentId, 0)
			};
		}).ToList();

		return new ConcessionApplicationsResponse
		{
			PendingCount = pendingCount,
			ApprovedCount = approvedCount,
			RejectedCount = rejectedCount,
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = (int) Math.Ceiling(totalCount / (double) pageSize),
			Items = items
		};
	}

	// ─── Delete ───────────────────────────────────────────────────────────────

	public async Task DeleteConcessionApplicationAsync(Guid studentConcessionTypeRequestId)
	{
		// studentConcessionTypeRequestId is the ConcessionRequestConcessionType.Id
		var link = await _concessionRequestConcessionTypeRepository.FindAsync(studentConcessionTypeRequestId);
		if (link == null)
			return; // Idempotent — already deleted

		var cr = await _concessionRequestRepository.GetAsync(link.ConcessionRequestId);

		if (cr.Status == ConcessionRequestStatus.APPROVED)
			throw new UserFriendlyException(
				"Cannot delete an approved concession application.",
				"CONFLICT");

		// Delete all related data
		var allLinks = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => x.ConcessionRequestId == cr.Id);
		await _concessionRequestConcessionTypeRepository.DeleteManyAsync(allLinks);

		var allMappings = await _feeHeadMappingRepository.GetListAsync(
			x => x.ConcessionRequestId == cr.Id);
		await _feeHeadMappingRepository.DeleteManyAsync(allMappings);

		var allValues = await _feeHeadValueRepository.GetListAsync(
			x => x.ConcessionRequestId == cr.Id);
		await _feeHeadValueRepository.DeleteManyAsync(allValues);

		await _concessionRequestRepository.DeleteAsync(cr.Id);
	}
}
