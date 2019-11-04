using Newtonsoft.Json;

namespace SpaceWars
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Star : GameObject
    {
        [JsonProperty(PropertyName = "star")]
        public override int ID { get; set; }

        [JsonProperty]
        public override Vector2D loc { get; set; } = new Vector2D(0, 0);
        public override int hp { get; set; } = 1000;
        public override int respawnTimer { get; set; }

        [JsonProperty]
        public double mass = 0.015;

        public Star() { }
    }
}
