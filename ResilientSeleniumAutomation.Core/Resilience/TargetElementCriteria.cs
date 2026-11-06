using System;
using System.Collections.Generic;

namespace ResilientSeleniumAutomation.Core.Resilience
{
    /// <summary>
    /// Type of HTML control expected for semantic matching.
    /// </summary>
    public enum ControlType
    {
        TextInput,
        RadioButtonGroup,
        RadioButtonOption,
        DropdownSelect,
        Checkbox,
        Button,
        Any
    }

    /// <summary>
    /// Declarative specification of an element to be located resiliently.
    /// Does not rely on rigid CSS or XPath selectors.
    /// </summary>
    public class TargetElementCriteria
    {
        /// <summary>
        /// Primary human-readable semantic concept (e.g. "First Name", "Gender", "State").
        /// </summary>
        public string SemanticLabel { get; set; } = string.Empty;

        /// <summary>
        /// Synonyms or related semantic tokens (e.g. ["fname", "first", "forename", "given name"]).
        /// </summary>
        public List<string> AlternativeLabels { get; set; } = new();

        /// <summary>
        /// Expected control type (TextInput, RadioButtonOption, DropdownSelect, etc.).
        /// </summary>
        public ControlType ExpectedControlType { get; set; } = ControlType.Any;

        /// <summary>
        /// Expected HTML tag (e.g. "input", "select", "button").
        /// </summary>
        public string? ExpectedTagName { get; set; }

        /// <summary>
        /// Expected HTML input type (e.g. "text", "radio", "checkbox").
        /// </summary>
        public string? ExpectedInputType { get; set; }

        /// <summary>
        /// Expected option value or text (e.g. "Male" for a radio button, or "United States" for a state option).
        /// </summary>
        public string? OptionValue { get; set; }

        /// <summary>
        /// Group of expected option values (e.g. ["Male", "Female", "Transgender"] for gender group).
        /// </summary>
        public List<string> ExpectedOptions { get; set; } = new();

        /// <summary>
        /// Contextual keywords expected in the ancestor form, container, or nearby section (e.g. "automationtestform", "Form").
        /// </summary>
        public List<string> ContextHints { get; set; } = new();

        /// <summary>
        /// Creates a text input criteria.
        /// </summary>
        public static TargetElementCriteria ForTextInput(string label, params string[] alternatives)
        {
            var criteria = new TargetElementCriteria
            {
                SemanticLabel = label,
                ExpectedControlType = ControlType.TextInput,
                ExpectedTagName = "input",
                ExpectedInputType = "text"
            };
            criteria.AlternativeLabels.AddRange(alternatives);
            criteria.ContextHints.Add("automationtestform");
            criteria.ContextHints.Add("form");
            return criteria;
        }

        /// <summary>
        /// Creates a radio button option criteria within a named group.
        /// </summary>
        public static TargetElementCriteria ForRadioOption(string groupLabel, string optionValue, params string[] alternatives)
        {
            var criteria = new TargetElementCriteria
            {
                SemanticLabel = groupLabel,
                OptionValue = optionValue,
                ExpectedControlType = ControlType.RadioButtonOption,
                ExpectedTagName = "input",
                ExpectedInputType = "radio"
            };
            criteria.AlternativeLabels.AddRange(alternatives);
            criteria.ContextHints.Add("automationtestform");
            criteria.ContextHints.Add("form");
            return criteria;
        }

        /// <summary>
        /// Creates a dropdown select criteria.
        /// </summary>
        public static TargetElementCriteria ForDropdown(string label, IEnumerable<string>? sampleExpectedOptions = null, params string[] alternatives)
        {
            var criteria = new TargetElementCriteria
            {
                SemanticLabel = label,
                ExpectedControlType = ControlType.DropdownSelect,
                ExpectedTagName = "select"
            };
            if (sampleExpectedOptions != null)
            {
                criteria.ExpectedOptions.AddRange(sampleExpectedOptions);
            }
            criteria.AlternativeLabels.AddRange(alternatives);
            criteria.ContextHints.Add("automationtestform");
            criteria.ContextHints.Add("form");
            return criteria;
        }
    }
}
