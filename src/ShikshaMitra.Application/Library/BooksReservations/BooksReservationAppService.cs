using ShikshaMitra.Library.BooksAssignments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Library.BooksReservations;

public class BooksReservationAppService(
	IRepository<BooksReservation, Guid> booksReservationRepository,
	IRepository<BooksAssignment, Guid> booksAssignmentRepository,
	IGuidGenerator guidGenerator
) : ApplicationService, IBooksReservationAppService
{
	public async Task<BooksReservationDto> CreateAsync(CreateUpdateBooksReservationDto createBooksReservationsDto)
	{
		var reservation = new BooksReservation(
			guidGenerator.Create(),
			CurrentTenant.Id,
			createBooksReservationsDto.BooksAssignmentId,
			createBooksReservationsDto.RequestedDate,
			createBooksReservationsDto.FulfilledDate,
			createBooksReservationsDto.Status
		);

		await booksReservationRepository.InsertAsync(reservation, autoSave: true);
		return ObjectMapper.Map<BooksReservation, BooksReservationDto>(reservation);
	}

	public async Task<ListResultDto<BooksReservationDto>> GetAllAsync()
	{
		var reservations = await booksReservationRepository.GetListAsync();
		var dtoList = ObjectMapper.Map<List<BooksReservation>, List<BooksReservationDto>>(reservations);
		return new ListResultDto<BooksReservationDto>(dtoList);
	}

	public async Task<BooksReservationDto> GetByIdAsync(Guid id)
	{
		var reservation = await booksReservationRepository.GetAsync(id);
		return ObjectMapper.Map<BooksReservation, BooksReservationDto>(reservation);
	}
}
