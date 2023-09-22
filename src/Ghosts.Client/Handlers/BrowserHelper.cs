
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Actions = OpenQA.Selenium.Interactions.Actions;
using Exception = System.Exception;
using NLog;
using System.Web;
using System.Collections.Generic;


namespace Ghosts.Client.Handlers
{

    
    public abstract class BrowserHelper
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        internal static readonly Random _random = new Random();
        public BaseBrowserHandler baseHandler = null;
        public IWebDriver Driver = null;


        /// <summary>
        /// This is used when the element being moved to may be out of the viewport (ie, at the bottom of the page).
        /// Chrome handles this OK, but Firefox throws an exception, have to manually
        /// scroll to ensure the element is in view
        /// </summary>
        /// <param name="targetElement"></param>
        public void MoveToElementAndClick(IWebElement targetElement)
        {
            BrowserHelperSupport.MoveToElementAndClick(Driver, targetElement);
        }

        public static List<string> GetRandomFiles(string targetDir, string pattern, int count, int maxSize)
        {
            try
            {
                while (true)
                {
                    if (count == 0) return null;
                    //divide maxSize by count so that there is no possibility of total attachment size exceeding maxSize
                    long maxSizeBytes = (maxSize * 1024 * 1024) / count; //maxSize is in MB
                    string[] filelist = Directory.GetFiles(targetDir, pattern);
                    if (filelist.Length == 0) return null;
                    //filter files by maxSizeBytes
                    List<string> filteredFiles = new List<string>();
                    foreach (string file in filelist)
                    {
                        try
                        {
                            FileInfo info = new FileInfo(file);
                            if (info.Length <= maxSizeBytes)
                            {
                                filteredFiles.Add(file);
                            }
                    
                        }
                        catch (ThreadAbortException)
                        {
                            throw;  //pass up
                        }
                        catch (Exception e)
                        {
                            Log.Error($"File access error: {e}");
                        }
                    }
                    if (filteredFiles.Count == 0) return null;         
                    
                    if (count == 1)
                    {
                        return new List<string>() { filteredFiles[_random.Next(0, filteredFiles.Count)] };
                    }
                    // need more than one, have to avoid duplicates, prune down
                    while (true)
                    {
                        if (filteredFiles.Count <= count) break;
                        var index = _random.Next(0, filteredFiles.Count);
                        filteredFiles.RemoveAt(index);
                    }

                    return filteredFiles;
                }
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch {
                //ignore others
            }
            return null;
        }
    
    

    }

    public class BrowserHelperSupport
    {
        public static void ElementClick(IWebDriver Driver, IWebElement targetElement)
        {
            Actions actions;
            actions = new Actions(Driver);
            actions.MoveToElement(targetElement).Click().Perform();
        }

        public static void MoveToElementAndContextMenu(IWebDriver Driver, IWebElement targetElement)
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
            actions.MoveToElement(targetElement).ContextClick().Perform();
        }


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

        public static void FirefoxHandleInsecureCertificate(IWebDriver Driver)
        {
            //look for security override
            IWebElement targetElement;
            try
            {
                Thread.Sleep(500);
                targetElement = Driver.FindElement(By.Id("advancedButton"));
                MoveToElementAndClick(Driver, targetElement); //click advanced 
                Thread.Sleep(500);
            }
            catch
            {
                return; //return if not present
            }
            try { 
                targetElement = Driver.FindElement(By.Id("exceptionDialogButton"));  
                MoveToElementAndClick(Driver, targetElement);   //accept risk and continue
                Thread.Sleep(1000);
                return;
            }
            catch { }
            //look for return button
            try
            {
                targetElement = Driver.FindElement(By.Id("advancedPanelReturnButton"));
                MoveToElementAndClick(Driver, targetElement);   //return, cannot continue
                Thread.Sleep(1000);
                
            }
            catch { }

        }

    }

    }