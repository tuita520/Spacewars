using System;
using System.Collections.Generic;

namespace SpaceWars
{
    public interface ISpaceWars
    {
        Vector2D loc { get; set; }
        int ID { get; set; }
    }

    public abstract class GameObject : ISpaceWars
    {
        public abstract int ID { get; set; }
        public abstract int hp { get; set; }
        public abstract int respawnTimer { get; set; }
        public abstract Vector2D loc { get; set; }
        public Vector2D velocity = new Vector2D(0, 0);
        public ServerWorld World { get; set; }
        //public bool gravityOnMe { get;set;} //whether gravity has any affect on this object
        public bool needsComponentsApplied = false;
        public HashSet<Component> components { get; set; } = new HashSet<Component>();
        public Dictionary<string, HashSet<ProcessOverride>> overridingProcessors = new Dictionary<string, HashSet<ProcessOverride>>();

        public void Update()
        {
            foreach (var component in components)
                if (component.active)
                    component.Update();
            components.RemoveWhere(comp => comp.active == false);
        }
        public void Apply_Components()
        {
            foreach (var component in components)
            {
                component.Apply();
                needsComponentsApplied = false;
            }
        }
        public virtual void Add_Component(Component component)
        {
            component.Container = this;
            components.Add(component);
        }
        public T Get_Component<T>(out bool suceeded) where T : Component
        {
            suceeded = false;
            foreach (Component component in components)
                if (component.GetType().Equals(typeof(T)))
                {
                    suceeded = true;
                    return (T)component;
                }
            return null;
        }

        public void Add_Override(string control, ProcessOverride overrideProcessor)
        {
            if (overridingProcessors.TryGetValue(control, out HashSet<ProcessOverride> processes))
                processes.Add(overrideProcessor);
            else overridingProcessors[control] = new HashSet<ProcessOverride>() { overrideProcessor };
        }
        public bool Try_Get_Overrides_For(string control, out HashSet<ProcessOverride> overrides)
        {
            if (overridingProcessors.TryGetValue(control, out HashSet<ProcessOverride> procedures) && procedures.Count > 0)
            { overrides = procedures; return true; }
            else overrides = null; return false;
        }
        public bool Remove_Override(string control, ProcessOverride overrideProcess)
        {
            if (Try_Get_Overrides_For(control, out HashSet<ProcessOverride> overrides))
            { overrides.Remove(overrideProcess); return true; }
            return false;
        }



    }

    public class Component
    {
        public GameObject Container { get; set; }
        public bool active = true;
        public virtual void Update() { }
        public virtual void Apply() { }
        static public Func<Ship, bool> defaultRemove = player => player.hp <= 0;
        public Func<Ship, bool> removeCondition;
    }


    /// <summary>
    /// Creates dual ships both assigned to one player, default condition says that when the player health becomes 0 the component will remove itself
    /// </summary>
    public class DualShipPowerUp : Component
    {
        private Ship owner;
        private Ship duplicate;
        int duplicateID;
        ProcessOverride DualFire;
        public DualShipPowerUp() : this(defaultRemove)
        { }
        public DualShipPowerUp(Func<Ship, bool> predicate)
        {
            removeCondition = predicate;
            DualFire = new ProcessOverride(Process_DualShip_Fire);
        }
        public override void Apply()
        {
            owner = Container as Ship;
            duplicate = new Ship() { name = "Bizarro" + owner.name };
            owner.World.Add_Duplicate_Gameobject(duplicate, out duplicateID);
            Duplicate_Ship_Info(owner, duplicate);
            owner.Add_Override("F", DualFire);
        }

        public override void Update()
        {
            if (!removeCondition(owner))
                Duplicate_Ship_Info(owner, duplicate);//duplicate might be affected by forces differently so logistics must be recalculated
            else
            {
                owner.Remove_Override("F", DualFire);
                duplicate.hp = 0;
                owner.World.additionalMessages += owner.World.JsonConvert_SpaceObject(duplicate);
                owner.World.Remove_Duplicate(duplicate);
                active = false;
            }
        }

        public void Process_DualShip_Fire(Ship player)
        {
            owner.World.Fire_Projectile(player);
            owner.World.Fire_Projectile(duplicate);
        }
        public void Duplicate_Ship_Info(Ship dupFrom, Ship dupTo)
        {//hacky method but necessary to implement gameplay without changing client
            Vector2D offset = dupFrom.dir.Get_Perpendicular() * (Container.World.SHIP_COLLISION_RADIUS * 2);   //location is perpendicular to orientation, multipied by the diameter
            dupTo.loc = dupFrom.loc - offset;
            //dupFrom.loc = new Vector2D(dupFrom.loc + offset);

            dupTo.velocity = new Vector2D(dupFrom.velocity);
            dupTo.dir = new Vector2D(dupFrom.dir);
            dupTo.thrust = dupFrom.thrust;
            dupTo.score = dupFrom.score;
        }
    }

    /// <summary>
    /// Allows a ship to fire in four directions, default condition says that when the player health becomes 0 the component will remove itself
    /// </summary>
    public class QuadFirePowerUp : Component
    {
        private Ship owner;
        public QuadFirePowerUp() : this(defaultRemove)
        { }
        public QuadFirePowerUp(Func<Ship, bool> predicate)
        {
            removeCondition = predicate;
        }
        public override void Apply()
        {
            owner = Container as Ship;
            owner.Add_Override("F", Quad_Fire_Process);
        }
        public void Quad_Fire_Process(Ship player)
        {
            Vector2D forward = new Vector2D(owner.dir);
            Vector2D backward = new Vector2D(-owner.dir);
            Vector2D right = forward.Get_Perpendicular();
            Vector2D left = -right;
            owner.World.Fire_Projectile(owner, forward);
            owner.World.Fire_Projectile(owner, backward);
            owner.World.Fire_Projectile(owner, right);
            owner.World.Fire_Projectile(owner, left);
        }
        public override void Update()
        {
            if (!removeCondition(owner)) return; //do nothing, update is only for removal, world needs to decide whether or not to fire based on user input
            else { active = false; owner.Remove_Override("F", Quad_Fire_Process); }
        }
    }
    public class ProjectileControlPowerUp : Component
    {
        private Ship owner;
        public int TOD;

        public ProjectileControlPowerUp()
        { }
        public ProjectileControlPowerUp(Func<Ship, bool> predicate)
        {
            removeCondition = predicate;
        }
        public override void Apply()
        {
            owner = Container as Ship;
            owner.Add_Override("F", Control_Fire);
            TOD = owner.World.framesElapsed;
            int frames = Get_Frame_Num(10);
            removeCondition = ship => owner.World.framesElapsed - TOD > frames;
        }
        public void Control_Fire(Ship player)
        {
            owner.World.Fire_Projectile(owner, owner.dir);
        }
        public override void Update()
        {
            if (!removeCondition(owner)) return; //do nothing, update is only for removal, world needs to decide whether or not to fire based on user input
            else { active = false; owner.Remove_Override("F", Control_Fire); }
        }
        public int Get_Frame_Num(int desiredWaitTime)
        {
            return (int)(1000.0 / owner.World.FRAME_TICK_TIME) * desiredWaitTime;
        }
    }
}
