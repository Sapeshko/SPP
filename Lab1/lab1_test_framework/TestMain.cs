using System.Reflection;
using System.Text;

namespace lab1_test_framework;

public class TestMain
{
     private readonly List<TestResult> _results = new();
        private readonly StringBuilder _logBuilder = new();
        private readonly string? _logFilePath;

        public TestMain(string? logFilePath = null)
        {
            _logFilePath = logFilePath;
        }

        public async Task<IReadOnlyList<TestResult>> ExecuteTestsAsync(Assembly assembly)
        {
            _results.Clear();
            _logBuilder.Clear();

            Log("           ТЕСТЫ");


            var testSuites = GetTestSuites(assembly);

            foreach (var suiteType in testSuites)
            {
                await ExecuteTestSuiteAsync(suiteType);
            }

            PrintSummary();
            
            if (!string.IsNullOrEmpty(_logFilePath))
            {
                await File.WriteAllTextAsync(_logFilePath, _logBuilder.ToString());
            }

            return _results;
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
       

            var instance = Activator.CreateInstance(suiteType);
            var methods = suiteType.GetMethods();

            var setupMethod = methods.FirstOrDefault(m => m.GetCustomAttribute<TestAttributes.SetupAttribute>() != null);
            var teardownMethod = methods.FirstOrDefault(m => m.GetCustomAttribute<TestAttributes.TeardownAttribute>() != null);

            // Обычные тесты
            var regularTests = methods.Where(m => 
                m.GetCustomAttribute<TestAttributes.TestMethodAttribute>() != null && 
                m.GetCustomAttribute<TestAttributes.SharedContextAttribute>() == null);

            foreach (var testMethod in regularTests)
            {
                await ExecuteTestMethodAsync(instance, testMethod, setupMethod, teardownMethod);
            }

            // Тесты с общим контекстом
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
                    await ExecuteTestMethodAsync(instance, test.Method, null, null, isSharedContext: true);
                }

                teardownMethod?.Invoke(instance, null);
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
            
            if (ignoreAttr != null)
            {
                Log($" {testAttr?.DisplayName ?? method.Name} - ПРОПУЩЕН: {ignoreAttr.Reason}");
                return;
            }

            var testCases = method.GetCustomAttributes<TestAttributes.TestCaseAttribute>().ToList();
            
            if (testCases.Any())
            {
                // Тест с параметрами
                foreach (var testCase in testCases)
                {
                    await ExecuteSingleTestAsync(instance, method, testCase.Parameters, 
                        testAttr, setupMethod, teardownMethod, isSharedContext);
                }
            }
            else
            {
                // Обычный тест без параметров
                await ExecuteSingleTestAsync(instance, method, null, 
                    testAttr, setupMethod, teardownMethod, isSharedContext);
            }
        }

        private async Task ExecuteSingleTestAsync(
            object instance,
            MethodInfo method,
            object[]? parameters,
            TestAttributes.TestMethodAttribute? testAttr,
            MethodInfo? setupMethod,
            MethodInfo? teardownMethod,
            bool isSharedContext)
        {
            var result = new TestResult
            {
                TestName = method.Name,
                ClassName = instance.GetType().Name,
                DisplayName = testAttr?.DisplayName ?? method.Name,
                Category = testAttr?.Category ?? "General",
                StartTime = DateTime.Now
            };

            var startTime = DateTime.UtcNow;

            try
            {
                if (!isSharedContext)
                {
                    setupMethod?.Invoke(instance, null);
                }

                var task = method.Invoke(instance, parameters) as Task;
                if (task != null)
                {
                    await task; // Асинхронный тест
                }

                if (!isSharedContext)
                {
                    teardownMethod?.Invoke(instance, null);
                }

                result.IsSuccess = true;
                Log($" {result.DisplayName} - ПРОЙДЕН");
            }
            catch (Exception ex)
            {
                var realEx = ex.InnerException ?? ex;
                result.IsSuccess = false;
                result.ErrorMessage = realEx.Message;
                result.StackTrace = realEx.StackTrace;

                if (realEx is TestAssertException)
                {
                    Log($" {result.DisplayName} - ОШИБКА ПРОВЕРКИ: {realEx.Message}");
                }
                else
                {
                    Log($" {result.DisplayName} - ИСКЛЮЧЕНИЕ: {realEx.Message}");
                }

                if (!isSharedContext)
                {
                    try { teardownMethod?.Invoke(instance, null); } catch { }
                }
            }

            result.EndTime = DateTime.Now;
            result.Duration = DateTime.UtcNow - startTime;
            
            _results.Add(result);
        }

        private void Log(string message)
        {
            Console.WriteLine(message);
            _logBuilder.AppendLine(message);
        }

        private void PrintSummary()
        {
            var total = _results.Count;
            var passed = _results.Count(r => r.IsSuccess);
            var failed = total - passed;

            Log("\nРЕЗУЛЬТАТЫ");
            Log($"ВСЕГО: {total}");
            Log($"ПРОЙДЕНО: {passed}");
            Log($"ПРОВАЛЕНО: {failed}");

            if (failed > 0)
            {
                Log("\nПроваленные тесты:");
                foreach (var failedTest in _results.Where(r => !r.IsSuccess))
                {
                    Log($"  - {failedTest.DisplayName}: {failedTest.ErrorMessage}");
                }
            }
        }
}