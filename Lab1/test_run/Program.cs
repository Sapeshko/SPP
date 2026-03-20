using System.Reflection;
using System.Runtime.Loader;
using lab1_test_framework;

namespace TestRunner
{
    class Program
    {
        static async Task Main(string[] args)
        {

            try
            {
                // Получаем базовую директорию
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                // Ищем сборку с тестами в разных местах
                string[] possiblePaths = new[]
                {
                    Path.Combine(baseDirectory, "LibraryManagerTests.dll"),
                    Path.Combine(baseDirectory, "..", "..", "..", "..", "bin", "Debug", "net8.0", "LibraryManagerTests.dll"),
                    Path.Combine(Directory.GetCurrentDirectory(), "LibraryManagerTests.dll"),
                    Path.Combine(AppContext.BaseDirectory, "LibraryManagerTests.dll")
                };

                string? testAssemblyPath = null;
                foreach (var path in possiblePaths)
                {
                    string fullPath = Path.GetFullPath(path);
                    
                    if (File.Exists(fullPath))
                    {
                        testAssemblyPath = fullPath;
                        break;
                    }
                }

                if (testAssemblyPath == null)
                {
                    Console.WriteLine(" Не удалось найти сборку с тестами!");
                    Console.WriteLine("\nПроверенные пути:");
                    foreach (var path in possiblePaths)
                    {
                        Console.WriteLine($"  - {Path.GetFullPath(path)}");
                    }
                    
                    Console.ReadKey();
                    return;
                }

                // Загружаем сборку с обработкой зависимостей
                Assembly testAssembly = Assembly.LoadFrom(testAssemblyPath);
               
                
                // Находим все классы с атрибутом TestSuite
                var testSuiteTypes = testAssembly.GetTypes()
                    .Where(t => t.GetCustomAttribute<TestAttributes.TestSuiteAttribute>() != null)
                    .ToList();
                    


                // Создаем исполнитель тестов с логированием в файл
                string logPath = Path.Combine(baseDirectory, $"test_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                
                var executor = new TestMain(logPath);
                
                // Запускаем тесты
                var results = await executor.ExecuteTestsAsync(testAssembly);
                
                if (results.Any())
                {
                    var totalTime = TimeSpan.FromTicks(results.Sum(r => r.Duration.Ticks));
                    Console.WriteLine($" Общее время: {totalTime:mm\\:ss\\.fff}");
                }

                if (results.Any(r => !r.IsSuccess))
                {
                    Console.WriteLine("\n Некоторые тесты не пройдены!");
                    
                    // Показываем детали проваленных тестов
                    Console.WriteLine("\nДетали ошибок:");
                    foreach (var failed in results.Where(r => !r.IsSuccess))
                    {
                        Console.WriteLine($"\n  {failed.DisplayName}:");
                        Console.WriteLine($"    {failed.ErrorMessage}");
                    }
                    
                    Environment.ExitCode = 1;
                }
                else
                {
                    Console.WriteLine("\n Все тесты пройдены!");
                    Environment.ExitCode = 0;
                }

            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
                Console.WriteLine($"Тип ошибки: {ex.GetType().Name}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                Console.ResetColor();
                Environment.ExitCode = -1;
            }

            Console.ReadKey();
        }
    }
}