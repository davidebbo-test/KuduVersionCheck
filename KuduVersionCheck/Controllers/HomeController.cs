using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;
using KuduVersionCheck.Models;
using Newtonsoft.Json;

namespace KuduVersionCheck.Controllers
{
    public class HomeController : AsyncController
    {
        public async Task<ActionResult> Index()
        {
            string[] stampNames = { "bay-001", "bay-003", "ch1-001", "blu-001", "blu-003", "db3-001", "db3-003", "am2-001", "am2-003", "hk1-001" };

            var client = new HttpClient();
            var stampEntries = await Task.WhenAll(
                stampNames.Select(async stampName => {
                    StampEntry stampEntry;

                    string testSite = String.Format("http://kudu-{0}.azurewebsites.net/", stampName);

                    try
                    {
                        var response = await client.GetAsync(testSite);
                        string responseString = await response.Content.ReadAsStringAsync();

                        stampEntry = JsonConvert.DeserializeObject<StampEntry>(responseString);
                    }
                    catch (Exception e)
                    {
                        if (e.InnerException != null)
                        {
                            e = e.InnerException;
                        }

                        stampEntry = new StampEntry() { CommitId = e.Message };
                    }

                    stampEntry.TestSite = testSite;
                    stampEntry.Name = stampName;
                    return stampEntry;
                }
            ));

            return View(stampEntries);
        }
    }
}
