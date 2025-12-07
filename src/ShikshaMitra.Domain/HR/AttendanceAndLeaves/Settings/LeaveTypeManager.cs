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
		string type,
		string color)
	{
		// Check if type already exists
		await CheckTypeAvailabilityAsync(type);

		var leaveType = new LeaveType(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			type,
			color
		);

		return leaveType;
	}

	public async Task UpdateAsync(
		LeaveType leaveType,
		string type,
		string color)
	{
		// Check if type already exists (excluding current leave type)
		if (leaveType.Type != type)
		{
			await CheckTypeAvailabilityAsync(type, leaveType.Id);
		}

		leaveType.Update(type, color);
	}

	private async Task CheckTypeAvailabilityAsync(string type, Guid? excludeId = null)
	{
		if (await _leaveTypeRepository.IsTypeExistsAsync(type, excludeId))
		{
			throw new BusinessException("ShikshaMitra:LeaveTypeAlreadyExists")
				.WithData("type", type);
		}
	}
}
