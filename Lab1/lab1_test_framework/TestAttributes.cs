namespace lab1_test_framework;

public class TestAttributes
{
     [AttributeUsage(AttributeTargets.Method)]
        public class TestMethodAttribute : Attribute
        {
            public string DisplayName { get; set; } = string.Empty;
            public string Category { get; set; } = "General";
            public bool IsAsync { get; set; }
        }
    
        [AttributeUsage(AttributeTargets.Class)]
        public class TestSuiteAttribute : Attribute
        {
            public string Description { get; set; } = string.Empty;
        }
    
        [AttributeUsage(AttributeTargets.Method)]
        public class SetupAttribute : Attribute { }
    
        [AttributeUsage(AttributeTargets.Method)]
        public class TeardownAttribute : Attribute { }
    
        [AttributeUsage(AttributeTargets.Method)]
        public class IgnoreAttribute : Attribute
        {
            public string Reason { get; set; } = "Not implemented yet";
        }
    
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
        public class TestCaseAttribute : Attribute
        {
            public object[] Parameters { get; }
            
            public TestCaseAttribute(params object[] parameters)
            {
                Parameters = parameters;
            }
        }
    
        [AttributeUsage(AttributeTargets.Method)]
        public class SharedContextAttribute : Attribute
        {
            public string ContextId { get; set; } = Guid.NewGuid().ToString();
            public int Order { get; set; } = 0;
        }
}