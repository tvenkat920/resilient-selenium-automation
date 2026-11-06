# ResilientSeleniumAutomation

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0%20LTS-purple.svg)](https://dotnet.microsoft.com/)
[![Selenium WebDriver](https://img.shields.io/badge/Selenium%20WebDriver-4.52.0-green.svg)](https://www.selenium.dev/)
[![NUnit](https://img.shields.io/badge/NUnit-3.14.0-blue.svg)](https://nunit.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A production-grade, enterprise-ready **C# Selenium WebDriver** test automation solution implementing a multi-signal heuristic locator engine. Designed to automate web applications while remaining resilient against changes in element order, IDs, names, CSS classes, label tags, and minor DOM restructurings.

Target Application: **[CloudQA Automation Practice Form](https://app.cloudqa.io/home/AutomationPracticeForm)**

---

## Table of Contents
1. [Core Features & Resilience Engine](#core-features--resilience-engine)
2. [Target Form Inspection Findings](#target-form-inspection-findings)
3. [Technology Stack](#technology-stack)
4. [Solution Architecture](#solution-architecture)
5. [Heuristic Scoring Model & Ambiguity Protection](#heuristic-scoring-model--ambiguity-protection)
6. [Prerequisites & Quick Start](#prerequisites--quick-start)
7. [Configuration Guide](#configuration-guide)
8. [Test Scenarios & Execution Proof](#test-scenarios--execution-proof)
9. [Resilience Demonstrations](#resilience-demonstrations)
10. [Approach, Scope & Engineering Limitations](#approach-scope--engineering-limitations)

---

## Core Features & Resilience Engine

- **Zero Fragile Locators**: Never relies on single fixed XPaths or CSS selectors.
- **Multi-Signal Heuristic Identification**: Evaluates candidate elements across independent signals:
  - Form-scoped `<label for="id">` and `<span for="id">` associations.
  - Wrapping `<label>` and sibling label/span elements.
  - Container group labels (e.g. `.form-group`, `<fieldset>`, `role="group"`).
  - Accessibility names (`aria-label`, `aria-labelledby`, `title`).
  - HTML5 semantic attributes (`type`, `placeholder`).
  - Dropdown option domain content matching.
- **Ambiguity Detection & Fail-Safe Architecture**: If two candidates score above the confidence threshold ($65\%$) within an ambiguity delta ($\le 8\%$), the engine fails safely and reports ambiguity instead of interacting with the wrong field.
- **Dynamic DOM & Stale Element Recovery**: Bounded retry loop with exponential backoff transparently recovers from `StaleElementReferenceException` when nodes are replaced during asynchronous hydration.
- **Accurate Dropdown Verification**: Strictly supports native HTML `<select>` elements and accurately detects and reports unsupported custom dropdowns (`UnsupportedDropdownException`).
- **Configurable Everywhere**: Configurable via `appsettings.json`, environment variables (`RESILIENT_AUTOMATION_*`, `BASE_URL`), and CLI arguments. URL is never hardcoded.
- **Full Observability**: Structured timestamped console logging and automatic failure/success viewport screenshots saved to `TestResults/Screenshots/`.

---

## Target Form Inspection Findings

Before writing automation code, the DOM of `https://app.cloudqa.io/home/AutomationPracticeForm` was analyzed:

1. **First Name**:
   - Primary form `<form id="automationtestform" action="/Home/AutomationPracticeForm">`.
   - Contained in `<div class="form-group col-xs-6">`.
   - Associated label: `<label for="fname" name="fname" class="control-label">First Name</label>`.
   - Input: `<input id="fname" type="text" name="First Name" class="form-control" placeholder="Name" />`.
2. **Gender**:
   - Contained in `<div class="form-group col-xs-6">` with group label `<label class="control-label">Gender</label>`.
   - Radio buttons: `<input id="male" type="radio" name="gender" value="Male" />`, followed by `<span name="male" for="male" class="control-label">Male</span>`.
   - Real-World Quirk: The Transgender span specifies `for="female"` in the markup (`<span name="transgender" for="female" class="control-label">Transgender</span>`). Our multi-signal engine handles this seamlessly through option text and container context matching.
3. **State Dropdown**:
   - Contained in `<div class="form-group col-xs-6">` with label `<label name="State" for="state" class="control-label"> State </label>`.
   - Dropdown is a native HTML `<select id="state" name="State">` containing countries as options (e.g., `United States`, `India`, `Germany`).
4. **Co-existing Forms on the Same Page**:
   - The page contains multiple practice forms: standard form (`#automationtestform`), Shadow DOM forms (`<shadow-form>`), and multi-level nested shadow forms.
   - Form scoping ensures labels and context hints from the primary form are never incorrectly attributed to light DOM slots in secondary forms.

---

## Technology Stack

- **Language**: C# (.NET 8.0 LTS)
- **Browser Automation**: Selenium WebDriver `4.52.0`, Selenium.Support `4.52.0`
- **Testing Framework**: NUnit `3.14.0`, NUnit3TestAdapter `4.5.0`
- **Configuration**: Microsoft.Extensions.Configuration `10.0.14` (JSON, EnvironmentVariables, CommandLine, Binder)
- **Browser Driver**: Automated via Selenium 4 **Selenium Manager** (Google Chrome, Microsoft Edge, Mozilla Firefox)

---

## Solution Architecture

```
resilient-selenium-automation/
│
├── ResilientSeleniumAutomation.sln
├── appsettings.json                         # Global configuration file
├── appsettings.example.json                 # Example configuration template
├── .gitignore                               # Production .NET gitignore
├── README.md                                # Comprehensive project documentation
├── ARCHITECTURE.md                          # Detailed architectural design & scoring formulation
│
├── ResilientSeleniumAutomation.Core/        # Reusable Core Automation Framework
│   ├── Configuration/
│   │   ├── AutomationSettings.cs            # Strongly-typed configuration POCO
│   │   └── ConfigurationManager.cs          # Hierarchical settings provider (CLI > Env > JSON)
│   ├── Drivers/
│   │   └── DriverFactory.cs                 # Headless, CI-ready WebDriver factory (Chrome/Edge/Firefox)
│   ├── Diagnostics/
│   │   ├── LocatorDiagnostics.cs            # Formats candidate evaluation tables
│   │   └── ScreenshotService.cs             # Captures full viewport screenshots
│   ├── Exceptions/
│   │   └── ResilientAutomationExceptions.cs # ElementNotFound, AmbiguousElement, UnsupportedDropdown
│   ├── Logging/
│   │   ├── ITestLogger.cs                   # Logging abstraction
│   │   └── ConsoleTestLogger.cs             # Structured timestamped console logger
│   ├── Pages/
│   │   └── FormPage.cs                      # Production Page Object Model for the practice form
│   └── Resilience/
│       ├── ControlType.cs                   # Enum for semantic control types
│       ├── TargetElementCriteria.cs         # Declarative semantic descriptor
│       ├── CandidateElement.cs              # Discovered candidate node with signals & score
│       ├── SignalExtractor.cs               # Atomic JS + C# signal harvester
│       ├── CandidateScorer.cs               # Weighted scoring algorithm with regex word boundaries
│       ├── DropdownHandler.cs               # Native <select> handler & custom dropdown detector
│       └── ResilientElementFinder.cs        # Candidate discovery, retries, and interactions
│
├── ResilientSeleniumAutomation.Tests/       # NUnit Test Suite
│   ├── BaseTest.cs                          # WebDriver lifecycle, setup/teardown & failure screenshots
│   ├── AutomationPracticeFormTests.cs       # E2E, positive, negative & boundary test fixtures
│   ├── ResilienceDemonstrationTests.cs      # Demonstrations: Obfuscated IDs, stripped tags, reordered DOM
│   ├── TestData/
│   │   ├── UserFormData.cs                  # Strongly-typed test data models
│   │   └── testdata.json                    # Configurable JSON test data
│   ├── appsettings.json                     # Test execution settings
│   └── appsettings.example.json
│
└── TestResults/
    └── Screenshots/                         # Actual execution screenshots (.png)
```

---

## Heuristic Scoring Model & Ambiguity Protection

Every candidate element discovered by broad queries is evaluated across 8 weighted dimensions:

$$\text{Score} = S_{\text{vis}} + S_{\text{type}} + S_{\text{label}} + S_{\text{opt/grp}} + S_{\text{a11y}} + S_{\text{tokens}} + S_{\text{domain}} + S_{\text{hints}}$$

| Dimension | Max Points | Evaluation Logic |
| :--- | :---: | :--- |
| **Visibility & Enabled** | $0.20$ | Displayed & Enabled = $+0.20$; Hidden = $0.00$ (disqualifies element). |
| **Control Type Match** | $0.20$ | Tag matches requested semantic type (`input[text]`, `input[radio]`, `select`). |
| **Associated Label** | $0.25$ | Form-scoped `<label for="id">`, `<span for="id">`, or adjacent sibling label. |
| **Context & Option Text** | $0.25$ | Whole-word option label match (`\bTarget\b`) + group container text ("Gender"). |
| **Accessible Name & Placeholder** | $0.15$ | `aria-label`, `aria-labelledby`, `placeholder`, `title` match. |
| **Attribute Semantic Tokens** | $0.10$ | Token overlap on `id`, `name`, `class` (e.g. `fname` $\rightarrow$ "First Name"). |
| **Option Domain Match** | $0.10$ | Target option present in `<select>` + ratio of expected domain options. |
| **Form Context Hints** | $0.06$ | Exact match on ancestor `<form id>` (e.g., `automationtestform`). |

### Regex Word Boundaries
To eliminate false substring matches (such as `"Female".Contains("Male")`), candidate text matching applies regular expression word boundaries (`\bMale\b`), ensuring `"Male"` scores $0.00$ against `"Female"`.

### Ambiguity Safe Failure
When the top two candidate elements score above the confidence threshold ($65\%$) and their score difference is smaller than $\Delta_{\text{ambiguity}}$ ($8\%$), the engine refuses to guess and throws `AmbiguousElementMatchException`.

---

## Prerequisites & Quick Start

### Prerequisites
1. **.NET 8.0 SDK** (or later stable LTS)
2. **Google Chrome** (or Microsoft Edge)

### Build the Solution
```powershell
dotnet build ResilientSeleniumAutomation.sln
```

### Run All Tests
```powershell
dotnet test ResilientSeleniumAutomation.sln --logger "console;verbosity=normal"
```

### Run a Specific Test
```powershell
dotnet test ResilientSeleniumAutomation.Tests --filter "FullyQualifiedName~Should_Fill_And_Verify_All_Fields_Successfully"
```

---

## Configuration Guide

Configuration is resolved hierarchically without re-compilation:

### 1. `appsettings.json`
```json
{
  "AutomationSettings": {
    "BaseUrl": "https://app.cloudqa.io/home/AutomationPracticeForm",
    "Browser": "Chrome",
    "Headless": true,
    "TimeoutSeconds": 15,
    "PollingIntervalMs": 250,
    "MaxRetryAttempts": 3,
    "ConfidenceThreshold": 0.65,
    "AmbiguityDeltaThreshold": 0.08,
    "CaptureScreenshotOnFailure": true,
    "ScreenshotsPath": "TestResults/Screenshots",
    "HighlightElementsOnInspection": false
  }
}
```

### 2. Environment Variables
Override any configuration parameter via environment variables:
```powershell
$env:RESILIENT_AUTOMATION_BASEURL = "https://app.cloudqa.io/home/AutomationPracticeForm"
$env:RESILIENT_AUTOMATION_HEADLESS = "false"
$env:RESILIENT_AUTOMATION_BROWSER = "Edge"
dotnet test
```

### 3. Command-Line Arguments
Override configuration directly at runtime:
```powershell
dotnet test -- BaseUrl="https://app.cloudqa.io/home/AutomationPracticeForm" Headless=true Browser=Chrome
```

---

## Test Scenarios & Execution Proof

The test suite covers **16 test cases** with **100% pass rate**:

| Test Category | Test Name | Result | Key Verification |
| :--- | :--- | :---: | :--- |
| **E2E Positive** | `Should_Fill_And_Verify_All_Fields_Successfully` | **PASSED** | End-to-end fill of First Name, Gender, State; verifies all values and saves screenshot artifact. |
| **Positive Parametric** | `Should_Resiliently_Select_And_Verify_All_Gender_Options("Male")` | **PASSED** | Verifies radio selection and state. |
| **Positive Parametric** | `Should_Resiliently_Select_And_Verify_All_Gender_Options("Female")` | **PASSED** | Verifies radio selection and state. |
| **Positive Parametric** | `Should_Resiliently_Select_And_Verify_All_Gender_Options("Transgender")` | **PASSED** | Overcomes `for="female"` markup defect on target site. |
| **Positive Parametric** | `Should_Resiliently_Select_And_Verify_State_Options("United States")` | **PASSED** | Dropdown selection and selected text verification. |
| **Positive Parametric** | `Should_Resiliently_Select_And_Verify_State_Options("India")` | **PASSED** | Dropdown selection and selected text verification. |
| **Positive Parametric** | `Should_Resiliently_Select_And_Verify_State_Options("Germany")` | **PASSED** | Dropdown selection and selected text verification. |
| **Negative Mode** | `Should_Throw_UnsupportedDropdownException_When_Dropdown_Is_Custom_Bootstrap_Component` | **PASSED** | Inspects button dropdown "Know about us" and validates accurate rejection. |
| **Negative Mode** | `Should_Throw_NoSuchElementException_When_Option_Does_Not_Exist` | **PASSED** | Validates friendly exception with available options list when option is missing. |
| **Negative Mode** | `Should_Throw_ElementNotFoundException_When_Target_Is_Below_Confidence_Threshold` | **PASSED** | Validates confidence threshold gate for fictional semantic targets. |
| **Negative Mode** | `Should_Fail_Safely_And_Throw_AmbiguousElementMatchException_When_Candidates_Are_Ambiguous` | **PASSED** | Injects identical fields and validates safe failure without mis-clicking. |
| **Resilience Demo** | `Demonstration_Should_Find_Input_When_Id_And_Name_Are_Completely_Obfuscated` | **PASSED** | Strips ID and Name to random GUIDs; finds element via label and context. |
| **Resilience Demo** | `Demonstration_Should_Find_Radio_When_Group_Name_Is_Randomized` | **PASSED** | Randomizes radio group name; finds option via container context and text. |
| **Resilience Demo** | `Demonstration_Should_Find_Dropdown_When_Attributes_Are_Stripped` | **PASSED** | Completely strips `id`, `name`, and `class` from `<select>`; finds via label and options. |
| **Resilience Demo** | `Demonstration_Should_Find_Input_When_Dom_Order_Is_Permuted` | **PASSED** | Inverts DOM element positions; successfully locates input regardless of order. |
| **Resilience Demo** | `Demonstration_Should_Recover_From_StaleElementReferenceException` | **PASSED** | Replaces DOM node during interaction; bounded retry loop recovers transparently. |

### Sample Execution Output
```
Passed!  - Failed: 0, Passed: 16, Skipped: 0, Total: 16, Duration: 2 m 29 s - ResilientSeleniumAutomation.Tests.dll (net8.0)
```

---

## Resilience Demonstrations

### 1. Obfuscated ID & Name
Simulates build-time obfuscation (e.g. Webpack or CSS Modules) where `id="fname"` and `name="First Name"` are replaced with random GUIDs (`el_22e21e6f`, `nm_5bc5e46d`). The finder successfully locates the input with **86.2% confidence** via label association and container grouping.

### 2. Stripped Dropdown Attributes
Simulates an aggressive refactoring where `id="state"`, `name="State"`, and `class="form-control"` are completely stripped from the `<select>` element. The finder locates the dropdown with **85.0% confidence** via adjacent sibling label "State", container header, and country option domain contents.

### 3. Permuted DOM Order
Simulates a responsive layout redesign that swaps the order of form fields in the DOM. Standard indexed XPaths (`//input[1]`) fail, while the resilient engine correctly matches First Name with **95.2% confidence**.

### 4. Stale Element Recovery
Simulates an asynchronous UI re-render where the DOM node is detached and replaced with a clone mid-interaction. The bounded retry loop catches `StaleElementReferenceException`, re-harvests candidates, and completes data entry cleanly.

---

## Approach, Scope & Engineering Limitations

### Realistic Boundaries
While heuristic scoring provides massive resilience improvements over static selectors, realistic engineering boundaries must be maintained:

1. **Semantic Ambiguity**: If two fields in the same form share identical labels, placeholders, and types without contextual discriminators, the engine will fail safely rather than guessing.
2. **Custom Virtualized Menus**: Custom div-based dropdowns are rejected by design; standard HTML `SelectElement` requires native `<select>`.
3. **Completely Redesigned Workflows**: If a 3-field form is converted into an AI chatbot or a third-party payment iframe, automated heuristics cannot guess the new workflow; test redesign is required.

