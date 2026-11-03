using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace MusicRecords.UITests;

public class MusicRecordsUITests : IDisposable
{
    private readonly IWebDriver _driver;
    private readonly WebDriverWait _wait;
    private const string Url = "http://127.0.0.1:5500/Main.html";

    public MusicRecordsUITests()
    {
        var options = new ChromeOptions();

        _driver = new ChromeDriver(options);

        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordsTable_DisplaysAllRecords_FromBackend()
    {
        _driver.Navigate().GoToUrl(Url);

        var rows = _wait.Until(driver =>
        {
            var elements = driver.FindElements(By.CssSelector("table tbody tr"));
            return elements.Count > 0 ? elements : null;
        });

        Assert.NotNull(rows);
        Assert.Equal(2, rows.Count);

        string pageSource = _driver.PageSource;
        Assert.Contains("Ta på mine frugter", pageSource);
        Assert.Contains("Citrusdrømme", pageSource);
    }

    public void Dispose()
    {
        _driver.Quit();
        _driver.Dispose();
    }
}