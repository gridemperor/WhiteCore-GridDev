//Written by skidz tweak
/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Aurora-Sim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Timers;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Utilities;

namespace Simple.Currency
{
    class GiveStipends
    {
        private readonly Timer taskTimer = new Timer();
        private readonly bool m_enabled = false;
        private SimpleCurrencyConfig m_options;
        readonly IScheduleService m_scheduler;
        private readonly IRegistryCore m_registry;
        private readonly SimpleCurrencyConnector m_currencyService;

        public GiveStipends(SimpleCurrencyConfig options, IRegistryCore registry, SimpleCurrencyConnector dustCurrencyService)
        {
            m_enabled = options.GiveStipends;
            if (!m_enabled) return;

            m_currencyService = dustCurrencyService;
            m_options = options;
            m_registry = registry;
            taskTimer.Interval = 360000;
            taskTimer.Elapsed += TimerElapsed;
            m_scheduler = registry.RequestModuleInterface<IScheduleService>();
            if (m_scheduler == null) return;
            m_scheduler.Register("StipendsPayout", StipendsPayOutEvent);
            if (m_options.StipendsLoadOldUsers) taskTimer.Enabled = true;
            registry.RequestModuleInterface<ISimulationBase>().EventManager.RegisterEventHandler("DeleteUserInformation", DeleteUserInformation);
            registry.RequestModuleInterface<ISimulationBase>().EventManager.RegisterEventHandler("CreateUserInformation", CreateUserInformation);
            registry.RequestModuleInterface<ISimulationBase>().EventManager.RegisterEventHandler("UpdateUserInformation", CreateUserInformation);


        }

        private object CreateUserInformation(string functionname, object parameters)
        {
            UUID userid = (UUID)parameters;
            IUserAccountService userService = m_registry.RequestModuleInterface<IUserAccountService>();
            UserAccount user = userService.GetUserAccount(null, userid);
            if (user == null) return null;
            if ((m_options.StipendsPremiumOnly) && ((user.UserFlags & Constants.USER_FLAG_MEMBER) != Constants.USER_FLAG_MEMBER)) return null;

            SchedulerItem i = m_scheduler.Get(user.PrincipalID.ToString(), "StipendsPayout");
            if (i != null) return null;
            RepeatType runevertype = (RepeatType)Enum.Parse(typeof(RepeatType), m_options.StipendsEveryType);
            int runevery = m_options.StipendsEvery;
            m_scheduler.Save(new SchedulerItem("StipendsPayout",
                                                OSDParser.SerializeJsonString(
                                                    new StipendsInfo() { AgentID = user.PrincipalID }.ToOSD()),
                                                false, UnixTimeStampToDateTime(user.Created), runevery,
                                                runevertype, user.PrincipalID) { HisotryKeep = true, HistoryReciept = true });
            return null;

        }

        private object DeleteUserInformation(string functionname, object parameters)
        {
            UUID user = (UUID)parameters;
            SchedulerItem i = m_scheduler.Get(user.ToString(), "StipendsPayout");
            if (i != null)
                m_scheduler.Remove(i.id);
            return null;
        }

        private object StipendsPayOutEvent(string functionName, object parameters)
        {
            if (functionName != "StipendsPayout") return null;
            StipendsInfo si = new StipendsInfo();
            si.FromOSD((OSDMap)OSDParser.DeserializeJson(parameters.ToString()));
            IUserAccountService userService = m_registry.RequestModuleInterface<IUserAccountService>();
            UserAccount ua = userService.GetUserAccount(null, si.AgentID);
            if ((ua != null) && (ua.UserFlags >= 0) && ((!m_options.StipendsPremiumOnly) || ((ua.UserLevel & Constants.USER_FLAG_MEMBER) == Constants.USER_FLAG_MEMBER)))
            {
                if (m_options.GiveStipendsOnlyWhenLoggedIn)
                {
                    ICapsService capsService = m_registry.RequestModuleInterface<ICapsService>();
                    IClientCapsService client = capsService.GetClientCapsService(ua.PrincipalID);
                    if (client == null) return "";
                }
                IMoneyModule mo = m_registry.RequestModuleInterface<IMoneyModule>();
                if (mo == null) return null;
                UUID transid = UUID.Random();
                if (m_currencyService.UserCurrencyTransfer(ua.PrincipalID, UUID.Zero, (uint)m_options.Stipend, "Stipend Payment", TransactionType.StipendPayment, transid))
                {
                    return transid.ToString();
                }
            }
            return "";
        }

        private void TimerElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            taskTimer.Enabled = false;
            IUserAccountService userService = m_registry.RequestModuleInterface<IUserAccountService>();
            List<UserAccount> users = new List<UserAccount>();
            users = userService.GetUserAccounts(new List<UUID> { UUID.Zero }, 0, m_options.StipendsPremiumOnly ? 600 : 0);
            foreach (UserAccount user in users)
            {
                SchedulerItem i = m_scheduler.Get(user.PrincipalID.ToString(), "StipendsPayout");
                if (i != null) continue;
                RepeatType runevertype = (RepeatType)Enum.Parse(typeof(RepeatType), m_options.StipendsEveryType);
                int runevery = m_options.StipendsEvery;
                m_scheduler.Save(new SchedulerItem("StipendsPayout",
                                                   OSDParser.SerializeJsonString(
                                                       new StipendsInfo() { AgentID = user.PrincipalID }.ToOSD()),
                                                   false, UnixTimeStampToDateTime(user.Created), runevery,
                                                   runevertype, user.PrincipalID) { HisotryKeep = true, HistoryReciept = true });
            }
        }

        private static DateTime UnixTimeStampToDateTime(int unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }
    }

    public class StipendsInfo : IDataTransferable
    {
        public UUID AgentID { get; set; }

        #region IDataTransferable
        /// <summary>
        ///   Serialize the module to OSD
        /// </summary>
        /// <returns></returns>
        public override OSDMap ToOSD()
        {
            return new OSDMap()
                       {
                           {"AgentID", OSD.FromUUID(AgentID)}
                       };
        }

        /// <summary>
        ///   Deserialize the module from OSD
        /// </summary>
        /// <param name = "map"></param>
        public override void FromOSD(OSDMap map)
        {
            AgentID = map["AgentID"].AsUUID();
        }

        #endregion
    }
}
