using System;

namespace ResilientSeleniumAutomation.Tests.TestData
{
    /// <summary>
    /// Strongly-typed test data model for form inputs.
    /// </summary>
    public class UserFormData
    {
        public string FirstName { get; set; } = "Alex";
        public string Gender { get; set; } = "Male";
        public string State { get; set; } = "United States";

        public static UserFormData Default => new();

        public static UserFormData FemaleVariant => new()
        {
            FirstName = "Sarah",
            Gender = "Female",
            State = "India"
        };

        public static UserFormData TransgenderVariant => new()
        {
            FirstName = "Taylor",
            Gender = "Transgender",
            State = "Germany"
        };
    }
}

