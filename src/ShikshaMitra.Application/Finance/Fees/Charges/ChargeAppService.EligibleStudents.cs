using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Records;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Fees.Charges;

public partial class ChargeAppService
{
	public async Task<ChargeEligibleStudentsResponse> GetEligibleStudentsAsync(GetChargeEligibleStudentsRequest input)
	{
		if (input.AcademicYearId == Guid.Empty || input.BoardId == Guid.Empty ||
			input.EducationMediumId == Guid.Empty || input.ClassIds is not { Count: > 0 } ||
			input.ClassIds.Contains(Guid.Empty) || input.Page < 1 || input.PageSize is < 1 or > 100 ||
			(long)(input.Page - 1) * input.PageSize > int.MaxValue ||
			(input.RollNoFrom.HasValue && input.RollNoTo.HasValue && input.RollNoFrom > input.RollNoTo))
			throw new AbpValidationException("Invalid charge student lookup scope or page.");
		if (input.Charged.HasValue && !input.ChargeCategoryId.HasValue)
			throw new AbpValidationException("A charge category is required for charged filtering.");

		var actionPermission = await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)
			? ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add
			: ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update;
		if (!await AuthorizationService.IsGrantedAsync(actionPermission))
			throw new AbpAuthorizationException("Charge create or edit permission is required.");
		await EnsureAcademicYearExistsAsync(input.AcademicYearId);
		if (input.ChargeCategoryId.HasValue && !await chargeCategoryRepository.AnyAsync(category =>
			category.Id == input.ChargeCategoryId.Value && category.AcademicYearId == input.AcademicYearId))
			throw new AbpValidationException("Charge category does not belong to the academic year.");

		var scopedIds = await financeScopeAccessService.GetRequestScopedStudentIdsQueryAsync(
			input.AcademicYearId, actionPermission);
		var classIds = input.ClassIds.Distinct().ToArray();
		var divisionIds = classIds.Length == 1 ? input.DivisionIds?.Distinct().ToArray() : null;
		var assignments = await studentFeeStructureRepository.GetQueryableAsync();
		var charges = await chargeRepository.GetQueryableAsync();
		var eligible = (await studentRepository.GetQueryableWithoutAutoIncludesAsync())
			.Where(student => student.AcademicYearId == input.AcademicYearId &&
				student.AcademicBoardId == input.BoardId &&
				student.AcademicMediumId == input.EducationMediumId &&
				student.StandardId.HasValue && Enumerable.Contains(classIds, student.StandardId.Value) &&
				student.DivisionId.HasValue && student.Status == StudentLifeCycleStatusEnum.Enrolled &&
				scopedIds.Contains(student.Id) &&
				assignments.Any(assignment => assignment.StudentId == student.Id &&
					assignment.AcademicYearId == input.AcademicYearId));
		if (divisionIds is { Length: > 0 })
			eligible = eligible.Where(student => student.DivisionId.HasValue &&
				Enumerable.Contains(divisionIds, student.DivisionId.Value));

		var candidateRows = await AsyncExecuter.ToListAsync(eligible
			.Select(student => new { student.Id, Name = (student.FirstName + " " +
				(student.MiddleName ?? "") + " " + student.LastName).Trim() })
			.OrderBy(row => row.Id));
		var candidateIds = candidateRows.Select(row => row.Id).ToList();
		var matching = eligible;
		if (input.DivisionFilterId.HasValue)
			matching = matching.Where(student => student.DivisionId == input.DivisionFilterId.Value);
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			matching = matching.Where(student =>
				(student.FirstName + " " + (student.MiddleName ?? "") + " " + student.LastName)
					.ToLower().Contains(search) ||
				(student.GrNo.HasValue && student.GrNo.Value.ToString().Contains(search)) ||
				(student.RollNo.HasValue && student.RollNo.Value.ToString().Contains(search)));
		}
		if (input.Gender.HasValue)
			matching = matching.Where(student => student.AdditionalDetail != null &&
				student.AdditionalDetail.Gender == input.Gender.Value);
		if (input.RollNoFrom.HasValue)
			matching = matching.Where(student => student.RollNo >= input.RollNoFrom.Value);
		if (input.RollNoTo.HasValue)
			matching = matching.Where(student => student.RollNo <= input.RollNoTo.Value);
		if (input.Charged.HasValue)
			matching = matching.Where(student => charges.Any(charge =>
				charge.StudentId == student.Id && charge.AcademicYearId == input.AcademicYearId &&
				charge.ChargeCategoryId == input.ChargeCategoryId!.Value &&
				(charge.Status == ChargeStatus.Pending || charge.Status == ChargeStatus.Approved)) == input.Charged.Value);

		var hasDisplayFilter = input.DivisionFilterId.HasValue || !string.IsNullOrWhiteSpace(input.Search) ||
			input.Gender.HasValue || input.RollNoFrom.HasValue || input.RollNoTo.HasValue || input.Charged.HasValue;
		var matchingRows = hasDisplayFilter
			? await AsyncExecuter.ToListAsync(matching
				.Select(student => new { student.Id, Name = (student.FirstName + " " +
					(student.MiddleName ?? "") + " " + student.LastName).Trim() })
				.OrderBy(row => row.Id))
			: candidateRows;
		var matchingIds = matchingRows.Select(row => row.Id).ToList();
		var items = await AsyncExecuter.ToListAsync(matching
			.OrderBy(student => student.RollNo).ThenBy(student => student.FirstName).ThenBy(student => student.Id)
			.Skip((input.Page - 1) * input.PageSize).Take(input.PageSize)
			.Select(student => new StudentLookupDto
			{
				StudentId = student.Id,
				StudentName = (student.FirstName + " " + (student.MiddleName ?? "") + " " + student.LastName).Trim(),
				GrNo = student.GrNo,
				RollNo = student.RollNo,
				StandardId = student.StandardId,
				StandardName = student.Standard != null ? student.Standard.Name : string.Empty,
				DivisionId = student.DivisionId,
				DivisionName = student.Division != null ? student.Division.Name : string.Empty,
				Gender = student.AdditionalDetail != null ? student.AdditionalDetail.Gender : null,
				HasFeeStructureAssignment = true,
				HasChargeInCategory = input.ChargeCategoryId.HasValue && charges.Any(charge =>
					charge.StudentId == student.Id && charge.AcademicYearId == input.AcademicYearId &&
					charge.ChargeCategoryId == input.ChargeCategoryId.Value &&
					(charge.Status == ChargeStatus.Pending || charge.Status == ChargeStatus.Approved))
			}));
		return new ChargeEligibleStudentsResponse
		{
			Items = items,
			CandidateStudentIds = candidateIds,
			MatchingStudentIds = matchingIds,
			MatchingStudentNames = matchingRows.ToDictionary(row => row.Id, row => row.Name),
			TotalCount = matchingIds.Count,
			Page = input.Page,
			PageSize = input.PageSize
		};
	}
}
