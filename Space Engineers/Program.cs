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
            //state
        //Vector3D _turtleLocation; // grid coord 
        //Vector3D _turtleFacing;   // yaw point
            //flags
        bool _suspended = false;
        bool _initialized = false;
            //hardware
        IMyGyro gyro;
        IMyShipConnector connector;
        IMyRemoteControl remote; 



        IEnumerator<bool> _stateMachine;
        IEnumerator<int> _subState;


        List<IMyTerminalBlock> _blockList = new List<IMyTerminalBlock>();
        StringBuilder _messageBuilder = new StringBuilder();

        public void Main(string argument, UpdateType updateSource)
        {
            //receive broadcasts
            if ((updateSource & UpdateType.IGC) > 0)
            {
                HandleMessageInit(updateSource);
                HandleMessageMap(updateSource);
                Echo(_messageBuilder.ToString());
            }

            // run normally
            if ((updateSource & UpdateType.Once) == UpdateType.Once)
            {
                if (!_suspended & _initialized)
                {
                    RunStateMachine(); 
                }
                Runtime.UpdateFrequency |= UpdateFrequency.Once;
            }

            _messageBuilder.Clear();
        }

        public void RunStateMachine()
        {
            if (_stateMachine != null)
            {
                if (!_stateMachine.MoveNext())
                {
                    _stateMachine.Dispose();
                    _stateMachine = null;
                }
            }
        }

        public IEnumerator<bool> Run()
        {
            int programCounter = 0;
            while (true) //main loop
            {
                while (programCounter == 0)
                {
                    programCounter += wrapper(test);
                    yield return true;
                }

                Echo("Onion\n");
                yield return true;
            }
            
        }


        public int wrapper(Func<IEnumerator<int>> funcName)
        {
            if (_subState == null)
            {
                _subState = funcName();
            }
            else
            {
                if (!_subState.MoveNext())
                {
                    _subState.Dispose();
                    _subState = null;
                    return 1;
                }
            }

            return 0;
        }
        public IEnumerator<int> test()
        {
            int count = 0;
            while (count<=100)
            {
                Echo(count++ +" Within test.\n");
                yield return 0;
            }
            yield return 1;
        }

        public void rotate(IMyGyro gyro, Vector3D rotationGoal)
        {
            Double heuristic = 200;
            bool atRotation = false;
            bool honing = false;
            bool reversed = false;

            Vector3D facePos = connector.GetPosition();
            float squaredDist = (float)(rotationGoal - facePos).LengthSquared();

            if (!atRotation)
            {
                //Echo("heuristic \n"+heuristic.ToString());
                //Echo("dist \n" + squaredDist.ToString());
                if (squaredDist < heuristic)
                {
                    if (heuristic != 200)
                    {
                        honing = true;
                        gyro.Yaw = gyro.Yaw * Math.Min((squaredDist / 45.0f), 1.2f);
                    }
                    heuristic = squaredDist;
                }
                else
                {
                    if (honing)
                    {
                        gyro.Yaw = 0;
                        gyro.GyroOverride = false;
                        atRotation = true;
                        //Echo("Eureka");
                    }
                    else
                    {
                        if (!reversed)
                        {
                            gyro.Yaw = -gyro.Yaw;
                            reversed = true;
                        }
                    }
                }
            }
        }

        
        private void HandleMessageMap(UpdateType updateSource)
        {
            while (_initialized & _turtleListenerMap.HasPendingMessage)
            {
                MyIGCMessage mapString = _turtleListenerMap.AcceptMessage();
                _messageBuilder.Append("Map update received.\n");
            }
            
        }

        private void HandleMessageInit(UpdateType message)
        {
            while (!_initialized | _turtleListenerInit.HasPendingMessage)
            {
                MyIGCMessage vectorString = _turtleListenerInit.AcceptMessage();
                string[] split = vectorString.Data.ToString().Split(';');
                var name = split[0];
                var data = split[1];
                switch (name)
                {
                    case "A":
                        Vector3D.TryParse(data, out _basisVecA);
                        break;
                    case "B":
                        Vector3D.TryParse(data, out _basisVecB);
                        break;
                    case "Up":
                        Vector3D.TryParse(data, out _basisVecUp);
                        break;
                    case "Origin":
                        Vector3D.TryParse(data, out _origin);
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
                _messageBuilder.Append("Successfully oriented.\nInitialized.");
                _turtleListenerInit.DisableMessageCallback();
            }
        }

        MyIni _ini = new MyIni();
        public Program()
        {
            //hardware
            remote = GridTerminalSystem.GetBlockWithName("Remote [t]") as IMyRemoteControl;
            gyro = GridTerminalSystem.GetBlockWithName("Gyroscope [t]") as IMyGyro;
            connector = GridTerminalSystem.GetBlockWithName("Connector [t]") as IMyShipConnector;
            _messageBuilder.Append("Hardware located.\n");
            //IGC
            _turtleListenerInit = IGC.RegisterBroadcastListener(_turtleInit);
            _turtleListenerInit.SetMessageCallback(_turtleInit);

            _turtleListenerMap = IGC.RegisterBroadcastListener(_turtleMap);
            _turtleListenerMap.SetMessageCallback(_turtleMap);
            _messageBuilder.Append("IGC loaded.\n");

            // load program state
            if (_ini.TryParse(Storage))
            {
                Vector3D.TryParse(_ini.Get("Basis", "A").ToString(), out _basisVecA);
                Vector3D.TryParse(_ini.Get("Basis", "B").ToString(), out _basisVecB);
                Vector3D.TryParse(_ini.Get("Basis", "Up").ToString(), out _basisVecUp);
                Vector3D.TryParse(_ini.Get("Basis", "Origin").ToString(), out _origin);
                _initialized = true;
                _messageBuilder.Append("Storage loaded.\nInitialized.");
            }
            else
            {
                _basisVecA = new Vector3D(0, 0, 0);
                _basisVecB = new Vector3D(0, 0, 0);
                _basisVecUp = new Vector3D(0, 0, 0);
                _origin = new Vector3D(0, 0, 0);
            }

            Echo(_messageBuilder.ToString());
            _stateMachine = Run();
            Runtime.UpdateFrequency |= UpdateFrequency.Once;
        }

        public void Save()
        {
            _ini.Clear();
            _ini.Set("Basis", "A", _basisVecA.ToString());
            _ini.Set("Basis", "B", _basisVecB.ToString());
            _ini.Set("Basis", "Up", _basisVecUp.ToString());
            _ini.Set("Basis", "Origin", _origin.ToString());
            Storage = _ini.ToString();
        }

    }
}
