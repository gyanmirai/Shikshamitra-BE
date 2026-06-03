using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Application.HR.Calendar.Holidays;

[RemoteService(false)]
public class HolidayMutationWorkflow(
	IHolidayRepository holidayRepository,
	IHolidayTypeRepository holidayTypeRepository,
	HolidayReadService holidayReadService)
	: ApplicationService, ITransientDependency
{
	public async Task<HolidayDto> CreateAsync(CreateUpdateHolidayDto input)
	{
		await EnsureHolidayTypeExistsAsync(input.HolidayTypeId);

		var holiday = Holiday.Create(
			CurrentTenant.Id,
			input.AcademicYearId,
			input.HolidayName,
			input.HolidayTypeId,
			input.StartDate,
			input.EndDate,
			input.Description,
			input.AudienceIds,
			input.Attachments
		);

		await holidayRepository.InsertAsync(holiday);
		var holidayDto = ObjectMapper.Map<Holiday, HolidayDto>(holiday);
		holidayDto.Status = (int)holiday.GetStatus(CurrentInstant());
		await holidayReadService.PopulateHolidayTypeAsync(holidayDto);
		return holidayDto;
	}

	public async Task<HolidayDto> UpdateAsync(Guid id, CreateUpdateHolidayDto input)
	{
		var holiday = await holidayRepository.GetAsync(id);

		holiday.Update(
			input.HolidayName,
			input.StartDate,
			input.EndDate,
			input.Description,
			input.AudienceIds,
			input.Attachments
		);

		await holidayRepository.UpdateAsync(holiday);
		var holidayDto = ObjectMapper.Map<Holiday, HolidayDto>(holiday);
		holidayDto.Status = (int)holiday.GetStatus(CurrentInstant());
		await holidayReadService.PopulateHolidayTypeAsync(holidayDto);
		return holidayDto;
	}

	private async Task EnsureHolidayTypeExistsAsync(Guid holidayTypeId)
	{
		var query = await holidayTypeRepository.GetQueryableAsync();
		var holidayTypeExists = await AsyncExecuter.AnyAsync(query.Where(x => x.Id == holidayTypeId));
		if (!holidayTypeExists)
		{
			throw new BusinessException(HRErrorCodes.HolidayTypeNotFound);
		}
	}

	private DateTimeOffset CurrentInstant() =>
		new(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
}
