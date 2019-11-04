using NetworkController;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;
using System.Xml;

namespace SpaceWars
{
    public delegate void ProcessOverride(Ship player);
    public delegate void ShipDied<Killer>(int playerID, Killer killedBy) where Killer : ISpaceWars;
    public delegate void ProjectileHit<Collider, CollidedWith>(Collider dealer, CollidedWith affected) where Collider : ISpaceWars where CollidedWith : ISpaceWars;

    public class ServerWorld
    {
        private int STARTING_SHIP_HEALTH = 5;
        private int PROJECTILE_VELOCITY = 15;
        private double PROJECTILE_DAMAGE = 1;
        private int MAX_PROJECTILES = 1000;
        private double ENGINE_STRENGTH = 0.08;
        private double TURN_RATE = 3;
        public double SHIP_COLLISION_RADIUS = 20;
        private double STAR_COLLISION_RADIUS = 35;
        private int UNIVERSE_SIZE = 750;
        public int FRAME_TICK_TIME = 20;
        private int PROJECTILE_FIRE_DELAY = 6;
        public int RESPAWN_DELAY = 300;
        private class GameMode
        {
            public static void Activate_Standard()
            {
                Standard = true;
            }
            public static void Activate_Extra()
            {
                Extra = true;
            }
            private static bool standard;
            private static bool extra;
            public static bool Standard
            {
                get { return standard; }
                set
                {
                    if (value)
                        for (int i = 0; i < gameModes.Count; i++)
                            gameModes[i] = false;
                    standard = value;
                }
            }
            public static bool Extra
            {
                get { return extra; }
                set
                {
                    if (value)
                        for (int i = 0; i < gameModes.Count; i++)
                            gameModes[i] = false;
                    extra = value;
                }
            }
            private static List<bool> gameModes = new List<bool>() { standard, extra };
        }
        public event ShipDied<Ship> KilledShip;
        private event ProjectileHit<Projectile, GameObject> ProjectileCollided;
        private HashSet<Ship> deadShips = new HashSet<Ship>();
        private Dictionary<int, Ship> ships = new Dictionary<int, Ship>();
        private Dictionary<int, GameObject> duplicates = new Dictionary<int, GameObject>();
        private Dictionary<int, Star> stars = new Dictionary<int, Star>();
        private Dictionary<int, Projectile> projectiles = new Dictionary<int, Projectile>();
        private Queue<int> projectileRemoveOrder = new Queue<int>();
        private HashSet<SocketState> clients = new HashSet<SocketState>();
        private HashSet<SocketState> disconnectedClients = new HashSet<SocketState>();
        private Dictionary<int, StringBuilder> ClientMessages = new Dictionary<int, StringBuilder>();
        private HashSet<GameObject> Needs_Components_Applied = new HashSet<GameObject>();
        Stopwatch watch = new Stopwatch();
        public int framesElapsed = 0;
        private bool adjustFrameTick = false;
        Random randyX = new Random();
        Random randyY = new Random();
        public string additionalMessages = "";


        public ServerWorld()
        {
            Read_Server_Settings();
            Unique_GameObject_ID_Enum = Produce_Unique_GameObject_ID().GetEnumerator();
            Unique_Projectile_ID_Enum = Produce_Unique_Proj_ID().GetEnumerator();
            Add_GameObject<Star>(out int id);//might play with more stars in extra game mode later
            KilledShip += Process_Ship_Death;
            ProjectileCollided += Projectile_Collision_Handler;
        }




        #region Updates


        public void Start_Game_Update_Loop()
        {
            if (GameMode.Extra)
                Extra_Process_StartUp();
            watch.Start();
            int adjustingFrameTime = FRAME_TICK_TIME;
            while (true)
            {
                adjustingFrameTime = clients.Count < 20 ?
                     FRAME_TICK_TIME + 15 :
                    adjustFrameTick ? FRAME_TICK_TIME + 8 : FRAME_TICK_TIME;//frame time adjusted when load is higher or clients are connecting

                if (watch.ElapsedMilliseconds >= adjustingFrameTime)//prevents updating too often, updates don't happen until the next frame has begun then wait again till it has finished
                {
                    watch.Restart();
                    framesElapsed++;
                    Update_Clients();
                }
            }
        }
        private void Extra_Process_StartUp()
        {
            TURN_RATE = 7;
            PROJECTILE_FIRE_DELAY = 3;
            STAR_COLLISION_RADIUS = 80;
            SHIP_COLLISION_RADIUS = 20;
            ENGINE_STRENGTH = 0.22;
            RESPAWN_DELAY = 50;
            FRAME_TICK_TIME = 21;
            MAX_PROJECTILES = 800;
        }
        private void Update_Clients()
        {
            lock (this)
            {
                Update_World();
            }
            string message = this.toString();
            lock (clients)
            {
                foreach (SocketState client in clients)
                {
                    try { Networking.Send_Data(client.theSocket, message); }
                    catch (System.Net.Sockets.SocketException e)
                    { Console.WriteLine(e.Message); disconnectedClients.Add(client); }
                }
            }
            Process_Disconnected_Clients();
        }
        private void Process_Disconnected_Clients()
        {
            lock (ClientMessages)//we'll be modifying all of these so lock every one
            {
                lock (this)
                {
                    lock (clients)
                    {
                        foreach (var client in disconnectedClients)
                        {
                            clients.Remove(client);
                            ClientMessages.Remove(client.ID);
                            Ship deceased = ships[client.ID];
                            deceased.hp = 0;
                            additionalMessages += JsonConvert_SpaceObject(deceased);//need to send the dead ship one more time
                            ships.Remove(client.ID);
                        }
                        disconnectedClients.Clear();
                    }
                }
            }
        }
        private void Update_World()
        {
            Update_Dead_Ship_Timers();
            Kill_All_Thrusters();//All thrusters need to be shut off, if another thrust request is sent then it will be processed this frame and turned back on
            Remove_MIA_Projectiles();//Note: remove projectiles immediately: any that die this update need to be sent once more as dead, lower methods mark them as dead, next update they will be removed
            if (GameMode.Extra)
            {
                Assign_Components_To_GameObjects();//first step is to determine and assign all due components to all gameObjects
                Get_Those_Who_Need_Components_Applied_From(ships);//next we need to add these to the hashset for applying, this is because some components can have methods that modify collections (which is safe to do here since the world is locked and this is sequential)
                Get_Those_Who_Need_Components_Applied_From(stars);//DANGEROUS methods are only dangerous if used outside of a lock(this)
                Apply_Components();//now go through and apply for all who need it, this circumvents collection modification restrictions
                Update_Components_For(ships); //Note: Again, update world is locked with the world (as are adds and removes), so no method can add except for the "DANGEROUS add" used for components
            }
            Process_All_Client_Requests();
            Apply_All_Forces();
            Wrap_Ships_If_Out_Of_Bounds();
            Kill_Projectiles_that_Collided_With(ships, SHIP_COLLISION_RADIUS);
            Kill_Projectiles_that_Collided_With(stars, STAR_COLLISION_RADIUS);
            Kill_OutOfBounds_Projectiles();
            Kill_Projectiles_Past_Max();
            Kill_Icarus();

        }
        /// <summary>
        /// Wraps all ships
        /// </summary>
        private void Wrap_Ships_If_Out_Of_Bounds()
        {
            foreach (Ship player in ships.Values)
                Wrap_If_Out_Of_Bounds(player);
        }
        /// <summary>
        /// Negates the axis of bound that was crossed for a gameObject
        /// </summary>
        /// <param name="theGameObject"></param>
        private void Wrap_If_Out_Of_Bounds<ObjClass>(ObjClass theGameObject) where ObjClass : GameObject
        {
            double LocX = theGameObject.loc.GetX();
            double LocY = theGameObject.loc.GetY();
            int bounds = UNIVERSE_SIZE / 2;
            if (bounds < LocX || LocX < -bounds)
                theGameObject.loc.NegateX();
            if (bounds < LocY || LocY < -bounds)
                theGameObject.loc.NegateY();
        }
        /// <summary>
        /// If a projectile has collided with any member of the class of gameobjects passed in, then it's "alive" will be set to false. NOTE: projectile remains in dictionary.
        /// </summary>
        /// <typeparam name="collidedWith"></typeparam>
        /// <param name="theGameObjects"></param>
        private void Kill_Projectiles_that_Collided_With<collidedWith>(Dictionary<int, collidedWith> theGameObjects, double collisionRadius) where collidedWith : GameObject
        {
            foreach (var hit in Get_Collisions(projectiles, theGameObjects, projectile => projectile.alive, collisionRadius))
            {
                Projectile proj = hit.Key;
                collidedWith OBJ = hit.Value;
                ProjectileCollided(proj, OBJ);
            }
        }
        /// <summary>
        /// delegat called when a collision occurs
        /// </summary>
        /// <param name="proj"></param>
        /// <param name="affected"></param>
        private void Projectile_Collision_Handler(Projectile proj, GameObject affected)
        {
            Ship killer;
            if (!ships.TryGetValue(proj.owner, out killer))
            {
                duplicates.TryGetValue(proj.owner, out GameObject dup);//specific to components
                killer = dup as Ship;
            }
            if (affected.GetType().Equals(typeof(Ship)) && affected.hp > 0 && proj.owner != affected.ID)
            {
                affected.hp -= (int)PROJECTILE_DAMAGE;
                if (affected.hp == 0)
                    KilledShip(affected.ID, killer);
                proj.alive = false;
            }
            else if (affected.GetType().Equals(typeof(Star)))
            {
                proj.alive = false;
            }
        }
        /// <summary>
        /// Burns those who fly to close to the sun.
        /// </summary>
        private void Kill_Icarus()
        {
            foreach (var burn in Get_Collisions(stars, ships, temp => true, STAR_COLLISION_RADIUS))
            {
                burn.Value.hp = 0;
                deadShips.Add(ships[burn.Value.ID]);
            }
        }
        private void Kill_OutOfBounds_Projectiles()
        {
            int bounds = UNIVERSE_SIZE / 2;
            foreach (Projectile proj in projectiles.Values)
            {
                if (proj.loc.GetX() < -bounds || proj.loc.GetY() < -bounds ||
                   bounds < proj.loc.GetX() || bounds < proj.loc.GetY())
                    proj.alive = false;
            }

        }
        /// <summary>
        /// Removes all dead projectiles.
        /// </summary>
        private void Remove_MIA_Projectiles()
        {
            RemoveAll(projectiles, value => !value.alive);
        }
        /// <summary>
        /// specifically for gamemodes where a crap ton of projectiles can pile up on the screen
        /// </summary>
        private void Kill_Projectiles_Past_Max()
        {
            int countAbove = projectiles.Count + 1;
            while (countAbove-- > MAX_PROJECTILES)
                try { projectiles[projectileRemoveOrder.Dequeue()].alive = false; }
                catch { }//we don't really care if the projectile wasn't found because that means it was removed by some other means and we'll continue on our mary way
        }

        /// <summary>
        /// Wrapper that applies all physical forces to all gameobjects.
        /// </summary>
        private void Apply_All_Forces()
        {
            Apply_All_ProjectileMotion();
            Apply_Forces_To_All_Ships();
        }
        private void Kill_All_Thrusters()
        {
            foreach (Ship ship in ships.Values)
                ship.thrust = false;
        }
        /// <summary>
        /// delegate called when ship died event occurs
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="killer"></param>
        private void Process_Ship_Death(int playerID, Ship killer)
        {
            killer.score += 1;
            deadShips.Add(ships[playerID]);
        }
        /// <summary>
        /// Manages respawning
        /// </summary>
        private void Update_Dead_Ship_Timers()
        {
            foreach (Ship ship in deadShips)
            {
                ship.respawnTimer -= 1;
                if (ship.respawnTimer == 0)
                {
                    ship.hp = STARTING_SHIP_HEALTH;
                    ship.velocity = new Vector2D(0, 0);
                    ship.loc = Get_Spawn_Location();
                    ship.respawnTimer = RESPAWN_DELAY;
                }
            }
            deadShips.RemoveWhere(ship => ship.hp != 0);
        }

        public void End_HandShake(SocketState theClientState)
        {
            theClientState.messageProcessor = Initiate_Client;
            Networking.GetData(theClientState);
        }
        private void Initiate_Client(SocketState theClientState)
        {
            adjustFrameTick = true;//increasing frame time in case dozens of clients connect at once
            lock (clients)
            {
                string totalData = theClientState.sb.ToString();
                string[] messages = Regex.Split(totalData, @"(?<=[\n])");
                Add_Contained_GameObject(new Ship() { name = messages[0], loc = Get_Spawn_Location(), respawnTimer = RESPAWN_DELAY }, out int id);
                Networking.Send_Data(theClientState.theSocket, id + "\n" + UNIVERSE_SIZE + "\n");

                clients.Add(theClientState);
                theClientState.ID = id; //Note: Setting here because networking shouldn't know about serverWorld, and ship ID must be the same as Client ID
                theClientState.messageProcessor = Process_Network_Message;//handoff thread to main processing funtion
            }
            adjustFrameTick = false;
            Networking.GetData(theClientState);
        }
        private void Process_Network_Message(SocketState sender)
        {
            string totalData = sender.sb.ToString();

            string[] messages = Regex.Split(totalData, @"(?<=[\n])");
            foreach (string message in messages)
            {
                if (message.Length == 0)
                    continue;
                if (message[message.Length - 1] != '\n')
                    break;
                lock (ClientMessages)
                {
                    if (message[0].ToString() == "(" && message[message.Length - 2].ToString() == ")")
                    {
                        string newMessage = message.Substring(1, message.Length - 3);
                        if (ClientMessages.TryGetValue(sender.ID, out StringBuilder existingMessage))
                            ClientMessages[sender.ID] = existingMessage.Append(newMessage);
                        else ClientMessages[sender.ID] = new StringBuilder(newMessage);
                        /*sTring builder is used since multiple duplicate requests can be sent in the span of one frame, we only want to process 
                         * one of each command per frame so we use the Contains method below*/
                    }
                }
                sender.sb.Remove(0, message.Length);
            }
            Networking.GetData(sender);//keep the loop going
        }
        /// <summary>
        /// Client request processor, executes once per frame.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="playerID"></param>
        private void Process_All_Client_Requests()
        {
            lock (ClientMessages)//Can't have getData loops adding while we're processing
            {
                foreach (var message in ClientMessages)
                {
                    Ship player = ships[message.Key];
                    Process_Client_Request(player, message.Value);
                    message.Value.Clear();
                }
            }
        }
        private void Process_Client_Request(Ship player, StringBuilder message)
        {
            if (!deadShips.Contains(player))//No updates for dead ships, waste of resources and can adversely affect components or even general game play (such as a ghost ship firing projectiles...
                Process_Client_Request(player, message.ToString());     // not a bad idea to implement later)
        }
        /// <summary>
        /// Processes requests sent by client but allows action taken to be overriden.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="playerID"></param>
        /// <param name="overrideingProcessor">delegate used to process overriden player function</param>
        /// <param name="overrideSelector">send this string with the functions to override: "F" for fire; "R" for turn right; "L" for turn left; "T" for thrust; or any permutation of these</param>
        private void Process_Client_Request(Ship player, string message)
        {
            /*this structure allows multiple overide processes for each command. Want to reverse turning while also 
            making the ship thrust backwards and adding recoil AND shooting in a sweeping pattern? You got it!*/
            if (message.Contains("F"))//first checks if the client has requested this function
            {
                if (player.Try_Get_Overrides_For("F", out HashSet<ProcessOverride> fireProc))//then checks if there's overrides to apply
                    Process_Override(player, fireProc);//if so, uses them in helper below
                else Fire_Projectile(player);//else default
            }
            if (message.Contains("R"))
            {
                if (player.Try_Get_Overrides_For("R", out HashSet<ProcessOverride> rightProc))
                    Process_Override(player, rightProc);
                else Apply_Ship_Rotation(player, "R");
            }
            if (message.Contains("L"))
            {
                if (player.Try_Get_Overrides_For("L", out HashSet<ProcessOverride> leftProc))
                    Process_Override(player, leftProc);
                else Apply_Ship_Rotation(player, "L");
            }
            if (message.Contains("T"))
            {
                if (player.Try_Get_Overrides_For("T", out HashSet<ProcessOverride> thrustProc))
                    Process_Override(player, thrustProc);
                else player.thrust = true;
            }
        }
        private void Process_Override(Ship player, HashSet<ProcessOverride> processors)
        {
            foreach (ProcessOverride process in processors)
                process(player);
        }

        #endregion




        #region Getting Game Info


        /// <summary>
        /// Returns the entire client world state as a string
        /// </summary>
        /// <returns></returns>
        private string toString()
        {
            lock (this)//Can't have the world modified while getting it's state
            {
                string message = "";
                foreach (Ship ship in ships.Values)
                    message += JsonConvert.SerializeObject(ship) + "\n";
                foreach (Ship ship in duplicates.Values)
                    message += JsonConvert.SerializeObject(ship) + "\n";
                foreach (Projectile proj in projectiles.Values)
                    message += JsonConvert.SerializeObject(proj) + "\n";
                foreach (Star star in stars.Values)
                    message += JsonConvert.SerializeObject(star) + "\n";
                message += additionalMessages;
                //Console.WriteLine(message);
                return message;
            }
        } //overriding ToString was causing strange error in debugging, not overriding fixed it, don't thing the debugger likes Json
        /// <summary>
        /// public wrapper for sending json objects outside of normal world update procedure
        /// </summary>
        /// <typeparam name="ObjClass"></typeparam>
        /// <param name="theObject"></param>
        /// <returns></returns>
        public string JsonConvert_SpaceObject<ObjClass>(ObjClass theObject) where ObjClass : ISpaceWars
        {
            return JsonConvert.SerializeObject(theObject) + "\n";
        }
        private int Get_Unique_GameObject_ID
        {//Note: caused bug because enumerator starts at default(type)/null, movenext must come first.
            get
            {
                Unique_GameObject_ID_Enum.MoveNext();
                return Unique_GameObject_ID_Enum.Current;
            }
        }
        private int Get_Unique_Projectile_ID
        {
            get
            {
                Unique_Projectile_ID_Enum.MoveNext();
                return Unique_Projectile_ID_Enum.Current;
            }
        }
        private IEnumerable<int> Produce_Unique_GameObject_ID()
        {
            int i = 1;
            while (true)
                yield return i++;
        }
        private IEnumerable<int> Produce_Unique_Proj_ID()
        {
            int i = 1;
            while (true)
            {
                yield return i++;
            }
        }
        private IEnumerator<int> Unique_GameObject_ID_Enum;
        private IEnumerator<int> Unique_Projectile_ID_Enum;
        //Add methods are in this region because they supply gameObject Id's
        /// <summary>
        /// Used for objects that need a reference back to the world, only ships do for now because of the Dual Ship Component
        /// </summary>
        /// <typeparam name="ObjClass"></typeparam>
        /// <param name="ID"></param>
        public void Add_Contained_GameObject<ObjClass>(out int ID) where ObjClass : GameObject, new()
        {
            Add_GameObject(new ObjClass() { World = this }, out int id);
            ID = id;
        }
        public void Add_Contained_GameObject<ObjClass>(ObjClass theGameObject, out int ID) where ObjClass : GameObject, new()
        {
            theGameObject.World = this;
            Add_GameObject(theGameObject, out int id);
            ID = id;
        }
        /// <summary>
        /// NOT thread safe, used only when certain multiple threads cannot modify the Dictionary
        /// </summary>
        /// <typeparam name="ObjClass"></typeparam>
        /// <param name="theGameObject"></param>
        /// <param name="ID"></param>
        public void DANGEROUS_Add_Contained_GameObject<ObjClass>(ObjClass theGameObject, out int ID) where ObjClass : GameObject, new()
        {
            if (typeof(ObjClass).Equals(typeof(Ship)))
            {
                ID = Get_Unique_GameObject_ID;
                theGameObject.ID = ID;
                theGameObject.World = this;
                ships.Add(ID, theGameObject as Ship);
            }
            else if (typeof(ObjClass).Equals(typeof(Star)))
            {
                ID = Get_Unique_GameObject_ID;
                theGameObject.ID = ID;
                theGameObject.World = this;
                stars.Add(ID, theGameObject as Star);
            }
            else throw new JerkFaceException();
        }
        public void Add_GameObject<ObjClass>(out int ID) where ObjClass : ISpaceWars, new()
        {
            Add_GameObject(new ObjClass(), out int id);
            ID = id;
        }
        public void Add_GameObject<ObjClass>(ObjClass theGameObject, out int ID) where ObjClass : ISpaceWars, new()
        {
            lock (this)
            {
                if (typeof(ObjClass).Equals(typeof(Ship)))
                {
                    ID = Get_Unique_GameObject_ID;
                    theGameObject.ID = ID;
                    ships.Add(ID, theGameObject as Ship);
                }
                else if (typeof(ObjClass).Equals(typeof(Star)))
                {
                    ID = Get_Unique_GameObject_ID;
                    theGameObject.ID = ID;
                    stars.Add(ID, theGameObject as Star);
                }
                else if (typeof(ObjClass).Equals(typeof(Projectile)))
                {
                    ID = Get_Unique_Projectile_ID;
                    theGameObject.ID = ID;
                    projectiles.Add(ID, theGameObject as Projectile);
                }
                else throw new JerkFaceException();
            }
        }
        public void Add_Duplicate_Gameobject<ObjClass>(ObjClass theGameObject, out int ID) where ObjClass : GameObject
        {
            ID = Get_Unique_GameObject_ID;
            theGameObject.ID = ID;
            duplicates.Add(ID, theGameObject);
        }
        public void Remove_GameObject<ObjClass>(ObjClass theGameObject) where ObjClass : ISpaceWars
        {
            lock (this)
            {
                if (typeof(ObjClass).Equals(typeof(Ship)))
                    ships.Remove(theGameObject.ID);
                else if (typeof(ObjClass).Equals(typeof(Star)))
                    stars.Remove(theGameObject.ID);
                else if (typeof(ObjClass).Equals(typeof(Projectile)))
                    projectiles.Remove(theGameObject.ID);
                else throw new JerkFaceException();
            }
        }
        public void DANGEROUS_Remove_GameObject<ObjClass>(ObjClass theGameObject) where ObjClass : GameObject
        {
            if (typeof(ObjClass).Equals(typeof(Ship)))
                ships.Remove(theGameObject.ID);
            else if (typeof(ObjClass).Equals(typeof(Star)))
                stars.Remove(theGameObject.ID);
            else throw new JerkFaceException();
        }
        public void Remove_Duplicate<ObjClass>(ObjClass theGameObject) where ObjClass : GameObject
        {
            duplicates.Remove(theGameObject.ID);
        }
        /// <summary>
        /// Dictionary RemoveAll, since .NET don't got one. Removes every occurance that satisfies a predicate. 
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dictionary"></param>
        /// <param name="predicate"></param>
        public void RemoveAll<TValue>(Dictionary<int, TValue> dictionary, Func<TValue, bool> predicate) where TValue : ISpaceWars
        {
            var gameObjects = dictionary.Values.Where(gameObject => predicate(gameObject)).ToList();
            foreach (var obj in gameObjects)
                Remove_GameObject(obj);
        }
        private Vector2D Get_Spawn_Location()
        {

            int bounds = UNIVERSE_SIZE / 2;
            int x = bounds; int y = bounds;
            for (int i = 0; i < 15; i++)//tries 15 times then gives up
            {
                x = randyX.Next(-bounds, bounds);
                y = randyY.Next(-bounds, bounds);
                if (Is_Occupied(x, y, out Vector2D newLocation)) continue;
                else return newLocation;
            }
            return new Vector2D(x, y);
        }
        private bool Is_Occupied(int x, int y, out Vector2D locaction)
        {
            locaction = new Vector2D(x, y);
            foreach (Ship ship in ships.Values)
            {
                double spaceBetween = (ship.loc - locaction).Length();
                if (spaceBetween < SHIP_COLLISION_RADIUS * 2)
                {
                    return true;
                }
            }
            foreach (Star star in stars.Values)
            {
                double spaceBetween = (star.loc - locaction).Length();
                if (spaceBetween < SHIP_COLLISION_RADIUS * 2)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Returns a dictionary where the key is the projectile id and the value is the id of the object it hit
        /// </summary>
        /// <typeparam name="Collider"></typeparam>
        /// <param name="collider"></param>
        /// <returns></returns>
        private Dictionary<Collider, CollidedWith> Get_Collisions<Collider, CollidedWith>(Dictionary<int, Collider> collider, Dictionary<int, CollidedWith> collidedWith,
            Func<Collider, bool> predicate, double collisionRadius) where Collider : ISpaceWars where CollidedWith : ISpaceWars
        {//Whoa Nellie look at that signature!
            Dictionary<Collider, CollidedWith> collisions = new Dictionary<Collider, CollidedWith>();
            foreach (var gameObject in collidedWith.Values)
            {
                //resulting vector from subtracting object and projectile locations must have magnitude less than collision radius
                //only the first collision is taken for each ship since multiple projectiles may have collided with multiple ships
                try
                {
                    Func<Collider, bool> conditions = colliderObj => predicate(colliderObj) && (gameObject.loc - colliderObj.loc).Length() < collisionRadius;
                    if (gameObject.GetType().Equals(typeof(Star)))
                    {
                        foreach (var hit in collider.Values.Where(conditions))
                        {
                            collisions[hit] = gameObject;
                        }
                    }
                    else
                    {
                        Collider hit = collider.Values.First(conditions);
                        if (hit != null)
                            collisions[hit] = gameObject;
                    }

                }
                catch { };

            }
            return collisions;
        }
        #endregion



        #region Mechanics


        private void Apply_All_ProjectileMotion()
        {
            foreach (Projectile proj in projectiles.Values)
                Apply_ProjectileMotion(proj);
        }
        private void Apply_ProjectileMotion(Projectile proj)
        {
            proj.loc += proj.dir * PROJECTILE_VELOCITY;
        }
        private void Apply_Forces_To_All_Ships()
        {
            foreach (Ship player in ships.Values)
                Apply_Forces_To_Ship(player);
        }
        private void Apply_Forces_To_Ship(Ship player)
        {
            Vector2D acceleration = Compute_Gravity_Force_On(player);
            if (player.thrust)
                acceleration += Compute_Thrust(player);
            player.velocity += acceleration;
            player.loc += player.velocity;
        }
        private void Apply_Ship_Rotation(Ship player, string direction)
        {
            switch (direction)
            {
                case "L":
                    player.dir.Rotate(-TURN_RATE);
                    break;
                case "R":
                    player.dir.Rotate(TURN_RATE);
                    break;
            }
        }
        private Vector2D Compute_Thrust(Ship player)
        {
            return player.dir * ENGINE_STRENGTH;
        }
        private Vector2D Compute_Gravity_Force_On<ObjClass>(ObjClass theGameObject) where ObjClass : GameObject
        {
            Vector2D gravity = new Vector2D();
            foreach (Star star in stars.Values)
                gravity += (star.loc - theGameObject.loc).Normalize() * star.mass;
            return gravity;
        }
        public void Fire_Projectile(Ship player)
        {
            Fire_Projectile(player, new Vector2D(player.dir));
        }
        public void Fire_Projectile(Ship player, Vector2D orientation)
        {
            //Note: simply creating it with a direction is equivalent to firing since every existing projectile will be moved every frame
            if (framesElapsed % PROJECTILE_FIRE_DELAY == 0)
            {
                Add_GameObject(new Projectile() { alive = true, dir = orientation, owner = player.ID, loc = player.loc }, out int id);
                projectileRemoveOrder.Enqueue(id);
            }
        }

        #endregion



        #region Component Methods



        private bool ShipQualified<ComponentType>(Ship player) where ComponentType : Component
        {
            player.Get_Component<ComponentType>(out bool suceeded);
            if (suceeded == false) // we don't wan't to assign if the component is already applied
                return true;
            return false;

        }
        /// <summary>
        /// Wrapper that runs all functions related to assigning components to gameObjects
        /// Note: hp != 0, is most likely an unnecessary lambda condition as no requests are processed on dead ships
        /// </summary>
        private void Assign_Components_To_GameObjects()
        {
            Func<Ship, bool> quadCondition = ship => !Ship_Within_X_Of_Star(ship, STAR_COLLISION_RADIUS * 2.5);
            Assign_<DualShipPowerUp>(ship => ship.score % 3 == 0 && ship.score != 0 && ShipQualified<DualShipPowerUp>(ship)); //if ship score is a multiple of three they get dual ships, when they die they'll lose the component
            Assign_<QuadFirePowerUp>(ship => !quadCondition(ship) && ShipQualified<QuadFirePowerUp>(ship), quadCondition);
            Assign_<ProjectileControlPowerUp>(ship => ship.hp == 0 && ShipQualified<ProjectileControlPowerUp>(ship));
        }
        private void Update_Components_For<ObjClass>(Dictionary<int, ObjClass> theGameObjects) where ObjClass : GameObject
        {
            foreach (var gameObject in theGameObjects.Values)
            {
                gameObject.Update();
            }
        }
        /// <summary>
        /// Uses provided predicate to determine whether or not to add the power up
        /// </summary>
        /// <param name="applyCondition">condition that must be satisfied to apply component</param>
        private void Assign_<ComponentType>(Func<Ship, bool> applyCondition) where ComponentType : Component, new()
        {
            Assign_<ComponentType>(applyCondition, Component.defaultRemove);
        }
        /// <summary>
        /// Uses provided predicate to determine whether or not to add the power up, and to determine when it is removed
        /// </summary>
        /// <typeparam name="ComponentType"></typeparam>
        /// <param name="applyCondition"></param>
        /// <param name="removeCondition"></param>
        private void Assign_<ComponentType>(Func<Ship, bool> applyCondition, Func<Ship, bool> removeCondition) where ComponentType : Component, new()
        {
            foreach (Ship player in ships.Values)
                if (applyCondition(player))
                {
                    player.Add_Component(new ComponentType() { removeCondition = removeCondition });
                    player.needsComponentsApplied = true;
                }
        }
        private void Get_Those_Who_Need_Components_Applied_From<gameOBJ>(Dictionary<int, gameOBJ> theGameObjects) where gameOBJ : GameObject
        {
            foreach (var gameObject in theGameObjects.Values)
                if (gameObject.needsComponentsApplied)
                    Needs_Components_Applied.Add(gameObject);
        }
        private void Apply_Components()
        {
            foreach (GameObject gameObject in Needs_Components_Applied)
            {
                gameObject.Apply_Components();
                gameObject.needsComponentsApplied = false;
            }
            Needs_Components_Applied.Clear();
        }
        private bool Ship_Within_X_Of_Star(Ship player, double x)
        {
            foreach (Star star in stars.Values)
                if ((star.loc - player.loc).Length() < x)
                    return true;
            return false;
        }
        #endregion



        private void Read_Server_Settings()
        {
            using (XmlReader reader = XmlReader.Create("..\\..\\..\\SpaceWars\\GameSettings.xml"))
            {

                while (reader.Read())
                {
                    if (reader.IsStartElement())
                    {

                        switch (reader.Name)
                        {
                            case "STARTING_SHIP_HEALTH":
                                STARTING_SHIP_HEALTH = int.Parse(reader.ReadString());
                                break;

                            case "PROJECTILE_VELOCITY":
                                PROJECTILE_VELOCITY = int.Parse(reader.ReadString());
                                break;
                            case "PROJECTILE_DAMAGE":
                                PROJECTILE_DAMAGE = double.Parse(reader.ReadString());
                                break;
                            case "MAX_PROJECTILES":
                                MAX_PROJECTILES = int.Parse(reader.ReadString());
                                break;
                            case "ENGINE_STRENGTH":
                                ENGINE_STRENGTH = double.Parse(reader.ReadString());
                                break;
                            case "TURN_RATE":
                                TURN_RATE = int.Parse(reader.ReadString());
                                break;
                            case "DEFAULT_SHIP_COLLISION_RADIUS":
                                SHIP_COLLISION_RADIUS = double.Parse(reader.ReadString());
                                break;
                            case "DEFAULT_STAR_COLLISION_RADIUS":
                                STAR_COLLISION_RADIUS = double.Parse(reader.ReadString());
                                break;
                            case "UNIVERSE_SIZE":
                                UNIVERSE_SIZE = int.Parse(reader.ReadString());
                                break;
                            case "FRAME_TICK_TIME":
                                FRAME_TICK_TIME = int.Parse(reader.ReadString());
                                break;
                            case "PROJECTILE_FIRE_DELAY":
                                PROJECTILE_FIRE_DELAY = int.Parse(reader.ReadString());
                                break;
                            case "RESPAWN_DELAY":
                                RESPAWN_DELAY = int.Parse(reader.ReadString());
                                break;
                            case "GAME_MODE":
                                switch (reader.ReadString())
                                {
                                    case "STANDARD":
                                        GameMode.Activate_Standard();
                                        break;
                                    case "EXTRA":
                                        GameMode.Activate_Extra();
                                        break;
                                }
                                break;
                        }
                    }
                }
            }
        }
    }


}
