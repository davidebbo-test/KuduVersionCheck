using System;
using System.Collections.Generic;
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
        public async Task<ActionResult> Index()
        {
            IEnumerable<string> stampNames = GetStampNames();

            var client = new HttpClient();
            var stampEntries = await Task.WhenAll(
                stampNames.Select(async stampName => {
                    string testSite = String.Format("http://kudu-{0}.azurewebsites.net/", stampName);

                    var stampEntry = new StampEntry { TestSite = testSite, Name = stampName };

                    try
                    {
                        var response = await client.GetAsync(testSite);
                        stampEntry.DataString = await response.Content.ReadAsStringAsync();
                    }
                    catch (Exception e)
                    {
                        if (e.InnerException != null)
                        {
                            e = e.InnerException;
                        }

                        stampEntry.DataString = String.Format("{{ error={0} }}", e.Message);
                    }

                    // Trim edge characters to make it more readable
                    stampEntry.DataString = stampEntry.DataString.Trim(new char[] { ' ', '\r', '\n', '{', '}', ',' });

                    return stampEntry;
                }
            ));

            return View(stampEntries);
        }

        private IEnumerable<string> GetStampNames()
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
            return hooks.Select(hook =>
            {
                Uri uri = new Uri(hook.SelectToken("url").ToString());

                // e.g. kudu-blu-001.scm.azurewebsites.net --> blu-001
                return uri.Host.Split('.')[0].Substring(5);
            });
        }
    }
}
