namespace LibraryManagement;

public class LibraryManager
{
    private readonly Dictionary<int, Book> _books = new();
    private readonly object _lock = new();
    private int _nextId = 1;

    public int TotalBorrowedBooks { get; private set; }

    public Book AddBook(string title, string author, int year)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new LibraryValidationException("Название книги пустое");

        if (string.IsNullOrWhiteSpace(author))
            throw new LibraryValidationException("Автор не указан");

        if (year < 1500 || year > DateTime.Now.Year)
            throw new LibraryValidationException("Некорректный год");

        lock (_lock)
        {
            var book = new Book
            {
                Id = _nextId++,
                Title = title,
                Author = author,
                Year = year
            };

            _books[book.Id] = book;
            return book;
        }
    }

    public bool BorrowBook(int id)
    {
        lock (_lock)
        {
            if (!_books.TryGetValue(id, out var book))
                return false;

            if (book.IsBorrowed)
                return false;

            book.IsBorrowed = true;
            book.BorrowedAt = DateTime.UtcNow;
            TotalBorrowedBooks++;
            return true;
        }
    }

    public bool ReturnBook(int id)
    {
        lock (_lock)
        {
            if (!_books.TryGetValue(id, out var book))
                return false;

            if (!book.IsBorrowed)
                return false;

            book.IsBorrowed = false;
            book.ReturnedAt = DateTime.UtcNow;
            return true;
        }
    }

    public async Task<List<Book>> SearchBooksAsync(string term)
    {
        await Task.Delay(50);

        if (string.IsNullOrWhiteSpace(term))
            return new List<Book>();

        return _books.Values
            .Where(b => b.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                     || b.Author.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public LibraryStatistics GetStatistics()
    {
        return new LibraryStatistics
        {
            TotalBooks = _books.Count,
            BorrowedBooks = _books.Values.Count(b => b.IsBorrowed),
            AvailableBooks = _books.Values.Count(b => !b.IsBorrowed)
        };
    }
}

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int Year { get; set; }
    public bool IsBorrowed { get; set; }
    public DateTime? BorrowedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
}

public class LibraryStatistics
{
    public int TotalBooks { get; set; }
    public int BorrowedBooks { get; set; }
    public int AvailableBooks { get; set; }
}

public class LibraryValidationException : Exception
{
    public LibraryValidationException(string message) : base(message) { }
}