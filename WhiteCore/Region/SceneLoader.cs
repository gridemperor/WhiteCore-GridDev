﻿using System.Collections.Generic;
using WhiteCore.Framework;
using WhiteCore.Framework.ModuleLoader;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Services;
using Nini.Config;

namespace WhiteCore.Region
{
    public class SceneLoader : ISceneLoader, IApplicationPlugin
    {
        private IConfigSource m_configSource;
        private ISimulationBase m_openSimBase;

        #region IApplicationPlugin Members

        public void PreStartup(ISimulationBase simBase)
        {
        }

        public void Initialize(ISimulationBase openSim)
        {
            m_openSimBase = openSim;
            m_configSource = openSim.ConfigSource;

            bool enabled = true;
            if (m_openSimBase.ConfigSource.Configs["SceneLoader"] != null)
                enabled = m_openSimBase.ConfigSource.Configs["SceneLoader"].GetBoolean("SceneLoader", true);

            if (enabled)
                m_openSimBase.ApplicationRegistry.RegisterModuleInterface<ISceneLoader>(this);
        }

        public void PostInitialise()
        {
        }

        public void Start()
        {
        }

        public void PostStart()
        {
        }

        public void Close()
        {
        }

        public void ReloadConfiguration(IConfigSource m_config)
        {
        }

        #endregion

        #region ISceneLoader Members

        public string Name
        {
            get { return "SceneLoader"; }
        }

        /// <summary>
        ///     Create a scene and its initial base structures.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public IScene CreateScene(ISimulationDataStore dataStore, RegionInfo regionInfo)
        {
            AgentCircuitManager circuitManager = new AgentCircuitManager();
            List<IClientNetworkServer> clientServers = WhiteCoreModuleLoader.PickupModules<IClientNetworkServer>();
            List<IClientNetworkServer> allClientServers = new List<IClientNetworkServer>();
            foreach (IClientNetworkServer clientServer in clientServers)
            {
                clientServer.Initialise((uint)regionInfo.RegionPort, m_configSource, circuitManager);
                allClientServers.Add(clientServer);
            }

            Scene scene = new Scene();
            scene.AddModuleInterfaces(m_openSimBase.ApplicationRegistry.GetInterfaces());
            scene.Initialize(regionInfo, dataStore, circuitManager, allClientServers);

            return scene;
        }

        #endregion
    }
}