using LibraryManagement;
using lab1_test_framework;

namespace LibraryManagerTests;

public class SharedContextLibraryTests
{
    private LibraryManager _manager = null!;

    [TestAttributes.Setup]
    public void Setup()
    {
        _manager = new LibraryManager();
    }

    [TestAttributes.SharedContext(ContextId = "BORROW_FLOW", Order = 1)]
    [TestAttributes.TestMethod(DisplayName = "Шаг 1: Добавляем книгу")]
    public void Step1_AddBook()
    {
        var book = _manager.AddBook("Clean Code", "Robert Martin", 2008);
        Assertions.IsNotNull(book);
    }

    [TestAttributes.SharedContext(ContextId = "BORROW_FLOW", Order = 2)]
    [TestAttributes.TestMethod(DisplayName = "Шаг 2: Берем книгу")]
    public void Step2_BorrowBook()
    {
        var book = _manager.SearchBooksAsync("Clean").Result.First();
        bool result = _manager.BorrowBook(book.Id);

        Assertions.IsTrue(result);
    }

    [TestAttributes.SharedContext(ContextId = "BORROW_FLOW", Order = 3)]
    [TestAttributes.TestMethod(DisplayName = "Шаг 3: Проверяем статус")]
    public void Step3_Verify()
    {
        var stats = _manager.GetStatistics();
        Assertions.AreEqual(1, stats.BorrowedBooks);
    }
}