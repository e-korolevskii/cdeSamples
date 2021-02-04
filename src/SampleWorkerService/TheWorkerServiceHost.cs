// SPDX-FileCopyrightText: 2021 C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using nsCDEngine.BaseClasses;
using nsCDEngine.Interfaces;
using nsCDEngine.ISM;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;

namespace cdeWorkerService
{
    public class TheNetCoreSettings : TheCDESettings
    {
        IConfiguration mConfig;

        public TheNetCoreSettings(IConfiguration pConfig) : base()
        {
            mConfig = pConfig;
        }

        public override bool HasSetting(string pSetting, Guid? pOwner = null)
        {
            if (base.HasSetting(pSetting, pOwner)) return true;
            return !string.IsNullOrEmpty(mConfig.GetValue<string>(pSetting));
        }

        public override string GetAppSetting(string pSetting, string alt, bool IsEncrypted, bool IsAltDefault = false, Guid? pOwner = null)
        {
            string tres = mConfig.GetValue<string>(pSetting, alt);
            if (!string.IsNullOrEmpty(tres)) return tres;
            return base.GetAppSetting(pSetting, alt, IsEncrypted, IsAltDefault, pOwner);
        }

        public override string GetSetting(string pSetting, Guid? pOwner = null)
        {
            string tres = mConfig.GetValue<string>(pSetting);
            if (!string.IsNullOrEmpty(tres)) return tres;
            return base.GetSetting(pSetting, pOwner);
        }
    }

    public class TheStartupLogger : ICDESystemLog
    {
        protected readonly ILogger<TheWorkerServiceHost> _logger;
        private string startupLogPath = "startup.log";


        public TheStartupLogger(ILogger<TheWorkerServiceHost> pLogger, string path)
        {
            _logger = pLogger;
            startupLogPath = path;
        }

        public void WriteToLog(eDEBUG_LEVELS LogLevel, int LogID, string pTopic, string pLogText, eMsgLevel pSeverity = eMsgLevel.l4_Message, bool NoLog = false)
        {
            Log($"SYSLOG: ID:{LogID} Topic:{pTopic} {pLogText}", pSeverity);
        }

        public void Log(string text, eMsgLevel LVL=eMsgLevel.l4_Message)
        {
            try
            {
                DateTimeOffset pTime = DateTimeOffset.Now;
                switch (LVL)
                {
                    case eMsgLevel.l1_Error:
                        _logger?.LogError($"{{time}}: {text}", pTime);
                        break;
                    case eMsgLevel.l2_Warning:
                        _logger?.LogWarning($"{{time}}: {text}", pTime);
                        break;
                    case eMsgLevel.l7_HostDebugMessage:
                    case eMsgLevel.l6_Debug:
                        _logger?.LogDebug($"{{time}}: {text}", pTime);
                        break;
                    default:
                        _logger?.LogInformation($"{{time}}: {text}", pTime);
                        break;
                }
                if (!string.IsNullOrEmpty(startupLogPath))
                    File.AppendAllText(startupLogPath, $"{pTime}: {text}\r\n");
            }
            catch { }
        }
    }

    public class TheWorkerServiceHost : BackgroundService
    {
        public IConfiguration Configuration { get; }
        protected TheStartupLogger StartupLog;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public TheWorkerServiceHost(IHostApplicationLifetime hostApplicationLifetime, ILogger<TheWorkerServiceHost> logger, IConfiguration tConfig)
        {
            _hostApplicationLifetime = hostApplicationLifetime;
            Configuration = tConfig;
            try
            {
                string tdir = Directory.GetCurrentDirectory();
                if (File.Exists("cdeenablestartuplog"))
                {
                    var lines = File.ReadAllLines("cdeenablestartuplog");
                    string path = null;
                    if (lines.Length > 0)
                    {
                        path = lines[0];
                        if (string.IsNullOrEmpty(path))
                            path = "startup.log";
                        if (lines.Length > 1)
                        {
                            int.TryParse(lines[1], out StartupDelay);
                        }
                    }
                    StartupLog = new TheStartupLogger(logger, path);
                }
            }
            catch { }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                StartupLog?.Log("PLEASE OVERRIDE! Worker running at: {time}");
                await Task.Delay(1000, stoppingToken);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            MyBaseApplication?.Shutdown(false, true);
            StartupLog?.Log(String.Format("Service: about to leave main function"));
            return base.StopAsync(cancellationToken);
        }

        private int StartupDelay;
        private Dictionary<string, string> MyArgList;
        protected cdeHostType m_hostType;
        protected string ServiceApplicationID = "SetInYour_ExecuteAsync";
        public TheBaseApplication MyBaseApplication;

        public async Task StartCDE(CancellationToken stoppingToken)
        {
            try
            {

                Thread.CurrentThread.Name = "Main thread";
                StartupLog?.Log("Service: Main function");

                {
                    StartupLog?.Log($"Service: Run interactive { Debugger.IsAttached} {Environment.UserInteractive}");
                    if (StartupDelay > 0)
                    {
                        StartupLog?.Log($"Waiting {StartupDelay} ms before starting host");
                        Thread.Sleep(StartupDelay);
                    }

                    OnStartInternal();
                    try
                    {
                        StartupLog?.Log("Service: Entering Service loop");
                        while (!stoppingToken.IsCancellationRequested)
                        {
                            await Task.Delay(250, stoppingToken);
                            if (!TheBaseAssets.MasterSwitch)
                            {
                                StartupLog?.Log("Service: Leaving key pressed loop because MasterSwitch was false");
                                break;
                            }
                        }
                        StartupLog?.Log("Service: Initiating shutdown after exiting key pressed loop");
                        _hostApplicationLifetime.StopApplication();
                        //MyBaseApplication?.Shutdown(false, true);
                    }
                    catch (System.InvalidOperationException)
                    {
                        StartupLog?.Log("Service: StdIn redirected to file. Waiting until application shuts down.");
                        TheBaseAssets.MasterSwitchCancelationToken.WaitHandle.WaitOne();
                    }
                    StartupLog?.Log("Service: about to leave main function");
                }
            }
            catch (Exception e)
            {
                StartupLog?.Log($"Service: Exception in main: {e} {e.StackTrace}");
            }
            StartupLog?.Log("Service: Leaving main function");
        }

        public TheWorkerServiceHost(cdeHostType pType)
        {
            m_hostType = pType;
            StartupLog?.Log("Service: Exiting service instance constructor");
        }

        public void OnStartInternal()
        {
            StartupLog?.Log("Service: Initializing");
            try
            {
                if (!Initialize())
                {
                    StartupLog?.Log("Service: Initializing FAILED");
                    return;
                }
            }
            catch (Exception e)
            {
                StartupLog?.Log($"Service: Exception initializing: {e}");
            }
            StartupLog?.Log("Service: Parsing parameters");
            MyBaseApplication = new TheBaseApplication();
            if (CU.CBool(TheBaseAssets.MySettings.GetSetting("UseRandomScope")) && CU.CBool(TheBaseAssets.MySettings.GetSetting("UseRandomDeviceID")))
            {
                TheBaseAssets.MyServiceHostInfo.SealID = TheScopeManager.GenerateNewScopeID();
                TheScopeManager.SetScopeIDFromEasyID(TheBaseAssets.MyServiceHostInfo.SealID);
            }
            StartupLog?.Log("Service: StartBaseApplication");

            try
            {
                if (!MyBaseApplication.StartBaseApplication(null, MyArgList))
                {
                    StartupLog?.Log("Service: StartBaseApplication failed");

                    AfterStartup(false);
                    return;
                }
                if (StartupLog!=null && MyArgList.ContainsKey("DisableConsole") && CU.CBool(MyArgList["DisableConsole"]))
                {
                    TheBaseAssets.MySYSLOG.RegisterEvent2("NewLogEntry", (pmsg, pNewLogEntry) =>
                    {
                        var tLog = pmsg?.Cookie as TheEventLogEntry;
                        if (tLog != null)
                            StartupLog?.Log($"ID:{tLog.EventID} SN:{tLog.Serial} {tLog.Message?.ToAllString()}", tLog.Message.LVL);
                    });
                }
            }
            catch (Exception ee)
            {
                StartupLog?.Log($"Service: StartBaseApplication failed with Exception: {ee}");

                AfterStartup(false);
                return;
            }
            StartupLog?.Log("Service: StartBaseApplication success");
            AfterStartup(true);
        }

        bool Initialize()
        {
            if (Configuration!=null)
                TheBaseAssets.MySettings = new TheNetCoreSettings(Configuration);
            MyArgList = (Configuration.GetChildren().ToDictionary(x => x.Key, x => x.Value));
            UpdateArgList(MyArgList);
            StartupLog?.Log("Service: Loading C-LabsCryptoLib");
            TheBaseAssets.LoadCrypto("C-LabsCryptoLib.dll", StartupLog, MyArgList.ContainsKey("DontVerifyTrust") && CU.CBool(MyArgList["DontVerifyTrust"]), null, MyArgList.ContainsKey("VerifyTrustPath") && CU.CBool(MyArgList["VerifyTrustPath"]), MyArgList.ContainsKey("DontVerifyIntegrity") && CU.CBool(MyArgList["DontVerifyIntegrity"]));
            if (TheBaseAssets.CryptoLoadMessage != null)
            {
                StartupLog?.Log($"Security initialization failed with {TheBaseAssets.CryptoLoadMessage}. Exiting...");
                return false;
            }
            StartupLog?.Log("Service: Setting apid");
            if (!TheScopeManager.SetApplicationID(ServiceApplicationID))
            {
                StartupLog?.Log("Application ID Illegal...exiting");
                return false;
            }
            StartupLog?.Log("Service: Creating service host");

            TheBaseAssets.MyServiceHostInfo = new TheServiceHostInfo(m_hostType)
            {
                CurrentVersion = CU.GetAssemblyVersion(this),
                DebugLevel = eDEBUG_LEVELS.OFF,
                UPnPIcon = "iconTopLogo.png",
                LocalServiceRoute = "LOCALHOST",
            };
            TheBaseAssets.MyServiceHostInfo.TO.HeartBeatRate = 3;
            TheBaseAssets.MyServiceHostInfo.StatusColors = ";#65bb00;orange";
            TheBaseAssets.MyServiceHostInfo.ApplicationName = "Factory-Relay";

            UpdateServiceHostInfo(TheBaseAssets.MyServiceHostInfo);

            if (m_hostType == cdeHostType.Application)
            {
                TheBaseAssets.MyServiceHostInfo.AddManifestFiles(new List<string> {
                    TheBaseAssets.MyServiceHostInfo.ISMMainExecutable,
                "C-DEngine.dll", "C-DMyNMIHtml5.dll"
                });
            }

            StartupLog?.Log("Service: Created service host");
            return true;
        }

        public virtual void UpdateArgList(Dictionary<string, string> argList)
        {
        }
        public virtual void AfterStartup(bool bSuccess) { }

        public virtual void UpdateServiceHostInfo(TheServiceHostInfo shi)
        {
        }
    }

}
