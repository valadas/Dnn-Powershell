﻿using Connect.DNN.Powershell.Common;
using Connect.DNN.Powershell.Data;
using Connect.DNN.Powershell.Framework;
using System.Management.Automation;
using System.Linq;
using Connect.DNN.Powershell.Core.Commands;
using Connect.DNN.Powershell.Framework.Models;

namespace Connect.DNN.Powershell.Commands.ContextManagement
{
    [Cmdlet("Use", "Site")]
    public class UseSite : BaseCmdLet
    {
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "keyonly")]
        public string Key { get; set; }

        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "fullsite")]
        public string Url { get; set; }

        [Parameter(Position = 1, Mandatory = false, ParameterSetName = "fullsite")]
        public string Username { get; set; }

        [Parameter(Position = 2, Mandatory = false, ParameterSetName = "fullsite")]
        public string Password { get; set; }

        protected override void ProcessRecord()
        {
            if (ParameterSetName == "keyonly")
            {
                WriteVerbose(string.Format("Switching to site {0}", Key));
                var site = SiteList.Instance().Sites[Key];
                if (site != null)
                {
                    if (System.DateTime.Now.AddHours(1) > site.Expires)
                    {
                        var res =CheckSite(site, Key);
                        if (res == ServerResponseStatus.Success)
                        {
                            site = SiteList.Instance().Sites[Key];
                        }
                        else
                        {
                            WriteWarning(string.Format("Error! Could not switch to site {0} because the login failed.", site.Url));
                            return;
                        }
                    }
                    var result = DnnPromptController.ProcessCommand(site, 5, "echo Hello World");
                    if (result.Status == ServerResponseStatus.Success)
                    {
                        DnnPromptController.CurrentSite = site;
                        WriteVerbose(string.Format("Switched to site {0}", Key));
                    }
                    else
                    {
                        WriteWarning(string.Format("Error! Could not switch to site {0}", site.Url));
                        return;
                    }
                }
                else
                {
                    WriteWarning(string.Format("Error! Could not find site {0}", Key));
                    return;
                }
            }
            else
            {
                WriteVerbose(string.Format("Setting site to {0}", Url));
                if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
                {
                    var creds = GetLogin();
                    Username = creds.Username;
                    Password = creds.Password.ToUnsecureString();
                }
                var result = DnnPromptController.GetToken(Url, Username, Password);
                if (result.Status == ServerResponseStatus.Success)
                {
                    DnnPromptController.CurrentSite = new Site()
                    {
                        Url = Url,
                        Token = result.Contents.Encrypt(),
                        Expires = System.DateTime.Now.AddDays(14)
                    };
                    WriteVerbose(string.Format("Success! Switched to site {0}", Url));
                }
                else
                {
                    WriteWarning(string.Format("Error! Could not log in to site {0}", Url));
                    return;
                }
            }
            // retrieve a list of portals so we can set the current portal
            var portals = PortalCommands.ListPortals(DnnPromptController.CurrentSite);
            DnnPromptController.CurrentSite.Portals = portals.Select(p => new Data.Portal() { PortalId = p.PortalId, PortalName = p.PortalName }).ToDictionary(p => p.PortalId);
            var portal = PortalCommands.GetPortal(DnnPromptController.CurrentSite);
            DnnPromptController.CurrentSite.PortalId = portal.PortalId;
            DnnPromptController.CurrentPortal = DnnPromptController.CurrentSite.Portals[DnnPromptController.CurrentSite.PortalId];
            WriteVerbose(string.Format("Current Portal ID is {0}", DnnPromptController.CurrentPortal.PortalId));
            WriteObject(DnnPromptController.CurrentSite);
        }
    }
}
