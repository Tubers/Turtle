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
        #region mdk macros
        // This script was deployed at $MDK_DATETIME$
        private const string Deployment = "$MDK_DATE$, $MDK_TIME$";
        #endregion

        //IGC
        string _turtleTag = "TURTLE TAG";
        IMyBroadcastListener _turtleListener;

        IEnumerator<bool> _stateMachine;
        MyIni _ini = new MyIni();

        List<IMyTerminalBlock> blockList = new List<IMyTerminalBlock>();
        StringBuilder messageBuilder = new StringBuilder();

        public Program()
        {
            Echo("Construct");
            // user config
            MyIniParseResult result;

            if (!_ini.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());

            //_outputNow = _ini.Get("demo", "outputNow").ToBoolean();

            // load program state
            //_ini.TryParse(Storage);
            //_intValue = _ini.Get("demo", "intValue").ToInt32(DefaultIntValue);
            Runtime.UpdateFrequency |= UpdateFrequency.Once;

            //IGC
            _turtleListener = IGC.RegisterBroadcastListener(_turtleTag);
            _turtleListener.SetMessageCallback(_turtleTag);
        }

        public void Save()
        {
            // save program state variables
            _ini.Clear();
            //_ini.Set("demo", "intValue", _intValue);
            //Storage = _ini.ToString();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            string data = "";

            

            if ((updateSource & UpdateType.IGC) > 0)
            {
                while (_turtleListener.HasPendingMessage)
                {
                    MyIGCMessage message = _turtleListener.AcceptMessage();

                    if (message.Tag == _turtleTag)
                    {
                        if (message.Data is string)
                        {
                            messageBuilder.Append(message.Data.ToString()+"\n");
                        }
                    }
                }

                Echo(messageBuilder.ToString());
            }

            if ((updateSource & UpdateType.Once) == UpdateType.Once)
            {
                RunStateMachine();
            }
        }

        public void RunStateMachine()
        {
            if (_stateMachine != null)
            {
                bool hasMoreSteps = _stateMachine.MoveNext();
                if (hasMoreSteps)
                {
                    Runtime.UpdateFrequency |= UpdateFrequency.Once;
                }
                else
                {
                    _stateMachine.Dispose();
                    // _stateMachine = RunStuffOverTime();
                    _stateMachine = null;
                }
            }
        }
        // var pt1 = (X: 3, Y: 0);

        // states methods
        public IEnumerator<bool> RunStuffOverTime()
        {
            yield return true;
            while (true)
            {
                yield return true;
            }
        }

    }
}
