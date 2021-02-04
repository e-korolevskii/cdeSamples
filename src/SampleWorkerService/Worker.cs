using cdeWorkerService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using nsCDEngine.ViewModels;
using System;
// SPDX-FileCopyrightText: 2021 C-Labs
//
// SPDX-License-Identifier: MPL-2.0


using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;

namespace SampleWorkerService
{
    public class MyRelay : TheWorkerServiceHost
    {
        public MyRelay(IHostApplicationLifetime hostApplicationLifetime, ILogger<TheWorkerServiceHost> logger, IConfiguration tConfig) : base(hostApplicationLifetime, logger, tConfig)
        {
            m_hostType = Environment.UserInteractive ? cdeHostType.Application : cdeHostType.Service;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ServiceApplicationID = "/cVjzPfjlO;{@QMj:jWpW]HKKEmed[llSlNUAtoE`]G?"; //This is the SDK Open Source ID. To get an OEM key, please contact info@c-labs.com
            await StartCDE(stoppingToken);
        }

        public override void UpdateServiceHostInfo(TheServiceHostInfo shi)
        {
            shi.Title = "My-Relay";
            shi.ApplicationTitle = "My Relay Portal";
            shi.ApplicationName = "My-Relay";
            shi.SiteName = "https://www.my-relay.com";
            shi.VendorName = "C-Labs";
            shi.VendorUrl = "http://www.mycompany.com";
            shi.Description = "The My-Relay Service";
            shi.ISMMainExecutable = "SampleWorkerService";
            shi.cdeMID = CU.CGuid("{AA2cde02-413B-401A-80BC-BAA04814145D}");    //Unique SHI cdeMID for TLS
            shi.VendorID = CU.CGuid("{7B8ED692-DD7D-40BD-9B98-2266CB0C645F}"); //Vendor Guid for TLS/SVS

            shi.AddManifestFiles(new List<string>
            {

            });

            StartupLog?.Log("SHI was Updated by Host");
        }

        public override void UpdateArgList(Dictionary<string, string> argList)
        {
            argList["AllowLocalHost"] = "true";
            argList["UseUserMapper"] = "true";
            argList["DontVerifyIntegrity"] = "true";
            StartupLog?.Log("Settings added by Host");
        }
    }
}
