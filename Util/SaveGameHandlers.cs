using SOD.Common;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityStandardAssets.Characters.FirstPerson;
using HarmonyLib;

namespace MurderMayhem
{
    public class SaveGameHandlers : MonoBehaviour
    {
        public SaveGameHandlers()
        {       
            Lib.SaveGame.OnAfterLoad += HandleGameLoaded;
            Lib.SaveGame.OnAfterNewGame += HandleNewGameStarted;
            Lib.SaveGame.OnBeforeNewGame += HandleGameBeforeNewGame;
            Lib.SaveGame.OnBeforeLoad += HandleGameBeforeLoad;
            Lib.SaveGame.OnBeforeDelete += HandleGameBeforeDelete;
            Lib.SaveGame.OnAfterDelete += HandleGameAfterDelete;
        }

        private void HandleNewGameStarted(object sender, EventArgs e)
        {
        }

        private void HandleGameLoaded(object sender, EventArgs e)
        {
        }

        private void HandleGameBeforeNewGame(object sender, EventArgs e)
        {
            Plugin.UpdateCustomCases();
            Plugin.Log?.LogInfo("Custom cases rescanned before new game");
        }

        private void HandleGameBeforeLoad(object sender, EventArgs e)
        {
            Plugin.UpdateCustomCases();
            Plugin.Log?.LogInfo("Custom cases rescanned after game load");
        }

        private void HandleGameBeforeDelete(object sender, EventArgs e)
        {

        }

        private void HandleGameAfterDelete(object sender, EventArgs e)
        {

        }
    }
}