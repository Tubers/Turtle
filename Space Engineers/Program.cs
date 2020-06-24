using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRage.GameServices;
using VRageMath;
using VRageRender;


namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        //world
            Vector3D[] _basis = new Vector3D[6];
            Vector3D _origin;
            Vector3D _stationConnector;
            //IGC
            string _turtleInit = "TURTLE INIT";
            IMyBroadcastListener _turtleListenerInit;
            long _turtleOperatorID;
        //state
            Vector3D _locationGoal; 
            int _rotationGoal;
        //flags
            bool _suspended = false;
            bool _initialized = false;
        //hardware
            IMyGyro _gyro;
            IMyShipConnector _connector;
            IMyRemoteControl _remote;
        //control
            IEnumerator<int> _stateMachine;
            Queue<stateStruct> _stateQueue = new Queue<stateStruct>();
            struct stateStruct
            {
                public Action paramFunc;
                public Func<IEnumerator<int>> stateFunc;
            }
            string _currentState = "Idle()";
        // util
            StringBuilder _messageBuilder = new StringBuilder();
            private MyIni _ini = new MyIni();
            int _runCount = 0;
            int _prevRunCount = 0;
            List<IMyTerminalBlock> list = new List<IMyTerminalBlock>();
            
        
        public void Main(string argument, UpdateType updateSource)
        {
            _messageBuilder.Clear();

            if ((updateSource & UpdateType.Once) == UpdateType.Once)
            {
                if (!_suspended & _initialized)
                {
                    _messageBuilder.AppendLine("\tRun");
                    _runCount++;
                    _stateMachine.MoveNext();
                    var sCase = _stateMachine.Current;
                    switch (sCase)
                    {
                        case 0: 
                            break;
                        case 1: 
                        case 2:
                            _prevRunCount = _runCount;
                            _runCount = 0;
                            _stateMachine.Dispose();
                            _stateMachine = null;
                            if (sCase == 1)
                            {
                                _stateQueue.Dequeue();
                            }
                            if (_stateQueue.Count > 0)
                            {
                                stateStruct next = _stateQueue.Peek();
                                                next.paramFunc();
                                _stateMachine = next.stateFunc();
                            }
                            else
                            {
                                _stateMachine = Idle();
                            }
                            break;
                    }
                    if (_runCount == 0)
                    {
                        string consoleString = string.Format("Queue: {0}\nState: {1}\nPrevious state took {2} ticks.", _stateQueue.Count, _currentState, _prevRunCount);
                        Echo(consoleString);
                        _messageBuilder.AppendLine(consoleString);
                        Me.CustomData += "\n" + _messageBuilder;
                    }
                }
                Runtime.UpdateFrequency |= UpdateFrequency.Once;
                return;
            }

            //receive broadcasts
            if ((updateSource & UpdateType.IGC) == UpdateType.IGC)
            {
                _messageBuilder.AppendLine("UpdateType: IGC");
                HandleOperatorMessage();
                HandleMessageInit();
                Me.CustomData += "\n" + _messageBuilder;
                return;
            }

            if ((updateSource & UpdateType.Trigger) == UpdateType.Trigger)
            {
                _messageBuilder.AppendLine("UpdateType: Trigger");
                // sensor abrupt stop set _suspend
                Me.CustomData += "\n" + _messageBuilder;
                return;
            }
        }

        public IEnumerator<int> Idle()
        {
            _currentState = "Idle()";
            yield return 0;
            while (true)
            {
                if (_stateQueue.Count > 0)
                {
                    yield return 2;
                }
                else
                    yield return 0;
            }
        }

        public IEnumerator<int> Move() //uses locationGoal
        {
            _currentState = "Move()";
            yield return 0;
            // ensure move successful 
            // tell turtle operator of move
            yield return 1;
        }
        public IEnumerator<int> Forward() //uses rotationGoal
        {
            _currentState = "Forward()";
            yield return 0;
            _remote.ClearWaypoints();
            _remote.SpeedLimit = 2.0f;
            _remote.FlightMode = FlightMode.OneWay;
            var remotePos = _remote.GetPosition();
            var diff = remotePos - _remote.CenterOfMass;
            var wp = 2.5 * (_basis[_rotationGoal]) + remotePos + diff;
            _remote.AddWaypoint(wp, "Forward");
            var curPosition = remotePos;
            yield return 0;
            while (Vector3D.DistanceSquared(curPosition, wp) > 2) // requires tweaking
            {
                _remote.SetAutoPilotEnabled(true);
                curPosition = _remote.GetPosition();
                yield return 0;
            }
            _remote.SetAutoPilotEnabled(false);
            yield return 1;
        }

        public IEnumerator<int> Rotate()
        {
            _currentState = "Rotate()";
            yield return 0;
            _gyro.GyroOverride = true;
            _gyro.Yaw = 0.3f;
            Vector3D direc;
            double prog = -1;
            double prevProg;
            bool reversed = false;
            var rot= _basis[_rotationGoal];
            yield return 0;
            while (true)
            {
                prevProg = prog;
                direc = Me.CubeGrid.WorldMatrix.Forward;
                prog = Vector3D.Dot(direc, rot);
                if (prog >= 0.995)
                {
                    _gyro.Yaw = 0;
                    _gyro.GyroOverride = false;
                    yield return 1;
                }
                _gyro.Yaw = (0.2f + (0.5f * (float) (1 - prog))) * (_gyro.Yaw < 0 ? -1 : 1);
                if (prog - prevProg < 0 & !reversed)
                {
                    _gyro.Yaw *= -1;
                    reversed = true;
                }
                yield return 0;
            }
        }
        void HandleOperatorMessage()
        {
            while (IGC.UnicastListener.HasPendingMessage)
            {
                var message = IGC.UnicastListener.AcceptMessage();
                if (message.Source == _turtleOperatorID)
                {
                    var data = message.Data.ToString();
                    string[] split = data.Split(';');
                    _messageBuilder.AppendLine(data);
                    var toEnqueue = new stateStruct();
                    var state = split[0];
                    var param = split[1];
                    switch (state)
                    {
                        case "R": //Rotate
                            toEnqueue.stateFunc = Rotate;
                            toEnqueue.paramFunc = () => _rotationGoal = int.Parse(param);
                            break;
                        case "F": //forward
                            break;
                        case "M": //Move
                            break;
                    }
                    _stateQueue.Enqueue(toEnqueue);
                }
            }
        }
        void HandleMessageInit()
        {
            if (!_initialized)
            {
                while (_turtleListenerInit.HasPendingMessage)
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
                        default:
                            _messageBuilder.AppendLine("_turtleListenerInit: Unknown");
                            break;
                    }
                }
                if (VectorsValid())
                {
                    _initialized = true;
                    _messageBuilder.AppendLine("Successfully oriented.\nInitialized.");
                    _turtleListenerInit.DisableMessageCallback();
                }
                else
                {
                    _initialized = false;
                    _messageBuilder.AppendLine("Could not orient.\nFailed to initialize.");
                }
            }
        }

        public Program()
        {
            //hardware
            _remote = GridTerminalSystem.GetBlockWithName("Remote [t]") as IMyRemoteControl;
            _gyro = GridTerminalSystem.GetBlockWithName("Gyroscope [t]") as IMyGyro;
            _connector = GridTerminalSystem.GetBlockWithName("Connector [t]") as IMyShipConnector;
            _messageBuilder.AppendLine("Hardware located.");
            //IGC
            IGC.UnicastListener.SetMessageCallback();
            _turtleListenerInit = IGC.RegisterBroadcastListener(_turtleInit);
            _turtleListenerInit.SetMessageCallback(_turtleInit);
            var op = GridTerminalSystem.GetBlockWithName("Operator [t]") as IMyProgrammableBlock;
            _turtleOperatorID = op.GetId();
            _messageBuilder.AppendLine("IGC loaded.");
            // load program state
            Load();
            // custom data reset - log info
            Me.CustomData = "\t\tPROGRAM LOG:\n\n" + _messageBuilder;
            Runtime.UpdateFrequency |= UpdateFrequency.Once;
            _stateMachine = Idle();
        }
        public void Save()
        {
            if (VectorsValid())
            {
                _ini.Clear();
                _ini.Set("Basis", "A", _basis[0].ToString());
                _basis[1] = -_basis[0];
                _ini.Set("Basis", "B", _basis[2].ToString());
                _basis[3] = -_basis[2];
                _ini.Set("Basis", "Up", _basis[4].ToString());
                _basis[5] = -_basis[4];
                _ini.Set("Location", "Origin", _origin.ToString());
                Storage = _ini.ToString();
            }
        }
        public void Load()
        {
            if (_ini.TryParse(Storage))
            {
                Vector3D.TryParse(_ini.Get("Basis", "A").ToString(), out _basis[0]);
                _basis[1] = -_basis[0];
                Vector3D.TryParse(_ini.Get("Basis", "B").ToString(), out _basis[2]);
                _basis[3] = -_basis[2];
                Vector3D.TryParse(_ini.Get("Basis", "Up").ToString(), out _basis[4]);
                _basis[5] = -_basis[4];
                Vector3D.TryParse(_ini.Get("Location", "Origin").ToString(), out _origin);
                _initialized = true;
                _turtleListenerInit.DisableMessageCallback();
                _messageBuilder.AppendLine("Storage loaded.\nInitialized.");
            }
        }
        public bool VectorsValid()
        {
            return _origin != null & !Vector3D.IsZero(_origin) &
                   _basis[0]!=null & !Vector3D.IsZero(_basis[0]) &  
                   _basis[2]!=null & !Vector3D.IsZero(_basis[2]) &
                   _basis[4]!=null & !Vector3D.IsZero(_basis[4]);
        }
        
    }
}
