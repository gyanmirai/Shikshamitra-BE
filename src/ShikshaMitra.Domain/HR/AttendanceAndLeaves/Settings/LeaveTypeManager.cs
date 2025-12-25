using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Domain service for managing employee leave types with business logic
/// </summary>
public class LeaveTypeManager : DomainService
{
	private readonly ILeaveTypeRepository _leaveTypeRepository;

	public LeaveTypeManager(ILeaveTypeRepository leaveTypeRepository)
	{
		_leaveTypeRepository = leaveTypeRepository;
	}

	public async Task<LeaveType> CreateAsync(
		Guid educationMediumStageId,
		string type,
		string color,
		string? description = null)
	{
		// Check if type already exists within the same organization
		await CheckTypeAvailabilityAsync(educationMediumStageId, type);

		var leaveType = new LeaveType(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			educationMediumStageId,
			type,
			color,
			description
		);

		return leaveType;
	}

	public async Task UpdateAsync(
		LeaveType leaveType,
		string type,
		string color,
		string? description = null)
	{
		// Check if type already exists (excluding current leave type) within the same organization
		if (leaveType.Type != type)
		{
			await CheckTypeAvailabilityAsync(leaveType.EducationMediumStageId, type, leaveType.Id);
		}

		leaveType.Update(type, color, description);
	}

	private async Task CheckTypeAvailabilityAsync(Guid educationMediumStageId, string type, Guid? excludeId = null)
	{
		if (await _leaveTypeRepository.IsTypeExistsAsync(educationMediumStageId, type, excludeId))
		{
			throw new BusinessException("ShikshaMitra:LeaveTypeAlreadyExists")
				.WithData("type", type);
		}
	}
}
