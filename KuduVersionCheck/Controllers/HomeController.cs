using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;
using KuduVersionCheck.Models;

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
    }
}
