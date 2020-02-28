// Contains cable object core and session for tracking/storing. My code is garbage sorry

using System.Collections.Generic;
using System;
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
using ProtoBuf;

using Churrosaur.Bezier;
using Churrosaur.Cables;

using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using Sandbox.Game.Entities;

namespace Churrosaur.Cables
{
    // Cable holds two ports, connects to and disconnects from them
    // creates gridgroup links
    public class Cable
    {
        public readonly long cableID;
        public Vector3D lengthVec;
        public CableStorage storage;
        public bool isOrphan = false;

        private CablePort headPort = null;
        private CablePort tailPort = null;
        private List<CablePort> ports = new List<CablePort>();
        private BezierDrawer drawer;

        static Color color = new Color(5, 5, 5, 255); // dark dark grey
        static readonly double snapLengthSquared = 400; // 20m
        static readonly double droop = 10; // cable sag, multiplied x gravity vector, manipulated by stretch

        #region Constructor
        public Cable()
        {
            MyAPIGateway.Utilities.ShowNotification("cable init");
            MyLog.Default.WriteLine("cable constructor");

            Random rand = new Random();
            cableID = rand.Next();
            storage = new CableStorage(cableID);
            drawer = new BezierDrawer(color); // TODO these should be static methods

            CableSession.Instance.registerCable(this);
        }
        public Cable(CablePort p) : this()
        {
            connectToHead(p);
        }
        public Cable(CableStorage storage)
        {
            MyAPIGateway.Utilities.ShowNotification("cable init");
            MyLog.Default.WriteLine("cable constructor from storage");

            cableID = storage.cableId;
            this.storage = new CableStorage(storage.cableId, storage.parentPortId, storage.childPortId);
            drawer = new BezierDrawer(color); // TODO these should be static methods 

            bool a = trySetPortsFromStorage();
            if (!a) isOrphan = true;


            CableSession.Instance.registerCable(this);
        }
        #endregion

        public bool trySetPortsFromStorage()
        {
            IMyEntity parentEntity, childEntity;
            bool a = MyAPIGateway.Entities.TryGetEntityById(storage.parentPortId, out parentEntity);
            bool b = MyAPIGateway.Entities.TryGetEntityById(storage.childPortId, out childEntity);
            if (a && b)
            {
                var parentPort = parentEntity.GameLogic.GetAs<CablePort>();
                var childPort = childEntity.GameLogic.GetAs<CablePort>();
                connectToHead(parentPort);
                connectToTail(childPort);
                return true;
            }
            else
            {
                MyLog.Default.WriteLine("Cable error: invalid ID's - setting as orphan cable");
                return false;
            }
        }

        #region Connect/Break
        // Cable is controller, manages connections on ports

        public void connectToHead(CablePort p)
        {
            MyAPIGateway.Utilities.ShowNotification("connect to cableparentport");
            MyLog.Default.WriteLine("Cable: connecting to parent/head");

            p.connectCable(this);

            if (headPort != null && headPort.cable != null) // reset if occupied
            {
                headPort.breakCable();
            }
            headPort = p;
            storage.parentPortId = p.Entity.EntityId;
        }

        public void connectToTail(CablePort p)
        {
            MyAPIGateway.Utilities.ShowNotification("connect to cablechildport");
            MyLog.Default.WriteLine("Cable: connecting to child/Tail");

            p.connectCable(this);

            if (tailPort != null && tailPort.cable != null)
            {
                tailPort.breakCable();
            }

            tailPort = p;
            storage.childPortId = p.Entity.EntityId;

            // if connecting childport to another block; link, store cable
            if (p.isWelder)
            {
                
            }
            else
            {
                linkGrids();
            }
        }

        
        public void closeCable()
        {
            MyLog.Default.WriteLine("cable closing");
            if (!tailPort.isWelder)
                breakGrids();

            headPort.breakCable();
            tailPort.breakCable();
            // Deletes references
            CableSession.Instance.deregisterCable(this);
        }
        #endregion

        #region Gridgroup Linking
        public void linkGrids()
        {
            MyLog.Default.WriteLine("linking...");
            //var a = parentPort.cube;
            //if (a == null)
            //    MyLog.Default.WriteLine("parent is null");
            //else
            //    MyLog.Default.WriteLine("parent is not null");

            MyLog.Default.WriteLine("cable: linking " + headPort.cube.DisplayNameText + tailPort.cube.DisplayNameText);
            //grid.CreateGridGroupLink(GridLinkTypeEnum, id, grid, otherGrid)
            MyCubeGrid.CreateGridGroupLink(GridLinkTypeEnum.Logical, cableID,
                                           headPort.cube.CubeGrid as MyCubeGrid,
                                            tailPort.cube.CubeGrid as MyCubeGrid);
        }
        public void breakGrids()
        {
            MyCubeGrid.BreakGridGroupLink(GridLinkTypeEnum.Logical, cableID,
                                           headPort.cube.CubeGrid as MyCubeGrid,
                                            tailPort.cube.CubeGrid as MyCubeGrid);
        }
        #endregion

        #region Update 
        public void update()
        {
            if (tailPort != null && headPort != null)
            {
                lengthVec = tailPort.position - headPort.position;

                // Snap if length between ports is > defined
                if (lengthVec.LengthSquared() >= snapLengthSquared)
                {
                    closeCable();
                }

                // Drawing
                if (headPort.updateCable || tailPort.updateCable)
                {
                    //bezier.createCurvePoints(pos, pos, handle, iterations)
                    drawer.createCurvePoints(headPort.position, tailPort.position, getSagHandle(), 3);
                }
                else
                {
                    // TODO add some conditional to not draw unseen cables
                }
                drawer.drawCurveFromList();
            }
        }

        private Vector3D getSagHandle()
        {
            // TODO add variable sag
            Vector3D mid = BezierDrawer.mid(headPort.position, tailPort.position);
            return mid + headPort.gravity.Normalize() * droop;
        }

        #endregion

    }

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | 
                                  MyUpdateOrder.AfterSimulation)]
    public class CableSession : MySessionComponentBase
    {
        public static string SAVE_FILENAME = "CableSave.b";
        public static readonly ushort MOD_UID = 1149; // TODO change to steamID

        public static CableSession Instance;

        private Dictionary<long, Cable> cableDict = new Dictionary<long, Cable>();
        private Dictionary<long, CableStorage> cableStorageDict = new Dictionary<long, CableStorage>();
        private CablesPacket cablePacket;

        // Flag to instantiate cables in update (packet received)
        bool needReloadCables = false;
        int reloadTimer = 0; // jank af. waiting for things to load in before trying to instantiate cables

        #region State overrides

        /**
         * register handlers
         * load cables from file
         * or load cables from server
        **/
        public override void LoadData()
        {
            Instance = this;

            // Subscribe to receiving cablemod packets
            MyAPIGateway.Multiplayer.RegisterMessageHandler(MOD_UID, receiveCablePacket);

            // If server
            // check for dictionary in file and load. 
            // If not present make new dictionary.

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                // Subscribe to player connection delegate
                MyVisualScriptLogicProvider.PlayerConnected += onPlayerConnect;

                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(SAVE_FILENAME, typeof(CablesPacket)))
                {
                    var reader = MyAPIGateway.Utilities.ReadBinaryFileInWorldStorage(SAVE_FILENAME, typeof(CablesPacket));
                    var bytes = reader.ReadBytes(2048); // arbitrary high
                    cablePacket = MyAPIGateway.Utilities.SerializeFromBinary<CablesPacket>(bytes);

                    MyLog.Default.WriteLine("CableSession: cableDict loaded");
                    cablePacket.printPacket();
                    needReloadCables = true;
                }
                else
                {
                    MyLog.Default.WriteLine("CableSession: new empty cabledict");
                    cablePacket = new CablesPacket(cableStorageDict);
                    cablePacket.printPacket();
                }

                //----------------tests------------------------
                //var reader = MyAPIGateway.Utilities.ReadBinaryFileInWorldStorage("testSave3.b", typeof(CableStorage));
                //var readerOut = MyAPIGateway.Utilities.SerializeFromBinary<CableStorage>(reader.ReadBytes(2048));
                //MyLog.Default.WriteLine("CableSession loaded: " + readerOut.str.i.ToString());
            }

            // If client

            else
            {

            }
        }

        // Instantiate Cables
        // if server, Sends cables to all clients
        public override void BeforeStart()
        {
            base.BeforeStart();

            // Instantiating cables from packet. This logs cables in cableDict and storage
            /*
            if (cablePacket != null && cablePacket.cableStorageDict != null)
            {
                instantiateCables();
            }
            else
            {
                MyLog.Default.WriteLine("beforeStart: no/empty cable packet");
            }
            */

            // Updates cablePacket from updated storageDict, sends to all clients to ensure sync
            // (In case local client joins early?)
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                //cablePacket.cableStorageDict = cableStorageDict;

                List<IMyPlayer> players = new List<IMyPlayer>(MyAPIGateway.Session.SessionSettings.MaxPlayers);
                MyAPIGateway.Players.GetPlayers(players);
                MyLog.Default.WriteLine("BeforeStart, sending packet to all players:");
                cablePacket.printPacket();
                foreach (IMyPlayer player in players)
                {
                    
                    if (player.SteamUserId == MyAPIGateway.Multiplayer.ServerId)
                    {
                        continue;
                    }
                    
                    try
                    {
                        var bytes = MyAPIGateway.Utilities.SerializeToBinary(cablePacket);
                        var steamID = player.SteamUserId;
                        MyAPIGateway.Multiplayer.SendMessageTo(MOD_UID, bytes, steamID);
                    }
                    catch (Exception e)
                    {
                        MyLog.Default.WriteLine(e.Message + "\n" + e.StackTrace);
                    }
                }
            }
            
        }

        // Update Cables
        // Catch to instantiate cables after first tick on load
        public override void UpdateAfterSimulation()
        {
            foreach (Cable c in cableDict.Values)
            {
                c.update();
            }

            // this is jank af, sorry
            if (needReloadCables)
            {
                MyLog.Default.WriteLine("Cables: Updating from packet");
                clearCables();
                instantiateCables();
                needReloadCables = false;
            }
        }

        // Save cables to file
        public override void SaveData()
        {
            // If server, saves dictionary of cables to world save
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                var packet = CreateAllCablesPacket();
                if (packet.cableStorageDict == null)
                {
                    MyLog.Default.WriteLine("saving null dict");
                }
                else
                {
                    MyLog.Default.WriteLine("saving dict: ");
                    printStoragedict();
                }
                var writer = MyAPIGateway.Utilities.WriteBinaryFileInWorldStorage(SAVE_FILENAME, typeof(CablesPacket));
                writer.Write(MyAPIGateway.Utilities.SerializeToBinary(packet));
                writer.Flush();
                writer.Close();

                //----------------tests------------
                //Cable c = new Cable();
                //testStruct str;
                ////str.c = c;
                //str.i = 5;

                //CableStorage storage = new CableStorage();
                //storage.str = str;

                //var writer = MyAPIGateway.Utilities.WriteBinaryFileInWorldStorage("testSave3.b", typeof(CableStorage));
                //writer.Write(MyAPIGateway.Utilities.SerializeToBinary(storage));
                //writer.Flush();
                //writer.Close();
            }
        }

        // Unregister message handlers
        protected override void UnloadData()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                MyVisualScriptLogicProvider.PlayerConnected -= onPlayerConnect;
            }
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(MOD_UID, receiveCablePacket);
        }

        #endregion

        public void registerCable(Cable c)
        {
            MyLog.Default.WriteLine("registering cable");
            cableDict.Add(c.cableID, c);
            cableStorageDict.Add(c.cableID, c.storage);
            
        }
        public void deregisterCable(Cable c)
        {
            cableDict.Remove(c.cableID);
            cableStorageDict.Remove(c.cableID);
        }

        private CablesPacket CreateAllCablesPacket()
        {
            var s = new CablesPacket();
            s.cableStorageDict = cableStorageDict;
            return s;
        }

        private void printStoragedict()
        {
            foreach(CableStorage cs in cableStorageDict.Values)
            {
                MyLog.Default.WriteLine("Printing storage dict: ");
                cs.printStorage();
            }
        }

        // Cycles through cables and checks if orphan cables matche parent with instantiated block
        public void checkOrphans(long id)
        {
            MyLog.Default.WriteLine("cables: checking orphans");
            foreach (Cable c in cableDict.Values)
            {
                if(c.isOrphan && (c.storage.childPortId == id || c.storage.parentPortId == id))
                {
                    MyLog.Default.WriteLine("cables: orphan found ports!");
                    if (c.trySetPortsFromStorage())
                    {
                        MyLog.Default.WriteLine("orphan connected to found port");
                    }
                    else
                    {
                        MyLog.Default.WriteLine("orphan could not connect to found port");
                    }
                    return;
                }
                MyLog.Default.WriteLine("No orphans found matching port");
            }
        }

        // called by event when packet received
        private void receiveCablePacket(byte[] bytes)
        {
            MyAPIGateway.Utilities.ShowNotification("Packet received!");
            MyLog.Default.WriteLine("Packet received. dict: ");
            cablePacket = MyAPIGateway.Utilities.SerializeFromBinary<CablesPacket>(bytes);
            cablePacket.printPacket();
            if (!MyAPIGateway.Multiplayer.IsServer)
            {
                MyLog.Default.WriteLine("Not server: ");
            }
            needReloadCables = true;
        }

        // called by delegate when player connects (only if server)
        private void onPlayerConnect(long id)
        {
            var steamID = MyAPIGateway.Multiplayer.Players.TryGetSteamId(id);
            MyAPIGateway.Utilities.ShowNotification("Player connected: " + steamID.ToString());
            MyLog.Default.WriteLine("Player connected: " + steamID.ToString());

            // if server connecting to itself, return
            if (steamID == MyAPIGateway.Multiplayer.ServerId)
            {
                MyLog.Default.WriteLine("Local client connected");
                return;
            }

            // send cablePacket to client
            cablePacket.printPacket();

            CablesPacket packetToSend;
            MyLog.Default.WriteLine("Sending packet from cableStoragedict");
            packetToSend = new CablesPacket(cableStorageDict);

            packetToSend.printPacket();

            MyLog.Default.WriteLine("Sending packet:");

            try
            {
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(packetToSend);
                MyAPIGateway.Multiplayer.SendMessageTo(MOD_UID, bytes, steamID);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine(e.Message + "\n" + e.StackTrace);
            }
        }

        private void instantiateCables()
        {
            MyLog.Default.WriteLine("Instantiating Cables");
            if (cablePacket == null)
            {
                MyLog.Default.WriteLine("Cable packet is null");
                return;
            }
            else MyLog.Default.WriteLine("Cable packet is not null");
            cablePacket.printPacket();
            foreach (CableStorage cs in cablePacket.cableStorageDict.Values)
            {
                new Cable(cs);
            }
        }

        private void clearCables()
        {
            List<Cable> temp = new List<Cable>();
            foreach (Cable c in cableDict.Values)
            {
                temp.Add(c);
            }
            foreach (Cable c in temp)
            {
                c.closeCable();
            }
        }

    }

    // Serializable packet of cable info.
    [ProtoContract]
    public class CableStorage
    {
        #region constructors
        public CableStorage() { }
        public CableStorage(long cableId)
        {
            this.cableId = cableId;
        }
        public CableStorage(long cableId, long parentPortId, long childPortId)
        {
            this.cableId = cableId;
            this.parentPortId = parentPortId;
            this.childPortId = childPortId;
        }
        #endregion

        [ProtoMember(1)]
        public testStruct str;

        [ProtoMember(2)]
        public long cableId = 0;

        [ProtoMember(3)]
        public long parentPortId = 0;

        [ProtoMember(4)]
        public long childPortId = 0;

        public void printStorage()
        {
            MyLog.Default.WriteLine("CableStorage: " + cableId.ToString() + " " + parentPortId.ToString() + " " + childPortId.ToString());
        }

    }

    // Serializable wrapper for dict of CableStorages
    [ProtoContract]
    public class CablesPacket
    {
        public CablesPacket() { }
        public CablesPacket(Dictionary<long,CableStorage> dict)
        {
            this.cableStorageDict = dict;
        }

        [ProtoMember(1)]
        public Dictionary<long, CableStorage> cableStorageDict;

        [ProtoMember(2)]
        public bool needsUpdate;

        public void printPacket()
        {
            if (cableStorageDict != null)
            {
                MyLog.Default.WriteLine("Packet storage exists");
                foreach (CableStorage cs in cableStorageDict.Values)
                {
                    cs.printStorage();
                }
            }
            else
            {
                MyLog.Default.WriteLine("Packet is empty");
            }
        }
    }

    [ProtoContract]
    public struct testStruct
    {
        [ProtoMember(1)]
        public int i;
    }

}