using Ghosts.Domain;
using Ghosts.Domain.Code;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using Actions = OpenQA.Selenium.Interactions.Actions;

namespace Ghosts.Client.Handlers
{
    internal class BlogHelperDrupal
    {





        /// <summary>
        /// Login into the Drupal site
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="baseHelper"></param>
        /// <param name="header"></param>
        /// <param name="user"></param>
        /// <param name="pw"></param>
        /// <returns></returns>
        public static bool DoInitialLogin(TimelineHandler handler, BlogHelper baseHelper, string header, string user, string pw)
        {
            //have the username, password
            string portal = baseHelper.site;
            RequestConfiguration config;
            Actions actions;


            //for drupal, cannot pass user/pw in the url

            string target = header + portal + "/";
            //navigate to the base site first

            try
            {
                config = RequestConfiguration.Load(handler, target);
                baseHelper.baseHandler.MakeRequest(config);
            }
            catch (System.Exception e)
            {
                baseHelper.baseHandler.DoLogTrace($"Blog:: Unable to parse site {target}, url may be malformed. Blog browser action will not be executed.");
                baseHelper.baseHandler.DoLogError(e);
                return false;
            }
            //now login 
            try
            {
                var targetElement = baseHelper.baseHandler.Driver.FindElement(By.CssSelector("input#edit-name.form-text.required"));
                targetElement.SendKeys(user);
                Thread.Sleep(500);
                targetElement = baseHelper.baseHandler.Driver.FindElement(By.CssSelector("input#edit-pass.form-text.required"));
                targetElement.SendKeys(pw);
                Thread.Sleep(500);
                targetElement = baseHelper.baseHandler.Driver.FindElement(By.CssSelector("input#edit-submit.form-submit"));
                actions = new Actions(baseHelper.baseHandler.Driver);
                actions.MoveToElement(targetElement).Click().Perform();
                Thread.Sleep(500);
                //check if login was successful
            }
            catch (System.Exception e)
            {
                baseHelper.baseHandler.DoLogTrace($"Blog:: Unable to login into site {target}, check username/password. Blog browser action will not be executed.");
                baseHelper.baseHandler.DoLogError(e);
                return false;
            }

            //check login success
            try
            {
                var targetElement = baseHelper.baseHandler.Driver.FindElement(By.CssSelector("div[class='messages error']"));
                //if reach here, login was unsuccessful
                baseHelper.baseHandler.DoLogTrace($"Blog:: Unable to login into site {target}, check username/password. Blog browser action will not be executed.");
                return false;
            }
            catch
            {
                //ignore
            }



            return true;
        }

        /// <summary>
        /// Browse to an existing blog entry
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="baseHelper"></param>
        /// <returns></returns>
        public static bool DoBrowse(TimelineHandler handler, BlogHelper baseHelper)
        {
            Actions actions;

            // first check if we are looking at a blog entry
            try
            {
                var targetElement = baseHelper.baseHandler.Driver.FindElement(By.CssSelector("li.blog_usernames_blog"));
                //if get here, need to go the page that has all of the entries
                actions = new Actions(baseHelper.baseHandler.Driver);
                actions.MoveToElement(targetElement).Click().Perform();
                Thread.Sleep(1000);
            }
            catch
            {

            }

            try
            {
                var targetElements = baseHelper.baseHandler.Driver.FindElements(By.CssSelector("li.node-readmore"));
                if (targetElements.Count > 0)
                {
                    int docNum = baseHelper.baseHandler.DoRandomNext(0, targetElements.Count);
                    actions = new Actions(baseHelper.baseHandler.Driver);
                    actions.MoveToElement(targetElements[docNum]).Click().Perform();
                    return true;
                }
            }
            catch
            {

            }
            baseHelper.baseHandler.DoLogTrace($"Blog:: No articles to browse on site {baseHelper.site}.");
            return true;
        }
    }
}
