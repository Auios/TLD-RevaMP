using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityLeasesTools
    {

        public static void LeaseArm(string key) => RememberLeaseRequest(key);
    }
}
