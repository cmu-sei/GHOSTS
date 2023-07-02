using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using System.Collections.Generic;
using System.Linq;

namespace ghosts.client.linux.handlers
{
    public class BrowserHelperSupport
    {
        public static void MoveToElementAndClick(IWebDriver Driver, IWebElement targetElement)
        {
            Actions actions;

            if (Driver is OpenQA.Selenium.Firefox.FirefoxDriver)
            {
                IJavaScriptExecutor je = (IJavaScriptExecutor)Driver;
                //be safe and scroll to element
                je.ExecuteScript("arguments[0].scrollIntoView()", targetElement);
                Thread.Sleep(500);
            }
            actions = new Actions(Driver);
            actions.MoveToElement(targetElement).Click().Perform();
        }

    }

}

