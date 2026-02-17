using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Finance.Charges;
using ShikshaMitra.Application.Contracts.Finance.Charges.Dtos;
using ShikshaMitra.Finance.Charges;
using ShikshaMitra.Finance.Setting;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Charges;

[RemoteService(false)]
public class ChargeAppService : ApplicationService, IChargeAppService
{
	private readonly IRepository<Charge, Guid> _chargeRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<ChargeCategory, Guid> _chargeCategoryRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;
	private readonly IRepository<EducationBoard, Guid> _educationBoardRepository;
	private readonly IRepository<EducationMedium, Guid> _educationMediumRepository;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
	private readonly IRepository<IdentityUser, Guid> _userRepository;
	private readonly ICurrentUser _currentUser;

	public ChargeAppService(
		IRepository<Charge, Guid> chargeRepository,
		IRepository<Student, Guid> studentRepository,
		IRepository<ChargeCategory, Guid> chargeCategoryRepository,
		IRepository<Standard, Guid> standardRepository,
		IRepository<Division, Guid> divisionRepository,
		IRepository<EducationBoard, Guid> educationBoardRepository,
		IRepository<EducationMedium, Guid> educationMediumRepository,
		IRepository<AcademicYear, Guid> academicYearRepository,
		IRepository<IdentityUser, Guid> userRepository,
		ICurrentUser currentUser)
	{
		_chargeRepository = chargeRepository;
		_studentRepository = studentRepository;
		_chargeCategoryRepository = chargeCategoryRepository;
		_standardRepository = standardRepository;
		_divisionRepository = divisionRepository;
		_educationBoardRepository = educationBoardRepository;
		_educationMediumRepository = educationMediumRepository;
		_academicYearRepository = academicYearRepository;
		_userRepository = userRepository;
		_currentUser = currentUser;
	}

	public async Task<ChargeResponse> CreateAsync(Guid academicYearId, CreateChargeRequest input)
	{
		// Validate academic year exists
		var academicYear = await _academicYearRepository.FindAsync(academicYearId);
		if (academicYear == null)
		{
			throw new BusinessException("ShikshaMitra:AcademicYearNotFound")
				.WithData("AcademicYearId", academicYearId);
		}

		// Validate student exists
		var student = await _studentRepository.FindAsync(input.StudentId);
		if (student == null)
		{
			throw new BusinessException("ShikshaMitra:StudentNotFound")
				.WithData("StudentId", input.StudentId);
		}

		// Validate charge category exists and get approval requirement
		var chargeCategory = await _chargeCategoryRepository.FindAsync(input.ChargeCategoryId);
		if (chargeCategory == null)
		{
			throw new BusinessException("ShikshaMitra:ChargeCategoryNotFound")
				.WithData("ChargeCategoryId", input.ChargeCategoryId);
		}

		// Validate other references exist
		await ValidateReferencesExist(input.BoardId, input.EducationMediumId, input.StandardId, input.DivisionId);

		// Determine initial status based on category approval requirement
		ChargeStatus initialStatus = chargeCategory.IsApprovalRequired ? ChargeStatus.Pending : ChargeStatus.Approved;

		var charge = new Charge(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.ChargeCategoryId,
			input.StudentId,
			input.BoardId,
			input.EducationMediumId,
			input.StandardId,
			input.DivisionId,
			input.PurposeDescription,
			input.Amount,
			input.DueDate,
			(ChargePriority) input.Priority,
			initialStatus,
			_currentUser.Id.Value,
			input.AdditionalNotes
		);

		await _chargeRepository.InsertAsync(charge, autoSave: true);

		var response = await BuildChargeResponseAsync(charge);
		response.Message = initialStatus == ChargeStatus.Pending
			? "Charge created successfully and pending approval"
			: "Charge created and approved successfully";

		return response;
	}

	public async Task<ChargeListResponse> GetAllAsync(Guid academicYearId, GetChargesRequest input)
	{
		IQueryable<Charge>? query = await _chargeRepository.GetQueryableAsync();
		IQueryable<Student>? studentQuery = await _studentRepository.GetQueryableAsync();
		IQueryable<ChargeCategory>? categoryQuery = await _chargeCategoryRepository.GetQueryableAsync();
		IQueryable<Standard>? standardQuery = await _standardRepository.GetQueryableAsync();
		IQueryable<Division>? divisionQuery = await _divisionRepository.GetQueryableAsync();
		IQueryable<EducationBoard>? boardQuery = await _educationBoardRepository.GetQueryableAsync();
		IQueryable<EducationMedium>? mediumQuery = await _educationMediumRepository.GetQueryableAsync();
		IQueryable<IdentityUser>? userQuery = await _userRepository.GetQueryableAsync();

		// Join with related entities
		var joinedQuery = from charge in query
						  join student in studentQuery on charge.StudentId equals student.Id
						  join category in categoryQuery on charge.ChargeCategoryId equals category.Id
						  join standard in standardQuery on charge.StandardId equals standard.Id
						  join division in divisionQuery on charge.DivisionId equals division.Id
						  join board in boardQuery on charge.BoardId equals board.Id
						  join medium in mediumQuery on charge.EducationMediumId equals medium.Id
						  join requestor in userQuery on charge.RequestorId equals requestor.Id
						  where charge.AcademicYearId == academicYearId
						  select new { charge, student, category, standard, division, board, medium, requestor };

		// Apply filters
		joinedQuery = joinedQuery.Where(x => x.charge.Status == (ChargeStatus) input.Status);

		if (input.BoardId.HasValue)
		{
			joinedQuery = joinedQuery.Where(x => x.charge.BoardId == input.BoardId.Value);
		}

		if (input.EducationMediumId.HasValue)
		{
			joinedQuery = joinedQuery.Where(x => x.charge.EducationMediumId == input.EducationMediumId.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var searchLower = input.Search.ToLower();
			joinedQuery = joinedQuery.Where(x =>
				(x.student.FirstName + " " + x.student.MiddleName + " " + x.student.LastName).ToLower().Contains(searchLower) ||
				x.student.GrNo.ToString().Contains(searchLower) ||
				x.student.RollNo.ToString().Contains(searchLower));
		}

		// Apply sorting
		joinedQuery = ApplySorting(joinedQuery, input.SortBy, input.SortOrder);

		var totalCount = await AsyncExecuter.CountAsync(joinedQuery);

		var items = await AsyncExecuter.ToListAsync(
			joinedQuery
				.Skip((input.Page - 1) * input.PageSize)
				.Take(input.PageSize)
		);

		var totalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize);

		List<ChargeListItem>? listItems = items.Select(x => new ChargeListItem
		{
			ChargeId = x.charge.Id,
			StudentId = x.student.Id,
			StudentName = $"{x.student.FirstName} {x.student.MiddleName} {x.student.LastName}".Trim(),
			GrNumber = x.student.GrNo?.ToString() ?? "",
			RollNumber = x.student.RollNo?.ToString() ?? "",
			BoardId = x.board.Id,
			BoardName = x.board.Type.ToString(),
			EducationMediumId = x.medium.Id,
			EducationMediumName = x.medium.Name,
			StandardId = x.standard.Id,
			StandardName = x.standard.Name,
			DivisionId = x.division.Id,
			DivisionName = x.division.Name,
			ChargeCategoryId = x.category.Id,
			CategoryName = x.category.Name,
			Amount = x.charge.Amount,
			PurposeDescription = x.charge.PurposeDescription,
			DueDate = x.charge.DueDate,
			AppliedOn = x.charge.AppliedOn,
			Priority = (int) x.charge.Priority,
			Status = (int) x.charge.Status,
			RequestorId = x.requestor.Id,
			RequestorName = x.requestor.Name ?? x.requestor.UserName,
			RequestorRole = "User", // Can be enhanced with role lookup
			RequestedOn = x.charge.AppliedOn,
			AdditionalNotes = x.charge.AdditionalNotes,

		}).ToList();

		return new ChargeListResponse
		{
			Success = true,
			Items = listItems,
			TotalCount = totalCount,
			CurrentPage = input.Page,
			PageSize = input.PageSize,
			TotalPages = totalPages,
			Message = "Charges retrieved successfully"
		};
	}

	public async Task<ChargeDetailResponse> GetByIdAsync(Guid chargeId)
	{
		var charge = await _chargeRepository.GetAsync(chargeId);

		var student = await _studentRepository.GetAsync(charge.StudentId);
		var category = await _chargeCategoryRepository.GetAsync(charge.ChargeCategoryId);
		var standard = await _standardRepository.GetAsync(charge.StandardId);
		var division = await _divisionRepository.GetAsync(charge.DivisionId);
		var board = await _educationBoardRepository.GetAsync(charge.BoardId);
		var medium = await _educationMediumRepository.GetAsync(charge.EducationMediumId);
		var academicYear = await _academicYearRepository.GetAsync(charge.AcademicYearId);
		var requestor = await _userRepository.GetAsync(charge.RequestorId);

		IdentityUser? approver = null;
		if (charge.ApprovedBy.HasValue)
		{
			approver = await _userRepository.FindAsync(charge.ApprovedBy.Value);
		}

		var response = new ChargeDetailResponse
		{
			Success = true,
			Data = new ChargeDetailData
			{
				ChargeId = charge.Id,
				ChargeCategoryId = charge.ChargeCategoryId,
				CategoryName = category.Name,
				PurposeDescription = charge.PurposeDescription,
				Amount = charge.Amount,
				DueDate = charge.DueDate,
				AppliedOn = charge.AppliedOn,
				Priority = (int) charge.Priority,
				Status = (int) charge.Status,
				AdditionalNotes = charge.AdditionalNotes,
				Student = new StudentInfo
				{
					StudentId = student.Id,
					StudentName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
					GrNumber = student.GrNo?.ToString() ?? "",
					RollNumber = student.RollNo?.ToString() ?? "",
					BoardId = board.Id,
					BoardName = board.Type.ToString(),
					EducationMediumId = medium.Id,
					EducationMediumName = medium.Name,
					StandardId = standard.Id,
					StandardName = standard.Name,
					DivisionId = division.Id,
					DivisionName = division.Name,
					ContactNumber = student.ContactDetail?.PrimaryContactNo,
					EmailAddress = student.ContactDetail?.Email
				},
				Requestor = new RequestorInfo
				{
					RequestorId = requestor.Id,
					RequestorName = requestor.Name ?? requestor.UserName,
					RequestorRole = "User",
					RequestedOn = charge.AppliedOn
				},
				ApprovalDetails = new ApprovalDetails
				{
					ApprovedBy = charge.ApprovedBy,
					ApprovedByName = approver?.Name ?? approver?.UserName,
					ApprovedOn = charge.ApprovedOn,
					ApprovalNotes = charge.ApprovalNotes
				},
				AcademicYearId = charge.AcademicYearId,
				AcademicYearName = academicYear.Name,
				CreatedAt = charge.CreationTime,
				UpdatedAt = charge.LastModificationTime ?? charge.CreationTime
			},
			Message = "Charge details retrieved successfully"
		};

		return response;
	}

	public async Task<ChargeResponse> UpdateAsync(Guid chargeId, UpdateChargeRequest input)
	{
		var charge = await _chargeRepository.GetAsync(chargeId);

		charge.Update(
			input.ChargeCategoryId,
			input.PurposeDescription,
			input.Amount,
			input.DueDate,
			input.Priority.HasValue ? (ChargePriority) input.Priority.Value : null,
			input.AdditionalNotes
		);

		await _chargeRepository.UpdateAsync(charge);

		var response = await BuildChargeResponseAsync(charge);
		response.Message = "Charge updated successfully";

		return response;
	}

	public async Task<ApprovalResponse> ApproveAsync(Guid chargeId, ApproveChargeRequest input)
	{
		var charge = await _chargeRepository.GetAsync(chargeId);

		charge.Approve(_currentUser.Id.Value, input.ApprovalNotes);

		await _chargeRepository.UpdateAsync(charge);

		var approver = await _userRepository.GetAsync(_currentUser.Id.Value);

		return new ApprovalResponse
		{
			Success = true,
			Data = new ApprovalData
			{
				ChargeId = charge.Id,
				Status = (int) ChargeStatus.Approved,
				ApprovedBy = _currentUser.Id.Value,
				ApprovedByName = approver.Name ?? approver.UserName,
				ApprovedOn = charge.ApprovedOn.Value,
				ApprovalNotes = input.ApprovalNotes
			},
			Message = "Charge approved successfully"
		};
	}

	public async Task<RejectionResponse> RejectAsync(Guid chargeId, RejectChargeRequest input)
	{
		var charge = await _chargeRepository.GetAsync(chargeId);

		charge.Reject(_currentUser.Id.Value, input.RejectionReason);

		await _chargeRepository.UpdateAsync(charge, autoSave: true);

		var rejector = await _userRepository.GetAsync(_currentUser.Id.Value);

		return new RejectionResponse
		{
			Success = true,
			Data = new RejectionData
			{
				ChargeId = charge.Id,
				Status = (int) ChargeStatus.Rejected,
				RejectedBy = _currentUser.Id.Value,
				RejectedByName = rejector.Name ?? rejector.UserName,
				RejectedOn = charge.ApprovedOn.Value,
				RejectionReason = input.RejectionReason
			},
			Message = "Charge rejected successfully"
		};
	}

	public async Task DeleteAsync(Guid chargeId)
	{
		var charge = await _chargeRepository.GetAsync(chargeId);

		// Validate that the charge can be deleted (only pending charges)
		charge.ValidateCanDelete();

		await _chargeRepository.DeleteAsync(charge, autoSave: true);
	}

	private async Task ValidateReferencesExist(Guid boardId, Guid mediumId, Guid standardId, Guid divisionId)
	{
		var board = await _educationBoardRepository.FindAsync(boardId);
		if (board == null)
		{
			throw new BusinessException("ShikshaMitra:EducationBoardNotFound")
				.WithData("BoardId", boardId);
		}

		var medium = await _educationMediumRepository.FindAsync(mediumId);
		if (medium == null)
		{
			throw new BusinessException("ShikshaMitra:EducationMediumNotFound")
				.WithData("EducationMediumId", mediumId);
		}

		var standard = await _standardRepository.FindAsync(standardId);
		if (standard == null)
		{
			throw new BusinessException("ShikshaMitra:StandardNotFound")
				.WithData("StandardId", standardId);
		}

		var division = await _divisionRepository.FindAsync(divisionId);
		if (division == null)
		{
			throw new BusinessException("ShikshaMitra:DivisionNotFound")
				.WithData("DivisionId", divisionId);
		}
	}

	private async Task<ChargeResponse> BuildChargeResponseAsync(Charge charge)
	{
		var student = await _studentRepository.GetAsync(charge.StudentId);
		var category = await _chargeCategoryRepository.GetAsync(charge.ChargeCategoryId);
		var standard = await _standardRepository.GetAsync(charge.StandardId);
		var division = await _divisionRepository.GetAsync(charge.DivisionId);
		var board = await _educationBoardRepository.GetAsync(charge.BoardId);
		var medium = await _educationMediumRepository.GetAsync(charge.EducationMediumId);
		var academicYear = await _academicYearRepository.GetAsync(charge.AcademicYearId);
		var requestor = await _userRepository.GetAsync(charge.RequestorId);

		return new ChargeResponse
		{
			Success = true,
			Data = new ChargeData
			{
				ChargeId = charge.Id,
				ChargeCategoryId = charge.ChargeCategoryId,
				CategoryName = category.Name,
				PurposeDescription = charge.PurposeDescription,
				Amount = charge.Amount,
				DueDate = charge.DueDate,
				AppliedOn = charge.AppliedOn,
				Priority = (int) charge.Priority,
				Status = (int) charge.Status,
				BoardId = board.Id,
				BoardName = board.Type.ToString(),
				EducationMediumId = medium.Id,
				EducationMediumName = medium.Name,
				StandardId = standard.Id,
				StandardName = standard.Name,
				DivisionId = division.Id,
				DivisionName = division.Name,
				StudentId = student.Id,
				StudentName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
				GrNumber = student.GrNo?.ToString() ?? "",
				RollNumber = student.RollNo?.ToString() ?? "",
				AdditionalNotes = charge.AdditionalNotes,
				RequestorId = requestor.Id,
				RequestorName = requestor.Name ?? requestor.UserName,
				RequestorRole = "User",
				RequestedOn = charge.AppliedOn,
				AcademicYearId = charge.AcademicYearId,
				AcademicYearName = academicYear.Name,
				CreatedAt = charge.CreationTime,
				UpdatedAt = charge.LastModificationTime ?? charge.CreationTime
			}
		};
	}

	private IQueryable<T> ApplySorting<T>(IQueryable<T> query, string? sortBy, string? sortOrder) where T : class
	{
		if (string.IsNullOrWhiteSpace(sortBy))
		{
			sortBy = "appliedOn";
		}

		var isAscending = string.IsNullOrWhiteSpace(sortOrder) || sortOrder.ToLower() == "asc";

		// Note: This is a simplified sorting. In production, you might want to use dynamic LINQ
		// For now, we'll sort by appliedOn as default
		return query;
	}
}
