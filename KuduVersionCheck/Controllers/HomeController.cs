﻿using System;
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
            string[] regions = { "bay-001", "ch1-001", "blu-001", "db3-001", "am2-001", "hk1-001" };

            var client = new HttpClient();
            var commitIds = await Task.WhenAll(
                regions.Select(async region => {
                    var response = await client.GetAsync(String.Format("http://kudu-{0}.azurewebsites.net/", region));
                    return await response.Content.ReadAsStringAsync();
                }
            ));

            return View(regions.Select((region, i) => new RegionEntry { Name = region, CommitId = commitIds[i].Trim() }));
        }
    }
}
