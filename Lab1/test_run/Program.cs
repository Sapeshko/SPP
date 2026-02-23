using System.Reflection;
using System.Runtime.Loader;
using lab1_test_framework;

namespace TestRunner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("ЗАПУСК ТЕСТОВОГО ФРЕЙМВОРКА");
            Console.WriteLine("============================\n");

            try
            {
                // Получаем базовую директорию
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                Console.WriteLine($"Базовая директория: {baseDirectory}");

                // Ищем сборку с тестами в разных местах
                string[] possiblePaths = new[]
                {
                    Path.Combine(baseDirectory, "LibraryManagerTests.dll"),
                    Path.Combine(baseDirectory, "..", "..", "..", "..", "TaskManagerTests", "bin", "Debug", "net8.0", "LibraryManagerTests.dll"),
                    Path.Combine(Directory.GetCurrentDirectory(), "LibraryManagerTests.dll"),
                    Path.Combine(AppContext.BaseDirectory, "LibraryManagerTests.dll")
                };

                string? testAssemblyPath = null;
                foreach (var path in possiblePaths)
                {
                    string fullPath = Path.GetFullPath(path);
                    Console.WriteLine($"Проверяем путь: {fullPath}");
                    
                    if (File.Exists(fullPath))
                    {
                        testAssemblyPath = fullPath;
                        Console.WriteLine($" Найдено: {fullPath}");
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
                    
                    Console.WriteLine("\nНажмите любую клавишу для выхода...");
                    Console.ReadKey();
                    return;
                }

                // Загружаем сборку с обработкой зависимостей
                Assembly testAssembly = Assembly.LoadFrom(testAssemblyPath);
                
                // Выводим информацию о загруженной сборке
                Console.WriteLine($"\n Сборка загружена: {testAssembly.FullName}");
                
                // Находим все классы с атрибутом TestSuite
                var testSuiteTypes = testAssembly.GetTypes()
                    .Where(t => t.GetCustomAttribute<TestAttributes.TestSuiteAttribute>() != null)
                    .ToList();
                    
                Console.WriteLine($"Найдено тестовых наборов: {testSuiteTypes.Count}");
                foreach (var type in testSuiteTypes)
                {
                    var attr = type.GetCustomAttribute<TestAttributes.TestSuiteAttribute>();
                    Console.WriteLine($"  - {type.Name}: {attr?.Description}");
                }

                // Создаем исполнитель тестов с логированием в файл
                string logPath = Path.Combine(baseDirectory, $"test_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                
                var executor = new TestExecutor(logPath);
                
                // Запускаем тесты
                var results = await executor.ExecuteTestsAsync(testAssembly);

                // Выводим детальную статистику
                Console.WriteLine("\n ДЕТАЛЬНАЯ СТАТИСТИКА:");
                Console.WriteLine($"Всего тестов: {results.Count}");
                Console.WriteLine($" Успешно: {results.Count(r => r.IsSuccess)}");
                Console.WriteLine($" Провалено: {results.Count(r => !r.IsSuccess)}");
                
                if (results.Any())
                {
                    var totalTime = TimeSpan.FromTicks(results.Sum(r => r.Duration.Ticks));
                    Console.WriteLine($" Общее время: {totalTime:mm\\:ss\\.fff}");
                }

                if (results.Any(r => !r.IsSuccess))
                {
                    Console.WriteLine("\n⚠ Некоторые тесты не пройдены!");
                    
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
                    Console.WriteLine("\n Все тесты успешно пройдены!");
                    Environment.ExitCode = 0;
                }

                Console.WriteLine($"\n Лог сохранен в: {logPath}");
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

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }
    }
}