using System;
using System.Collections.Generic;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace MusicRecords.UITests;

[TestCaseOrderer(
    "MusicRecords.UITests.MusicRecordsTestCaseOrderer",
    "MusicRecordsTest")]
public class MusicRecordsUITests : IDisposable
{
    private static readonly string[] AllTitles =
        ["Ta på mine frugter", "Citrusdrømme"];

    private readonly IWebDriver _driver;
    private readonly WebDriverWait _wait;
    private readonly string _url =
        Environment.GetEnvironmentVariable("MUSIC_RECORDS_UI_URL")
        ?? "http://127.0.0.1:5500/Main.html";

    public MusicRecordsUITests()
    {
        var options = new ChromeOptions();

        _driver = new ChromeDriver(options);
        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void RecordsTable_DisplaysAllRecords_FromBackend()
    {
        OpenRecordsPage();
    }

    [Fact]
    public void Search_ByTitle_DisplaysOnlyMatchingRecord()
    {
        OpenRecordsPage();
        SetFilters("Citrus", string.Empty);

        ClickAndWaitForRequest("searchButton");

        AssertRecords("Citrusdrømme");
    }

    [Fact]
    public void Search_ByArtist_DisplaysBothRecords()
    {
        OpenRecordsPage();
        SetFilters(string.Empty, "Appelsin");

        ClickAndWaitForRequest("searchButton");

        AssertRecords(AllTitles);
    }

    [Fact]
    public void Search_ByTitleAndArtist_CombinesFiltersWithAnd()
    {
        OpenRecordsPage();
        SetFilters("Citrus", "Ukendt");

        ClickAndWaitForRequest("searchButton");

        AssertNoResults();
    }

    [Fact]
    public void Search_ByUnknownTitle_DisplaysNoResultsMessage()
    {
        OpenRecordsPage();
        SetFilters("Ukendt", string.Empty);

        ClickAndWaitForRequest("searchButton");

        AssertNoResults();
    }

    [Fact]
    public void Reset_AfterSearch_ClearsBothFiltersAndDisplaysAllRecords()
    {
        OpenRecordsPage();
        SetFilters("Citrus", "Appelsin");
        ClickAndWaitForRequest("searchButton");
        AssertRecords("Citrusdrømme");

        ClickAndWaitForRequest("resetButton");

        AssertFiltersCleared();
        AssertRecords(AllTitles);
    }

    [Fact]
    public void Login_WithValidCredentials_DisplaysUsernameAndRole()
    {
        OpenRecordsPage();

        var username = GetRequiredSetting("MUSIC_RECORDS_TEST_USERNAME");
        var password = GetRequiredSetting("MUSIC_RECORDS_TEST_PASSWORD");

        LoginWith(username, password);

        Assert.Equal(username, _driver.FindElement(By.Id("currentUsername")).Text);

        var role = _driver.FindElement(By.Id("currentRole")).Text;
        Assert.False(string.IsNullOrWhiteSpace(role));

        var expectedRole = Environment.GetEnvironmentVariable("MUSIC_RECORDS_TEST_ROLE");
        if (!string.IsNullOrWhiteSpace(expectedRole))
        {
            Assert.Equal(expectedRole, role);
        }
    }

    [Fact]
    public void Login_WithIncorrectPassword_DisplaysErrorMessage()
    {
        OpenRecordsPage();

        _driver.FindElement(By.Id("username"))
            .SendKeys(GetRequiredSetting("MUSIC_RECORDS_TEST_USERNAME"));
        _driver.FindElement(By.Id("password"))
            .SendKeys("invalid-password-for-ui-test");
        _driver.FindElement(By.Id("loginButton")).Click();

        var error = _wait.Until(driver =>
        {
            var element = driver.FindElements(By.Id("loginError")).SingleOrDefault();
            return element is { Displayed: true } ? element : null;
        });

        Assert.NotNull(error);
        Assert.Contains("Forkert brugernavn eller adgangskode.", error!.Text);
        Assert.True(_driver.FindElement(By.Id("loginButton")).Displayed);
    }

    [Fact]
    public void Logout_ClearsLoginStatus_AndKeepsRecordsVisible()
    {
        OpenRecordsPage();

        LoginWith(
            GetRequiredSetting("MUSIC_RECORDS_TEST_USERNAME"),
            GetRequiredSetting("MUSIC_RECORDS_TEST_PASSWORD"));

        _driver.FindElement(By.Id("logoutButton")).Click();

        _wait.Until(driver =>
            driver.FindElements(By.Id("loginStatus")).Count == 0
            && driver.FindElements(By.Id("loginButton")).Any(element => element.Displayed));

        AssertRecords(AllTitles);
    }

    [Fact]
    public void AddRecord_AsAdmin_DisplaysNewRecord()
    {
        OpenRecordsPage();

        LoginWith(
            GetRequiredSetting("MUSIC_RECORDS_TEST_USERNAME"),
            GetRequiredSetting("MUSIC_RECORDS_TEST_PASSWORD"));

        Assert.Equal("Admin", _driver.FindElement(By.Id("currentRole")).Text);

        var title = $"Selenium-{Guid.NewGuid():N}";
        const string artist = "Selenium test";
        const string duration = "234";
        var year = DateTime.UtcNow.Year.ToString();

        _driver.FindElement(By.Id("recordTitle")).SendKeys(title);
        _driver.FindElement(By.Id("recordArtist")).SendKeys(artist);
        _driver.FindElement(By.Id("recordDuration")).SendKeys(duration);

        var yearInput = _driver.FindElement(By.Id("recordPublicationYear"));
        yearInput.Clear();
        yearInput.SendKeys(year);

        _driver.FindElement(By.Id("createRecordButton")).Click();

        _wait.Until(driver =>
            driver.FindElements(By.Id("recordSuccess")).Any(element => element.Displayed)
            || driver.FindElements(By.Id("recordError")).Any(element => element.Displayed));

        var success = _driver.FindElements(By.Id("recordSuccess"))
            .SingleOrDefault(element => element.Displayed);
        var error = _driver.FindElements(By.Id("recordError"))
            .SingleOrDefault(element => element.Displayed);

        Assert.True(success is not null, error?.Text ?? "Oprettelsen blev ikke bekræftet.");
        Assert.Contains(title, success!.Text);

        var row = _wait.Until(driver =>
            driver.FindElements(By.CssSelector("#recordsTable tbody tr"))
                .FirstOrDefault(candidate =>
                    candidate.FindElements(By.TagName("td"))
                        .FirstOrDefault()?.Text == title));

        Assert.NotNull(row);

        var cells = row!.FindElements(By.TagName("td"));
        Assert.Equal(title, cells[0].Text);
        Assert.Equal(artist, cells[1].Text);
        Assert.Equal(duration, cells[2].Text);
        Assert.Equal(year, cells[3].Text);
    }

    private void OpenRecordsPage()
    {
        _driver.Navigate().GoToUrl(_url);
        AssertRecords(AllTitles);
    }

    private bool IsUiIdle(IWebDriver driver)
    {
        var searchButton = driver.FindElements(By.Id("searchButton")).SingleOrDefault();
        var resetButton = driver.FindElements(By.Id("resetButton")).SingleOrDefault();

        return searchButton is { Displayed: true, Enabled: true }
            && resetButton is { Displayed: true, Enabled: true }
            && !driver.FindElements(By.CssSelector("#app > [role='status']:not(#noResults)"))
                .Any(element => element.Displayed)
            && !driver.FindElements(By.CssSelector("#app [role='alert']"))
                .Any(element => element.Displayed);
    }

    private void AssertRecords(params string[] expectedTitles)
    {
        var rows = _wait.Until(driver =>
        {
            try
            {
                if (!IsUiIdle(driver))
                {
                    return null;
                }

                var titles = driver.FindElements(By.CssSelector("#recordsTable tbody tr"))
                    .Select(row => row.FindElement(By.CssSelector("td:first-child")).Text)
                    .ToArray();

                return titles.Length == expectedTitles.Length
                    && expectedTitles.All(title =>
                        titles.Contains(title, StringComparer.Ordinal))
                    ? titles
                    : null;
            }
            catch (StaleElementReferenceException)
            {
                return null;
            }
        });

        Assert.NotNull(rows);
        Assert.Equal(expectedTitles.Length, rows.Length);

        foreach (var title in expectedTitles)
        {
            Assert.Contains(title, rows);
        }
    }

    private void SetFilters(string title, string artist)
    {
        var titleFilter = _driver.FindElement(By.Id("titleFilter"));
        titleFilter.Clear();
        titleFilter.SendKeys(title);

        var artistFilter = _driver.FindElement(By.Id("artistFilter"));
        artistFilter.Clear();
        artistFilter.SendKeys(artist);
    }

    private void ClickAndWaitForRequest(string buttonId)
    {
        var scriptDriver = (IJavaScriptExecutor)_driver;

        scriptDriver.ExecuteScript("""
            const button = document.getElementById('searchButton');
            window.musicRecordsRequestCompleted = false;
            let started = false;
            const observer = new MutationObserver(mutations => {
                started = started
                    || button.disabled
                    || mutations.some(mutation => mutation.oldValue === null);

                if (started && !button.disabled) {
                    window.musicRecordsRequestCompleted = true;
                    observer.disconnect();
                }
            });

            observer.observe(button, {
                attributes: true,
                attributeFilter: ['disabled'],
                attributeOldValue: true
            });
            """);

        _driver.FindElement(By.Id(buttonId)).Click();

        _wait.Until(driver => (bool)((IJavaScriptExecutor)driver)
            .ExecuteScript("return window.musicRecordsRequestCompleted === true;"));
    }

    private void AssertNoResults()
    {
        var message = _wait.Until(driver =>
        {
            try
            {
                var element = driver.FindElements(By.Id("noResults")).SingleOrDefault();

                return IsUiIdle(driver)
                    && driver.FindElements(By.CssSelector("#recordsTable tbody tr")).Count == 0
                    && element is { Displayed: true }
                    && element.Text == "Ingen musikplader fundet"
                    ? element
                    : null;
            }
            catch (StaleElementReferenceException)
            {
                return null;
            }
        });

        Assert.NotNull(message);
        Assert.True(message.Displayed);
        Assert.Equal("Ingen musikplader fundet", message.Text);
        Assert.Empty(_driver.FindElements(By.CssSelector("#recordsTable tbody tr")));
    }

    private void AssertFiltersCleared()
    {
        _wait.Until(driver =>
            driver.FindElement(By.Id("titleFilter")).GetAttribute("value") == string.Empty
            && driver.FindElement(By.Id("artistFilter")).GetAttribute("value") == string.Empty);

        Assert.Equal(
            string.Empty,
            _driver.FindElement(By.Id("titleFilter")).GetAttribute("value"));
        Assert.Equal(
            string.Empty,
            _driver.FindElement(By.Id("artistFilter")).GetAttribute("value"));
    }

    private void LoginWith(string username, string password)
    {
        _driver.FindElement(By.Id("username")).SendKeys(username);
        _driver.FindElement(By.Id("password")).SendKeys(password);
        _driver.FindElement(By.Id("loginButton")).Click();

        _wait.Until(driver =>
            driver.FindElements(By.Id("loginStatus")).Any(element => element.Displayed));
    }

    private static string GetRequiredSetting(string name)
    {
        return Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException(
                $"Angiv miljøvariablen {name} lokalt, før Selenium-testene køres.");
    }

    public void Dispose()
    {
        try
        {
            _driver.Quit();
        }
        finally
        {
            _driver.Dispose();
        }
    }
}

public sealed class MusicRecordsTestCaseOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(
        IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        return testCases
            .OrderBy(testCase => GetOrder(testCase.TestMethod.Method.Name))
            .ThenBy(
                testCase => testCase.TestMethod.Method.Name,
                StringComparer.Ordinal);
    }

    private static int GetOrder(string testName)
    {
        if (testName == nameof(
            MusicRecordsUITests.RecordsTable_DisplaysAllRecords_FromBackend))
        {
            return 0;
        }

        if (testName == nameof(
            MusicRecordsUITests.AddRecord_AsAdmin_DisplaysNewRecord))
        {
            return 2;
        }

        return 1;
    }
}