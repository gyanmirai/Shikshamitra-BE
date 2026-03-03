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
	private readonly IRepository<FeeStructureDetail, Guid> _feeStructureDetailRepository;
	private readonly ICurrentUser _currentUser;

	private readonly IRepository<EducationBoard, Guid> _boardRepository;

	public ConcessionRequestAppService(
		IRepository<EducationBoard, Guid> boardRepository,
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
		IRepository<Standard, Guid> standardRepository,
		IRepository<Division, Guid> divisionRepository,
		IRepository<EducationMedium, Guid> educationMediumRepository,
		IRepository<EducationBoard, Guid> educationBoardRepository,
		IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
		ICurrentUser currentUser)
	{
		_boardRepository = boardRepository;
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
		_standardRepository = standardRepository;
		_divisionRepository = divisionRepository;
		_educationMediumRepository = educationMediumRepository;
		_educationBoardRepository = educationBoardRepository;
		_feeStructureDetailRepository = feeStructureDetailRepository;
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

		// Find an existing request for this student + academic year that has at least one pending concession type
		var crQuery = await _concessionRequestRepository.GetQueryableAsync();
		var linkQuery = await _concessionRequestConcessionTypeRepository.GetQueryableAsync();

		var existingRequest = (from cr in crQuery
							   join link in linkQuery on cr.Id equals link.ConcessionRequestId
							   where cr.StudentId == input.StudentId
								  && cr.AcademicYear == academicYear
								  && (link.Status == ConcessionRequestStatus.PENDING || link.Status == ConcessionRequestStatus.DRAFT)
							   select cr).FirstOrDefault();

		ConcessionRequest requestToUse;
		List<SelectedConcessionTypeDto> respondSelectedTypes;

		if (existingRequest != null)
		{
			// Merge into existing
			requestToUse = existingRequest;

			var existingLinks = await _concessionRequestConcessionTypeRepository.GetListAsync(
				x => x.ConcessionRequestId == existingRequest.Id);
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
				new ConcessionRequestConcessionType(GuidGenerator.Create(), existingRequest.Id, ctId)).ToList();
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
			var existingRequestsForValidation = await _concessionRequestRepository.GetListAsync(x =>
				x.StudentId == input.StudentId &&
				x.AcademicYear == academicYear &&
				x.Id != concessionRequest.Id);
			var existingRequestIds = existingRequestsForValidation.Select(x => x.Id).ToList();

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

			// respondSelectedTypes = concessionTypes.Select(ct => new SelectedConcessionTypeDto
			// {
			// 	ConcessionRequestConcessionTypeId=
			// 	ConcessionTypeId = ct.Id,
			// 	Name = ct.Name
			// }).ToList();
			var concessionTypeIds = links
	.Select(x => x.ConcessionTypeId)
	.Distinct()
	.ToList();

			respondSelectedTypes = links.Select(l => new SelectedConcessionTypeDto
			{
				ConcessionRequestConcessionTypeId = l.Id,
				ConcessionTypeId = l.ConcessionTypeId,
				Name = concessionTypes
					.FirstOrDefault(x => x.Id == l.ConcessionTypeId)?.Name
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
				Status = (int) ConcessionRequestStatus.DRAFT,
				SelectedConcessionTypes = respondSelectedTypes,
				CreatedAt = requestToUse.CreationTime
			}
		};
	}

	// ─── Step 1+2 PUT (Update) ────────────────────────────────────────────────

	public async Task<UpdateConcessionRequestResponse> UpdateConcessionRequestAsync(
		Guid concessionRequestId, UpdateConcessionRequestInput input)
	{
		var cr = await _concessionRequestRepository.GetAsync(concessionRequestId);

		// Block update if any concession type in this request is already processed
		var existingLinks = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);

		if (existingLinks.Any(l => l.Status == ConcessionRequestStatus.APPROVED ||
								   l.Status == ConcessionRequestStatus.REJECTED))
		{
			throw new BusinessException("ShikshaMitra:ConcessionRequestStatusConflict")
				.WithData("Message", "One or more concession types in this request have already been processed and cannot be modified.");
		}

		// Validate that submitted concession type IDs exist
		var newConcessionTypes = await _concessionTypeRepository.GetListAsync(
			x => input.ConcessionTypeIds.Contains(x.Id));

		var invalidIds = input.ConcessionTypeIds.Except(newConcessionTypes.Select(c => c.Id)).ToList();
		if (invalidIds.Any())
			throw new BusinessException("ShikshaMitra:ConcessionTypesNotFound")
				.WithData("InvalidIds", string.Join(", ", invalidIds))
				.WithData("Message", $"Concession type(s) not found: {string.Join(", ", invalidIds)}");

		// Full replacement of concession type links
		await _concessionRequestConcessionTypeRepository.DeleteManyAsync(existingLinks);

		var newLinks = input.ConcessionTypeIds.Select(ctId =>
			new ConcessionRequestConcessionType(GuidGenerator.Create(), concessionRequestId, ctId)).ToList();
		await _concessionRequestConcessionTypeRepository.InsertManyAsync(newLinks, autoSave: true);

		// Resolve fee structure (may change if studentId changed)
		var studentFeeStructure = await _studentFeeStructureRepository.FirstOrDefaultAsync(
			x => x.StudentId == input.StudentId);

		// Update basic fields on the aggregate
		cr.UpdateBasicDetails(
			input.BoardId,
			input.EducationMediumId,
			input.StandardId,
			input.DivisionId,
			input.StudentId,
			studentFeeStructure?.Id);

		await _concessionRequestRepository.UpdateAsync(cr);

		var selectedTypes = newLinks.Select(l => new SelectedConcessionTypeDto
		{
			ConcessionRequestConcessionTypeId = l.Id,
			ConcessionTypeId = l.ConcessionTypeId,
			Name = newConcessionTypes.FirstOrDefault(c => c.Id == l.ConcessionTypeId)?.Name ?? ""
		}).ToList();

		return new UpdateConcessionRequestResponse
		{
			Success = true,
			Data = new UpdateConcessionRequestData
			{
				ConcessionRequestId = cr.Id,
				BoardId = cr.BoardId,
				EducationMediumId = cr.EducationMediumId,
				StandardId = cr.StandardId,
				DivisionId = cr.DivisionId,
				StudentId = cr.StudentId,
				Status = (int) ConcessionRequestStatus.DRAFT,
				SelectedConcessionTypes = selectedTypes,
				UpdatedAt = DateTime.UtcNow
			}
		};
	}

	// ─── Step 1 GET ──────────────────────────────────────────────────────────

	public async Task<GetBasicDetailsResponse> GetBasicDetailsAsync(Guid concessionRequestId)
	{
		var cr = await _concessionRequestRepository.GetAsync(concessionRequestId);

		EducationBoard? board = await _boardRepository.GetAsync(cr.BoardId);
		var educationMedium = await _educationMediumRepository.GetAsync(cr.EducationMediumId);
		var standard = await _standardRepository.GetAsync(cr.StandardId);
		var division = await _divisionRepository.GetAsync(cr.DivisionId);
		Student? student = await _studentRepository.GetAsync(cr.StudentId);

		// Load the concession type links
		var links = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);
		var ctIds = links.Select(x => x.ConcessionTypeId).ToList();
		var concessionTypes = await _concessionTypeRepository.GetListAsync(x => ctIds.Contains(x.Id));
		var ctDict = concessionTypes.ToDictionary(x => x.Id, x => x);

		var selectedTypes = links.Select(l => new SelectedConcessionTypeDto
		{
			ConcessionTypeId = l.ConcessionTypeId,
			ConcessionRequestConcessionTypeId = l.Id,
			Name = ctDict.GetValueOrDefault(l.ConcessionTypeId)?.Name ?? ""
		}).ToList();

		return new GetBasicDetailsResponse
		{
			Success = true,
			Data = new GetBasicDetailsData
			{
				ConcessionRequestId = cr.Id,
				BoardId = cr.BoardId,
				BoardName = board.Type.ToString(),
				EducationMediumId = cr.EducationMediumId,
				EducationMediumName = educationMedium.Name,
				StandardId = cr.StandardId,
				StandardName = standard.Name,
				DivisionId = cr.DivisionId,
				DivisionName = division.Name,
				StudentId = cr.StudentId,
				StudentName = student.FirstName + " " + student.MiddleName + " " + student.LastName,
				Status = links.Any() ? (int) links.First().Status : (int) ConcessionRequestStatus.DRAFT,
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

		var newMappings = new List<ConcessionRequestFeeHeadMapping>();
		foreach (var item in input.FeeHeadsData)
		{
			foreach (var feeHeadId in item.feeHeadIds)
			{
				newMappings.Add(new ConcessionRequestFeeHeadMapping(
					GuidGenerator.Create(),
					concessionRequestId,
					item.ConcessionTypeId,
					item.ConcessionRequestConcessionTypeId,
					feeHeadId));
			}
		}
		await _feeHeadMappingRepository.InsertManyAsync(newMappings, autoSave: true);

		cr.AdvanceStep(3);
		await _concessionRequestRepository.UpdateAsync(cr);

		return new SaveFeeHeadsDataResponse { Success = true };
	}

	// ─── Step 4 PUT ──────────────────────────────────────────────────────────

	public async Task<SaveValueEntryResponse> SaveValueEntryAsync(Guid concessionRequestId, SaveValueEntryRequest input)
	{
		var cr = await _concessionRequestRepository.GetAsync(concessionRequestId);

		// Check if any of the concession types in this request are already processed
		var links_step4 = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);

		if (links_step4.Any(l => l.Status == ConcessionRequestStatus.APPROVED ||
						  l.Status == ConcessionRequestStatus.REJECTED))
		{
			throw new BusinessException("ShikshaMitra:ConcessionRequestStatusConflict")
				.WithData("Message", "One or more concession types in this request have already been processed and cannot be modified.");
		}

		// Load step 3 mappings
		var mappings = await _feeHeadMappingRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);

		var studentFeeStructureId = cr.FeeStructureId;
		var stundetStandarad = await _studentFeeStructureRepository.FirstOrDefaultAsync(
			x => x.StudentId == cr.StudentId);

		if (stundetStandarad == null)
		{
			throw new BusinessException("ShikshaMitra:StudentFeeStructureNotFound")
				.WithData("Message", "Student fee structure not found.");
		}

		// Load student fee details for annual base amounts from FeeStructureDetail
		var feeDetails = await _feeStructureDetailRepository.GetListAsync(
			x => x.FeeStructureStandardId == stundetStandarad.FeeStructureStandardId);
		var annualBaseDict = feeDetails.ToDictionary(x => x.FeeHeadId, x => (double) x.Amount);

		var feeHeadIdsForNames = feeDetails.Select(x => x.FeeHeadId).ToList();
		var feeHeadsForNames = await _feeHeadRepository.GetListAsync(x => feeHeadIdsForNames.Contains(x.Id));
		var feeHeadNameDict = feeHeadsForNames.ToDictionary(x => x.Id, x => x.Name);

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
					block.ConcessionRequestConcessionTypeId,
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
				RequestStatus = (int) ConcessionRequestStatus.PENDING,
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

		// Check if any of the concession types in this request are already processed
		var links_step5 = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);

		if (links_step5.Any(l => l.Status == ConcessionRequestStatus.APPROVED ||
						  l.Status == ConcessionRequestStatus.REJECTED))
		{
			throw new BusinessException("ShikshaMitra:ConcessionRequestStatusConflict")
				.WithData("Message", "One or more concession types in this request have already been processed and cannot be modified.");
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
						block.ConcessionRequestConcessionTypeId,
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

		// Transition all DRAFT concession type links for this request to PENDING
		// This marks the application as fully submitted and ready for admin review
		var draftLinks = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId && x.Status == ConcessionRequestStatus.DRAFT);
		foreach (var draftLink in draftLinks)
			draftLink.MarkAsPending();
		if (draftLinks.Any())
			await _concessionRequestConcessionTypeRepository.UpdateManyAsync(draftLinks, autoSave: true);

		return new UploadDocumentsResponse
		{
			Success = true,
			Data = new UploadDocumentsData
			{
				ConcessionRequestId = concessionRequestId,
				RequestStatus = (int) ConcessionRequestStatus.PENDING,
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

	// ─── Step 3 GET — Fee Heads (Edit/Resume) ────────────────────────────────

	public async Task<GetFeeHeadsResponse> GetConcessionRequestFeeHeadsAsync(Guid concessionRequestId)
	{
		var cr = await _concessionRequestRepository.GetAsync(concessionRequestId);

		// Load all concession type links for this request
		var links = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);

		// Load saved fee head mappings (Step 3)
		var mappings = await _feeHeadMappingRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);

		// Load concession type details for name and discountType
		var ctIds = links.Select(x => x.ConcessionTypeId).Distinct().ToList();
		var concessionTypes = await _concessionTypeRepository.GetListAsync(x => ctIds.Contains(x.Id));
		var ctDict = concessionTypes.ToDictionary(x => x.Id, x => x);

		// Group saved mappings by concession type
		var mappingsByCtId = mappings
			.GroupBy(m => m.ConcessionTypeId)
			.ToDictionary(g => g.Key, g => g.Select(m => m.FeeHeadId).ToList());

		var concessionTypeDtos = links.Select(l =>
		{
			var ct = ctDict.GetValueOrDefault(l.ConcessionTypeId);
			return new ConcessionTypeFeeHeadDto
			{
				ConcessionTypeId = l.ConcessionTypeId,
				ConcessionName = ct?.Name ?? "",
				DiscountType = "",
				FeeHeadIds = mappingsByCtId.GetValueOrDefault(l.ConcessionTypeId) ?? new List<Guid>()
			};
		}).ToList();

		return new GetFeeHeadsResponse
		{
			Success = true,
			Data = new GetFeeHeadsData
			{
				ConcessionRequestId = cr.Id,
				StudentId = cr.StudentId,
				ConcessionTypes = concessionTypeDtos
			}
		};
	}

	// ─── Step 4 GET — Value Entries (Edit/Resume) ─────────────────────────────

	public async Task<GetValueEntriesResponse> GetConcessionRequestValueEntriesAsync(Guid concessionRequestId)
	{
		var cr = await _concessionRequestRepository.GetAsync(concessionRequestId);

		// Load concession type links
		var links = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);

		// Load saved fee head values (Step 4)
		var feeHeadValues = await _feeHeadValueRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);

		// Load concession type names
		var ctIds = links.Select(x => x.ConcessionTypeId).Distinct().ToList();
		var concessionTypes = await _concessionTypeRepository.GetListAsync(x => ctIds.Contains(x.Id));
		var ctDict = concessionTypes.ToDictionary(x => x.Id, x => x);

		// Load fee head names
		var feeHeadIds = feeHeadValues.Select(v => v.FeeHeadId).Distinct().ToList();
		var feeHeads = feeHeadIds.Any()
			? await _feeHeadRepository.GetListAsync(x => feeHeadIds.Contains(x.Id))
			: new System.Collections.Generic.List<FeeHead>();
		var fhDict = feeHeads.ToDictionary(x => x.Id, x => x);

		// Group values by concession type
		var valuesByCtId = feeHeadValues
			.GroupBy(v => v.ConcessionTypeId)
			.ToDictionary(g => g.Key, g => g.ToList());

		var concessionTypeValueDtos = links.Select(l =>
		{
			var ct = ctDict.GetValueOrDefault(l.ConcessionTypeId);
			var values = valuesByCtId.GetValueOrDefault(l.ConcessionTypeId)
				?? new System.Collections.Generic.List<ConcessionRequestFeeHeadValue>();

			return new ConcessionTypeValueEntryDto
			{
				ConcessionTypeId = l.ConcessionTypeId,
				ConcessionName = ct?.Name ?? "",
				FeeHeadValues = values.Select(v => new FeeHeadValueEntryDto
				{
					FeeHeadId = v.FeeHeadId,
					FeeHeadName = fhDict.GetValueOrDefault(v.FeeHeadId)?.Name ?? "",
					BasedOn = (int) v.BasedOn,
					Amount = v.Amount,
					NetAmount = v.NetAmount
				}).ToList()
			};
		}).ToList();

		return new GetValueEntriesResponse
		{
			Success = true,
			Data = new GetValueEntriesData
			{
				ConcessionRequestId = cr.Id,
				StudentId = cr.StudentId,
				ConcessionTypeValues = concessionTypeValueDtos
			}
		};
	}

	// ─── Step 5 GET — Document Upload (Edit/Resume) ───────────────────────────

	public async Task<GetDocumentUploadResponse> GetConcessionRequestDocumentsForResumeAsync(Guid concessionRequestId)
	{
		var cr = await _concessionRequestRepository.GetAsync(concessionRequestId);

		// Load concession type links
		var links = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);

		var ctIds = links.Select(x => x.ConcessionTypeId).Distinct().ToList();

		// Load concession type names
		var concessionTypes = await _concessionTypeRepository.GetListAsync(x => ctIds.Contains(x.Id));
		var ctDict = concessionTypes.ToDictionary(x => x.Id, x => x);

		// Load document slot definitions from the concession type configuration
		var docDefs = await _concessionTypeDocumentRepository.GetListAsync(
			x => ctIds.Contains(x.ConcessionTypeId));

		// Load previously uploaded documents for this request
		var uploadedDocs = await _concessionRequestDocumentRepository.GetListAsync(
			x => x.ConcessionRequestId == concessionRequestId);
		// Key: (ConcessionTypeId, DocumentId)
		var uploadedDocDict = uploadedDocs.ToDictionary(
			d => (d.ConcessionTypeId, d.DocumentId),
			d => d);

		// Group doc definitions by concession type
		var docDefsByCtId = docDefs
			.GroupBy(d => d.ConcessionTypeId)
			.ToDictionary(g => g.Key, g => g.ToList());

		var concessionTypeDocDtos = links.Select(l =>
		{
			var ct = ctDict.GetValueOrDefault(l.ConcessionTypeId);
			var slots = docDefsByCtId.GetValueOrDefault(l.ConcessionTypeId)
				?? new System.Collections.Generic.List<ConcessionTypeDocument>();

			var documentSlots = slots.Select(slot =>
			{
				// Parse accepted formats from comma-separated string of ints
				var acceptedFormats = new System.Collections.Generic.List<int>();
				if (!string.IsNullOrWhiteSpace(slot.AcceptedFormats))
				{
					foreach (var part in slot.AcceptedFormats.Split(','))
					{
						if (int.TryParse(part.Trim(), out var fmt))
							acceptedFormats.Add(fmt);
					}
				}

				// Check if this slot has been previously uploaded
				uploadedDocDict.TryGetValue((l.ConcessionTypeId, slot.Id), out var uploaded);

				return new DocumentSlotDto
				{
					DocumentId = slot.Id,
					DocumentName = slot.DocumentName,
					AcceptedFormat = acceptedFormats,
					Url = uploaded?.Url,
					Status = uploaded?.Status ?? 0,
					UploadedAt = uploaded?.UploadedAt
				};
			}).ToList();

			return new ConcessionTypeDocumentUploadDto
			{
				ConcessionTypeId = l.ConcessionTypeId,
				ConcessionTypeName = ct?.Name ?? "",
				Documents = documentSlots
			};
		}).ToList();

		return new GetDocumentUploadResponse
		{
			Success = true,
			Data = new GetDocumentUploadData
			{
				ConcessionRequestId = cr.Id,
				StudentId = cr.StudentId,
				ConcessionTypeDocuments = concessionTypeDocDtos
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

		var crQuery = await _concessionRequestRepository.GetQueryableAsync();
		var linkQuery = await _concessionRequestConcessionTypeRepository.GetQueryableAsync();

		var query = from link in linkQuery
					join cr in crQuery on link.ConcessionRequestId equals cr.Id
					where cr.StudentId == studentId
					select new { link, cr };

		if (status.HasValue)
			query = query.Where(x => (int) x.link.Status == status.Value);

		var totalCount = query.Count();
		var allItems = query.OrderByDescending(x => x.link.CreationTime).ToList();

		var allForStudentLinks = (from l in linkQuery
								  join cr in crQuery on l.ConcessionRequestId equals cr.Id
								  where cr.StudentId == studentId
								  select l).ToList();
		var draftApplications = allForStudentLinks.Count(x => x.Status == ConcessionRequestStatus.DRAFT);
		var pendingApplications = allForStudentLinks.Count(x => x.Status == ConcessionRequestStatus.PENDING);
		var approvedApplications = allForStudentLinks.Count(x => x.Status == ConcessionRequestStatus.APPROVED);
		var rejectedApplications = allForStudentLinks.Count(x => x.Status == ConcessionRequestStatus.REJECTED);

		var pagedItems = allItems.Skip((page - 1) * pageSize).Take(pageSize).ToList();
		var requestIds = pagedItems.Select(x => x.cr.Id).Distinct().ToList();
		var linkIds = pagedItems.Select(x => x.link.Id).ToList();

		var ctIds = pagedItems.Select(x => x.link.ConcessionTypeId).Distinct().ToList();
		var concessionTypes = await _concessionTypeRepository.GetListAsync(x => ctIds.Contains(x.Id));
		var ctDict = concessionTypes.ToDictionary(x => x.Id, x => x);

		// Load fee head values
		var feeHeadValues = await _feeHeadValueRepository.GetListAsync(
			x => requestIds.Contains(x.ConcessionRequestId));

		// Load uploaded documents
		var uploadedDocs = await _concessionRequestDocumentRepository.GetListAsync(
			x => requestIds.Contains(x.ConcessionRequestId));

		// Load doc definitions for names
		var docDefIds = uploadedDocs.Select(d => d.DocumentId).Distinct().ToList();
		var docDefs = await _concessionTypeDocumentRepository.GetListAsync(x => docDefIds.Contains(x.Id));
		var docDefDict = docDefs.ToDictionary(x => x.Id, x => x);

		var items = new List<ConcessionRequestItemDto>();
		foreach (var item in pagedItems)
		{
			var cr = item.cr;
			var link = item.link;

			var ct = ctDict.GetValueOrDefault(link.ConcessionTypeId);
			var crValues = feeHeadValues.Where(v =>
				v.ConcessionRequestId == cr.Id && v.ConcessionTypeId == link.ConcessionTypeId).ToList();

			// Load fee head names for these values
			var valFhIds = crValues.Select(v => v.FeeHeadId).Distinct().ToList();
			var valFeeHeads = await _feeHeadRepository.GetListAsync(x => valFhIds.Contains(x.Id));
			var valFhDict = valFeeHeads.ToDictionary(x => x.Id, x => x);

			var feeHeadItems = crValues.Select(v => new FeeHeadItemDto
			{
				FeeHeadId = v.FeeHeadId,
				FeeHeadName = valFhDict.ContainsKey(v.FeeHeadId) ? valFhDict[v.FeeHeadId].Name : "",
				Amount = v.DiscountAmount,
				NetAmount = v.NetAmount,
				Description = valFhDict.ContainsKey(v.FeeHeadId) ? valFhDict[v.FeeHeadId].Description : null
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
				Status = (int) link.Status,
				TotalAmount = crValues.Sum(v => v.NetAmount),
				FeeHeads = feeHeadItems,
				SubmittedDocuments = submittedDocuments,
				IsDocumentUploaded = isDocumentUploaded,
				CreatedAt = link.CreationTime,
				UpdatedAt = link.LastModificationTime,
				Message = link.ApproverMessage ?? link.RejectionReason,
				ApprovedAt = link.ApprovedAt
			});
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
				TotalApplications = allForStudentLinks.Count,
				DraftApplications = draftApplications,
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

		if (link.Status == ConcessionRequestStatus.DRAFT)
			throw new BusinessException("ShikshaMitra:CannotApproveDraftRequest")
				.WithData("Message", "Cannot approve a concession request that has not been fully submitted by the applicant.");

		if (link.Status == ConcessionRequestStatus.APPROVED)
			throw new BusinessException("ShikshaMitra:ConcessionRequestAlreadyApproved")
				.WithData("Message", "Concession request is already approved.");

		if (link.Status == ConcessionRequestStatus.REJECTED)
			throw new BusinessException("ShikshaMitra:CannotApproveRejectedRequest")
				.WithData("Message", "Cannot approve a rejected concession request.");

		var approverId = _currentUser.Id ?? Guid.Empty;
		link.Approve(approverId, input.Message);
		await _concessionRequestConcessionTypeRepository.UpdateAsync(link);

		return new ApprovalResponse
		{
			StudentConcessionTypeRequestId = studentConcessionTypeRequestId,
			Status = (int) ConcessionRequestStatus.APPROVED,
			StatusName = "Approved",
			ApprovedAt = link.ApprovedAt!.Value,
			ApproverId = approverId,
			ApproverMessage = link.ApproverMessage,
			Message = "Application approved successfully"
		};
	}

	// ─── Reject ──────────────────────────────────────────────────────────────

	public async Task<RejectionResponse> RejectConcessionRequestAsync(
		Guid studentConcessionTypeRequestId, RejectionRequest input)
	{
		var link = await _concessionRequestConcessionTypeRepository.GetAsync(studentConcessionTypeRequestId);
		var cr = await _concessionRequestRepository.GetAsync(link.ConcessionRequestId);

		if (link.Status == ConcessionRequestStatus.DRAFT)
			throw new BusinessException("ShikshaMitra:CannotRejectDraftRequest")
				.WithData("Message", "Cannot reject a concession request that has not been fully submitted by the applicant.");

		if (link.Status == ConcessionRequestStatus.REJECTED)
			throw new BusinessException("ShikshaMitra:ConcessionRequestAlreadyRejected")
				.WithData("Message", "Concession request is already rejected.");

		if (link.Status == ConcessionRequestStatus.APPROVED)
			throw new BusinessException("ShikshaMitra:CannotRejectApprovedRequest")
				.WithData("Message", "Cannot reject an approved concession request.");

		var rejectorId = _currentUser.Id ?? Guid.Empty;
		link.Reject(rejectorId, input.Message);
		await _concessionRequestConcessionTypeRepository.UpdateAsync(link);

		return new RejectionResponse
		{
			StudentConcessionTypeRequestId = studentConcessionTypeRequestId,
			Status = (int) ConcessionRequestStatus.REJECTED,
			StatusName = "Rejected",
			RejectedAt = link.RejectedAt!.Value,
			RejectorId = rejectorId,
			RejectionReason = link.RejectionReason,
			Message = "Application rejected successfully"
		};
	}

	// ─── Get Concession Applications (admin list) ─────────────────────────────

	public async Task<ConcessionApplicationsResponse> GetConcessionApplicationsAsync(
		Guid boardId, Guid educationMediumId, int? status, int page, int pageSize, string? search)
	{
		var crQuery = await _concessionRequestRepository.GetQueryableAsync();
		var linkQuery = await _concessionRequestConcessionTypeRepository.GetQueryableAsync();

		// ── Fetch all requests for this board+medium ──────────────────────────
		var allRequests = crQuery
			.Where(cr => cr.BoardId == boardId && cr.EducationMediumId == educationMediumId)
			.ToList();

		var allRequestIds = allRequests.Select(cr => cr.Id).ToList();

		// ── Fetch all concession type links for these requests ─────────────────
		var allLinks = linkQuery
			.Where(l => allRequestIds.Contains(l.ConcessionRequestId))
			.ToList();

		// ── Build a lookup: requestId → its links ─────────────────────────────
		var linksByRequestId = allLinks
			.GroupBy(l => l.ConcessionRequestId)
			.ToDictionary(g => g.Key, g => g.ToList());

		// ── Tab counts: number of distinct REQUESTS having at least one link of that status
		int draftCount = allRequests.Count(cr => linksByRequestId.TryGetValue(cr.Id, out var ls) && ls.Any(l => l.Status == ConcessionRequestStatus.DRAFT));
		int pendingCount = allRequests.Count(cr => linksByRequestId.TryGetValue(cr.Id, out var ls) && ls.Any(l => l.Status == ConcessionRequestStatus.PENDING));
		int approvedCount = allRequests.Count(cr => linksByRequestId.TryGetValue(cr.Id, out var ls) && ls.Any(l => l.Status == ConcessionRequestStatus.APPROVED));
		int rejectedCount = allRequests.Count(cr => linksByRequestId.TryGetValue(cr.Id, out var ls) && ls.Any(l => l.Status == ConcessionRequestStatus.REJECTED));

		// ── Status filter: keep requests that have at least one link matching the requested status
		var filteredRequests = allRequests;
		if (status.HasValue)
		{
			var targetStatus = (ConcessionRequestStatus) status.Value;
			var requestIdsWithStatus = allLinks
				.Where(l => l.Status == targetStatus)
				.Select(l => l.ConcessionRequestId)
				.ToHashSet();
			filteredRequests = filteredRequests
				.Where(cr => requestIdsWithStatus.Contains(cr.Id))
				.ToList();
		}

		// ── Load student data for search + response ───────────────────────────
		var studentIds = filteredRequests.Select(cr => cr.StudentId).Distinct().ToList();
		var students = await _studentRepository.GetListAsync(x => studentIds.Contains(x.Id));
		var studentDict = students.ToDictionary(x => x.Id, x => x);

		// ── Apply search filter ───────────────────────────────────────────────
		if (!string.IsNullOrWhiteSpace(search))
		{
			filteredRequests = filteredRequests.Where(cr =>
			{
				if (!studentDict.TryGetValue(cr.StudentId, out var s)) return false;
				var name = string.Join(" ", new[] { s.FirstName, s.MiddleName, s.LastName }
					.Where(val => !string.IsNullOrWhiteSpace(val)));
				return name.Contains(search, StringComparison.OrdinalIgnoreCase)
					|| s.RollNo?.ToString().Contains(search) == true
					|| s.GrNo?.ToString().Contains(search) == true;
			}).ToList();
		}

		// ── Sort + paginate ───────────────────────────────────────────────────
		filteredRequests = filteredRequests
			.OrderByDescending(cr => cr.CreationTime)
			.ToList();

		int totalCount = filteredRequests.Count;
		var pagedRequests = filteredRequests.Skip((page - 1) * pageSize).Take(pageSize).ToList();

		// ── Load standards/divisions for paged items ──────────────────────────
		var standardIds = pagedRequests.Select(cr => cr.StandardId).Distinct().ToList();
		var divisionIds = pagedRequests.Select(cr => cr.DivisionId).Distinct().ToList();
		var standards = await _standardRepository.GetListAsync(x => standardIds.Contains(x.Id));
		var divisions = await _divisionRepository.GetListAsync(x => divisionIds.Contains(x.Id));
		var standardDict = standards.ToDictionary(x => x.Id, x => x.Name);
		var divisionDict = divisions.ToDictionary(x => x.Id, x => x.Name);

		// ── Build response items ──────────────────────────────────────────────
		var items = pagedRequests.Select(cr =>
		{
			studentDict.TryGetValue(cr.StudentId, out var s);
			var name = s != null
				? string.Join(" ", new[] { s.FirstName, s.MiddleName, s.LastName }
					.Where(val => !string.IsNullOrWhiteSpace(val)))
				: "";

			var requestLinks = linksByRequestId.GetValueOrDefault(cr.Id, new List<ConcessionRequestConcessionType>());

			return new ConcessionApplicationDto
			{
				ConcessionRequestId = cr.Id,
				StudentId = cr.StudentId,
				StudentName = name,
				RollNo = s?.RollNo?.ToString() ?? "",
				GrNumber = s?.GrNo?.ToString(),
				StandardName = standardDict.GetValueOrDefault(cr.StandardId, ""),
				DivisionName = divisionDict.GetValueOrDefault(cr.DivisionId, ""),
				RequestDate = cr.CreationTime.ToString("yyyy-MM-dd"),
				Email = string.Empty,
				ContactNo = string.Empty,
				TotalConcessionApplications = requestLinks.Count,
				DraftCount = requestLinks.Count(l => l.Status == ConcessionRequestStatus.DRAFT),
				PendingCount = requestLinks.Count(l => l.Status == ConcessionRequestStatus.PENDING),
				ApprovedCount = requestLinks.Count(l => l.Status == ConcessionRequestStatus.APPROVED),
				RejectedCount = requestLinks.Count(l => l.Status == ConcessionRequestStatus.REJECTED),
				CreatedAt = cr.CreationTime,
				UpdatedAt = cr.LastModificationTime
			};
		}).ToList();

		return new ConcessionApplicationsResponse
		{
			DraftCount = draftCount,
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

		if (link.Status == ConcessionRequestStatus.APPROVED)
			throw new BusinessException("ShikshaMitra:CannotDeleteApprovedRequest")
				.WithData("Message", "Cannot delete an approved concession application.");

		var crId = link.Id;

		// Delete all related data
		var allLinks = await _concessionRequestConcessionTypeRepository.GetListAsync(
			x => x.Id == crId);
		await _concessionRequestConcessionTypeRepository.DeleteManyAsync(allLinks);

		var allMappings = await _feeHeadMappingRepository.GetListAsync(
			x => x.ConcessionRequestConcessionTypeId == crId);
		await _feeHeadMappingRepository.DeleteManyAsync(allMappings);

		var allValues = await _feeHeadValueRepository.GetListAsync(
			x => x.ConcessionRequestConcessionTypeId == crId);
		await _feeHeadValueRepository.DeleteManyAsync(allValues);

		var allDocs = await _concessionRequestDocumentRepository.GetListAsync(
			x => x.ConcessionRequestConcessionTypeId == crId);
		await _concessionRequestDocumentRepository.DeleteManyAsync(allDocs);

		await _concessionRequestRepository.DeleteAsync(crId);
	}
}
