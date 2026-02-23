namespace lab1_test_framework;

public class TestResult
{
    public string TestName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
        
    public override string ToString()
    {
        var status = IsSuccess ? "✓" : "✗";
        var color = IsSuccess ? ConsoleColor.Green : ConsoleColor.Red;
            
        return $"[{status}] {DisplayName} - {Duration.TotalMilliseconds:F0}ms";
    }
}