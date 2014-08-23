/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the WhiteCore-Sim Project nor the
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

using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using Nini.Config;
using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.IO;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Archivers
{
    /// <summary>
    ///     This module loads and saves OpenSimulator inventory archives
    /// </summary>
    public class InventoryArchiverModule : IService, IInventoryArchiverModule
    {
        /// <summary>
        /// The default save/load archive directory.
        /// </summary>
        private string m_archiveDirectory = Constants.DEFAULT_USERINVENTORY_DIR;

        /// <summary>
        ///     The file to load and save inventory if no filename has been specified
        /// </summary>
        protected const string DEFAULT_INV_BACKUP_FILENAME = "user-inventory.iar";

        /// <value>
        ///     All scenes that this module knows about
        /// </value>
        private readonly Dictionary<UUID, IScene> m_scenes = new Dictionary<UUID, IScene>();

        /// <value>
        ///     Pending save completions initiated from the console
        /// </value>
        protected List<Guid> m_pendingConsoleSaves = new List<Guid>();

        private IRegistryCore m_registry;

        public string Name
        {
            get { return "Inventory Archiver Module"; }
        }

        #region IInventoryArchiverModule Members

        public event InventoryArchiveSaved OnInventoryArchiveSaved;

        public bool ArchiveInventory(
            Guid id, string firstName, string lastName, string invPath, Stream saveStream)
        {
            return ArchiveInventory(id, firstName, lastName, invPath, saveStream, new Dictionary<string, object>());
        }

        public bool ArchiveInventory(
            Guid id, string firstName, string lastName, string invPath, Stream saveStream,
            Dictionary<string, object> options)
        {
            UserAccount userInfo = m_registry.RequestModuleInterface<IUserAccountService>()
                                             .GetUserAccount(null, firstName, lastName);

            if (userInfo != null)
            {
                try
                {
                    bool UseAssets = true;
                    if (options.ContainsKey("assets"))
                    {
                        object Assets = null;
                        options.TryGetValue("assets", out Assets);
                        bool.TryParse(Assets.ToString(), out UseAssets);
                    }
                    new InventoryArchiveWriteRequest(id, this, m_registry, userInfo, invPath, saveStream, UseAssets,
                                                     null, new List<AssetBase>()).Execute();
                }
                catch (EntryPointNotFoundException e)
                {
                    MainConsole.Instance.ErrorFormat(
                        "[ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream."
                        + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                    MainConsole.Instance.Error(e);

                    return false;
                }

                return true;
            }

            return false;
        }

        #endregion

        #region IService Members

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;
            m_registry.RegisterModuleInterface<IInventoryArchiverModule>(this);
            if (m_scenes.Count == 0)
            {
                OnInventoryArchiveSaved += SaveInvConsoleCommandCompleted;

                if (MainConsole.Instance != null)
                {
                    MainConsole.Instance.Commands.AddCommand(
                        "load iar",
                        "load iar <first> <last> [<inventory path> [<IAR path>]]",
                        //"load iar [--merge] <first> <last> <inventory path> <password> [<IAR path>]",
                        "Load user inventory archive (IAR). "
                        +
                        "--merge is an option which merges the loaded IAR with existing inventory folders where possible, rather than always creating new ones"
                        + "<first> is user's first name." + Environment.NewLine
                        + "<last> is user's last name." + Environment.NewLine
                        + "<inventory path> is the path inside the user's inventory where the IAR should be loaded." +
                        Environment.NewLine
                        + "<IAR path> is the filesystem path or URI from which to load the IAR."
                        +
                        string.Format("  If this is not given then the 'User' archive in the {0} directory is used",
                            m_archiveDirectory),
                        HandleLoadInvConsoleCommand, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "save iar",
                        "save iar <first> <last> [<inventory path> [<IAR path>]]",
                        "Save user inventory archive (IAR). <first> is the user's first name." + Environment.NewLine
                        + "<last> is the user's last name." + Environment.NewLine
                        + "<inventory path> is the path inside the user's inventory for the folder/item to be saved." +
                        Environment.NewLine
                        + "<IAR path> is the filesystem path at which to save the IAR."
                        +
                        string.Format("  If this is not given then the archive will be saved in " + m_archiveDirectory),
                        HandleSaveInvConsoleCommand, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "save iar withoutassets",
                        "save iar withoutassets <first> <last> [<inventory path> [<IAR path>]]",
                        "Save user inventory archive (IAR) withOUT assets. This version will NOT load on another grid/standalone other than the current grid/standalone! " +
                        "<first> is the user's first name." + Environment.NewLine
                        + "<last> is the user's last name." + Environment.NewLine
                        + "<inventory path> is the path inside the user's inventory for the folder/item to be saved." +
                        Environment.NewLine
                        + "<IAR path> is the filesystem path at which to save the IAR."
                        +
                        string.Format("  If this is not given then the archive will be saved in" + m_archiveDirectory),
                        HandleSaveInvWOAssetsConsoleCommand, false, true);
                }
            }
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup()
        {
        }

        #endregion

        /// <summary>
        ///     Trigger the inventory archive saved event.
        /// </summary>
        protected internal void TriggerInventoryArchiveSaved(
            Guid id, bool succeeded, UserAccount userInfo, string invPath, Stream saveStream,
            Exception reportedException)
        {
            InventoryArchiveSaved handlerInventoryArchiveSaved = OnInventoryArchiveSaved;
            if (handlerInventoryArchiveSaved != null)
                handlerInventoryArchiveSaved(id, succeeded, userInfo, invPath, saveStream, reportedException);
        }

        public bool ArchiveInventory(
            Guid id, string firstName, string lastName, string invPath, string savePath,
            Dictionary<string, object> options)
        {
            UserAccount userInfo = m_registry.RequestModuleInterface<IUserAccountService>()
                                             .GetUserAccount(null, firstName, lastName);

            if (userInfo != null)
            {
                try
                {
                    bool UseAssets = true;
                    if (options.ContainsKey("assets"))
                    {
                        object Assets = null;
                        options.TryGetValue("assets", out Assets);
                        bool.TryParse(Assets.ToString(), out UseAssets);
                    }
                    new InventoryArchiveWriteRequest(id, this, m_registry, userInfo, invPath, savePath, UseAssets).
                        Execute();
                }
                catch (EntryPointNotFoundException e)
                {
                    MainConsole.Instance.ErrorFormat(
                        "[ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream.\n"
                        + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                    MainConsole.Instance.Error(e);

                    return false;
                }

                return true;
            }

            return false;
        }

        public bool DearchiveInventory(
            string firstName, string lastName, string invPath, string loadPath,
            Dictionary<string, object> options)
        {
            UserAccount userInfo = m_registry.RequestModuleInterface<IUserAccountService>()
                                             .GetUserAccount(null, firstName, lastName);

            if (userInfo != null)
            {
                InventoryArchiveReadRequest request;
                bool merge = (options.ContainsKey("merge") && (bool) options["merge"]);

                try
                {
                    request = new InventoryArchiveReadRequest(m_registry, userInfo, invPath, loadPath, merge, UUID.Zero);
                }
                catch (EntryPointNotFoundException e)
                {
                    MainConsole.Instance.ErrorFormat(
                        "[ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream.\n"
                        + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                    MainConsole.Instance.Error(e);

                    return false;
                }

                request.Execute(false);

                return true;
            }

            return false;
        }

        /// <summary>
        ///     Load inventory from an inventory file archive
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void HandleLoadInvConsoleCommand(IScene scene, string[] cmdparams)
        {
            try
            {
                //MainConsole.Instance.Info(
                //    "[Inventory Archiver]: PLEASE NOTE THAT THIS FACILITY IS EXPERIMENTAL.  BUG REPORTS WELCOME.");

                Dictionary<string, object> options = new Dictionary<string, object>();
                List<string> newParams = new List<string>(cmdparams);
                foreach (string param in cmdparams)
                {
                    if (param.StartsWith("--skip-assets", StringComparison.CurrentCultureIgnoreCase))
                    {
                        options["skip-assets"] = true;
                        newParams.Remove(param);
                    }
                    if (param.StartsWith("--merge", StringComparison.CurrentCultureIgnoreCase))
                    {
                        options["merge"] = true;
                        newParams.Remove(param);
                    }
                }

                if (newParams.Count < 4)
                {
                    MainConsole.Instance.Error(
                        "[Inventory Archiver]: usage is: load iar <first name> <last name> [<inventory path> [<load file path>]] [--merge]");
                    return;
                }

                string firstName = newParams[2];
                string lastName = newParams[3];
                string archiveFileName = firstName+"_"+lastName+".iar";
               
                // optional...
                string invPath = "/";
                if (cmdparams.Length > 4)
                    invPath = newParams[4];
                if (invPath == "/")
                    options["merge"] = true;                // always merge if using the root folder

                string loadPath = (newParams.Count > 5 ? newParams[5] : m_archiveDirectory+"/" + archiveFileName);

                //some file sanity checks
                loadPath = PathHelpers.VerifyReadFile(loadPath, ".iar", m_archiveDirectory);

                if (loadPath != "")
                {
                    MainConsole.Instance.InfoFormat(
                        "[Inventory Archiver]: Loading archive {0} to inventory path {1} for {2} {3}",
                        loadPath, invPath, firstName, lastName);

                    if (DearchiveInventory(firstName, lastName, invPath, loadPath, options))
                        MainConsole.Instance.InfoFormat(
                            "[Inventory Archiver]: Loaded archive {0} for {1} {2}",
                            loadPath, firstName, lastName);
                }
            }
            catch (InventoryArchiverException e)
            {
                MainConsole.Instance.ErrorFormat("[Inventory Archiver]: {0}", e.Message);
            }
        }

        /// <summary>
        ///     Save inventory to a file archive
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void HandleSaveInvWOAssetsConsoleCommand(IScene scene, string[] cmdparams)
        {
            if (cmdparams.Length < 5)
            {
                MainConsole.Instance.Error(
                    "[Inventory Archiver]: usage is: save iar withoutassets <first name> <last name> [<inventory path> [<save file path>]]");
                return;
            }

            // MainConsole.Instance.Info(
            //    "[Inventory Archiver]: PLEASE NOTE THAT THIS FACILITY IS EXPERIMENTAL.  BUG REPORTS WELCOME.");

            string firstName = cmdparams[3];
            string lastName = cmdparams[4];
            string archiveFileName = firstName+"_"+lastName+".iar";

            // optional...
            string invPath = "*";
            if (cmdparams.Length > 5)
                invPath = cmdparams[5];

            string savePath = (cmdparams.Length > 6 ? cmdparams[6] : m_archiveDirectory + "/" + archiveFileName);

            //some file sanity checks
            savePath = PathHelpers.VerifyWriteFile (savePath, ".iar", m_archiveDirectory, true);

            MainConsole.Instance.InfoFormat(
                "[Inventory Archiver]: Saving archive {0} using inventory path {1} for {2} {3} without assets",
                savePath, invPath, firstName, lastName);

            Guid id = Guid.NewGuid();
            Dictionary<string, object> options = new Dictionary<string, object> {{"Assets", false}};
            ArchiveInventory(id, firstName, lastName, invPath, savePath, options);

            lock (m_pendingConsoleSaves)
                m_pendingConsoleSaves.Add(id);
        }

        /// <summary>
        ///     Save inventory to a file archive
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void HandleSaveInvConsoleCommand(IScene scene, string[] cmdparams)
        {
            if (cmdparams.Length < 4)
            {
                MainConsole.Instance.Error(
                    "[Inventory Archiver]: usage is: save iar <first name> <last name> [<inventory path> [<save file path>]]");
                return;
            }

            try
            {
                //MainConsole.Instance.Info(
                //    "[Inventory Archiver]: PLEASE NOTE THAT THIS FACILITY IS EXPERIMENTAL.  BUG REPORTS WELCOME.");

                string firstName = cmdparams[2];
                string lastName = cmdparams[3];
                string archiveFileName = firstName+"_"+lastName+".iar";

                // optional...
                string invPath = "/*";
                if (cmdparams.Length > 4)
                    invPath = cmdparams[4];

                string savePath = (cmdparams.Length > 5 ? cmdparams[5] : m_archiveDirectory + "/" + archiveFileName);

                //some file sanity checks
                savePath = PathHelpers.VerifyWriteFile (savePath, ".iar", m_archiveDirectory, true);

                MainConsole.Instance.InfoFormat(
                    "[Inventory Archiver]: Saving archive {0} using inventory path {1} for {2} {3}",
                    savePath, invPath, firstName, lastName);

                Guid id = Guid.NewGuid();

                Dictionary<string, object> options = new Dictionary<string, object> {{"Assets", true}};
                ArchiveInventory(id, firstName, lastName, invPath, savePath, options);

                lock (m_pendingConsoleSaves)
                    m_pendingConsoleSaves.Add(id);
            }
            catch (InventoryArchiverException e)
            {
                MainConsole.Instance.ErrorFormat("[Inventory Archiver]: {0}", e.Message);
            }
        }

        private void SaveInvConsoleCommandCompleted(
            Guid id, bool succeeded, UserAccount userInfo, string invPath, Stream saveStream,
            Exception reportedException)

        {
            lock (m_pendingConsoleSaves)
            {
                if (m_pendingConsoleSaves.Contains(id))
                    m_pendingConsoleSaves.Remove(id);
                else
                    return;
            }

            if (succeeded)
            {
                MainConsole.Instance.InfoFormat("[Inventory Archiver]: Saved archive for {0} {1}", userInfo.FirstName,
                                                userInfo.LastName);
            }
            else
            {
                MainConsole.Instance.ErrorFormat(
                    "[Inventory Archiver]: Archive save for {0} {1} failed - {2}",
                    userInfo.FirstName, userInfo.LastName, reportedException.Message);
            }
        }
    }
}