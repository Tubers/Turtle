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
        /*
         
          Custom Data of programmable block
          [Basis]
          A=A
          B=B
          Up=Up
          Origin=Origin

        */

        MyIni _ini = new MyIni();
        private bool alreadyRun = false;

        //IGC
        string _turtleTag = "TURTLE TAG";
        IMyBroadcastListener _turtleListener;

        //Basis
        Vector3D _basisVecA, _basisVecB, _basisVecUp, _origin;

        //grid map

        public Program()
        {
            //load program state
            if (alreadyRun & _ini.TryParse(Storage))
            {
                Vector3D.TryParse(_ini.Get("Basis", "A").ToString(), out _basisVecA);
                Vector3D.TryParse(_ini.Get("Basis", "B").ToString(), out _basisVecB);
                Vector3D.TryParse(_ini.Get("Basis", "Up").ToString(), out _basisVecUp);
                Vector3D.TryParse(_ini.Get("Basis", "Origin").ToString(), out _origin);
            }
            //load config 
            else
            {
                MyIniParseResult result;
                if (!_ini.TryParse(Me.CustomData, out result))
                    throw new Exception(result.ToString());

                string nameA = _ini.Get("Basis", "A").ToString("A");
                IMyTerminalBlock blockA = GridTerminalSystem.GetBlockWithName(nameA);
                Vector3D pointA = blockA.GetPosition();

                string nameB = _ini.Get("Basis", "B").ToString("B");
                IMyTerminalBlock blockB = GridTerminalSystem.GetBlockWithName(nameB);
                Vector3D pointB = blockB.GetPosition();

                string nameUp = _ini.Get("Basis", "Up").ToString("Up");
                IMyTerminalBlock blockUp = GridTerminalSystem.GetBlockWithName(nameUp);
                Vector3D pointUp = blockUp.GetPosition();

                string nameOrigin = _ini.Get("Basis", "Origin").ToString("Origin");
                IMyTerminalBlock blockOrigin = GridTerminalSystem.GetBlockWithName(nameOrigin);
                _origin = blockOrigin.GetPosition();

                
                 _basisVecA = Vector3D.Normalize(pointA - _origin);
                 _basisVecB = Vector3D.Normalize(pointB - _origin);
                 _basisVecUp = Vector3D.Normalize(pointUp - _origin);
                 alreadyRun = true;
            }

            //IGC
            _turtleListener = IGC.RegisterBroadcastListener(_turtleTag);
            _turtleListener.SetMessageCallback(_turtleTag);

            Runtime.UpdateFrequency |= UpdateFrequency.Update100;
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



        public void Main(string argument, UpdateType updateSource)
        {
            BroadcastBasis();
        }

        private void BroadcastBasis()
        {
            IGC.SendBroadcastMessage(_turtleTag, "A;"+_basisVecA);
            IGC.SendBroadcastMessage(_turtleTag, "B;"+_basisVecB);
            IGC.SendBroadcastMessage(_turtleTag, "Up;"+_basisVecUp);
            IGC.SendBroadcastMessage(_turtleTag, "origin;"+_origin);
        }
    }
}
