using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities.Cube;
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
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), true, "CablePort")]
    public class CablePort : MyGameLogicComponent
    {

        #region fields
       
        // Reference objects
        public IMyCubeBlock cube;
        public Cable cable = null;

        public bool updateCable = true;
        public bool isWelder = false;  //toggled true in welderport init
        public Vector3D gravity { get; private set; }
        public Vector3D position { get; private set; }

        #endregion

        #region setup, cleanup
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME
                        | MyEntityUpdateEnum.EACH_FRAME
                        | MyEntityUpdateEnum.EACH_100TH_FRAME;

            MyAPIGateway.Utilities.ShowNotification("CableBlock Init");

            cube = Entity as IMyCubeBlock;

            MyLog.Default.WriteLine("CableBlock: after init");
        }

        public override void UpdateOnceBeforeFrame()
        {
            // overwritten in wrench               
            MyLog.Default.WriteLine("CableBlock: " + cube.DefinitionDisplayNameText);

            // Because cables are anonymous, pings the session with entityId to try and spawn any unconnected cables
            MyLog.Default.WriteLine("CableBlock: pinging session for orphans");
            CableSession.Instance.checkOrphans(Entity.EntityId);
        }

        public override void Close()
        {
            base.Close();
            if (cable != null)
            {
                cable.closeCable(); // will in turn call this.break
            }
        }

        #endregion

        public override void UpdateBeforeSimulation()
        {
            var newPosition = Entity.GetPosition();

            // determine cable update, 
            if (cable != null)
            {   
                if (position != newPosition)
                {
                    updateCable = true;
                    position = newPosition;
                }
                else
                {
                    updateCable = false;
                }
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            updateGrav();
        }

        private void updateGrav()
        {
            // get grav vector
            var tempGrav = MyParticlesManager.CalculateGravityInPoint(Entity.PositionComp.GetPosition());
            //MyAPIGateway.Utilities.ShowNotification(Entity.DisplayName + gravity.ToString());

            // if grav changed, flip flag, update grav
            if (tempGrav != gravity)
            {
                gravity = tempGrav;
                updateCable = true;
            }
        }

        public void breakCable()
        {
            cable = null;
        }
        public void connectCable(Cable c)
        {
            cable = c;
        }

    }
}