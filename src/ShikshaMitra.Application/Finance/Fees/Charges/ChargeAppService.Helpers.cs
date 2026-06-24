using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Charges;

public partial class ChargeAppService
{
	private async Task<ChargeAudienceDto> ResolveAudienceDtoAsync(CreateChargeRequest input)
	{
		if (input.Audience is not null)
			return input.Audience;

		if (!input.StudentId.HasValue)
			throw new BusinessException(FinanceChargeErrorCodes.EmptyChargeAudience);

		var student = await studentRepository.FindAsync(input.StudentId.Value)
			?? throw new BusinessException(FinanceChargeErrorCodes.StudentNotFound)
				.WithData("StudentId", input.StudentId.Value);

		return ChargeAudienceMapper.BuildLegacyAudience(student, input.StudentId.Value);
	}

	private async Task<ChargeResponse> BuildChargeResponseAsync(ChargeRequest request, int studentCount, string message)
	{
		var category = await chargeCategoryRepository.GetAsync(request.ChargeCategoryId);
		var requestor = await userRepository.GetAsync(request.RequestorId);
		var academicYear = await academicYearRepository.GetAsync(request.AcademicYearId);
		var lines = await chargeRepository.GetListAsync(c => c.ChargeRequestId == request.Id);
		var status = DeriveRequestStatus(lines.Select(l => l.Status).ToList());

		return new ChargeResponse
		{
			Success = true,
			Data = new ChargeData
			{
				ChargeId = request.Id, ChargeName = request.ChargeName,
				ResolvedStudentCount = studentCount,
				ChargeCategoryId = request.ChargeCategoryId, CategoryName = category.Name,
				PurposeDescription = request.PurposeDescription, Amount = request.Amount,
				DueDate = request.DueDate, AppliedOn = request.AppliedOn,
				Priority = (int)request.Priority, Status = (int)status,
				BoardId = request.BoardId, EducationMediumId = request.EducationMediumId,
				AdditionalNotes = request.AdditionalNotes,
				RequestorId = request.RequestorId,
				RequestorName = requestor.Name ?? requestor.UserName ?? string.Empty,
				RequestedOn = request.AppliedOn,
				AcademicYearId = request.AcademicYearId, AcademicYearName = academicYear.Name
			},
			Message = message
		};
	}

	private static ChargeListItem MapToListItem(ChargeRequestJoinResult r) =>
		new()
		{
			ChargeId = r.Request.Id, ChargeName = r.Request.ChargeName,
			BoardId = r.Request.BoardId, EducationMediumId = r.Request.EducationMediumId,
			ChargeCategoryId = r.Category.Id, CategoryName = r.Category.Name,
			Amount = r.Request.Amount, PurposeDescription = r.Request.PurposeDescription,
			DueDate = r.Request.DueDate, AppliedOn = r.Request.AppliedOn,
			Priority = (int)r.Request.Priority,
			DisplayStatus = r.PendingCount > 0 ? 0 : 1,
			StudentCount = r.StudentCount, ProcessedCount = r.ProcessedCount,
			RequestorId = r.Requestor.Id,
			RequestorName = r.Requestor.Name ?? r.Requestor.UserName ?? string.Empty,
			RequestedOn = r.Request.AppliedOn, AdditionalNotes = r.Request.AdditionalNotes
		};

	private static ChargeLineListItem MapToLineListItem(ChargeLineJoinResult line, ChargeCategory? category)
	{
		var student = line.Student;
		return new ChargeLineListItem
		{
			ChargeId = line.Charge.ChargeRequestId, ChargeLineId = line.Charge.Id,
			ChargeName = line.Charge.ChargeName, StudentId = student.Id,
			StudentName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
			GrNumber = student.GrNo?.ToString() ?? string.Empty,
			RollNumber = student.RollNo?.ToString() ?? string.Empty,
			StandardName = line.Standard.Name, DivisionName = line.Division.Name,
			ChargeCategoryId = line.Charge.ChargeCategoryId, CategoryName = category?.Name ?? string.Empty,
			Amount = line.Charge.Amount, PurposeDescription = line.Charge.PurposeDescription,
			DueDate = line.Charge.DueDate, Status = (int)line.Charge.Status
		};
	}

	private static ChargeStudentLineDto MapToStudentLine(ChargeLineJoinResult line)
	{
		var student = line.Student;
		return new ChargeStudentLineDto
		{
			StudentId = student.Id,
			StudentName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
			GrNumber = student.GrNo?.ToString() ?? string.Empty,
			RollNumber = student.RollNo,
			StandardName = line.Standard.Name, DivisionName = line.Division.Name,
			Status = (int)line.Charge.Status
		};
	}

	private static ChargeStatus DeriveRequestStatus(IReadOnlyList<ChargeStatus> lineStatuses)
	{
		if (lineStatuses.Count == 0 || lineStatuses.Any(s => s == ChargeStatus.Pending))
			return ChargeStatus.Pending;
		return lineStatuses.All(s => s == ChargeStatus.Rejected) ? ChargeStatus.Rejected : ChargeStatus.Approved;
	}

	private async Task<ChargeRequest> GetChargeRequestOrThrowAsync(Guid chargeRequestId) =>
		await chargeRequestRepository.FindAsync(chargeRequestId)
			?? throw new BusinessException(FinanceChargeErrorCodes.ChargeRequestNotFound)
				.WithData("ChargeId", chargeRequestId);

	private async Task<ChargeRequestDetailResult> GetChargeRequestDetailsOrThrowAsync(Guid chargeRequestId) =>
		await chargeRequestRepository.GetWithLinesAsync(chargeRequestId)
			?? throw new BusinessException(FinanceChargeErrorCodes.ChargeRequestNotFound)
				.WithData("ChargeId", chargeRequestId);

	private async Task EnsureAcademicYearExistsAsync(Guid academicYearId)
	{
		if (await academicYearRepository.FindAsync(academicYearId) is null)
			throw new BusinessException(FinanceChargeErrorCodes.AcademicYearNotFound)
				.WithData("AcademicYearId", academicYearId);
	}

	private async Task EnsureAllLinesPendingOrThrowAsync(Guid chargeRequestId, bool forDelete)
	{
		var lines = await chargeRepository.GetListAsync(c => c.ChargeRequestId == chargeRequestId);
		if (lines.Count == 0) return;
		if (forDelete && lines.Any(l => l.Status != ChargeStatus.Pending))
			throw new BusinessException(FinanceChargeErrorCodes.ChargeRequestNotFullyPending);
	}

	private bool IsPrivilegedUser() =>
		CurrentUser.IsInRole("admin") || CurrentUser.IsInRole("Finance Officer");
}
