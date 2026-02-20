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

namespace ShikshaMitra.Application.Finance.ConcessionRequests;

[Authorize]
[RemoteService(false)]
public class ConcessionRequestAppService : ApplicationService, IConcessionRequestAppService
{
	private readonly IRepository<ConcessionRequest, Guid> _concessionRequestRepository;
	private readonly IRepository<ConcessionRequestConcessionType, Guid> _concessionRequestConcessionTypeRepository;
	private readonly IRepository<ConcessionRequestFeeHeadMapping, Guid> _feeHeadMappingRepository;
	private readonly IRepository<ConcessionRequestFeeHeadValue, Guid> _feeHeadValueRepository;
	private readonly IRepository<ConcessionRequestDocument, Guid> _concessionRequestDocumentRepository;
	private readonly IRepository<ConcessionType, Guid> _concessionTypeRepository;
	private readonly IRepository<ConcessionTypeDocument, Guid> _concessionTypeDocumentRepository;
	private readonly IRepository<FeeHead, Guid> _feeHeadRepository;
	private readonly IRepository<FeeStructure, Guid> _feeStructureRepository;
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
		IRepository<ConcessionRequestDocument, Guid> concessionRequestDocumentRepository,
		IRepository<ConcessionType, Guid> concessionTypeRepository,
		IRepository<ConcessionTypeDocument, Guid> concessionTypeDocumentRepository,
		IRepository<FeeHead, Guid> feeHeadRepository,
		IRepository<FeeStructure, Guid> feeStructureRepository,
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
		_concessionRequestDocumentRepository = concessionRequestDocumentRepository;
		_concessionTypeRepository = concessionTypeRepository;
		_concessionTypeDocumentRepository = concessionTypeDocumentRepository;
		_feeHeadRepository = feeHeadRepository;
		_feeStructureRepository = feeStructureRepository;
		_studentRepository = studentRepository;
		_studentFeeStructureRepository = studentFeeStructureRepository;
		_studentFeeHeadRepository = studentFeeHeadRepository;
		_standardRepository = standardRepository;
		_divisionRepository = divisionRepository;
		_educationMediumRepository = educationMediumRepository;
		_educationBoardRepository = educationBoardRepository;
		_currentUser = currentUser;
	}

	// ─── Step 1+2 POST (merged) ───────────────────────────────────────────────

	public async Task<CreateConcessionRequestResponse> CreateConcessionRequestAsync(CreateConcessionRequestInput input)
	{
		// Validate student exists
		var student = await _studentRepository.GetAsync(input.StudentId);

		// Resolve fee structure (student snapshot)
		var studentFeeStructure = await _studentFeeStructureRepository.FirstOrDefaultAsync(
			x => x.StudentId == input.StudentId);

		Guid? feeStructureId = studentFeeStructure?.Id;

		// Determine academic year
		var now = DateTime.UtcNow;
		var academicYear = now.Month >= 6
			? $"{now.Year % 100:D2}-{(now.Year + 1) % 100:D2}"
			: $"{(now.Year - 1) % 100:D2}-{now.Year % 100:D2}";


		// Validate and attach concession types (Step 2)
		var concessionTypes = await _concessionTypeRepository.GetListAsync(
			x => input.ConcessionTypeIds.Contains(x.Id));

		var invalidIds = input.ConcessionTypeIds.Except(concessionTypes.Select(c => c.Id)).ToList();
		// 1. CreateConcessionRequestAsync - Concession Types Not Found
		if (invalidIds.Any())
			throw new BusinessException("ShikshaMitra:ConcessionTypesNotFound")
				.WithData("InvalidIds", string.Join(", ", invalidIds))
				.WithData("Message", $"Concession type(s) not found: {string.Join(", ", invalidIds)}");

		// Check for existing pending request for merging
		var pendingRequest = await _concessionRequestRepository.FirstOrDefaultAsync(x =>
			x.StudentId == input.StudentId &&
			x.AcademicYear == academicYear &&
			x.Status == ConcessionRequestStatus.PENDING);

		ConcessionRequest requestToUse;
		List<SelectedConcessionTypeDto> respondSelectedTypes;

		if (pendingRequest != null)
		{
			// Merge into existing
			requestToUse = pendingRequest;

			var existingLinks = await _concessionRequestConcessionTypeRepository.GetListAsync(
				x => x.ConcessionRequestId == pendingRequest.Id);
			var existingTypeIds = existingLinks.Select(x => x.ConcessionTypeId).ToHashSet();

			// Validate duplicate types
			var duplicateTypeIds = input.ConcessionTypeIds.Where(id => existingTypeIds.Contains(id)).ToList();
			if (duplicateTypeIds.Any())
			{
				var duplicateNames = concessionTypes
					.Where(ct => duplicateTypeIds.Contains(ct.Id))
					.Select(ct => ct.Name)
					.ToList();

				var namesString = string.Join(", ", duplicateNames);
				throw new BusinessException("ShikshaMitra:DuplicateConcessionRequest")
					.WithData("ConcessionTypes", namesString)
					.WithData("Message", $"You have already applied for the following concession(s) in this academic year: {namesString}. Duplicate requests are not allowed.");
			}

			// Add new links
			var newLinks = input.ConcessionTypeIds.Select(ctId =>
				new ConcessionRequestConcessionType(GuidGenerator.Create(), pendingRequest.Id, ctId)).ToList();
			await _concessionRequestConcessionTypeRepository.InsertManyAsync(newLinks, autoSave: true);

			// Prepare response types (existing + new)
			var allTypeIds = existingTypeIds.Concat(input.ConcessionTypeIds).Distinct().ToList();
			var allTypes = await _concessionTypeRepository.GetListAsync(x => allTypeIds.Contains(x.Id));

			respondSelectedTypes = allTypes.Select(ct => new SelectedConcessionTypeDto
			{
				ConcessionTypeId = ct.Id,
				Name = ct.Name
			}).ToList();
		}
		else
		{
			// Create New
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
			requestToUse = concessionRequest;

			// Validation: Prevent duplicate requests for the same concession type in the same academic year
			var existingRequests = await _concessionRequestRepository.GetListAsync(x =>
				x.StudentId == input.StudentId &&
				x.AcademicYear == academicYear &&
				x.Status != ConcessionRequestStatus.REJECTED &&
				x.Id != concessionRequest.Id);

			var existingRequestIds = existingRequests.Select(x => x.Id).ToList();

			if (existingRequestIds.Any())
			{
				var existingLinks = await _concessionRequestConcessionTypeRepository.GetListAsync(
					x => existingRequestIds.Contains(x.ConcessionRequestId));

				var existingConcessionTypeIds = existingLinks.Select(x => x.ConcessionTypeId).ToHashSet();
				var duplicateTypeIds = input.ConcessionTypeIds.Where(id => existingConcessionTypeIds.Contains(id)).ToList();

				if (duplicateTypeIds.Any())
				{
					var duplicateNames = concessionTypes
						.Where(ct => duplicateTypeIds.Contains(ct.Id))
						.Select(ct => ct.Name)
						.ToList();

					var namesString = string.Join(", ", duplicateNames);
					throw new BusinessException("ShikshaMitra:DuplicateConcessionRequest")
						.WithData("ConcessionTypes", namesString)
						.WithData("Message", $"You have already applied for the following concession(s) in this academic year: {namesString}. Duplicate requests are not allowed.");
				}
			}

			var links = input.ConcessionTypeIds.Select(ctId =>
				new ConcessionRequestConcessionType(GuidGenerator.Create(), concessionRequest.Id, ctId)).ToList();
			await _concessionRequestConcessionTypeRepository.InsertManyAsync(links, autoSave: true);

			respondSelectedTypes = concessionTypes.Select(ct => new SelectedConcessionTypeDto
			{
				ConcessionTypeId = ct.Id,
				Name = ct.Name
			}).ToList();
		}

		return new CreateConcessionRequestResponse
		{
			Success = true,
			Data = new CreateConcessionRequestData
			{
				ConcessionRequestId = requestToUse.Id,
				BoardId = requestToUse.BoardId,
				EducationMediumId = requestToUse.EducationMediumId,
				StandardId = requestToUse.StandardId,
				DivisionId = requestToUse.DivisionId,
				StudentId = requestToUse.StudentId,
				Status = (int) requestToUse.Status,
				SelectedConcessionTypes = respondSelectedTypes,
				CreatedAt = requestToUse.CreationTime
			}
		};
	}

	// ─── Step 1 GET ──────────────────────────────────────────────────────────

	public async Task<GetBasicDetailsResponse> GetBasicDetailsAsync(Guid concessionRequestId)
	{
		var cr = await _concessionRequestRepository.GetAsync(concessionRequestId);

		// Load the concession type links
		var links = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);
		var ctIds = links.Select(x => x.ConcessionTypeId).ToList();
		var concessionTypes = await _concessionTypeRepository.GetListAsync(x => ctIds.Contains(x.Id));
		var ctDict = concessionTypes.ToDictionary(x => x.Id, x => x);

		var selectedTypes = links.Select(l => new SelectedConcessionTypeDto
		{
			ConcessionTypeId = l.ConcessionTypeId,
			Name = ctDict.GetValueOrDefault(l.ConcessionTypeId)?.Name ?? ""
		}).ToList();

		return new GetBasicDetailsResponse
		{
			Success = true,
			Data = new GetBasicDetailsData
			{
				ConcessionRequestId = cr.Id,
				BoardId = cr.BoardId,
				EducationMediumId = cr.EducationMediumId,
				StandardId = cr.StandardId,
				DivisionId = cr.DivisionId,
				StudentId = cr.StudentId,
				Status = (int) cr.Status,
				SelectedConcessionTypes = selectedTypes,
				CreatedAt = cr.CreationTime,
				UpdatedAt = cr.LastModificationTime
			}
		};
	}

	// ─── Step 3 PUT ──────────────────────────────────────────────────────────

	public async Task<SaveFeeHeadsDataResponse> SaveFeeHeadsDataAsync(Guid concessionRequestId, SaveFeeHeadsDataRequest input)
	{
		var cr = await _concessionRequestRepository.GetAsync(concessionRequestId);

		// Load the registered concession types for this request
		var registeredLinks = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);
		var registeredCtIds = registeredLinks.Select(x => x.ConcessionTypeId).ToHashSet();

		// Validate that all ConcessionTypeIds in the input are registered (Step 1+2)
		foreach (var item in input.FeeHeadsData)
		{

		}

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
		var cr = await _concessionRequestRepository.GetAsync(concessionRequestId);

		if (cr.Status == ConcessionRequestStatus.APPROVED ||
			cr.Status == ConcessionRequestStatus.REJECTED)
		{
			throw new BusinessException("ShikshaMitra:ConcessionRequestStatusConflict")
				.WithData("Message", "Concession request has already been submitted and cannot be modified.");
		}

		// Load step 3 mappings
		var mappings = await _feeHeadMappingRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);

		// Load student fee heads for annual base amounts
		var studentFeeHeads = await _studentFeeHeadRepository.GetListAsync(
			x => x.StudentFeeStructureId == cr.FeeStructureId);
		var annualBaseDict = studentFeeHeads.ToDictionary(x => x.FeeHeadId, x => (double) x.Amount);
		var feeHeadNameDict = studentFeeHeads.ToDictionary(x => x.FeeHeadId, x => x.FeeHeadName);

		// Load concession types for response
		var allCtIds = mappings.Select(m => m.ConcessionTypeId).Distinct().ToList();
		var concessionTypes = await _concessionTypeRepository.GetListAsync(x => allCtIds.Contains(x.Id));
		var ctDict = concessionTypes.ToDictionary(x => x.Id, x => x);

		// Validate all step-3 fee heads are present in payload
		foreach (var mapping in mappings)
		{
			var block = input.ConcessionTypeValues.FirstOrDefault(b => b.ConcessionTypeId == mapping.ConcessionTypeId);
			if (block == null || !block.FeeHeadValues.Any(v => v.FeeHeadId == mapping.FeeHeadId))
			{


				throw new BusinessException("ShikshaMitra:IncompleteValueEntry")
					.WithData("Message", "All fee heads mapped in Step 3 must have a value entry.");
			}
		}

		// Validate submitted feeHeads exist in step-3 mapping for that concession type
		foreach (var block in input.ConcessionTypeValues)
		{
			var mappedFeeHeadIds = mappings
				.Where(m => m.ConcessionTypeId == block.ConcessionTypeId)
				.Select(m => m.FeeHeadId)
				.ToHashSet();

			foreach (var entry in block.FeeHeadValues)
			{
				if (!mappedFeeHeadIds.Contains(entry.FeeHeadId))
					throw new BusinessException("ShikshaMitra:FeeHeadNotFoundInMapping")
						.WithData("FeeHeadId", entry.FeeHeadId)
						.WithData("ConcessionTypeId", block.ConcessionTypeId)
						.WithData("Message", $"Fee head {entry.FeeHeadId} was not found in the mapping for concession type {block.ConcessionTypeId}.");
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
				var annualBase = annualBaseDict.GetValueOrDefault(entry.FeeHeadId, 0);

				if (entry.BasedOn == DiscountBasedOn.PERCENTAGE)
				{
					if (entry.Amount < 0 || entry.Amount > 100)
						throw new BusinessException("ShikshaMitra:InvalidPercentageDiscount")
							.WithData("Message", "Percentage discount cannot exceed 100%.");
				}
				else
				{
					if (entry.Amount < 0 || entry.Amount > annualBase)
						throw new BusinessException("ShikshaMitra:InvalidFlatDiscount")
							.WithData("Message", "Flat discount amount cannot exceed the annual base amount.");
				}

				var discountAmount = entry.BasedOn == DiscountBasedOn.PERCENTAGE
					? (entry.Amount / 100.0) * annualBase
					: entry.Amount;
				var netAmount = annualBase - discountAmount;

				if (Math.Abs(entry.NetAmount - netAmount) > 0.01)
					throw new BusinessException("ShikshaMitra:NetAmountMismatch")
						.WithData("FeeHeadId", entry.FeeHeadId)
						.WithData("Expected", netAmount)
						.WithData("Received", entry.NetAmount)
						.WithData("Message", $"Net amount mismatch for fee head {entry.FeeHeadId}. Expected {netAmount:F2}, got {entry.NetAmount:F2}.");

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
				ConcessionType = 0, // ConcessionType enum integer (default 0; extend as needed)
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

	// ─── Step 5 PUT — Document Upload ────────────────────────────────────────

	public async Task<UploadDocumentsResponse> UploadDocumentsAsync(Guid concessionRequestId, UploadDocumentsRequest input)
	{
		var cr = await _concessionRequestRepository.GetAsync(concessionRequestId);

		if (cr.Status == ConcessionRequestStatus.APPROVED ||
			cr.Status == ConcessionRequestStatus.REJECTED)
		{
			throw new BusinessException("ShikshaMitra:ConcessionRequestStatusConflict")
				.WithData("Message", "Concession request has already been submitted and cannot be modified.");
		}

		// Load concession type links for this request to validate ConcessionTypeIds
		var links = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);
		var validConcessionTypeIds = links.Select(x => x.ConcessionTypeId).ToHashSet();

		// Load all concession type document definitions
		var ctIds = input.ConcessionTypeDocuments.Select(b => b.ConcessionTypeId).ToList();
		var concessionTypes = await _concessionTypeRepository.GetListAsync(x => ctIds.Contains(x.Id));
		var ctDict = concessionTypes.ToDictionary(x => x.Id, x => x);

		var allConcessionTypeDocDefs = await _concessionTypeDocumentRepository.GetListAsync(
			x => ctIds.Contains(x.ConcessionTypeId));
		var docDefDict = allConcessionTypeDocDefs.ToDictionary(x => x.Id, x => x);

		var resultBlocks = new List<ConcessionTypeDocumentResult>();

		foreach (var block in input.ConcessionTypeDocuments)
		{
			// Validate concession type is assigned to this request
			if (!validConcessionTypeIds.Contains(block.ConcessionTypeId))
				throw new BusinessException("ShikshaMitra:ConcessionTypeNotAssigned_Step1")
					.WithData("ConcessionTypeId", block.ConcessionTypeId)
					.WithData("Message", $"Concession type {block.ConcessionTypeId} is not assigned to this concession request. Please complete Step 1 first.");

			var ct = ctDict.GetValueOrDefault(block.ConcessionTypeId);

			foreach (var docEntry in block.Documents)
			{
				// Upsert document record
				var existing = await _concessionRequestDocumentRepository.FirstOrDefaultAsync(
					x => x.ConcessionRequestId == concessionRequestId
					  && x.ConcessionTypeId == block.ConcessionTypeId
					  && x.DocumentId == docEntry.DocumentId);

				if (existing != null)
				{
					existing.Update(docEntry.Url, docEntry.Status);
					await _concessionRequestDocumentRepository.UpdateAsync(existing);
				}
				else
				{
					var newDoc = new ConcessionRequestDocument(
						GuidGenerator.Create(),
						CurrentTenant.Id,
						concessionRequestId,
						block.ConcessionTypeId,
						docEntry.DocumentId,
						docEntry.Url,
						docEntry.Status);
					await _concessionRequestDocumentRepository.InsertAsync(newDoc);
				}
			}

			// Build response result
			var uploadedDocs = new List<UploadedDocumentDetail>();
			foreach (var docEntry in block.Documents)
			{
				var docDef = docDefDict.GetValueOrDefault(docEntry.DocumentId);
				uploadedDocs.Add(new UploadedDocumentDetail
				{
					DocumentId = docEntry.DocumentId,
					DocumentName = docDef?.DocumentName ?? docEntry.DocumentId.ToString(),
					Url = docEntry.Url,
					Status = docEntry.Status,
					UploadedAt = docEntry.Status == 1 ? DateTime.UtcNow : null
				});
			}

			resultBlocks.Add(new ConcessionTypeDocumentResult
			{
				ConcessionTypeId = block.ConcessionTypeId,
				ConcessionTypeName = ct?.Name ?? "",
				Documents = uploadedDocs
			});
		}

		cr.AdvanceStep(5);
		await _concessionRequestRepository.UpdateAsync(cr);

		return new UploadDocumentsResponse
		{
			Success = true,
			Data = new UploadDocumentsData
			{
				ConcessionRequestId = concessionRequestId,
				RequestStatus = (int) cr.Status,
				ConcessionTypeDocuments = resultBlocks
			}
		};
	}

	// ─── GET Documents ────────────────────────────────────────────────────────

	public async Task<GetDocumentsResponse> GetConcessionDocumentsAsync(
		Guid studentConcessionTypeRequestId, int? status, int page, int pageSize)
	{
		// studentConcessionTypeRequestId is the ConcessionRequestConcessionType link Id
		var link = await _concessionRequestConcessionTypeRepository.GetAsync(studentConcessionTypeRequestId);
		var concessionRequestId = link.ConcessionRequestId;

		var docs = await _concessionRequestDocumentRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId
			  && x.ConcessionTypeId == link.ConcessionTypeId);

		if (status.HasValue)
			docs = docs.Where(x => x.Status == status.Value).ToList();

		// Load document definitions for names
		var docIds = docs.Select(d => d.DocumentId).ToList();
		var docDefs = await _concessionTypeDocumentRepository.GetListAsync(x => docIds.Contains(x.Id));
		var docDefDict = docDefs.ToDictionary(x => x.Id, x => x);

		var totalCount = docs.Count;
		var pagedDocs = docs.Skip((page - 1) * pageSize).Take(pageSize).ToList();

		var items = pagedDocs.Select(d => new ConcessionDocumentItem
		{
			DocumentId = d.DocumentId,
			DocumentName = docDefDict.GetValueOrDefault(d.DocumentId)?.DocumentName ?? d.DocumentId.ToString(),
			Url = d.Url,
			Status = d.Status,
			UploadedAt = d.UploadedAt
		}).ToList();

		return new GetDocumentsResponse
		{
			Success = true,
			StudentConcessionTypeRequestId = studentConcessionTypeRequestId,
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = (int) Math.Ceiling(totalCount / (double) pageSize),
			Items = items
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

		var query = await _concessionRequestRepository.GetQueryableAsync();
		query = query.Where(x => x.StudentId == studentId);
		if (status.HasValue)
			query = query.Where(x => (int) x.Status == status.Value);

		var totalCount = query.Count();
		var allRequests = query.OrderByDescending(x => x.CreationTime).ToList();

		var allForStudent = await _concessionRequestRepository.GetListAsync(x => x.StudentId == studentId);
		var pendingApplications = allForStudent.Count(x => x.Status == ConcessionRequestStatus.PENDING);
		var approvedApplications = allForStudent.Count(x => x.Status == ConcessionRequestStatus.APPROVED);
		var rejectedApplications = allForStudent.Count(x => x.Status == ConcessionRequestStatus.REJECTED);

		var pagedRequests = allRequests.Skip((page - 1) * pageSize).Take(pageSize).ToList();
		var requestIds = pagedRequests.Select(r => r.Id).ToList();

		// Load concession type links
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

		// Load uploaded documents
		var uploadedDocs = await _concessionRequestDocumentRepository.GetListAsync(
			x => requestIds.Contains(x.ConcessionRequestId));
		// Load doc definitions for names
		var docDefIds = uploadedDocs.Select(d => d.DocumentId).Distinct().ToList();
		var docDefs = await _concessionTypeDocumentRepository.GetListAsync(x => docDefIds.Contains(x.Id));
		var docDefDict = docDefs.ToDictionary(x => x.Id, x => x);

		var items = new List<ConcessionRequestItemDto>();
		foreach (var cr in pagedRequests)
		{
			var crLinks = concessionTypeLinks.Where(x => x.ConcessionRequestId == cr.Id).ToList();
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

				// Uploaded documents for this concession type
				var crDocs = uploadedDocs
					.Where(d => d.ConcessionRequestId == cr.Id && d.ConcessionTypeId == link.ConcessionTypeId)
					.ToList();
				var submittedDocuments = crDocs.Select(d => new SubmittedDocumentDto
				{
					DocumentId = d.DocumentId,
					DocumentName = docDefDict.GetValueOrDefault(d.DocumentId)?.DocumentName ?? d.DocumentId.ToString(),
					Url = d.Url,
					UploadedAt = d.UploadedAt,
					Status = d.Status
				}).ToList();

				bool isDocumentUploaded = submittedDocuments.Any() && submittedDocuments.All(d => d.Status == 1);

				items.Add(new ConcessionRequestItemDto
				{
					StudentConcessionTypeRequestId = link.Id,
					ConcessionTypeId = link.ConcessionTypeId,
					ConcessionTypeName = ct?.Name ?? "",
					Status = (int) cr.Status,
					TotalAmount = crValues.Sum(v => v.NetAmount),
					FeeHeads = feeHeadItems,
					SubmittedDocuments = submittedDocuments,
					IsDocumentUploaded = isDocumentUploaded,
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
				TotalApplications = allForStudent.Count,
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
		var link = await _concessionRequestConcessionTypeRepository.GetAsync(studentConcessionTypeRequestId);
		var cr = await _concessionRequestRepository.GetAsync(link.ConcessionRequestId);

		if (cr.Status == ConcessionRequestStatus.APPROVED)
			throw new BusinessException("ShikshaMitra:ConcessionRequestAlreadyApproved")
				.WithData("Message", "Concession request is already approved.");

		if (cr.Status == ConcessionRequestStatus.REJECTED)
			throw new BusinessException("ShikshaMitra:CannotApproveRejectedRequest")
				.WithData("Message", "Cannot approve a rejected concession request.");

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
			throw new BusinessException("ShikshaMitra:ConcessionRequestAlreadyRejected")
				.WithData("Message", "Concession request is already rejected.");

		if (cr.Status == ConcessionRequestStatus.APPROVED)
			throw new BusinessException("ShikshaMitra:CannotRejectApprovedRequest")
				.WithData("Message", "Cannot reject an approved concession request.");

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

		// Status counts
		var allForFilter = await _concessionRequestRepository.GetListAsync(
			x => x.BoardId == boardId && x.EducationMediumId == educationMediumId);
		int pendingCount = allForFilter.Count(x => x.Status == ConcessionRequestStatus.PENDING);
		int approvedCount = allForFilter.Count(x => x.Status == ConcessionRequestStatus.APPROVED);
		int rejectedCount = allForFilter.Count(x => x.Status == ConcessionRequestStatus.REJECTED);

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
		}

		int totalCount = allRequests.Count;
		var pagedRequests = allRequests.Skip((page - 1) * pageSize).Take(pageSize).ToList();

		// Load standards/divisions
		var standardIds = pagedRequests.Select(r => r.StandardId).Distinct().ToList();
		var divisionIds = pagedRequests.Select(r => r.DivisionId).Distinct().ToList();
		var standards = await _standardRepository.GetListAsync(x => standardIds.Contains(x.Id));
		var divisions = await _divisionRepository.GetListAsync(x => divisionIds.Contains(x.Id));
		var standardDict = standards.ToDictionary(x => x.Id, x => x.Name);
		var divisionDict = divisions.ToDictionary(x => x.Id, x => x.Name);

		// Load concession type names per request (first concession type name)
		var requestIds = pagedRequests.Select(r => r.Id).ToList();
		var ctLinks = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => requestIds.Contains(x.ConcessionRequestId));
		var ctIds = ctLinks.Select(x => x.ConcessionTypeId).Distinct().ToList();
		var concessionTypes = await _concessionTypeRepository.GetListAsync(x => ctIds.Contains(x.Id));
		var ctNameDict = concessionTypes.ToDictionary(x => x.Id, x => x.Name);

		var items = pagedRequests.Select(r =>
		{
			studentDict.TryGetValue(r.StudentId, out var s);
			var name = s != null
				? string.Join(" ", new[] { s.FirstName, s.MiddleName, s.LastName }
					.Where(x => !string.IsNullOrWhiteSpace(x)))
				: "";

			// Find first concession type name for this request
			var firstCtLink = ctLinks.FirstOrDefault(l => l.ConcessionRequestId == r.Id);
			var concessionTypeName = firstCtLink != null
				? ctNameDict.GetValueOrDefault(firstCtLink.ConcessionTypeId, "")
				: "";

			var statusName = r.Status switch
			{
				ConcessionRequestStatus.PENDING => "Pending",
				ConcessionRequestStatus.APPROVED => "Approved",
				ConcessionRequestStatus.REJECTED => "Rejected",
				_ => r.Status.ToString()
			};

			return new ConcessionApplicationDto
			{
				ConcessionRequestId = r.Id,
				StudentId = r.StudentId,
				StudentName = name,
				RollNo = s?.RollNo?.ToString() ?? "",
				GrNumber = s?.GrNo?.ToString(),
				StandardName = standardDict.GetValueOrDefault(r.StandardId, ""),
				DivisionName = divisionDict.GetValueOrDefault(r.DivisionId, ""),
				ConcessionType = concessionTypeName,
				RequestDate = r.CreationTime.ToString("yyyy-MM-dd"),
				Email = string.Empty,
				ContactNo = string.Empty,
				Status = (int) r.Status,
				StatusName = statusName,
				CreatedAt = r.CreationTime,
				UpdatedAt = r.LastModificationTime,
				ApproverComments = r.ApproverMessage ?? r.RejectionReason,
				ApprovedAt = r.ApprovedAt
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
		var link = await _concessionRequestConcessionTypeRepository.FindAsync(studentConcessionTypeRequestId);
		if (link == null)
			return; // Idempotent

		var cr = await _concessionRequestRepository.GetAsync(link.ConcessionRequestId);

		if (cr.Status == ConcessionRequestStatus.APPROVED)
			throw new BusinessException("ShikshaMitra:CannotDeleteApprovedRequest")
				.WithData("Message", "Cannot delete an approved concession application.");

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

		var allDocs = await _concessionRequestDocumentRepository.GetListAsync(
			x => x.ConcessionRequestId == cr.Id);
		await _concessionRequestDocumentRepository.DeleteManyAsync(allDocs);

		await _concessionRequestRepository.DeleteAsync(cr.Id);
	}
}
