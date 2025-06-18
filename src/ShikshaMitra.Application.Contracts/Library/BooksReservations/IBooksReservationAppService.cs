using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Library.BooksReservations;

public interface IBooksReservationAppService
{
	Task<BooksReservationDto> CreateAsync(CreateUpdateBooksReservationDto input);
	Task<ListResultDto<BooksReservationDto>> GetAllAsync();
	Task<BooksReservationDto> GetByIdAsync(Guid id);
}
