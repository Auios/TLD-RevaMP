using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntitySeatTools
    {

        /// Test (`mp getouttry <n>`): what the game does every frame a passenger looks at a place to get out to - GetOut
        /// there without the use key, n times (fpscontroller.Update). The player stays seated.
        public static string GetOutTry(int n)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null || !pl.Bsitting) return "{\"error\":\"not seated\"}";
            long logged = EntitySeats.SeatLeavesLogged;
            for (int i = 0; i < n; i++) pl.GetOut(pl.transform.position + pl.transform.right, false);
            return "{\"calls\":" + n + ",\"seated\":" + (pl.Bsitting ? "true" : "false") + ",\"seatLeavesLogged\":" + (EntitySeats.SeatLeavesLogged - logged) + "}";
        }
    }
}
