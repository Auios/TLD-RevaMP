using System.Linq;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityCargoTools
    {

        /// In the local player's hands: picked up (fpscontroller.pickedUp) or in the right hand.
        /// Test only (bridge `mp forceheld <net>`): this item counts as in the local player's hands — a player carrying
        /// a part holds the mouse button down, which a test can't (the bridge's pickup is dropped the next frame).
        public static tosaveitemscript ForceHeld;
    }
}
