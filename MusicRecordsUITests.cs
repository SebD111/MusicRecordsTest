using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace MusicRecords.UITests;

public class MusicRecordsUITests : IDisposable
{
    private static readonly string[] AllTitles = ["Ta på mine frugter", "Citrusdrømme"];
    private readonly IWebDriver _driver;
    private readonly WebDriverWait _wait;
    private readonly string _url = Environment.GetEnvironmentVariable("MUSIC_RECORDS_UI_URL")
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
                    && expectedTitles.All(title => titles.Contains(title, StringComparer.Ordinal))
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

        // Observe the loading transition so unchanged results cannot satisfy a new request.
        scriptDriver.ExecuteScript("""
            const button = document.getElementById('searchButton');
            window.musicRecordsRequestCompleted = false;
            let started = false;
            const observer = new MutationObserver(mutations => {
                started = started || button.disabled || mutations.some(mutation => mutation.oldValue === null);
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

        Assert.Equal(string.Empty, _driver.FindElement(By.Id("titleFilter")).GetAttribute("value"));
        Assert.Equal(string.Empty, _driver.FindElement(By.Id("artistFilter")).GetAttribute("value"));
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