using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmExamSlotManager(
	IRepository<AdmissionCrmExamSlot, Guid> slotRepository,
	IRepository<AcademicYear, Guid> academicYearRepository) : DomainService
{
	public async Task<AdmissionCrmExamSlot> CreateAsync(
		Guid academicYearId,
		Guid? playbookId,
		string? slotTemplateCode,
		string? examDefinitionCode,
		string slotName,
		string? campus,
		string? roomOrHall,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		int studentLimit,
		int roomNo)
	{
		if (!await academicYearRepository.AnyAsync(x => x.Id == academicYearId))
			throw new BusinessException("AdmissionCrm:AcademicYearNotFound");
		await EnsureNoOverlapAsync(academicYearId, roomNo, startDate, endDate, null);
		return new AdmissionCrmExamSlot(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			playbookId, slotTemplateCode, examDefinitionCode, slotName, campus, roomOrHall,
			startDate, endDate, studentLimit, roomNo);
	}

	public async Task UpdateAsync(AdmissionCrmExamSlot slot, string slotName, string? campus,
		string? roomOrHall, DateTimeOffset startDate, DateTimeOffset endDate, int studentLimit, int roomNo)
	{
		await EnsureNoOverlapAsync(slot.AcademicYearId, roomNo, startDate, endDate, slot.Id);
		slot.Update(slotName, campus, roomOrHall, startDate, endDate, studentLimit, roomNo);
	}

	private async Task EnsureNoOverlapAsync(Guid academicYearId, int roomNo, DateTimeOffset start,
		DateTimeOffset end, Guid? excludingId)
	{
		var slots = await slotRepository.GetListAsync(x => x.AcademicYearId == academicYearId &&
			x.RoomNo == roomNo && x.IsActive);
		if (slots.Any(x => (!excludingId.HasValue || x.Id != excludingId.Value) && x.StartDate < end && start < x.EndDate))
			throw new BusinessException("AdmissionCrm:ExamSlotOverlap");
	}
}
