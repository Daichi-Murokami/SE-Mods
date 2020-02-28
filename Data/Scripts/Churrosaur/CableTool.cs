using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using VRage.Utils;

using Churrosaur.Bezier;
using Churrosaur.Cables;

using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Churrosaur.Cables
{
    // Wrenchport extends cableport, cable connects to this on the wrench
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Welder), true, "CableConnector")]
    public class WrenchPort : CablePort
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME
                        | MyEntityUpdateEnum.EACH_FRAME
                        | MyEntityUpdateEnum.EACH_10TH_FRAME;

            MyAPIGateway.Utilities.ShowNotification("WrenchPort Init");
            MyLog.Default.WriteLine("WrenchPort: after init");
        }

        public override void UpdateOnceBeforeFrame()
        {
            isWelder = true;
            //slim = null;
        }
    }

    /**
     * Wrench logic:
     * gets cable blocks that are hit by welder
     * creates and logs new cable on hit,
     * if empty it connects them block -> welder 
     * else connects block to block
     */
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Welder), true, "CableConnector")]
    public class HoseWrench : MyGameLogicComponent
    {
        #region fields

        IMyWelder welder;
        MyCasterComponent caster;
        bool isFiring;

        IMyTerminalBlock port1, port2;
        CablePort hitCablePort; // CableBlock.CablePort class
        WrenchPort wrenchCablePort;
        bool p1Set = false;

        Cable cable = null;

        #endregion

        #region setup, cleanup
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME
                        | MyEntityUpdateEnum.EACH_FRAME
                        | MyEntityUpdateEnum.EACH_10TH_FRAME;

            MyAPIGateway.Utilities.ShowNotification("CableTool Init " + Entity.EntityId.ToString());
            MyLog.Default.WriteLine("CableTool: after init");
        }

        public override void UpdateOnceBeforeFrame()
        {
            welder = Entity as IMyWelder;
            caster = Entity.Components.Get<MyCasterComponent>();
            wrenchCablePort = Entity.GameLogic.GetAs<WrenchPort>();
        }

        public override void Close()
        {
            base.Close();
            // if drawing, stop drawing if put away
            MyLog.Default.WriteLine("tool close");
            if (cable != null)
            {
                MyLog.Default.WriteLine("tool closing cable");
                cable.closeCable();
            }
                
        }
        #endregion

        #region welder checking
        public override void UpdateBeforeSimulation()
        {
            checkWelderFired();
        }

        // Triggers welderFired only on first tick of activation
        private void checkWelderFired()
        {
            if (welder.IsShooting && !isFiring)
            {
                onWelderFired();
                isFiring = true;
            }
            if (!welder.IsShooting && isFiring)
            {
                isFiring = false;
            }
        }

        public override void UpdateBeforeSimulation10()
        {

        }
        #endregion

        private void onWelderFired()
        {
            IMySlimBlock hit = null;

            // Return if nothing hit
            if (caster.HitBlock == null)
                return;

            // elif hit
            hit = caster.HitBlock as IMySlimBlock;
            MyAPIGateway.Utilities.ShowNotification(hit.FatBlock.EntityId.ToString());
            // test shenanigans-------------------
            var id = hit.FatBlock.EntityId;
            IMyEntity ent;
            if (MyAPIGateway.Entities.TryGetEntityById(id, out ent))
                MyAPIGateway.Utilities.ShowNotification(ent.EntityId.ToString() + "SUCCESS!");
            else
            {
                MyAPIGateway.Utilities.ShowNotification("failure to find");
            }



            // if cableport
            if (hit.FatBlock.BlockDefinition.SubtypeName == "CablePort")
                interactWithPort(hit);
        }

        private void interactWithPort(IMySlimBlock hit)
        {
            var hitEntity = hit as IMyEntity;
            var hitCablePort = hit.FatBlock.GameLogic.GetAs<Cables.CablePort>();

            // if hit port is unoccupied
            if (hitCablePort.cable == null)
            {
                MyAPIGateway.Utilities.ShowNotification("hit port unoccupied");
                // if welder is unoccupied
                if (cable == null)
                {
                    MyAPIGateway.Utilities.ShowNotification("creating new cable");
                    // new cable
                    cable = new Cable();
                    MyLog.Default.WriteLine("Tool: connect cable to self");
                    cable.connectToHead(hitCablePort);
                    cable.connectToTail(wrenchCablePort);
                    //CableSession.Instance.registerCable(cable);
                }
                // elif welder is already holding cable
                // - connect to new port
                else
                {
                    MyLog.Default.WriteLine("Tool: connect cable to cable");
                    cable.connectToTail(hitCablePort);
                    cable = null; // free welder
                }
            }
            else
            {
                // TODO occupied port error
                MyAPIGateway.Utilities.ShowNotification("Port occupied");
            }
            
        }
    }
}