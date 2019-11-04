using Newtonsoft.Json;

namespace SpaceWars
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Ship : GameObject
    {
        [JsonProperty(PropertyName = "ship")]
        public override int ID { get; set; }

        [JsonProperty]
        public override Vector2D loc { get; set; }

        [JsonProperty]
        public Vector2D dir = new Vector2D(0, -1);

        [JsonProperty]
        public bool thrust = false;

        [JsonProperty]
        public string name;

        [JsonProperty]
        public override int hp { get; set; } = 5;
        public override int respawnTimer { get; set; }

        [JsonProperty]
        public int score = 0;

        public Ship()
        {

        }
    }
}
