﻿using System.Collections.Generic;
using Nini.Config;

namespace WhiteCore.Framework.SceneInfo
{

    #region Delegates

    public delegate void NewScene(IScene scene);

    public delegate void NoParam();

    #endregion

    public interface ISceneManager
    {
        /// <summary>
        ///     Starts the region
        /// </summary>
        /// <param name="newRegion"></param>
        void StartRegions(out bool newRegion);

        /// <summary>
        ///     Shuts down the given region
        /// </summary>
        /// <param name="shutdownType"></param>
        /// <param name="p"></param>
        void CloseRegion(IScene scene, ShutdownType shutdownType, int p);

        /// <summary>
        ///     Removes and resets terrain and objects from the database
        /// </summary>
        void ResetRegion(IScene scene);

        /// <summary>
        ///     Restart the given region
        /// </summary>
        void RestartRegion(IScene scene);

        /// <summary>
        /// Creates and adds a region from supplied info.
        /// </summary>
        /// <param name="regionInfo">Region info.</param>
        void CreateRegion (RegionInfo regionInfo);

        /// <summary>
        /// Finds the current region info.
        /// </summary>
        /// <returns>The current region info.</returns>
        Dictionary<string, int> FindCurrentRegionInfo();

        void HandleStartupComplete(List<string> data);

        IConfigSource ConfigSource { get; }

        List<IScene> Scenes { get; }

        event NewScene OnCloseScene;
        event NewScene OnAddedScene;
        event NewScene OnFinishedAddingScene;
    }
}