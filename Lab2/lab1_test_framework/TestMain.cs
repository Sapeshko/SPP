using System.Reflection;
using System.Text;
using System.Collections.Concurrent;

namespace lab1_test_framework;

public class TestMain
{
    private readonly ConcurrentBag<TestResult> _results = new();
    private readonly StringBuilder _logBuilder = new();
    private readonly object _logLock = new();
    private readonly string? _logFilePath;
    private int _maxDegreeOfParallelism = Environment.ProcessorCount;
    private int _activeTests;
    private readonly SemaphoreSlim _parallelismSemaphore;

    public TestMain(string? logFilePath = null)
    {
        _logFilePath = logFilePath;
        _parallelismSemaphore = new SemaphoreSlim(_maxDegreeOfParallelism);
    }

    public TestMain SetMaxDegreeOfParallelism(int maxDegree)
    {
        _maxDegreeOfParallelism = maxDegree > 0 ? maxDegree : Environment.ProcessorCount;
        return this;
    }

    public async Task<IReadOnlyList<TestResult>> ExecuteTestsAsync(Assembly assembly)
    {
        _results.Clear();
        _logBuilder.Clear();

        Log($"           ТЕСТЫ (MaxDegreeOfParallelism: {_maxDegreeOfParallelism})");
        Log($"           Время начала: {DateTime.Now:HH:mm:ss.fff}");

        var testSuites = GetTestSuites(assembly);
        var allTestTasks = new List<Task>();

        foreach (var suiteType in testSuites)
        {
            allTestTasks.Add(ExecuteTestSuiteAsync(suiteType));
        }

        await Task.WhenAll(allTestTasks);

        PrintSummary();

        if (!string.IsNullOrEmpty(_logFilePath))
        {
            lock (_logLock)
            {
                File.WriteAllText(_logFilePath, _logBuilder.ToString());
            }
        }

        return _results.ToList();
    }

    private IEnumerable<Type> GetTestSuites(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<TestAttributes.TestSuiteAttribute>() != null);
    }

    private async Task ExecuteTestSuiteAsync(Type suiteType)
    {
        var suiteAttr = suiteType.GetCustomAttribute<TestAttributes.TestSuiteAttribute>();
        var suiteName = suiteAttr?.Description ?? suiteType.Name;

        Log($"\n=== {suiteName} ===");

        var instance = Activator.CreateInstance(suiteType);
        var methods = suiteType.GetMethods();

        var setupMethod = methods.FirstOrDefault(m => m.GetCustomAttribute<TestAttributes.SetupAttribute>() != null);
        var teardownMethod = methods.FirstOrDefault(m => m.GetCustomAttribute<TestAttributes.TeardownAttribute>() != null);

        // Обычные тесты
        var regularTests = methods.Where(m =>
            m.GetCustomAttribute<TestAttributes.TestMethodAttribute>() != null &&
            m.GetCustomAttribute<TestAttributes.SharedContextAttribute>() == null);

        var regularTestTasks = regularTests.Select(testMethod =>
            ExecuteTestMethodWithParallelismAsync(instance, testMethod, setupMethod, teardownMethod));

        await Task.WhenAll(regularTestTasks);

        // Тесты с общим контекстом (их нельзя распараллеливать внутри группы)
        var sharedContextTests = methods
            .Select(m => new {
                Method = m,
                Attr = m.GetCustomAttribute<TestAttributes.SharedContextAttribute>()
            })
            .Where(x => x.Attr != null)
            .GroupBy(x => x.Attr!.ContextId);

        foreach (var group in sharedContextTests)
        {
            Log($"\n  Контекст ID: {group.Key}");

            // Выполняем setup один раз для группы
            setupMethod?.Invoke(instance, null);

            var orderedTests = group.OrderBy(x => x.Attr!.Order);

            foreach (var test in orderedTests)
            {
                await ExecuteTestMethodWithParallelismAsync(instance, test.Method, null, null, isSharedContext: true);
            }

            teardownMethod?.Invoke(instance, null);
        }
    }

    private async Task ExecuteTestMethodWithParallelismAsync(
        object instance,
        MethodInfo method,
        MethodInfo? setupMethod,
        MethodInfo? teardownMethod,
        bool isSharedContext = false)
    {
        await _parallelismSemaphore.WaitAsync();
        Interlocked.Increment(ref _activeTests);

        try
        {
            await ExecuteTestMethodAsync(instance, method, setupMethod, teardownMethod, isSharedContext);
        }
        finally
        {
            Interlocked.Decrement(ref _activeTests);
            _parallelismSemaphore.Release();
        }
    }

    private async Task ExecuteTestMethodAsync(
        object instance,
        MethodInfo method,
        MethodInfo? setupMethod,
        MethodInfo? teardownMethod,
        bool isSharedContext = false)
    {
        var testAttr = method.GetCustomAttribute<TestAttributes.TestMethodAttribute>();
        var ignoreAttr = method.GetCustomAttribute<TestAttributes.IgnoreAttribute>();
        var timeoutAttr = method.GetCustomAttribute<TestAttributes.TimeoutAttribute>();

        if (ignoreAttr != null)
        {
            Log($"  [ПРОПУЩЕН] {testAttr?.DisplayName ?? method.Name}: {ignoreAttr.Reason}");
            return;
        }

        var testCases = method.GetCustomAttributes<TestAttributes.TestCaseAttribute>().ToList();

        if (testCases.Any())
        {
            // Тест с параметрами
            var testTasks = testCases.Select(testCase =>
                ExecuteSingleTestWithTimeoutAsync(instance, method, testCase.Parameters,
                    testAttr, setupMethod, teardownMethod, isSharedContext, timeoutAttr));
            await Task.WhenAll(testTasks);
        }
        else
        {
            // Обычный тест без параметров
            await ExecuteSingleTestWithTimeoutAsync(instance, method, null,
                testAttr, setupMethod, teardownMethod, isSharedContext, timeoutAttr);
        }
    }

    private async Task ExecuteSingleTestWithTimeoutAsync(
        object instance,
        MethodInfo method,
        object[]? parameters,
        TestAttributes.TestMethodAttribute? testAttr,
        MethodInfo? setupMethod,
        MethodInfo? teardownMethod,
        bool isSharedContext,
        TestAttributes.TimeoutAttribute? timeoutAttr)
    {
        var result = new TestResult
        {
            TestName = method.Name,
            ClassName = instance.GetType().Name,
            DisplayName = testAttr?.DisplayName ?? method.Name,
            Category = testAttr?.Category ?? "General",
            StartTime = DateTime.Now,
            IsTimeout = false
        };

        var startTime = DateTime.UtcNow;
        var cts = new CancellationTokenSource();

        try
        {
            var executionTask = ExecuteSingleTestInternalAsync(instance, method, parameters,
                setupMethod, teardownMethod, isSharedContext, cts.Token);

            Task completedTask;
            if (timeoutAttr != null)
            {
                // Есть ограничение по времени
                var timeoutTask = Task.Delay(timeoutAttr.Milliseconds, cts.Token);
                completedTask = await Task.WhenAny(executionTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // Таймаут!
                    await cts.CancelAsync();
                    result.IsTimeout = true;
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Превышен лимит времени выполнения ({timeoutAttr.Milliseconds} мс)";

                    try
                    {
                        await executionTask; // Ждем, чтобы избежать исключения
                    }
                    catch (OperationCanceledException)
                    {
                        // Ожидаемая отмена
                    }

                    LogWithThread($"  [ТАЙМАУТ] {result.DisplayName} - {result.ErrorMessage}");
                }
                else
                {
                    // Тест завершился вовремя
                    await executionTask;
                    result.IsSuccess = true;
                    LogWithThread($"  [ПРОЙДЕН] {result.DisplayName} (поток: {Environment.CurrentManagedThreadId})");
                }
            }
            else
            {
                // Нет таймаута
                await executionTask;
                result.IsSuccess = true;
                LogWithThread($"  [ПРОЙДЕН] {result.DisplayName} (поток: {Environment.CurrentManagedThreadId})");
            }
        }
        catch (Exception ex)
        {
            var realEx = ex.InnerException ?? ex;
            result.IsSuccess = false;
            result.ErrorMessage = realEx.Message;
            result.StackTrace = realEx.StackTrace;

            if (!result.IsTimeout)
            {
                if (realEx is TestAssertException)
                {
                    LogWithThread($"  [ОШИБКА ПРОВЕРКИ] {result.DisplayName}: {realEx.Message}");
                }
                else
                {
                    LogWithThread($"  [ИСКЛЮЧЕНИЕ] {result.DisplayName}: {realEx.Message}");
                }
            }
        }

        result.EndTime = DateTime.Now;
        result.Duration = DateTime.UtcNow - startTime;

        _results.Add(result);
    }

    private async Task ExecuteSingleTestInternalAsync(
        object instance,
        MethodInfo method,
        object[]? parameters,
        MethodInfo? setupMethod,
        MethodInfo? teardownMethod,
        bool isSharedContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (!isSharedContext)
            {
                setupMethod?.Invoke(instance, null);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var result = method.Invoke(instance, parameters);

            if (result is Task task)
            {
                await task.WaitAsync(cancellationToken);
            }

            if (!isSharedContext)
            {
                teardownMethod?.Invoke(instance, null);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (!isSharedContext)
            {
                try { teardownMethod?.Invoke(instance, null); } catch { }
            }
            throw;
        }
    }

    private void Log(string message)
    {
        lock (_logLock)
        {
            Console.WriteLine(message);
            _logBuilder.AppendLine(message);
        }
    }

    private void LogWithThread(string message)
    {
        lock (_logLock)
        {
            Console.WriteLine(message);
            _logBuilder.AppendLine(message);
        }
    }

    private void PrintSummary()
    {
        var resultsList = _results.ToList();
        var total = resultsList.Count;
        var passed = resultsList.Count(r => r.IsSuccess);
        var failed = resultsList.Count(r => !r.IsSuccess && !r.IsTimeout);
        var timeout = resultsList.Count(r => r.IsTimeout);

        Log($"\n{new string('=', 50)}");
        Log("РЕЗУЛЬТАТЫ");
        Log($"ВСЕГО: {total}");
        Log($"ПРОЙДЕНО: {passed}");
        Log($"ПРОВАЛЕНО: {failed}");
        Log($"ТАЙМАУТ: {timeout}");

        if (failed > 0)
        {
            Log("\nПроваленные тесты:");
            foreach (var failedTest in resultsList.Where(r => !r.IsSuccess && !r.IsTimeout))
            {
                Log($"  - {failedTest.DisplayName}: {failedTest.ErrorMessage}");
            }
        }

        if (timeout > 0)
        {
            Log("\nТесты с превышением времени:");
            foreach (var timeoutTest in resultsList.Where(r => r.IsTimeout))
            {
                Log($"  - {timeoutTest.DisplayName}: {timeoutTest.ErrorMessage}");
            }
        }

        Log($"\nВремя окончания: {DateTime.Now:HH:mm:ss.fff}");
    }
}