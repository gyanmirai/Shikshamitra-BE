using System;
using System.Threading.Tasks;
using ShikshaMitra.Authors;
using ShikshaMitra.Books;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra;

public class ShikshaMitraSeederContributor(
    IRepository<Book, Guid> bookRepository,
    IAuthorRepository authorRepository,
    AuthorManager authorManager) : IDataSeedContributor, ITransientDependency
{
    public async Task SeedAsync(DataSeedContext context)
    {
        if (await bookRepository.GetCountAsync() > 0)
        {
            return;
        }

        var orwell = await authorRepository.InsertAsync(
            await authorManager.CreateAsync(
                "George Orwell",
                new DateTime(1903, 06, 25),
                "Orwell produced literary criticism and poetry, fiction and polemical journalism; and is best known for the allegorical novella Animal Farm (1945) and the dystopian novel Nineteen Eighty-Four (1949)."
            )
        );

        var douglas = await authorRepository.InsertAsync(
            await authorManager.CreateAsync(
                "Douglas Adams",
                new DateTime(1952, 03, 11),
                "Douglas Adams was an English author, screenwriter, essayist, humorist, satirist and dramatist. Adams was an advocate for environmentalism and conservation, a lover of fast cars, technological innovation and the Apple Macintosh, and a self-proclaimed 'radical atheist'."
            )
        );

        await bookRepository.InsertAsync(
            new Book
            {
                AuthorId = orwell.Id, // SET THE AUTHOR
                Name = "1984",
                Type = BookType.Dystopia,
                PublishDate = new DateTime(1949, 6, 8),
                Price = 19.84f
            },
            autoSave: true
        );

        await bookRepository.InsertAsync(
            new Book
            {
                AuthorId = douglas.Id, // SET THE AUTHOR
                Name = "The Hitchhiker's Guide to the Galaxy",
                Type = BookType.ScienceFiction,
                PublishDate = new DateTime(1995, 9, 27),
                Price = 42.0f
            },
            autoSave: true
        );
    }
}