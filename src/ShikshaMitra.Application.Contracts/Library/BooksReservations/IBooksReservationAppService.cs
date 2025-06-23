using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Library.BooksReservations;

public interface IBooksReservationAppService
{
	Task<BooksReservationDto> CreateAsync(CreateUpdateBooksReservationDto input);
	Task<ListResultDto<BooksReservationDto>> GetAllAsync();
	Task<BooksReservationDto> GetByIdAsync(Guid id);
}
