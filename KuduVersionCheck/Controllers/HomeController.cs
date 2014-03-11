using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
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
            var viewModel = new StampEntriesViewModel();

            // Get the secret key that shows the Kudu urls
            viewModel.ShowConsole = (mode == ConfigurationManager.AppSettings["ScmMode"]);

            IEnumerable<StampEntry> stampEntries = await GetStampEntriesAsync();
            viewModel.Groups = stampEntries.GroupBy(e => e.Environment);

            // Find the first non-error entry to get the columns
            var entry = stampEntries.FirstOrDefault(e => !e.Data.ContainsKey("Error"));

            viewModel.Columns = entry.Data.Keys;

            return View(viewModel);
        }

        private async Task<IEnumerable<StampEntry>> GetStampEntriesAsync()
        {
            return await Task.WhenAll(GetDeployUrls().Select(url => GetStampEntryAsync(url)));
        }

        private async Task<StampEntry> GetStampEntryAsync(string url)
        {
            var uri = new Uri(url);

            var entry = new StampEntry();

            // waws-prod-blu-001.cloudapp.net --> blu-001
            IPHostEntry host = Dns.GetHostEntry(uri.Host);
            string subDomain = host.HostName.Split('.')[0];
            entry.Name = subDomain.Substring(subDomain.Length - 7);
            entry.Environment = subDomain.Substring(0, subDomain.Length - 8);

            // Make sure it matches the test site name
            // e.g. kudu-blu-001.scm.azurewebsites.net --> blu-001
            string expectedName = uri.Host.Split('.')[0].Substring(5);
            if (entry.Name != expectedName) entry.Mismatch = true;

            // Yank the .scm token
            string siteHostName = uri.Host.Replace(".scm.", ".");
            entry.TestSiteUrl = String.Format("http://{0}/", siteHostName);

            entry.Data = await RequestSiteData(entry.TestSiteUrl);

            entry.ConsoleUrl = url.Replace("/deploy", "/DebugConsole");

            return entry;
        }

        private async Task<IDictionary<string,string>> RequestSiteData(string testSite)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(testSite);
                    string dataString = await response.Content.ReadAsStringAsync();

                    IDictionary<string, JToken> token = JObject.Parse(dataString);

                    return token.ToDictionary(entry => entry.Key, entry => entry.Value.ToString());
                }
                catch (Exception e)
                {
                    if (e.InnerException != null)
                    {
                        e = e.InnerException;
                    }

                    return new Dictionary<string, string>() { { "Error", e.Message } };
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
