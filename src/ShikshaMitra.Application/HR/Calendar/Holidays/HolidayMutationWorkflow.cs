using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Application.HR.Calendar.Holidays;

[RemoteService(false)]
public class HolidayMutationWorkflow(
	IHolidayRepository holidayRepository,
	IHolidayTypeRepository holidayTypeRepository,
	HolidayReadService holidayReadService,
	IRepository<AcademicYear, Guid> academicYearRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<HolidayDto> CreateAsync(CreateUpdateHolidayDto input)
	{
		await EnsureHolidayTypeExistsAsync(input.HolidayTypeId);
		await EnsureHolidayInsideAcademicYearAsync(
			input.AcademicYearId,
			input.HolidayName,
			input.StartDate,
			input.EndDate);

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
		await EnsureHolidayTypeExistsAsync(input.HolidayTypeId);
		if (input.AcademicYearId != Guid.Empty && input.AcademicYearId != holiday.AcademicYearId)
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarHolidayAcademicYearMismatch)
				.WithData("field", nameof(input.AcademicYearId))
				.WithData("date", input.StartDate.Date.ToString("yyyy-MM-dd"));
		}

		await EnsureHolidayInsideAcademicYearAsync(
			holiday.AcademicYearId,
			input.HolidayName,
			input.StartDate,
			input.EndDate);

		holiday.Update(
			input.HolidayName,
			input.HolidayTypeId,
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

	private async Task EnsureHolidayInsideAcademicYearAsync(
		Guid academicYearId,
		string holidayName,
		DateTimeOffset startDate,
		DateTimeOffset endDate)
	{
		if (academicYearId == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.AcademicYearRequired)
				.WithData("field", nameof(academicYearId));
		}

		var fromDate = startDate.Date;
		var toDate = endDate.Date;
		if (toDate < fromDate)
		{
			throw new BusinessException(HRErrorCodes.EventDateRangeInvalid)
				.WithData("field", nameof(CreateUpdateHolidayDto.EndDate))
				.WithData("fromDate", fromDate.ToString("yyyy-MM-dd"))
				.WithData("toDate", toDate.ToString("yyyy-MM-dd"));
		}

		var academicYear = await academicYearRepository.GetAsync(academicYearId);
		if (fromDate < academicYear.StartDate.Date || toDate > academicYear.EndDate.Date)
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarHolidayOutsideAcademicYear)
				.WithData("field", nameof(CreateUpdateHolidayDto.StartDate))
				.WithData("holidayName", holidayName)
				.WithData("fromDate", fromDate.ToString("yyyy-MM-dd"))
				.WithData("toDate", toDate.ToString("yyyy-MM-dd"))
				.WithData("academicYear", academicYear.Name)
				.WithData("academicYearStart", AcademicYearBusinessDate.Format(academicYear.StartDate))
				.WithData("academicYearEnd", AcademicYearBusinessDate.Format(academicYear.EndDate));
		}
	}

	private DateTimeOffset CurrentInstant() =>
		new(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
}
