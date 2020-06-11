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
        //world
        Vector3D[] _basis = new Vector3D[6];
            Vector3D _origin;
        //IGC
            string _turtleInit = "TURTLE INIT";
            IMyBroadcastListener _turtleListenerInit;
            string _turtleMap = "TURTLE MAP";
            IMyBroadcastListener _turtleListenerMap;
        //state
            //Vector3D _turtleLocation; // grid coord 
            //Vector3D _turtleFacing;   // yaw point
            Direction _rotationGoal;
        //flags
            bool _suspended = false;
            bool _initialized = false;
        //hardware
            IMyGyro gyro;
            IMyShipConnector connector;
            IMyRemoteControl remote; 
        //control
            IEnumerator<bool> _stateMachine;
            IEnumerator<int> _subState;

        List<IMyTerminalBlock> _blockList = new List<IMyTerminalBlock>();
        StringBuilder _messageBuilder = new StringBuilder();
        int runCount = 0;

        public void Main(string argument, UpdateType updateSource)
        {

            if ((updateSource & UpdateType.Terminal) > 0)
            {
                gyro.Yaw = 0;
                gyro.GyroOverride = false;
                _suspended = true;
            }
            //receive broadcasts
            if ((updateSource & UpdateType.IGC) > 0)
            {
                HandleMessageInit();
                HandleMessageMap();
                Me.CustomData += _messageBuilder.ToString();
                _messageBuilder.Clear();
            }

            // run normally
            if ((updateSource & UpdateType.Once) == UpdateType.Once)
            {
                
                runCount++;
                if (!_suspended & _initialized)
                {
                    _messageBuilder.Append("\tRUN:"+runCount+"\n");
                    if (!_stateMachine.MoveNext())
                    {
                        _stateMachine.Dispose();
                        _stateMachine = null;
                    }
                }
                Runtime.UpdateFrequency |= UpdateFrequency.Once;
            }
            if (runCount < 15 | ((runCount % 60) == 0 & (Me.CustomData.Length < 2000)))
            {
                Me.CustomData += "\n"+_messageBuilder.ToString();
            }
            _messageBuilder.Clear();
            
        }

        public IEnumerator<bool> Run()
        {
            var _programCounter = 0;
            //main loop
            while (true) 
            {
                _messageBuilder.Append("inside Main while.\n");
                while (_programCounter == 0)
                {
                    _rotationGoal = Direction.BNEG;
                    _programCounter += Wrapper(Rotate); 
                    yield return true;
                }


                yield return true;
            }
        }

        public IEnumerator<int> Rotate()
        {
            //init
            
            var rot = remote.CenterOfMass + (5 * _basis[(int) _rotationGoal]);
            Vector3D facePos;
            float squaredDist;
            float greed = 200.0f;
            bool reversed = false;
            bool honing = false;
            gyro.GyroOverride = true;
            gyro.Yaw = 0.3f;
            while (true)
            {
                facePos = connector.GetPosition();
                squaredDist = (float) (rot - facePos).LengthSquared();
                if (squaredDist < greed)
                {
                    if (greed != 200)
                    {
                        honing = true;
                    }
                    greed = squaredDist;
                }
                else
                {
                    if (squaredDist == greed)
                    {}
                    else if (honing)
                    {
                        gyro.Yaw = 0;
                        gyro.GyroOverride = false;
                        yield return 1;
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
                yield return 0;
            }
        }

        public int Wrapper(Func<IEnumerator<int>> funcName)
        {
            _messageBuilder.Append("inside wrapper while.\n");
            if (_subState == null)
            {
                _subState = funcName();
            }
            if (!_subState.MoveNext() | _subState.Current == 1)
            {
                _subState.Dispose();
                _subState = null;
                return 1;
            }
            else
                return 0;
        }

        private void HandleMessageMap()
        {
            while (_initialized & _turtleListenerMap.HasPendingMessage)
            {
                var mapString = _turtleListenerMap.AcceptMessage();
                _messageBuilder.Append("Map update received.\n");
            }
            
        }
        private void HandleMessageInit()
        {
            while (!_initialized | _turtleListenerInit.HasPendingMessage)
            {
                var vectorString = _turtleListenerInit.AcceptMessage();
                string[] split = vectorString.Data.ToString().Split(';');
                var name = split[0];
                var data = split[1];
                switch (name)
                {
                    case "A":
                        Vector3D.TryParse(data, out _basis[0]);
                        _basis[1] = -_basis[0];
                        break;
                    case "B":
                        Vector3D.TryParse(data, out _basis[2]);
                        _basis[3] = -_basis[2];
                        break;
                    case "Up":
                        Vector3D.TryParse(data, out _basis[4]);
                        _basis[5] = -_basis[4];
                        break;
                    case "Origin":
                        Vector3D.TryParse(data, out _origin);
                        break;
                }
            }
            if (Vector3D.IsZero(_origin)
                | Vector3D.IsZero(_basis[0])
                | Vector3D.IsZero(_basis[2])
                | Vector3D.IsZero(_basis[4]))
            {
                _initialized = false;
                _messageBuilder.Append("Could not orient.\nFailed to initialize.\n");
            }
            else
            {
                _initialized = true;
                _messageBuilder.Append("Successfully oriented.\nInitialized.\n");
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
                Vector3D.TryParse(_ini.Get("Basis", "A").ToString(), out _basis[0]);
                _basis[1] = -_basis[0];
                Vector3D.TryParse(_ini.Get("Basis", "B").ToString(), out _basis[2]);
                _basis[3] = -_basis[2];
                Vector3D.TryParse(_ini.Get("Basis", "Up").ToString(), out _basis[4]);
                _basis[5] = -_basis[4];
                Vector3D.TryParse(_ini.Get("Basis", "Origin").ToString(), out _origin);
                _initialized = true;
                _messageBuilder.Append("Storage loaded.\nInitialized.\n");
            }
            else
            {
                _basis[0] = new Vector3D(0, 0, 0);
                _basis[2] = new Vector3D(0, 0, 0);
                _basis[4] = new Vector3D(0, 0, 0);
                _origin = new Vector3D(0, 0, 0);
            }

            // custom data reset - log info
            Me.CustomData = "\t\tPROGRAM LOG:\n\n"+_messageBuilder.ToString();
            _messageBuilder.Clear();
            _stateMachine = Run();
            Runtime.UpdateFrequency |= UpdateFrequency.Once;
        }

        public void Save()
        {
            if (_initialized)
            {
                _ini.Clear();
                _ini.Set("Basis", "A", _basis[0].ToString());
                _basis[1] = -_basis[0];
                _ini.Set("Basis", "B", _basis[2].ToString());
                _basis[3] = -_basis[2];
                _ini.Set("Basis", "Up", _basis[4].ToString());
                _basis[5] = -_basis[4];
                _ini.Set("Basis", "Origin", _origin.ToString());
                Storage = _ini.ToString();
                Me.CustomData += _messageBuilder.ToString();
            }
        }

    }
}
