using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;
using KuduVersionCheck.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KuduVersionCheck.Controllers
{
    public class HomeController : AsyncController
    {
        public async Task<ActionResult> Index(string mode)
        {
            // Get the secret key that shows the Kudu urls
            ViewBag.ShowConsole = (mode == ConfigurationManager.AppSettings["ScmMode"]);

            return View(await GetStampEntriesAsync());
        }

        private async Task<IEnumerable<StampEntry>> GetStampEntriesAsync()
        {
            return await Task.WhenAll(GetDeployUrls().Select(url => GetStampEntryAsync(url)));
        }

        private async Task<StampEntry> GetStampEntryAsync(string url)
        {
            var uri = new Uri(url);

            var entry = new StampEntry();

            // e.g. kudu-blu-001.scm.azurewebsites.net --> blu-001
            entry.Name = uri.Host.Split('.')[0].Substring(5);

            entry.TestSiteUrl = String.Format("http://kudu-{0}.azurewebsites.net/", entry.Name);

            entry.DataString = await RequestSiteContent(entry.TestSiteUrl);

            entry.ConsoleUrl = url.Replace("/deploy", "/DebugConsole");

            return entry;
        }

        private async Task<string> RequestSiteContent(string testSite)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(testSite);
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception e)
                {
                    if (e.InnerException != null)
                    {
                        e = e.InnerException;
                    }

                    return String.Format("{{ error={0} }}", e.Message);
                }
            }
        }

        private IEnumerable<string> GetDeployUrls()
        {
            string hookPath = Environment.ExpandEnvironmentVariables(@"%HOME%\site\deployments\hooks");
            if (!System.IO.File.Exists(hookPath))
            {
                hookPath = Server.MapPath("~/App_Data/hooks");
                if (!System.IO.File.Exists(hookPath))
                {
                    throw new Exception("Can't find hooks file " + hookPath);
                }
            }

            string hooksContent = System.IO.File.ReadAllText(hookPath);
            JArray hooks = JArray.Parse(hooksContent);
            return hooks.Select(hook => hook.SelectToken("url").ToString());
        }
    }
}
