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
        
        public Program()
        { 
            nav =  GridTerminalSystem.GetBlockWithName("Navigation [t]") as IMyProgrammableBlock;
        }

        public void Save()
        {
          
        }

        private IMyProgrammableBlock nav;
        int count = 0;
        private string TAG = "NAV";
        public void Main(string argument, UpdateType updateSource)
        {
            IGC.SendUnicastMessage(nav.EntityId, TAG, "HELLO WORLD! "+count++);
        }
    }
}
