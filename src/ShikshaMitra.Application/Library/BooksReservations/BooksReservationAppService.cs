using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Library.BooksReservations;

public class BooksReservationAppService(
	IRepository<BooksReservation, Guid> booksReservationRepository,
	IRepository<Book, Guid> bookRepository,
	IRepository<Member, Guid> memberRepository,
	IGuidGenerator guidGenerator
) : ApplicationService, IBooksReservationAppService
{
	public async Task<BooksReservationDto> CreateAsync(CreateUpdateBooksReservationDto createDto)
	{
		var bookExists = await bookRepository.AnyAsync(b => b.Id == createDto.BookId);
		if (!bookExists)
		{
			throw new BusinessException("Invalid BookId. The specified book does not exist.");
		}

		var memberExists = await memberRepository.AnyAsync(m => m.Id == createDto.MemberId);
		if (!memberExists)
		{
			throw new BusinessException("Invalid MemberId. The specified member does not exist.");
		}

		var reservation = new BooksReservation(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			bookId: createDto.BookId,
			memberId: createDto.MemberId,
			requestedDate: createDto.RequestedDate,
			fulfilledDate: createDto.FulfilledDate,
			status: createDto.Status
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
