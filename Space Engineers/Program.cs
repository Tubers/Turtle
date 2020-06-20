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
        //state
            //Vector3D _turtleLocation; // grid coord 
            Direction _rotationGoal;
        //flags
            bool _suspended = false;
            bool _initialized = false;
        //hardware
            IMyGyro gyro;
            IMyShipConnector connector;
            IMyRemoteControl remote; 
        //control
            IEnumerator<int> _stateMachine;
            Queue<> _actions = new Queue<>();
        // util
            StringBuilder _messageBuilder = new StringBuilder();
            private MyIni _ini = new MyIni();
            int _runCount = 0;
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
            // custom data reset - log info
            Me.CustomData = "\t\tPROGRAM LOG:\n\n" + _messageBuilder;
            Runtime.UpdateFrequency |= UpdateFrequency.Once;
        }
        public void Save()
        {
            if (areVectorsValid())
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
        public void Main(string argument, UpdateType updateSource)
        {
            _runCount++;
            //receive broadcasts
            if ((updateSource & UpdateType.IGC) == UpdateType.IGC)
            {
                //unicast state set messages
                if (IGC.UnicastListener.HasPendingMessage)
                {
                    var message = IGC.UnicastListener.AcceptMessage();
                    //enqueue a method
                }else if (_turtleListenerInit.IsActive)
                {
                    HandleMessageInit();
                }
            }
            //
            if ((updateSource & UpdateType.Trigger) == UpdateType.Trigger)
            {
                // sensor abrupt stop set _suspend
            }
            //
            if ((updateSource & UpdateType.Once) == UpdateType.Once)
            {
                Runtime.UpdateFrequency |= UpdateFrequency.Once;
                if (!_suspended & _initialized)
                {
                    _messageBuilder.Append("\tRUN: \n");

                    if (_stateMachine == null & _actions.Count>0)
                    {
                        var nextAction = _actions.Peek();
                        _stateMachine = nextAction();

                    }
                    else if (_stateMachine == null & _actions.Count==0)
                    {
                        _stateMachine = Idle();
                    }
                    if (!_stateMachine.MoveNext() | _stateMachine.Current == 1)
                    {
                        _stateMachine.Dispose();
                        _stateMachine = null;
                        _actions.Dequeue();
                    }

                }//!_suspended & _initialized
            }

            


            if (_runCount < 15 | ((_runCount % 60) == 0 & (Me.CustomData.Length < 2000)))
            {
                Me.CustomData += "\n"+_messageBuilder.ToString();
            }
            _messageBuilder.Clear();
        }

        public IEnumerator<int> Idle(Vector3D data)
        {
            // turn off hover thrusters
            while (_actions.Count == 0)
            {
                yield return 0;
            }
            // turn back on hover thrusters
            yield return 1;
        }
        public IEnumerator<int> Move(Vector3D data)
        {
            yield return 0;
            // ensure move successful 
            // tell turtle operator of move
            yield return 1;
        }

        public IEnumerator<int> Rotate(Vector3D data)
        {
            gyro.GyroOverride = true;
            gyro.Yaw = 0.3f;
            Vector3D direc;
            double prog = -1;
            double prevProg;
            bool reversed = false;
            var rot= _basis[(int)_rotationGoal];
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
            if (areVectorsValid())
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

        public bool areVectorsValid()
        {
            return
                   _origin!=null & Vector3D.IsZero(_origin) &
                   _basis[0]!=null & Vector3D.IsZero(_basis[0]) &  
                   _basis[2]!=null & Vector3D.IsZero(_basis[2]) &
                   _basis[4]!=null & Vector3D.IsZero(_basis[4]);
        }
        
        
    }
}
