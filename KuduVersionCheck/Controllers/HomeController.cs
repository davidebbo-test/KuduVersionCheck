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
        const string sourceStamp = "msftinthk1-501";
        const string sourceStampShort = "inthk1-501";

        public async Task<ActionResult> Index(string mode)
        {
            var viewModel = new StampEntriesViewModel();

            // Get the secret key that shows the Kudu urls
            viewModel.ShowConsole = (mode == ConfigurationManager.AppSettings["ScmMode"]);

            IEnumerable<StampEntry> stampEntries = await GetStampEntriesAsync();
            ApplyStyle(stampEntries);

            viewModel.Groups = stampEntries.OrderByDescending(e => e.Environment).GroupBy(e => e.Environment);

            // Find the first non-error entry to get the columns
            var entry = stampEntries.FirstOrDefault(e => !e.Data.ContainsKey("Error"));

            viewModel.Columns = entry.Data.Keys;

            return View(viewModel);
        }

        public ActionResult Csv(string[] endpoint, string toolset)
        {
            if (endpoint == null || toolset == null)
            {
                throw new ArgumentException("endpoint and toolset need to be passed on query string");
            }

            int count = 0;
            Response.Output.WriteLine("BatchId,Endpoint,ScaleUnit,Cluster,Template,SettingsFile,Toolset,Tenants,StartState");

            //var urls = GetDeployUrls().Where(s => s.Contains("azurewebsites.net")).OrderBy(s => s);
            var urls = GetDeployUrls().Where(s => s.Contains(sourceStamp)).OrderBy(s => s);

            foreach (var url in urls)
            {
                var uri = new Uri(url);

                string stampName = uri.Host.Split('.')[0];
                stampName = stampName.Substring(5);

                var csvEntry = new List<string>();

                // BatchId
                csvEntry.Add("0");

                // Endpoint
                csvEntry.Add(endpoint[count++ % endpoint.Length]);

                // ScaleUnit
                csvEntry.Add("waws-prod-" + stampName);

                // Cluster
                csvEntry.Add("");

                // Template
                csvEntry.Add("Antares_RapidUpdate.xml");

                // SettingsFile
                csvEntry.Add("Settings_Antares.xml");

                // Toolset
                csvEntry.Add(String.Format(@"ext_ANT={0};usecmtcore;usedcsm;usewadi", toolset));

                // Tenants
                csvEntry.Add("");

                // StartState
                csvEntry.Add("Pause");

                Response.Output.WriteLine(String.Join(",", csvEntry));
            }

            var cd = new System.Net.Mime.ContentDisposition
            {
                FileName = "RapidUpdate.csv",
                Inline = false,
            };
            Response.AppendHeader("Content-Disposition", cd.ToString()); 

            return null;
        }

        public ActionResult Acis()
        {
            return AcisInternal((sourceStamp, targetStamp) =>
                String.Format("{0},{1}", sourceStamp, targetStamp));
        }

        public ActionResult AcisCommandLine()
        {
            return AcisInternal((sourceStamp, targetStamp) =>
                String.Format("SyncRapidUpdateContainerForStamps {0} {1}", sourceStamp, targetStamp));
        }

        [NonAction]
        private ActionResult AcisInternal(Func<string,string,string> generateLine)
        {
            var urls = GetDeployUrls().Where(s => s.Contains("azurewebsites.net") && !s.Contains(".p.") && !s.Contains(sourceStamp)).OrderBy(s => s);

            foreach (var url in urls)
            {
                var uri = new Uri(url);

                string stampName = uri.Host.Split('.')[0];
                stampName = stampName.Substring(5);

                string sourceStampToUse = sourceStamp;

                // This is to work around a wierd storage issue
                if (stampName == "hk1-007")
                {
                    sourceStampToUse = "am2-001";
                }

                Response.Output.WriteLine(generateLine("waws-prod-" + sourceStampToUse, "waws-prod-" + stampName));
            }

            var cd = new System.Net.Mime.ContentDisposition
            {
                FileName = "RapidUpdateAcis.txt",
                Inline = false,
            };
            Response.AppendHeader("Content-Disposition", cd.ToString());

            return null;
        }

        private async Task<IEnumerable<StampEntry>> GetStampEntriesAsync()
        {
            return await Task.WhenAll(GetDeployUrls().Select(GetStampEntryAsyncWithFallbackAsync));
        }

        private async Task<StampEntry> GetStampEntryAsyncWithFallbackAsync(string url)
        {
            try
            {
                return await GetStampEntryAsync(url);
            }
            catch (Exception e)
            {
                return new StampEntry
                {
                    Name = new Uri(url).Host,
                    Data = new Dictionary<string, StampCell>() { { "kudu", new StampCell(e.Message) } }
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
            entry.Name = uri.Host.Split('.')[0].Substring(5);

            // Make sure the dns matches the test site name
            // e.g. kudu-blu-001.scm.azurewebsites.net --> blu-001
            string[] segments = subDomain.Split('-');
            if (segments.Length >= 2)
            {
                string dnsSiteName = segments[segments.Length - 2] + '-' + segments[segments.Length - 1];
                if (entry.Name != dnsSiteName) entry.Mismatch = true;
            }

            if (subDomain.Contains("msft"))
            {
                entry.Environment = "msft";
            }
            else if (segments.Length >= 2)
            {
                entry.Environment = subDomain.Substring(0, subDomain.Length - entry.Name.Length - 1);
            }
            else
            {
                entry.Environment = subDomain;
                if (Char.IsDigit(subDomain[subDomain.Length - 1]))
                {
                    entry.Environment = entry.Environment.Substring(0, subDomain.Length - 1);
                }
            }

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

        private async Task<IDictionary<string, StampCell>> RequestSiteData(string testSite)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var response = await client.GetAsync(testSite);
                    string dataString = await response.Content.ReadAsStringAsync();

                    IDictionary<string, JToken> token = JObject.Parse(dataString);

                    return token.ToDictionary(entry => entry.Key, entry => new StampCell(entry.Value.ToString()));
                }
                catch (Exception e)
                {
                    if (e.InnerException != null)
                    {
                        e = e.InnerException;
                    }

                    return new Dictionary<string, StampCell>() { { "Error", new StampCell(e.Message) } };
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
            // Find the 'reference' stamp
            var baseStamp = stampEntries.FirstOrDefault(e => e.Name.EndsWith(sourceStampShort, StringComparison.OrdinalIgnoreCase) && !e.Data.ContainsKey("Error"));
            if (baseStamp == null)
            {
                return stampEntries;
            }

            // Go through all the stamps
            foreach (var stamp in stampEntries)
            {
                if (stamp.Data.ContainsKey("Error"))
                {
                    // For error, color red.
                    stamp.Style = "background-color: red; color: white";
                }
                else
                {
                    // Go through all the data columns
                    bool mismatch = false;
                    foreach (var dataEntry in stamp.Data)
                    {
                        // don't cause mismatch for any column that ends with $
                        if (dataEntry.Key.EndsWith("$")) continue;

                        StampCell referenceCell, currentCell = dataEntry.Value;
                        if (baseStamp.Data.TryGetValue(dataEntry.Key, out referenceCell))
                        {
                            // If the cell matches the reference, make it green
                            if (FilterValue(dataEntry.Key, currentCell.Value) == FilterValue(dataEntry.Key, referenceCell.Value))
                            {
                                currentCell.Style = "background-color: green; color: white";
                            }
                            else
                            {
                                mismatch = true;
                            }
                        }
                    }

                    // If there are no mismatches, default the row to green
                    if (!mismatch)
                    {
                        stamp.Style = "background-color: green; color: white";
                    }

                    // Now set all the $ columns to the default style of the row
                    foreach (var dataEntry in stamp.Data.Where(e => e.Key.EndsWith("$")))
                    {
                        dataEntry.Value.Style = stamp.Style;
                    }
                }
            }


            return stampEntries;
        }

        private string FilterValue(string key, string value)
        {
            // excluding waws last number of version version as it may vary between workers
            if (key == "waws")
            {
                int lastPeriodIndex = value.LastIndexOf(".");
                if (lastPeriodIndex > 0)
                {
                    value = value.Substring(0, value.Length - lastPeriodIndex);
                }
            }

            return value;
        }
    }
}
