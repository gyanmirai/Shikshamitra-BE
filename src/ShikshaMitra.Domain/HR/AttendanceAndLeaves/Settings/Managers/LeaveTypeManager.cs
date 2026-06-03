using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeaveTypeManager : DomainService
{
	private readonly ILeaveTypeRepository _leaveTypeRepository;

	public LeaveTypeManager(ILeaveTypeRepository leaveTypeRepository)
	{
		_leaveTypeRepository = leaveTypeRepository;
	}

	public async Task<LeaveType> CreateAsync(
		Guid academicYearId,
		string type,
		string color,
		string? description = null)
	{
		await CheckTypeAvailabilityAsync(academicYearId, type);

		return new LeaveType(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			type,
			color,
			description
		);
	}

	public async Task UpdateAsync(
		LeaveType leaveType,
		string type,
		string color,
		string? description = null)
	{
		if (!string.Equals(leaveType.Type, type, StringComparison.OrdinalIgnoreCase))
		{
			await CheckTypeAvailabilityAsync(leaveType.AcademicYearId, type, leaveType.Id);
		}

		leaveType.Update(type, color, description);
	}

	private async Task CheckTypeAvailabilityAsync(Guid academicYearId, string type, Guid? excludeId = null)
	{
		if (await _leaveTypeRepository.IsTypeExistsAsync(academicYearId, type, excludeId))
		{
			throw new BusinessException(HRErrorCodes.LeaveTypeAlreadyExists)
				.WithData("type", type);
		}
	}
}
