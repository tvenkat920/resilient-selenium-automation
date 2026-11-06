using System;
using System.Collections.Generic;
using System.Linq;
using OpenQA.Selenium;
using ResilientSeleniumAutomation.Core.Logging;

namespace ResilientSeleniumAutomation.Core.Resilience
{
    /// <summary>
    /// Responsible for extracting independent semantic signals from candidate DOM elements:
    /// - Associated label text (for attribute matching id)
    /// - Wrapping & sibling labels / spans
    /// - Ancestor form-group / container context text
    /// - Accessible name (aria-label, aria-labelledby, title)
    /// - Placeholder and HTML5 attributes
    /// - Dropdown option texts and values
    /// </summary>
    public class SignalExtractor
    {
        private readonly IWebDriver _driver;
        private readonly ITestLogger _logger;

        public SignalExtractor(IWebDriver driver, ITestLogger? logger = null)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _logger = logger ?? new ConsoleTestLogger();
        }

        /// <summary>
        /// Populates all signals for a candidate element using atomic JavaScript evaluation with Selenium fallback.
        /// </summary>
        public CandidateElement ExtractSignals(IWebElement element)
        {
            var candidate = new CandidateElement(element);

            try
            {
                if (_driver is IJavaScriptExecutor js)
                {
                    ExtractSignalsViaJs(js, element, candidate);
                    return candidate;
                }
            }
            catch (Exception ex)
            {
                _logger.Debug($"JavaScript signal extraction fallback triggered: {ex.Message}");
            }

            // Fallback to pure WebDriver API
            ExtractSignalsViaWebDriver(element, candidate);
            return candidate;
        }

        private static void ExtractSignalsViaJs(IJavaScriptExecutor js, IWebElement element, CandidateElement candidate)
        {
            const string script = @"
                var el = arguments[0];
                var form = el.closest('form');
                var result = {
                    tagName: (el.tagName || '').toLowerCase(),
                    id: el.id || '',
                    name: el.getAttribute('name') || '',
                    className: el.className || '',
                    inputType: (el.getAttribute('type') || '').toLowerCase(),
                    placeholder: el.getAttribute('placeholder') || '',
                    value: el.value || el.getAttribute('value') || '',
                    title: el.getAttribute('title') || '',
                    ariaLabel: el.getAttribute('aria-label') || '',
                    ariaLabelledBy: el.getAttribute('aria-labelledby') || '',
                    formId: form ? (form.id || '') : '',
                    associatedLabelText: '',
                    wrappingLabelText: '',
                    siblingLabelText: '',
                    containerContextText: '',
                    radioOptionLabelText: '',
                    isDisplayed: !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length),
                    isEnabled: !el.disabled,
                    options: []
                };

                // 1. Associated label by 'for' attribute matching ID (scoped to same form container)
                if (el.id) {
                    var forLabel = document.querySelector('label[for=""' + el.id + '""], span[for=""' + el.id + '""]');
                    if (forLabel) {
                        var labelForm = forLabel.closest('form');
                        var elForm = el.closest('form');
                        if (!labelForm || !elForm || labelForm === elForm) {
                            result.associatedLabelText = (forLabel.textContent || '').trim();
                        }
                    }
                }

                // 2. Wrapping label
                var parentLabel = el.closest('label');
                if (parentLabel) {
                    result.wrappingLabelText = (parentLabel.textContent || '').trim();
                }

                // 3. Radio / Checkbox option text: look at adjacent sibling span/label
                if (result.inputType === 'radio' || result.inputType === 'checkbox') {
                    var next = el.nextElementSibling;
                    if (next && (next.tagName === 'SPAN' || next.tagName === 'LABEL')) {
                        result.radioOptionLabelText = (next.textContent || '').trim();
                    } else {
                        var prev = el.previousElementSibling;
                        if (prev && (prev.tagName === 'SPAN' || prev.tagName === 'LABEL')) {
                            result.radioOptionLabelText = (prev.textContent || '').trim();
                        }
                    }
                }

                // 4. Sibling labels
                var prevSib = el.previousElementSibling;
                while (prevSib) {
                    if (prevSib.tagName === 'LABEL' || prevSib.tagName === 'SPAN') {
                        result.siblingLabelText = (prevSib.textContent || '').trim();
                        break;
                    }
                    prevSib = prevSib.previousElementSibling;
                }

                // 5. Ancestor container label or legend (e.g. form-group, fieldset)
                var container = el.closest('.form-group, fieldset, [role=""group""]');
                if (container) {
                    var groupLabel = container.querySelector('label.control-label, legend, label');
                    if (groupLabel && groupLabel !== el && !groupLabel.contains(el)) {
                        result.containerContextText = (groupLabel.textContent || '').trim();
                    }
                }

                // 6. Aria-labelledby lookup
                if (result.ariaLabelledBy) {
                    var labelledByEl = document.getElementById(result.ariaLabelledBy);
                    if (labelledByEl) {
                        result.ariaLabel = (labelledByEl.textContent || '').trim();
                    }
                }

                // 7. Dropdown options
                if (result.tagName === 'select') {
                    for (var i = 0; i < el.options.length; i++) {
                        var optText = (el.options[i].text || el.options[i].value || '').trim();
                        if (optText) {
                            result.options.push(optText);
                        }
                    }
                }

                return result;
            ";

            if (js.ExecuteScript(script, element) is Dictionary<string, object> dict)
            {
                candidate.TagName = dict.TryGetValue("tagName", out var tag) ? tag?.ToString() ?? "" : "";
                candidate.Id = dict.TryGetValue("id", out var id) ? id?.ToString() : null;
                candidate.Name = dict.TryGetValue("name", out var name) ? name?.ToString() : null;
                candidate.ClassName = dict.TryGetValue("className", out var cls) ? cls?.ToString() : null;
                candidate.InputType = dict.TryGetValue("inputType", out var type) ? type?.ToString() : null;
                candidate.Placeholder = dict.TryGetValue("placeholder", out var ph) ? ph?.ToString() : null;
                candidate.Value = dict.TryGetValue("value", out var val) ? val?.ToString() : null;
                candidate.Title = dict.TryGetValue("title", out var title) ? title?.ToString() : null;
                candidate.AriaLabel = dict.TryGetValue("ariaLabel", out var aria) ? aria?.ToString() : null;
                candidate.AriaLabelledBy = dict.TryGetValue("ariaLabelledBy", out var alb) ? alb?.ToString() : null;
                candidate.FormId = dict.TryGetValue("formId", out var fid) ? fid?.ToString() : null;
                candidate.AssociatedLabelText = dict.TryGetValue("associatedLabelText", out var alt) ? alt?.ToString() : null;
                candidate.WrappingLabelText = dict.TryGetValue("wrappingLabelText", out var wlt) ? wlt?.ToString() : null;
                candidate.SiblingLabelText = dict.TryGetValue("siblingLabelText", out var slt) ? slt?.ToString() : null;
                candidate.ContainerContextText = dict.TryGetValue("containerContextText", out var cct) ? cct?.ToString() : null;
                candidate.RadioOptionLabelText = dict.TryGetValue("radioOptionLabelText", out var rolt) ? rolt?.ToString() : null;
                candidate.IsDisplayed = dict.TryGetValue("isDisplayed", out var disp) && Convert.ToBoolean(disp);
                candidate.IsEnabled = dict.TryGetValue("isEnabled", out var en) && Convert.ToBoolean(en);

                if (dict.TryGetValue("options", out var opts) && opts is IEnumerable<object> optList)
                {
                    candidate.SelectOptions = optList.Select(o => o.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
                }
            }
        }

        private void ExtractSignalsViaWebDriver(IWebElement element, CandidateElement candidate)
        {
            try { candidate.TagName = element.TagName.ToLowerInvariant(); } catch { }
            try { candidate.Id = element.GetAttribute("id"); } catch { }
            try { candidate.Name = element.GetAttribute("name"); } catch { }
            try { candidate.ClassName = element.GetAttribute("class"); } catch { }
            try { candidate.InputType = element.GetAttribute("type")?.ToLowerInvariant(); } catch { }
            try { candidate.Placeholder = element.GetAttribute("placeholder"); } catch { }
            try { candidate.Value = element.GetAttribute("value"); } catch { }
            try { candidate.Title = element.GetAttribute("title"); } catch { }
            try { candidate.AriaLabel = element.GetAttribute("aria-label"); } catch { }
            try { candidate.IsDisplayed = element.Displayed; } catch { candidate.IsDisplayed = false; }
            try { candidate.IsEnabled = element.Enabled; } catch { candidate.IsEnabled = false; }

            // Find associated label
            if (!string.IsNullOrEmpty(candidate.Id))
            {
                try
                {
                    var label = _driver.FindElements(By.XPath($"//*[@for='{candidate.Id}']")).FirstOrDefault();
                    if (label != null) candidate.AssociatedLabelText = label.Text?.Trim();
                }
                catch { }
            }

            // Options for select
            if (candidate.TagName == "select")
            {
                try
                {
                    var optionElements = element.FindElements(By.TagName("option"));
                    candidate.SelectOptions = optionElements.Select(o => o.Text.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
                }
                catch { }
            }
        }
    }
}
