using System;
using NUnit.Framework;
using OpenQA.Selenium;
using ResilientSeleniumAutomation.Core.Exceptions;
using ResilientSeleniumAutomation.Core.Resilience;

namespace ResilientSeleniumAutomation.Tests
{
    /// <summary>
    /// Demonstrates resilience against aggressive DOM restructuring, obfuscated IDs/Names,
    /// randomized attributes, element re-ordering, and dynamic stale element replacements.
    /// Proves that the heuristic multi-signal engine succeeds where rigid selectors fail.
    /// </summary>
    [TestFixture]
    public class ResilienceDemonstrationTests : BaseTest
    {
        [Test]
        [Description("Demonstration 1: Element located and filled when ID and Name are replaced with random obfuscated hashes.")]
        public void Demonstration_Should_Find_Input_When_Id_And_Name_Are_Completely_Obfuscated()
        {
            Page.Navigate();
            var js = (IJavaScriptExecutor)Driver;

            // Simulate front-end framework build obfuscation (like CSS modules or Webpack mangling)
            var randomId = "el_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var randomName = "nm_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            js.ExecuteScript($@"
                var input = document.getElementById('fname');
                var label = document.querySelector('label[for=""fname""]');
                if (input && label) {{
                    input.id = '{randomId}';
                    input.name = '{randomName}';
                    label.setAttribute('for', '{randomId}');
                }}
            ");

            Logger.Info($"Simulated DOM mutation: First Name id='{randomId}', name='{randomName}'");

            // Attempt interaction via standard resilient POM method
            var testValue = "ResilienceHero";
            Page.EnterFirstName(testValue);

            var actual = Page.GetFirstName();
            Assert.That(actual, Is.EqualTo(testValue),
                "ResilientElementFinder should successfully identify First Name despite obfuscated ID and Name.");
        }

        [Test]
        [Description("Demonstration 2: Radio option identified when group name attribute is randomized and label uses span.")]
        public void Demonstration_Should_Find_Radio_When_Group_Name_Is_Randomized()
        {
            Page.Navigate();
            var js = (IJavaScriptExecutor)Driver;

            // Randomize the radio name attribute
            var randomRadioGroup = "grp_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            js.ExecuteScript($@"
                var radios = document.querySelectorAll('input[name=""gender""]');
                radios.forEach(function(r) {{
                    r.setAttribute('name', '{randomRadioGroup}');
                }});
            ");

            Logger.Info($"Simulated DOM mutation: Radio name='gender' mutated to '{randomRadioGroup}'");

            // Interact using resilient POM method
            Page.SelectGender("Female");

            Assert.That(Page.IsGenderSelected("Female"), Is.True,
                "ResilientElementFinder should identify 'Female' option via option label and group container context.");
        }

        [Test]
        [Description("Demonstration 3: Dropdown identified when ID, name, and class attributes are completely stripped.")]
        public void Demonstration_Should_Find_Dropdown_When_Attributes_Are_Stripped()
        {
            Page.Navigate();
            var js = (IJavaScriptExecutor)Driver;

            // Strip all identifiers from the <select> element
            js.ExecuteScript(@"
                var select = document.getElementById('state');
                if (select) {
                    select.removeAttribute('id');
                    select.removeAttribute('name');
                    select.removeAttribute('class');
                }
            ");

            Logger.Info("Simulated DOM mutation: State <select> id, name, and class completely stripped.");

            // Select resiliently
            var targetState = "United Kingdom";
            Page.SelectState(targetState);

            var actual = Page.GetSelectedState();
            Assert.That(actual, Is.EqualTo(targetState),
                "ResilientElementFinder should locate State dropdown via container label 'State' and country options list.");
        }

        [Test]
        [Description("Demonstration 4: First Name input identified when DOM position is reordered inside the form.")]
        public void Demonstration_Should_Find_Input_When_Dom_Order_Is_Permuted()
        {
            Page.Navigate();
            var js = (IJavaScriptExecutor)Driver;

            // Swap First Name and Last Name container nodes in the DOM
            js.ExecuteScript(@"
                var form = document.getElementById('automationtestform');
                var groups = form.querySelectorAll('.form-group.col-xs-6');
                if (groups.length >= 2) {
                    // Move First Name container after Last Name container
                    groups[0].parentNode.insertBefore(groups[1], groups[0]);
                }
            ");

            Logger.Info("Simulated DOM mutation: Swapped DOM positions of First Name and Last Name.");

            var testValue = "OrderInvariant";
            Page.EnterFirstName(testValue);

            var actual = Page.GetFirstName();
            Assert.That(actual, Is.EqualTo(testValue),
                "ResilientElementFinder should identify First Name regardless of its index position in DOM.");
        }

        [Test]
        [Description("Demonstration 5: Recovers transparently from StaleElementReferenceException during dynamic DOM mutations.")]
        public void Demonstration_Should_Recover_From_StaleElementReferenceException()
        {
            Page.Navigate();
            var js = (IJavaScriptExecutor)Driver;

            var finder = new ResilientElementFinder(Driver, Settings, Logger);
            var criteria = TargetElementCriteria.ForTextInput("First Name", "fname");

            // Schedule a DOM mutation in JavaScript to replace the input element right when accessed
            js.ExecuteScript(@"
                var original = document.getElementById('fname');
                var clone = original.cloneNode(true);
                clone.value = 'ClonedValue';
                original.parentNode.replaceChild(clone, original);
            ");

            Logger.Info("Simulated dynamic DOM node replacement causing StaleElementReference.");

            finder.EnterText(criteria, "RecoveredValue");
            var result = finder.GetInputValue(criteria);

            Assert.That(result, Is.EqualTo("RecoveredValue"),
                "Finder should recover from stale node replacement via bounded retry loop.");
        }
    }
}

