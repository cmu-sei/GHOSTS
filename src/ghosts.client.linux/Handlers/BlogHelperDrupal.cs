using System;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using OpenQA.Selenium;
using Actions = OpenQA.Selenium.Interactions.Actions;

namespace ghosts.client.linux.handlers
{

    public class BlogHelperDrupal : BlogHelper
    {

        public BlogHelperDrupal(BaseBrowserHandler callingHandler, IWebDriver callingDriver)
        {
            base.Init(callingHandler, callingDriver);

        }

        /// <summary>
        /// Login into the Drupal site
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="baseHelper"></param>
        /// <param name="header"></param>
        /// <param name="user"></param>
        /// <param name="pw"></param>
        /// <returns></returns>
        public override bool DoInitialLogin(TimelineHandler handler, string user, string pw)
        {
            //have the username, password

            RequestConfiguration config;
            Actions actions;


            //for drupal, cannot pass user/pw in the url

            var target = header + site + "/";
            //navigate to the base site first

            try
            {
                config = RequestConfiguration.Load(handler, target);
                baseHandler.MakeRequest(config);
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (System.Exception e)
            {
                Log.Trace($"Blog:: Unable to parse site {target}, url may be malformed. Blog browser action will not be executed.");
                Log.Error(e);
                return false;
            }
            //now login 
            try
            {
                var targetElement = Driver.FindElement(By.CssSelector("input#edit-name.form-text.required"));
                targetElement.SendKeys(user);
                Thread.Sleep(500);
                targetElement = Driver.FindElement(By.CssSelector("input#edit-pass.form-text.required"));
                targetElement.SendKeys(pw);
                Thread.Sleep(500);
                targetElement = Driver.FindElement(By.CssSelector("input#edit-submit.form-submit"));
                actions = new Actions(Driver);
                actions.MoveToElement(targetElement).Click().Perform();
                Thread.Sleep(500);
                //check if login was successful
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (System.Exception e)
            {
                Log.Trace($"Blog:: Unable to login into site {target}, check username/password. Blog browser action will not be executed.");
                Log.Error(e);
                return false;
            }

            //check login success
            try
            {
                var targetElement = Driver.FindElement(By.CssSelector("div[class='messages error']"));
                //if reach here, login was unsuccessful
                Log.Trace($"Blog:: Unable to login into site {target}, check username/password. Blog browser action will not be executed.");
                return false;
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch
            {
                //ignore
            }

            return true;
        }
        public override bool DoReply(TimelineHandler handler, string reply)
        {

            //first, browse to an existing entry
            if (DoBrowse(handler))
            {
                try
                {

                    var targetElement = Driver.FindElement(By.CssSelector("textarea#edit-comment-body-und-0-value.text-full.form-textarea.required"));
                    targetElement.SendKeys(reply);
                    Thread.Sleep(1000);
                    targetElement = Driver.FindElement(By.CssSelector("input#edit-submit.form-submit"));
                    MoveToElementAndClick(targetElement);
                    Log.Trace($"Blog:: Added reply to site {site}.");
                }
                catch (ThreadAbortException)
                {
                    throw;  //pass up
                }
                catch
                {
                    return true;
                }

            }
            return true;
        }


        /// <summary>
        /// Upload a blog entry
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="baseHelper"></param>
        /// <returns></returns>
        public override bool DoUpload(TimelineHandler handler, string subject, string body)
        {

            RequestConfiguration config;

            var target = header + site + "/node/add/blog";
            //navigate to the add content page
            try
            {
                config = RequestConfiguration.Load(handler, target);
                baseHandler.MakeRequest(config);
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (System.Exception e)
            {
                Log.Trace($"Blog:: Unable to navigate to {target} while adding content.");
                Log.Error(e);
                return true;  //dont abort handler  because of this
            }

            //add subject,body
            try
            {
                var targetElement = Driver.FindElement(By.CssSelector("input#edit-title.form-text.required"));
                targetElement.SendKeys(subject);
                targetElement = Driver.FindElement(By.CssSelector("textarea#edit-body-und-0-value"));
                targetElement.SendKeys(body);
                Thread.Sleep(1000);
                targetElement = Driver.FindElement(By.CssSelector("input#edit-submit.form-submit"));
                MoveToElementAndClick(targetElement);
                Log.Trace($"Blog:: Added post to site {site}.");

            }
            catch (Exception e)
            {
                Log.Trace($"Blog:: Error while posting content to site {site}.");
                Log.Error(e);
            }


            return true;
        }

        /// <summary>
        /// Delete an existing blog entry
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="baseHelper"></param>
        /// <returns></returns>
        public override bool DoDelete(TimelineHandler handler)
        {
            Actions actions;
            //first, browse to an existing entry
            if (DoBrowse(handler))
            {
                try
                {
                    //successfully browsed to an entry and in view mode
                    var targetElement = Driver.FindElement(By.CssSelector("ul.tabs.primary"));
                    var tabLinks = targetElement.FindElements(By.XPath(".//a"));
                    if (tabLinks.Count > 0)
                    {

                        foreach (var link in tabLinks)
                        {
                            var hrefValue = link.GetAttribute("href");
                            if (hrefValue.Contains("edit"))
                            {
                                //click the Edit tab
                                actions = new Actions(Driver);
                                actions.MoveToElement(link).Click().Perform();
                                Thread.Sleep(2000);
                                //edit is in an overlay, that contains an iframe
                                var overlay = Driver.FindElement(By.Id("overlay-container"));
                                var iframe = overlay.FindElement(By.CssSelector("iframe.overlay-element.overlay-active"));
                                Driver.SwitchTo().Frame(iframe);
                                targetElement = Driver.FindElement(By.CssSelector("input#edit-delete.form-submit"));
                                MoveToElementAndClick(targetElement);
                                Thread.Sleep(1000);
                                //another overlay pops up
                                overlay = Driver.FindElement(By.Id("overlay"));
                                //this overlay does not have an iframe
                                targetElement = overlay.FindElement(By.CssSelector("input#edit-submit.form-submit"));
                                //press delete button
                                actions = new Actions(Driver);
                                actions.MoveToElement(targetElement).Click().Perform();
                                Thread.Sleep(1000);
                                Log.Trace($"Blog:: Deleted post from site {site}.");


                            }

                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    throw;  //pass up
                }
                catch
                {
                    return true;
                }

            }
            return true;
        }


        /// <summary>
        /// Browse to an existing blog entry
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="baseHelper"></param>
        /// <returns></returns>
        public override bool DoBrowse(TimelineHandler handler)
        {
            Actions actions;
            RequestConfiguration config;
            var target = header + site + "/blog";
            //navigate to the blog page
            try
            {
                config = RequestConfiguration.Load(handler, target);
                baseHandler.MakeRequest(config);
                Thread.Sleep(1000);
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch (System.Exception e)
            {
                Log.Trace($"Blog:: Unable to navigate to {target} while browsing.");
                Log.Error(e);
                return true;  //dont abort handler  because of this
            }

            //first pick a page if there are multiple pages
            var onTargetPage = false;
            try
            {
                //first, check if there is a link to a last page
                var targetElement = Driver.FindElement(By.CssSelector("li.pager-last.last"));
                // if get there, then there is a last page element. Get the link to it and parse out the last page number
                var pageLink = targetElement.FindElement(By.XPath(".//a"));
                var href = pageLink.GetAttribute("href");
                var charSeparators = new char[] { '=' };
                var words = href.Split(charSeparators, 2, StringSplitOptions.None);
                if (words.Length == 2)
                {
                    var pageNumString = words[1];
                    if (int.TryParse(pageNumString, out var lastpage))
                    {
                        var pageNum = _random.Next(0, lastpage + 1);
                        var targetPage = header + site + $"/node?page={pageNum}";
                        config = RequestConfiguration.Load(handler, targetPage);
                        baseHandler.MakeRequest(config);
                        onTargetPage = true;
                        Thread.Sleep(1000);
                    }
                }


            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch { }

            if (!onTargetPage)
            {
                //unable to navigate to a target page, perhaps there was not a last page link
                //just pick a random page from the li.pager-items at bottom
                try
                {

                    var targetElements = Driver.FindElements(By.CssSelector("li.pager-item"));
                    if (targetElements.Count > 0)
                    {
                        var pageNum = _random.Next(0, targetElements.Count + 1);
                        if (pageNum != targetElements.Count)
                        {
                            //pick a different page
                            actions = new Actions(Driver);
                            actions.MoveToElement(targetElements[pageNum]).Click().Perform();
                            Thread.Sleep(1000);

                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    throw;  //pass up
                }
                catch
                { }
            }

            //on some page, click a random readmore link
            try
            {
                var targetElements = Driver.FindElements(By.CssSelector("li.node-readmore.first"));

                if (targetElements.Count > 0)
                {
                    var docNum = _random.Next(0, targetElements.Count);
                    var targetElement = targetElements[docNum];
                    var pageLink = targetElement.FindElement(By.XPath(".//a"));
                    var href = pageLink.GetAttribute("href");
                    if (href != null)
                    {
                        //string targetpost = header + site + href;
                        config = RequestConfiguration.Load(handler, href);
                        baseHandler.MakeRequest(config);
                        Thread.Sleep(1000);
                        Log.Trace($"Blog:: Browsed post on site {site}.");
                        return true;
                    }
                }
            }
            catch (ThreadAbortException)
            {
                throw;  //pass up
            }
            catch { }
            Log.Trace($"Blog:: No articles to browse on site {site}.");
            return true;
        }
    }


}
