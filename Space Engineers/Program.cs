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
            IMyGyro gyro;
            IMyShipConnector connector;
            IMyRemoteControl remote;
        //control
            IEnumerator<int> _stateMachine;
            Queue<stateStruct> _stateQueue = new Queue<stateStruct>();
            stateStruct _currentState;
            struct stateStruct
            {
                public Action state;
                public Func<IEnumerator<int>> yieldAction;
            }
        // util
            StringBuilder _messageBuilder = new StringBuilder();
            private MyIni _ini = new MyIni();
            int _runCount = 0;
            List<IMyTerminalBlock> list = new List<IMyTerminalBlock>();
        
        public void Main(string argument, UpdateType updateSource)
        {
            _messageBuilder.Clear();

            if ((updateSource & UpdateType.Once) == UpdateType.Once)
            {
                Runtime.UpdateFrequency |= UpdateFrequency.Once;
                if (!_suspended & _initialized)
                {
                    _messageBuilder.Append("\tRUN: \n");
                    _runCount++;
                    _stateMachine.MoveNext();
                    var sCase = _stateMachine.Current;
                    switch (sCase)
                    {
                        case 0: //state incomplete
                            break;
                        case 1: //state finished
                        case 2:
                            if (sCase==1)
                                _stateQueue.Dequeue();
                            _runCount = 0;
                            _stateMachine.Dispose();
                            _stateMachine = null;
                            if (_stateQueue.Count > 0)
                            {
                                var next = _stateQueue.Peek();
                                next.state();
                                _stateMachine = next.yieldAction();
                            }
                            else
                            {
                                _stateMachine = Idle();
                            }
                            break;
                    }
                    if (_runCount < 15 | ((_runCount % 60) == 0 & (Me.CustomData.Length < 2000)))
                    {
                        Me.CustomData += "\n" + _messageBuilder;
                    }
                }//!_suspended & _initialized
                return;
            }

            //receive broadcasts
            if ((updateSource & UpdateType.IGC) == UpdateType.IGC)
            {
                _messageBuilder.Append("UpdateType: IGC\n");
                if (IGC.UnicastListener.HasPendingMessage)
                {
                    var message = IGC.UnicastListener.AcceptMessage();
                    if (message.Source == _turtleOperatorID)
                    {
                        string[] split = message.Data.ToString().Split(';');
                        var toEnqueue = new stateStruct();
                        var yieldAction = split[0];
                        var state = split[1];
                        switch (yieldAction)
                        {
                            case "R": //Rotate
                                toEnqueue.yieldAction = Rotate;
                                toEnqueue.state = () => _rotationGoal = Int32.Parse(state);
                                break;
                            case "F": //forward
                                break;
                            case "M": //Move
                                break;
                        }
                        _stateQueue.Enqueue(toEnqueue);
                    }
                }
                else if (_turtleListenerInit.IsActive)
                {
                    HandleMessageInit();
                }
                Me.CustomData += "\n" + _messageBuilder;
                return;
            }
            if ((updateSource & UpdateType.Trigger) == UpdateType.Trigger)
            {
                _messageBuilder.Append("UpdateType: Trigger\n");
                // sensor abrupt stop set _suspend
                Me.CustomData += "\n" + _messageBuilder;
            }
        }
        public IEnumerator<int> Idle()
        {
            var thrusters = GridTerminalSystem.GetBlockGroupWithName("Hover thrust");
            thrusters.GetBlocks(list);
            foreach (IMyThrust thruster in list)
            {
                var setOff = thruster.GetActionWithName("OnOff_Off");
                setOff.Apply(thruster);
            }
            yield return 2;
            while (true)
            {
                _messageBuilder.Append("IDLE\n");
                if (_stateQueue.Count > 0)
                {
                    thrusters.GetBlocks(list);
                    foreach (IMyThrust thruster in list)
                    {
                        var setOff = thruster.GetActionWithName("OnOff_On");
                        setOff.Apply(thruster);
                    }
                    yield return 1;
                }
                else
                    yield return 2;
            }
        }

        public IEnumerator<int> Move() //uses locationGoal
        {
            yield return 0;
            // ensure move successful 
            // tell turtle operator of move
            yield return 1;
        }

        public IEnumerator<int> forward() //uses rotationGoal
        {
            yield return 0;
            remote.ClearWaypoints();
            remote.SpeedLimit = 2.0f;
            remote.FlightMode = FlightMode.OneWay;
            var remotePos = remote.GetPosition();
            var diff = remotePos - remote.CenterOfMass;
            var wp = 2.5 * (_basis[_rotationGoal]) + remotePos + diff;
            remote.AddWaypoint(wp, "forward");
            var curPosition = remotePos;
            while (Vector3D.DistanceSquared(curPosition, wp)> 2) // requires tweaking
            {
                remote.SetAutoPilotEnabled(true);
                curPosition = remote.GetPosition();
                yield return 0;
            }
            remote.SetAutoPilotEnabled(false);
            // send unicast
            yield return 1;
        }

        public IEnumerator<int> Rotate()
        {
            yield return 0;
            gyro.GyroOverride = true;
            gyro.Yaw = 0.3f;
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
                if (prog >= 0.99)
                {
                    gyro.Yaw = 0;
                    gyro.GyroOverride = false;
                    // send unicast
                    yield return 1;
                }
                //adjust
                gyro.Yaw = (0.3f + (0.1f * (float) (1 - prog))) * (gyro.Yaw < 0 ? -1 : 1);
                if (prog - prevProg < 0 & !reversed)
                {
                    gyro.Yaw *= -1;
                    reversed = true;
                }
                yield return 0;
            }
        }

        private void HandleMessageInit()
        {
            while (!_initialized & _turtleListenerInit.HasPendingMessage)
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
                        _messageBuilder.Append("_turtleListenerInit: Unknown\n");
                        break;
                }
            }
            if (VectorsValid())
            {
                _initialized = true;
                _messageBuilder.Append("Successfully oriented.\nInitialized.\n");
                _turtleListenerInit.DisableMessageCallback();
            }
            else
            {
                _initialized = false;
                _messageBuilder.Append("Could not orient.\nFailed to initialize.\n");
            }
        }

        public Program()
        {
            //hardware
            remote = GridTerminalSystem.GetBlockWithName("Remote [t]") as IMyRemoteControl;
            gyro = GridTerminalSystem.GetBlockWithName("Gyroscope [t]") as IMyGyro;
            connector = GridTerminalSystem.GetBlockWithName("Connector [t]") as IMyShipConnector;
            _messageBuilder.Append("Hardware located.\n");
            //IGC
            IGC.UnicastListener.SetMessageCallback();
            _turtleListenerInit = IGC.RegisterBroadcastListener(_turtleInit);
            _turtleListenerInit.SetMessageCallback(_turtleInit);
            var op = GridTerminalSystem.GetBlockWithName("Operator [t]") as IMyProgrammableBlock;
            _turtleOperatorID = op.GetId();
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
                Vector3D.TryParse(_ini.Get("Location", "Origin").ToString(), out _origin);
                _initialized = true;
                _turtleListenerInit.DisableMessageCallback();
                _messageBuilder.Append("Storage loaded.\nInitialized.\n");
            }
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

        public bool VectorsValid()
        {
            return _origin != null & !Vector3D.IsZero(_origin) &
                   _basis[0]!=null & !Vector3D.IsZero(_basis[0]) &  
                   _basis[2]!=null & !Vector3D.IsZero(_basis[2]) &
                   _basis[4]!=null & !Vector3D.IsZero(_basis[4]);
        }
        
    }
}
