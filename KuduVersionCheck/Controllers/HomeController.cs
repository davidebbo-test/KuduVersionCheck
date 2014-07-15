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

            IEnumerable<StampEntry> stampEntries = ApplyStyle(await GetStampEntriesAsync());
            viewModel.Groups = stampEntries.GroupBy(e => e.Environment);

            // Find the first non-error entry to get the columns
            var entry = stampEntries.FirstOrDefault(e => !e.Data.ContainsKey("Error"));

            viewModel.Columns = entry.Data.Keys;

            return View(viewModel);
        }

        private async Task<IEnumerable<StampEntry>> GetStampEntriesAsync()
        {
            return await Task.WhenAll(GetDeployUrls().Select(url => GetStampEntryAsyncWithFallbackAsync(url)));
        }

        private async Task<StampEntry> GetStampEntryAsyncWithFallbackAsync(string url)
        {
            try
            {
                return await GetStampEntryAsync(url);
            }
            catch (Exception e)
            {
                return new StampEntry()
                {
                    Name = new Uri(url).Host,
                    Data = new Dictionary<string, string>() { { "kudu", e.Message } }
                };
            }
        }

        private async Task<StampEntry> GetStampEntryAsync(string url)
        {
            var uri = new Uri(url);

            var entry = new StampEntry();

            // waws-prod-blu-001.cloudapp.net --> blu-001
            IPHostEntry host = Dns.GetHostEntry(uri.Host);
            string subDomain = host.HostName.Split('.')[0];

            // e.g. waws-prod-msfthk1-901 -> msfthk1-901
            string[] segments = subDomain.Split('-');
            entry.Name = segments[segments.Length - 2] + '-' + segments[segments.Length - 1];

            if (subDomain.Contains("msft"))
            {
                entry.Environment = "msft";
            }
            else
            {
                entry.Environment = subDomain.Substring(0, subDomain.Length - entry.Name.Length - 1);
            }

            // Make sure it matches the test site name
            // e.g. kudu-blu-001.scm.azurewebsites.net --> blu-001
            string expectedName = uri.Host.Split('.')[0].Substring(5);
            if (entry.Name != expectedName) entry.Mismatch = true;

            // Don't show the msft prefix in the site name to keep things short
            if (entry.Name.StartsWith("msft"))
            {
                entry.Name = entry.Name.Substring(4);
            }

            // Yank the .scm token
            string siteHostName = uri.Host.Replace(".scm.", ".");
            entry.TestSiteUrl = String.Format("http://{0}/", siteHostName);

            DateTime start = DateTime.Now;
            entry.Data = await RequestSiteData(entry.TestSiteUrl);
            entry.Duration = DateTime.Now - start;

            entry.ConsoleUrl = url.Replace("/deploy", "/basicauth");

            return entry;
        }

        private async Task<IDictionary<string,string>> RequestSiteData(string testSite)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
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

        private IEnumerable<StampEntry> ApplyStyle(IEnumerable<StampEntry> stampEntries)
        {
            // For error, color red.
            var result = stampEntries.Select(e =>
            {
                if (e.Data.ContainsKey("Error"))
                {
                    e.Style = "background-color: red; color: white";
                }

                return e;
            });

            // Pick cq1 as latest
            var cq1 = result.FirstOrDefault(e => e.Name.Equals("cq1-001", StringComparison.OrdinalIgnoreCase) && !e.Data.ContainsKey("Error"));
            if (cq1 == null)
            {
                return stampEntries;
            }

            int green = GetColorKey(cq1);

            // Any matching cq1, color green.
            return result.Select(e => 
            {
                if (String.IsNullOrEmpty(e.Style) && GetColorKey(e) == green)
                {
                    e.Style = "background-color: green; color: white"; 
                }

                return e;
            });
        }

        private int GetColorKey(StampEntry entry)
        {
            int colorKey = 0;

            foreach (var pair in entry.Data.OrderBy(p => p.Key))
            {
                // excluding waws version as it may vary between workers
                if (!pair.Key.Equals("waws", StringComparison.OrdinalIgnoreCase))
                {
                    colorKey ^= pair.Value.GetHashCode();
                }
            }

            return colorKey;
        }
    }
}
