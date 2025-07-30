using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using CustomMainMenusAPI;
using MTM101BaldAPI.AssetTools;

namespace EndlessFloorsForever
{
    internal static class CustomMainMenuSupport
    {
        internal static void InitSupport()
        {
            MainMenuObject.CreateMenuObject("men_ArcadeForever", AssetLoader.SpriteFromMod(EndlessForeverPlugin.Instance, Vector2.one/2f, 1f, "Menu_INFForever.png"));
        }
    }
}
