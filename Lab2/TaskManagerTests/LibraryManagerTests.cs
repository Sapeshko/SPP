using LibraryManagement;
using lab1_test_framework;

namespace LibraryManagerTests;

[TestAttributes.TestSuite(Description = "Тесты менеджера библиотеки")]
public class LibraryManagerTestSuite
{
    private LibraryManager _manager = null!;

    [TestAttributes.Setup]
    public void Init()
    {
        _manager = new LibraryManager();
    }

    [TestAttributes.TestMethod(DisplayName = "Добавление книги")]
    public void AddBook_ShouldWork()
    {
        var book = _manager.AddBook("1984", "George Orwell", 1949);

        InTest.IsNotNull(book);
        InTest.AreEqual("1984", book.Title);
    }

    [TestAttributes.TestMethod(DisplayName = "Выдача книги")]
    public void BorrowBook_ShouldChangeStatus()
    {
        var book = _manager.AddBook("Test", "Author", 2000);

        bool result = _manager.BorrowBook(book.Id);

        InTest.IsTrue(result);
        InTest.IsTrue(book.IsBorrowed);
    }

    [TestAttributes.TestMethod(DisplayName = "Тест с таймаутом - долгий поиск")]
    [TestAttributes.Timeout(100)] // 100 мс лимит
    public async Task SlowSearch_ShouldTimeout()
    {
        // Искусственная задержка для демонстрации таймаута
        await Task.Delay(500);
        var results = await _manager.SearchBooksAsync("test");
        InTest.IsNotNull(results);
    }

    [TestAttributes.TestMethod(DisplayName = "Возврат книги")]
    public void ReturnBook_ShouldUpdateStatus()
    {
        var book = _manager.AddBook("Clean Code", "Robert Martin", 2008);
        _manager.BorrowBook(book.Id);

        bool returned = _manager.ReturnBook(book.Id);

        InTest.IsTrue(returned);
        InTest.IsFalse(book.IsBorrowed);
        InTest.IsNotNull(book.ReturnedAt);
    }

    [TestAttributes.TestMethod(DisplayName = "Двойная выдача одной книги")]
    public void BorrowBook_Twice_ShouldFailSecondTime()
    {
        var book = _manager.AddBook("Refactoring", "Martin Fowler", 1999);
        bool firstBorrow = _manager.BorrowBook(book.Id);
        bool secondBorrow = _manager.BorrowBook(book.Id);

        InTest.IsTrue(firstBorrow);
        InTest.IsFalse(secondBorrow);
    }

    [TestAttributes.TestMethod(DisplayName = "Демонстрация производительности - множество операций")]
    public async Task PerformanceTest_MultipleOperations()
    {
        // Добавляем много книг
        for (int i = 0; i < 100; i++)
        {
            _manager.AddBook($"Book {i}", $"Author {i}", 2000 + (i % 24));
        }

        // Выполняем поиски
        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(_manager.SearchBooksAsync("Book"));
        }

        await Task.WhenAll(tasks);

        var stats = _manager.GetStatistics();
        InTest.AreEqual(100, stats.TotalBooks);
    }

    [TestAttributes.TestMethod(DisplayName = "Асинхронный поиск по заголовку")]
    public async Task SearchBooksAsync_ShouldReturnResults()
    {
        _manager.AddBook("Harry Potter", "Rowling", 1997);

        var results = await _manager.SearchBooksAsync("Harry");

        InTest.CollectionCount(results, 1);
    }

    [TestAttributes.TestMethod(DisplayName = "Асинхронный поиск по автору")]
    public async Task SearchBooksAsync_ByAuthor_ShouldReturnResults()
    {
        _manager.AddBook("The Hobbit", "J.R.R. Tolkien", 1937);

        var results = await _manager.SearchBooksAsync("Tolkien");

        InTest.CollectionCount(results, 1);
    }

    [TestAttributes.TestMethod(DisplayName = "Проверка статистики")]
    public void Statistics_ShouldBeCorrect()
    {
        var b1 = _manager.AddBook("A", "Author", 2000);
        _manager.BorrowBook(b1.Id);

        var stats = _manager.GetStatistics();

        InTest.AreEqual(1, stats.TotalBooks);
        InTest.AreEqual(1, stats.BorrowedBooks);
        InTest.AreEqual(0, stats.AvailableBooks);
    }

    [TestAttributes.TestMethod(DisplayName = "Добавление книги с некорректным годом должно падать")]
    public void AddBook_InvalidYear_ShouldThrow()
    {
        InTest.Throws<LibraryValidationException>(() =>
            _manager.AddBook("Future Book", "Author", DateTime.Now.Year + 1));
    }

    [TestAttributes.TestMethod(DisplayName = "Книга становится доступной после возврата")]
    public void BorrowThenReturn_ShouldMakeBookAvailable()
    {
        var book = _manager.AddBook("Domain-Driven Design", "Eric Evans", 2003);
        _manager.BorrowBook(book.Id);

        _manager.ReturnBook(book.Id);

        var stats = _manager.GetStatistics();
        InTest.AreEqual(0, stats.BorrowedBooks);
        InTest.AreEqual(1, stats.AvailableBooks);
    }
}