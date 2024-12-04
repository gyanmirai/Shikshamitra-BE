using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Authors;

public class AuthorManager(IAuthorRepository authorRepository) : DomainService
{
	public async Task<Author> CreateAsync(
		string name,
		DateTime birthDate)
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));

		Author? existingAuthor = await authorRepository.FindByNameAsync(name);
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

		Author? existingAuthor = await authorRepository.FindByNameAsync(newName);
		if (existingAuthor != null && existingAuthor.Id != author.Id)
		{
			throw new AuthorAlreadyExistsException(newName);
		}

		author.ChangeName(newName);
	}
}