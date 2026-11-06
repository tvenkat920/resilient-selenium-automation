using System;
using System.IO;
using NUnit.Framework;
using OpenQA.Selenium;
using ResilientSeleniumAutomation.Core.Diagnostics;
using ResilientSeleniumAutomation.Core.Exceptions;
using ResilientSeleniumAutomation.Core.Resilience;
using ResilientSeleniumAutomation.Tests.TestData;

namespace ResilientSeleniumAutomation.Tests
{
    /// <summary>
    /// Comprehensive test suite for AutomationPracticeForm.
    /// Covers core requirements for First Name, Gender, and State automation,
    /// along with positive verification, boundary checks, and negative failure modes.
    /// </summary>
    [TestFixture]
    public class AutomationPracticeFormTests : BaseTest
    {
        [Test]
        [Description("Positive E2E: Identifies and fills First Name, Gender, and State, verifying all values.")]
        public void Should_Fill_And_Verify_All_Fields_Successfully()
        {
            var testData = UserFormData.Default;

            // 1. Navigate to configurable BaseUrl
            Page.Navigate();

            // 2. Automate First Name
            Page.EnterFirstName(testData.FirstName);
            var actualFirstName = Page.GetFirstName();
            Assert.That(actualFirstName, Is.EqualTo(testData.FirstName), "First Name value did not match entered text.");

            // 3. Automate Gender
            Page.SelectGender(testData.Gender);
            var isGenderSelected = Page.IsGenderSelected(testData.Gender);
            Assert.That(isGenderSelected, Is.True, $"Gender radio option '{testData.Gender}' was not selected.");
            var actualSelectedGender = Page.GetSelectedGender();
            Assert.That(actualSelectedGender, Is.EqualTo(testData.Gender), "Selected gender did not match expected value.");

            // 4. Automate State
            Page.SelectState(testData.State);
            var actualSelectedState = Page.GetSelectedState();
            Assert.That(actualSelectedState, Is.EqualTo(testData.State), "State dropdown selected option did not match expected value.");

            // 5. Capture positive proof screenshot
            var screenshotPath = ScreenshotService.CaptureScreenshot(Driver, "E2E_Form_Filled_Success", Settings, Logger);
            if (!string.IsNullOrEmpty(screenshotPath))
            {
                TestContext.AddTestAttachment(screenshotPath, "E2E Filled Form Screenshot");
            }

            Logger.Info("E2E Form Automation completed and verified successfully.");
        }

        [TestCase("Male")]
        [TestCase("Female")]
        [TestCase("Transgender")]
        [Description("Positive: Verifies resilient radio button selection across all supported gender options.")]
        public void Should_Resiliently_Select_And_Verify_All_Gender_Options(string genderOption)
        {
            Page.Navigate();

            Page.SelectGender(genderOption);

            Assert.That(Page.IsGenderSelected(genderOption), Is.True,
                $"Expected gender option '{genderOption}' to be selected.");
            Assert.That(Page.GetSelectedGender(), Is.EqualTo(genderOption),
                $"Detected selected gender does not match '{genderOption}'.");
        }

        [TestCase("United States")]
        [TestCase("India")]
        [TestCase("Germany")]
        [Description("Positive: Verifies resilient dropdown selection and verification for multiple states/countries.")]
        public void Should_Resiliently_Select_And_Verify_State_Options(string stateOption)
        {
            Page.Navigate();

            Page.SelectState(stateOption);

            var actual = Page.GetSelectedState();
            Assert.That(actual, Is.EqualTo(stateOption),
                $"Expected State dropdown to reflect '{stateOption}', but found '{actual}'.");
        }

        [Test]
        [Description("Negative Requirement 7: Accurately identifies and reports unsupported custom dropdown components.")]
        public void Should_Throw_UnsupportedDropdownException_When_Dropdown_Is_Custom_Bootstrap_Component()
        {
            Page.Navigate();

            // Attempting to wrap the 'Know about us' custom bootstrap button dropdown with DropdownHandler
            var ex = Assert.Throws<UnsupportedDropdownException>(() =>
            {
                Page.InspectCustomDropdown();
            });

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex!.ActualTagName, Is.EqualTo("button"));
            Assert.That(ex.Message, Does.Contain("Found <button> instead of native HTML <select>"));

            Logger.Info($"Verified UnsupportedDropdownException: {ex.Message}");
        }

        [Test]
        [Description("Negative: Attempting to select a non-existent option in native dropdown throws clear exception.")]
        public void Should_Throw_NoSuchElementException_When_Option_Does_Not_Exist()
        {
            Page.Navigate();

            var ex = Assert.Throws<NoSuchElementException>(() =>
            {
                Page.SelectState("NonExistentAtlantisTerritory");
            });

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex!.Message, Does.Contain("was not found in the dropdown"));
            Assert.That(ex.Message, Does.Contain("Available options"));

            Logger.Info($"Verified graceful failure for invalid dropdown option: {ex.Message}");
        }

        [Test]
        [Description("Negative Requirement 10: Confirms candidate engine fails safely when target element does not meet confidence threshold.")]
        public void Should_Throw_ElementNotFoundException_When_Target_Is_Below_Confidence_Threshold()
        {
            Page.Navigate();

            var finder = new ResilientElementFinder(Driver, Settings, Logger);
            var fictionalCriteria = TargetElementCriteria.ForTextInput("CompletelyFictionalCreditCardSecurityCodeXYZ");

            var ex = Assert.Throws<ElementNotFoundException>(() =>
            {
                finder.FindElement(fictionalCriteria);
            });

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex!.TargetSemantic, Is.EqualTo("CompletelyFictionalCreditCardSecurityCodeXYZ"));
            Assert.That(ex.Message, Does.Contain("below required threshold"));

            Logger.Info($"Verified low-confidence safe rejection: {ex.Message}");
        }

        [Test]
        [Description("Negative Requirement 10: Confirms ambiguity detection aborts safely if two candidates have similar high scores.")]
        public void Should_Fail_Safely_And_Throw_AmbiguousElementMatchException_When_Candidates_Are_Ambiguous()
        {
            Page.Navigate();

            // Inject two identical dummy test fields into the DOM to simulate an ambiguous form collision
            var js = (IJavaScriptExecutor)Driver;
            js.ExecuteScript(@"
                var container = document.createElement('div');
                container.id = 'ambiguity-test-container';
                container.innerHTML = `
                    <div class='form-group col-xs-6'>
                        <label for='ambig_1' class='control-label'>Emergency Contact</label>
                        <input id='ambig_1' type='text' class='form-control' placeholder='Emergency Contact' />
                    </div>
                    <div class='form-group col-xs-6'>
                        <label for='ambig_2' class='control-label'>Emergency Contact</label>
                        <input id='ambig_2' type='text' class='form-control' placeholder='Emergency Contact' />
                    </div>
                `;
                document.body.appendChild(container);
            ");

            try
            {
                var finder = new ResilientElementFinder(Driver, Settings, Logger);
                var ambiguousCriteria = TargetElementCriteria.ForTextInput("Emergency Contact");

                var ex = Assert.Throws<AmbiguousElementMatchException>(() =>
                {
                    finder.FindElement(ambiguousCriteria);
                });

                Assert.That(ex, Is.Not.Null);
                Assert.That(ex!.TargetSemantic, Is.EqualTo("Emergency Contact"));
                Assert.That(ex.Message, Does.Contain("Ambiguous match detected"));
                Assert.That(ex.Delta, Is.LessThanOrEqualTo(Settings.AmbiguityDeltaThreshold));

                Logger.Info($"Verified ambiguity safe failure: {ex.Message}");
            }
            finally
            {
                // Clean up injected test element
                js.ExecuteScript("var el = document.getElementById('ambiguity-test-container'); if (el) el.remove();");
            }
        }
    }
}

