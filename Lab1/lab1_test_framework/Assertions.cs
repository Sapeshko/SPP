namespace lab1_test_framework;

public class Assertions
{
    public static void AreEqual<T>(T actual, T expected, string message = "")
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            throw new TestAssertException(
                string.IsNullOrEmpty(message)
                    ? $"Ожидалось: {expected}, получено: {actual}"
                    : message);
        }
    }

    public static void AreNotEqual<T>(T actual, T expected, string message = "")
    {
        if (EqualityComparer<T>.Default.Equals(actual, expected))
        {
            throw new TestAssertException(
                string.IsNullOrEmpty(message)
                    ? $"Значения не должны быть равны: {actual}"
                    : message);
        }
    }

    public static void IsTrue(bool condition, string message = "")
    {
        if (!condition)
        {
            throw new TestAssertException(
                string.IsNullOrEmpty(message) ? "Условие должно быть true" : message);
        }
    }

    public static void IsFalse(bool condition, string message = "")
    {
        if (condition)
        {
            throw new TestAssertException(
                string.IsNullOrEmpty(message) ? "Условие должно быть false" : message);
        }
    }

    public static void IsNull(object obj, string message = "")
    {
        if (obj != null)
        {
            throw new TestAssertException(
                string.IsNullOrEmpty(message) ? "Объект должен быть null" : message);
        }
    }

    public static void IsNotNull(object obj, string message = "")
    {
        if (obj == null)
        {
            throw new TestAssertException(
                string.IsNullOrEmpty(message) ? "Объект не должен быть null" : message);
        }
    }

    public static void Throws<T>(Action action, string message = "") where T : Exception
    {
        try
        {
            action();
            throw new TestAssertException(
                string.IsNullOrEmpty(message)
                    ? $"Ожидалось исключение {typeof(T).Name}"
                    : message);
        }
        catch (T)
        {
            // Ожидаемое исключение
        }
        catch (Exception ex)
        {
            throw new TestAssertException(
                $"Ожидалось {typeof(T).Name}, получено {ex.GetType().Name}");
        }
    }

    public static async Task ThrowsAsync<T>(Func<Task> action, string message = "") where T : Exception
    {
        try
        {
            await action();
            throw new TestAssertException(
                string.IsNullOrEmpty(message)
                    ? $"Ожидалось исключение {typeof(T).Name}"
                    : message);
        }
        catch (T)
        {
            // Ожидаемое исключение
        }
        catch (Exception ex)
        {
            throw new TestAssertException(
                $"Ожидалось {typeof(T).Name}, получено {ex.GetType().Name}");
        }
    }

    public static void IsGreaterThan(double actual, double expected, string message = "")
    {
        if (actual <= expected)
        {
            throw new TestAssertException(
                string.IsNullOrEmpty(message)
                    ? $"Ожидалось > {expected}, получено {actual}"
                    : message);
        }
    }

    public static void IsLessThan(int actual, int expected, string message = "")
    {
        if (actual >= expected)
        {
            throw new TestAssertException(
                string.IsNullOrEmpty(message)
                    ? $"Ожидалось < {expected}, получено {actual}"
                    : message);
        }
    }

    public static void Contains(string actual, string expected, string message = "")
    {
        if (!actual.Contains(expected))
        {
            throw new TestAssertException(
                string.IsNullOrEmpty(message)
                    ? $"Строка '{actual}' не содержит '{expected}'"
                    : message);
        }
    }

    public static void CollectionCount<T>(IEnumerable<T> collection, int expected, string message = "")
    {
        var count = collection.Count();
        if (count != expected)
        {
            throw new TestAssertException(
                string.IsNullOrEmpty(message)
                    ? $"В коллекции {count} элементов, ожидалось {expected}"
                    : message);
        }
    }
}

public class TestAssertException : Exception
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    public TestAssertException(string message) : base(message)
    {
    }
}