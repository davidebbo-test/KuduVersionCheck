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
        public async Task<ActionResult> Index2()
        {
            string[] regions = { "WestUS", "NorthCentralUS", "EastUS", "WestEurope", "NorthEurope", "EastAsia" };

            var client = new HttpClient();
            var responses = await Task.WhenAll(
                regions.Select(region => client.GetAsync(String.Format("http://Kudu{0}.azurewebsites.net/", region))));

            string[] commitIds = await Task.WhenAll(
                responses.Select(response => response.Content.ReadAsStringAsync()));

            return View();
        }

        public async Task<ActionResult> Index()
        {
            string[] regions = { "WestUS", "NorthCentralUS", "EastUS", "WestEurope", "NorthEurope", "EastAsia" };

            var client = new HttpClient();
            var commitIds = await Task.WhenAll(
                regions.Select(async region => {
                    var response = await client.GetAsync(String.Format("http://Kudu{0}.azurewebsites.net/", region));
                    return await response.Content.ReadAsStringAsync();
                }
            ));

            return View(regions.Select((region, i) => new RegionEntry { Name = region, CommitId = commitIds[i].Trim() }));
        }


        public async Task<ActionResult> Index3()
        {
            string[] regions = { "WestUS", "NorthCentralUS", "EastUS", "WestEurope", "NorthEurope", "EastAsia" };

            var client = new HttpClient();
            var commitIds = await Task.WhenAll(
                regions.Select(async region =>
                {
                    var response = await client.GetAsync(String.Format("http://Kudu{0}.azurewebsites.net/", region));
                    return response.Content.ReadAsStringAsync().Result.Trim();
                }
            ));
            var a = regions.Select((region, i) => new { region = region, commit = commitIds[i] });

            return View();
        }
    }
}
