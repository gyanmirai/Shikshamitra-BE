using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Transport;

[Authorize(ShikshaMitraSubModulePermissions.StopManagement)]
[RemoteService(false)]
public class TransportStopAppService(
	IRepository<TransportStop, Guid> stopRepository,
	IRepository<EducationMedium, Guid> academicMediumRepository,
	StudentScopeAccessService studentScopeAccessService
) : ApplicationService, ITransportStopAppService
{
	public async Task<PagedResultDto<TransportStopDto>> GetListAsync(TransportStopListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetListAsync));

		var query = await stopRepository.GetQueryableAsync();
		query = query.Where(stop => stop.AcademicMediumId == input.AcademicMediumId);

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			query = query.Where(stop =>
				stop.StopName.ToLower().Contains(search) ||
				stop.Landmark.ToLower().Contains(search) ||
				(stop.ZoneId != null && stop.ZoneId.ToLower().Contains(search)));
		}

		if (!string.IsNullOrWhiteSpace(input.ZoneId))
		{
			var zoneId = input.ZoneId.Trim();
			query = query.Where(stop => stop.ZoneId == zoneId);
		}

		if (input.Status.HasValue)
		{
			query = query.Where(stop => stop.Status == input.Status.Value);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderBy(stop => stop.StopName)
				.Skip(input.SkipCount)
				.Take(input.NormalizedPageSize));

		return new PagedResultDto<TransportStopDto>(
			totalCount,
			items.Select(MapStop).ToList());
	}

	public async Task<TransportStopDto> CreateAsync(Guid academicMediumId, CreateUpdateTransportStopDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(CreateAsync));
		ValidateStopInput(input);
		await EnsureStopNameIsUniqueAsync(academicMediumId, input.StopName);

		var stop = new TransportStop(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicMediumId,
			input.StopName,
			input.Landmark,
			NormalizeZone(input),
			input.Amount,
			input.ApproxKmFromSchool,
			input.PinCode,
			input.AddressLine1,
			input.AddressLine2,
			input.Description);

		await stopRepository.InsertAsync(stop, autoSave: true);
		return MapStop(stop);
	}

	public async Task<TransportStopDto> UpdateAsync(Guid stopId, Guid academicMediumId, CreateUpdateTransportStopDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(UpdateAsync));
		ValidateStopInput(input);

		var stop = await GetStopForMediumAsync(stopId, academicMediumId);
		await EnsureStopNameIsUniqueAsync(academicMediumId, input.StopName, stopId);

		stop.UpdateDetails(
			input.StopName,
			input.Landmark,
			NormalizeZone(input),
			input.Amount,
			input.ApproxKmFromSchool,
			input.PinCode,
			input.AddressLine1,
			input.AddressLine2,
			input.Description);

		await stopRepository.UpdateAsync(stop, autoSave: true);
		return MapStop(stop);
	}

	public async Task<TransportStopDto> ChangeStatusAsync(Guid stopId, Guid academicMediumId, ChangeTransportStatusDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(ChangeStatusAsync));
		var stop = await GetStopForMediumAsync(stopId, academicMediumId);
		stop.ChangeStatus(input.Status);
		await stopRepository.UpdateAsync(stop, autoSave: true);
		return MapStop(stop);
	}

	private async Task<TransportStop> GetStopForMediumAsync(Guid stopId, Guid academicMediumId)
	{
		var stop = await stopRepository.FindAsync(stopId);
		if (stop == null || stop.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportStop), stopId);
		}

		return stop;
	}

	private async Task EnsureCanUseMediumAsync(Guid academicMediumId, string operation)
	{
		await academicMediumRepository.GetAsync(academicMediumId);
		await studentScopeAccessService.EnsureCanAccessMediumAsync(academicMediumId, operation);
	}

	private async Task EnsureStopNameIsUniqueAsync(Guid academicMediumId, string stopName, Guid? excludingStopId = null)
	{
		var normalized = NormalizeName(stopName);
		var stops = await stopRepository.GetListAsync(stop =>
			stop.AcademicMediumId == academicMediumId &&
			(!excludingStopId.HasValue || stop.Id != excludingStopId.Value));

		if (stops.Any(stop => NormalizeName(stop.StopName) == normalized))
		{
			throw new BusinessException(TransportErrorCodes.StopAlreadyExists)
				.WithData("StopName", stopName)
				.WithData("AcademicMediumId", academicMediumId);
		}
	}

	private static void ValidateStopInput(CreateUpdateTransportStopDto input)
	{
		if (string.IsNullOrWhiteSpace(input.StopName))
		{
			throw new UserFriendlyException("Stop name is required.");
		}

		if (string.IsNullOrWhiteSpace(input.Landmark))
		{
			throw new UserFriendlyException("Landmark is required.");
		}

		if (input.Amount < 0)
		{
			throw new UserFriendlyException("Amount cannot be negative.");
		}

		if (input.ApproxKmFromSchool < 0)
		{
			throw new UserFriendlyException("Approx km from school cannot be negative.");
		}
	}

	private static TransportStopDto MapStop(TransportStop stop)
	{
		return new TransportStopDto
		{
			Id = stop.Id,
			AcademicMediumId = stop.AcademicMediumId,
			StopName = stop.StopName,
			Landmark = stop.Landmark,
			ZoneId = stop.ZoneId,
			Zone = stop.ZoneId,
			AddressLine1 = stop.AddressLine1,
			AddressLine2 = stop.AddressLine2,
			PinCode = stop.PinCode,
			Amount = stop.Amount,
			ApproxKmFromSchool = stop.ApproxKmFromSchool,
			Description = stop.Description,
			Status = stop.Status,
			AssignedStudents = 0,
			CreationTime = stop.CreationTime,
			CreatorId = stop.CreatorId,
			LastModificationTime = stop.LastModificationTime,
			LastModifierId = stop.LastModifierId
		};
	}

	private static string NormalizeName(string value)
	{
		return string.Join(" ", value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
			.ToUpperInvariant();
	}

	private static string? NormalizeZone(CreateUpdateTransportStopDto input)
	{
		return string.IsNullOrWhiteSpace(input.ZoneId)
			? input.Zone
			: input.ZoneId;
	}
}
