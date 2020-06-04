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
        private const string Deployment = "$MDK_DATE$, $MDK_TIME$";
        #endregion

        Vector3D _basisVecA, _basisVecB, _basisVecUp, _origin;

        //IGC
        string _turtleInit = "TURTLE INIT";
        IMyBroadcastListener _turtleListenerInit;

        string _turtleMap = "TURTLE MAP";
        IMyBroadcastListener _turtleListenerMap;

        IEnumerator<bool> _stateMachine;
        MyIni _ini = new MyIni();

        List<IMyTerminalBlock> _blockList = new List<IMyTerminalBlock>();
        StringBuilder _messageBuilder = new StringBuilder();

        //state
        private Vector3D _gridLocation;
        private Vector3D _gridFacing;

        //flags
        private bool _suspended = true;
        private bool _initialized = false;

        public Program()
        {
            //IGC
            _turtleListenerInit = IGC.RegisterBroadcastListener(_turtleInit);
            _turtleListenerInit.SetMessageCallback(_turtleInit);

            _turtleListenerMap = IGC.RegisterBroadcastListener(_turtleMap);
            _turtleListenerMap.SetMessageCallback(_turtleMap);

            // load program state
            if (_ini.TryParse(Storage))
            {
                Vector3D.TryParse(_ini.Get("Basis", "A").ToString(), out _basisVecA);
                Vector3D.TryParse(_ini.Get("Basis", "B").ToString(), out _basisVecB);
                Vector3D.TryParse(_ini.Get("Basis", "Up").ToString(), out _basisVecUp);
                Vector3D.TryParse(_ini.Get("Basis", "Origin").ToString(), out _origin);
                _initialized = true;
                _messageBuilder.Append("Storage loaded\n");
            }
            else
            {
                _basisVecA = new Vector3D(0,0,0);
                _basisVecB = new Vector3D(0, 0, 0);
                _basisVecUp = new Vector3D(0, 0, 0);
                _origin = new Vector3D(0, 0, 0);
            }
            
            Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update100;
        }

        public void Save()
        {
            // save program state variables
            _ini.Clear();
            _ini.Set("Basis", "A", _basisVecA.ToString());
            _ini.Set("Basis", "B", _basisVecB.ToString());
            _ini.Set("Basis", "Up", _basisVecUp.ToString());
            _ini.Set("Basis", "Origin", _origin.ToString());
            Storage = _ini.ToString();
        }

        public void Main(string argument, UpdateType updateSource)
        {

            if ((updateSource & UpdateType.IGC) > 0)
            {
                //receive broadcasts
                HandleTurtleInit(updateSource);
                Echo(_messageBuilder.ToString());
            }

            //fuel check
            if ((updateSource & UpdateType.Update100) == UpdateType.Update100)
            {
                //save position, save facing
            }

            if ((updateSource & UpdateType.Once) == UpdateType.Once)
            {
                if (_suspended | !_initialized)
                {
                    Runtime.UpdateFrequency |= UpdateFrequency.Once;
                }
                else
                {   // run normally
                    RunStateMachine();
                }
            }
        }

        private void HandleTurtleInit(UpdateType message)
        {
            while (!_initialized | _turtleListenerInit.HasPendingMessage)
            {
                MyIGCMessage vectorString = _turtleListenerInit.AcceptMessage();
                string[] split = vectorString.Data.ToString().Split(';');
                var name = split[0];
                var data = split[1];
                switch (name)
                {
                    case"A":
                        Vector3D.TryParse(data.ToString(), out _basisVecA);
                        break;
                    case"B":
                        Vector3D.TryParse(data.ToString(), out _basisVecB);
                        break;
                    case"Up":
                        Vector3D.TryParse(data.ToString(), out _basisVecUp);
                        break;
                    case"Origin":
                        Vector3D.TryParse(data.ToString(), out _origin);
                        break;
                }
            }
            if (Vector3D.IsZero(_origin)
                | Vector3D.IsZero(_basisVecA)
                | Vector3D.IsZero(_basisVecB)
                | Vector3D.IsZero(_basisVecUp))
            {
                _initialized = false;
            }
            else
            {
                _initialized = true;
                _messageBuilder.Append("Successfully initialized");
                _turtleListenerInit.DisableMessageCallback();
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
                    //_stateMachine = RunStuffOverTime();
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
