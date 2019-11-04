using Newtonsoft.Json;

namespace SpaceWars
{
    /* This was a gameObject, but it should probably be more lightweight since there will be many of them (note: pre-optimizing, yes, but so far there's no need for it to inherit). 
     Theres usefulness in projectiles having components, say, if a new mechanic was bullets with extra damage, but it might be better in that case to just make a different 
     projectile class. Though we'd need to make sure it serializes the same for the client's sake.*/
    [JsonObject(MemberSerialization.OptIn)]
    public class Projectile : ISpaceWars
    {
        [JsonProperty(PropertyName = "proj")]
        public int ID { get; set; }

        [JsonProperty]
        public Vector2D loc { get; set; }

        [JsonProperty]
        public Vector2D dir;

        [JsonProperty]
        public bool alive;

        [JsonProperty]
        public int owner;

        public string ownerName;

        public Projectile() { }
    }
}
