using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Authors;

public class AuthorManager : DomainService
{
	private readonly IAuthorRepository _authorRepository;

	public AuthorManager(IAuthorRepository authorRepository)
	{
		_authorRepository = authorRepository;
	}


	public async Task<Author> CreateAsync(
		string name,
		DateTime birthDate)
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));

		Author? existingAuthor = await _authorRepository.FindByNameAsync(name);
		if (existingAuthor != null)
		{
			throw new AuthorAlreadyExistsException(name);
		}

		return new Author(
			GuidGenerator.Create(),
			name,
			birthDate
		);
	}

	public async Task ChangeNameAsync(
		Author author,
		string newName)
	{
		Check.NotNull(author, nameof(author));
		Check.NotNullOrWhiteSpace(newName, nameof(newName));

		Author? existingAuthor = await _authorRepository.FindByNameAsync(newName);
		if (existingAuthor != null && existingAuthor.Id != author.Id)
		{
			throw new AuthorAlreadyExistsException(newName);
		}

		author.ChangeName(newName);
	}
}