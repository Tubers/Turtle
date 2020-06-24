using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        enum Direction : int
        {
            APOS = 0,
            ANEG = 1,
            BPOS = 2,
            BNEG = 3,
            UP = 4,
            DOWN = 5
        }
        long _navID;

        public Program()
        {
            nav = GridTerminalSystem.GetBlockWithName("Navigation [t]") as IMyProgrammableBlock;
            _navID = nav.EntityId;
        }

        IMyProgrammableBlock nav;
        string TAG = "NAV";

        private int run = 0;
        public void Main(string argument, UpdateType updateSource)
        {
            
            IGC.SendUnicastMessage(_navID, TAG, "R;" + run);
            Echo("unicast sent "+run);
            run=run++%4;
            
        }
    }
}
