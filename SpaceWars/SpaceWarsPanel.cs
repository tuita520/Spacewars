
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SpaceWars
{
    public class SpaceWarsPanel : Panel
    {
        private Bitmap starImage = new Bitmap("..\\..\\images\\star.jpg");
        private List<Bitmap> shipCoastImages = new List<Bitmap>()
        {
           new Bitmap( "..\\..\\images\\ship-coast-blue.png"),
           new Bitmap( "..\\..\\images\\ship-coast-brown.png"),
           new Bitmap( "..\\..\\images\\ship-coast-green.png"),
           new Bitmap( "..\\..\\images\\ship-coast-grey.png"),
           new Bitmap( "..\\..\\images\\ship-coast-red.png"),
           new Bitmap( "..\\..\\images\\ship-coast-violet.png"),
           new Bitmap( "..\\..\\images\\ship-coast-white.png"),
           new Bitmap( "..\\..\\images\\ship-coast-yellow.png"),
        };
        private List<Bitmap> shipThrustImages = new List<Bitmap>()
        {
            new Bitmap("..\\..\\images\\ship-thrust-blue.png"),
            new Bitmap("..\\..\\images\\ship-thrust-brown.png"),
            new Bitmap("..\\..\\images\\ship-thrust-green.png"),
            new Bitmap("..\\..\\images\\ship-thrust-grey.png"),
            new Bitmap("..\\..\\images\\ship-thrust-red.png"),
            new Bitmap("..\\..\\images\\ship-thrust-violet.png"),
            new Bitmap("..\\..\\images\\ship-thrust-white.png"),
            new Bitmap("..\\..\\images\\ship-thrust-yellow.png"),
        };
        private List<Bitmap> shotImages = new List<Bitmap>()
        {
            new Bitmap("..\\..\\images\\shot-blue.png"),
            new Bitmap("..\\..\\images\\shot-brown.png"),
            new Bitmap("..\\..\\images\\shot-green.png"),
            new Bitmap("..\\..\\images\\shot-grey.png"),
            new Bitmap("..\\..\\images\\shot-red.png"),
            new Bitmap("..\\..\\images\\shot-violet.png"),
            new Bitmap("..\\..\\images\\shot-white.png"),
            new Bitmap("..\\..\\images\\shot-yellow.png"),
        };

        private ClientWorld theWorld;
        public SpaceWarsPanel(ClientWorld world)
        {
            DoubleBuffered = true;
            Location = new Point(0, 0);
            BackColor = Color.Black;
            theWorld = world;
        }

        /// <summary>
        /// Helper method for DrawObjectWithTransform
        /// </summary>
        /// <param name="size">The world (and image) size</param>
        /// <param name="w">The worldspace coordinate</param>
        /// <returns></returns>
        private static int WorldSpaceToImageSpace(int size, double w)
        {
            return (int)w + size / 2;
        }
        /// <summary>
        /// This method performs a translation and rotation to draw an object in the world.
        /// </summary>
        /// <param name="e">PaintEventArgs to access the graphics (for drawing)</param>
        /// <param name="o">The object to draw</param>
        /// <param name="worldSize">The size of one edge of the world (assuming the world is square)</param>
        /// <param name="worldX">The X coordinate of the object in world space</param>
        /// <param name="worldY">The Y coordinate of the object in world space</param>
        /// <param name="angle">The orientation of the objec, measured in degrees clockwise from "up"</param>
        /// <param name="drawer">The drawer delegate. After the transformation is applied, the delegate is invoked to draw whatever it wants</param>
        private void DrawObjectWithTransform(PaintEventArgs e, object o, int worldSize, double worldX, double worldY, double angle, ObjectDrawer drawer)
        {
            // Perform the transformation
            int x = WorldSpaceToImageSpace(worldSize, worldX);
            int y = WorldSpaceToImageSpace(worldSize, worldY);
            e.Graphics.TranslateTransform(x, y);
            e.Graphics.RotateTransform((float)angle);
            // Draw the object 
            drawer(o, e);
            // Then undo the transformation
            e.Graphics.ResetTransform();
        }

        // A delegate for DrawObjectWithTransform
        // Methods matching this delegate can draw whatever they want using e  
        public delegate void ObjectDrawer(object o, PaintEventArgs e);
        /// <summary>
        /// Acts as a drawing delegate for DrawObjectWithTransform
        /// After performing the necessary transformation (translate/rotate)
        /// DrawObjectWithTransform will invoke this method
        /// </summary>
        /// <param name="theGameObject">The object to draw</param>
        /// <param name="e">The PaintEventArgs to access the graphics</param>
        private void PlayerDrawer(object theGameObject, PaintEventArgs e)
        {
            Ship player = theGameObject as Ship;
            int width = theWorld.shipSize;
            int height = theWorld.shipSize;
            if (player.thrust == true)
                e.Graphics.DrawImage(shipThrustImages[player.ID % 8], -(width / 2), -(height / 2), width, height);
            else
                e.Graphics.DrawImage(shipCoastImages[player.ID % 8], -(width / 2), -(height / 2), width, height);
        }
        /// <summary>
        /// Acts as a drawing delegate for DrawObjectWithTransform
        /// After performing the necessary transformation (translate/rotate)
        /// DrawObjectWithTransform will invoke this method
        /// </summary>
        /// <param name="theGameObject">The object to draw</param>
        /// <param name="e">The PaintEventArgs to access the graphics</param>
        private void ProjectileDrawer(object theGameObject, PaintEventArgs e)
        {
            Projectile proj = theGameObject as Projectile;
            int width = theWorld.projSize;
            int height = theWorld.projSize;
            e.Graphics.DrawImage(shotImages[proj.owner % 8], -(width / 2), -(height / 2), width, height);
        }
        /// <summary>
        /// Acts as a drawing delegate for DrawObjectWithTransform
        /// After performing the necessary transformation (translate/rotate)
        /// DrawObjectWithTransform will invoke this method
        /// </summary>
        /// <param name="theGameObject">The object to draw</param>
        /// <param name="e">The PaintEventArgs to access the graphics</param>
        private void StarDrawer(object theGameObject, PaintEventArgs e)
        {
            Star s = theGameObject as Star;
            int width = theWorld.starSize;
            int height = theWorld.starSize;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawImage(starImage, -(width / 2), -(height / 2), width, height);
        }

        /// <summary>
        /// Called every time the panel is invalidated
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPaint(PaintEventArgs e)
        {
            lock (theWorld.worldLock)
            {
                foreach (Ship ship in theWorld.ships.Values)
                    if (ship.hp != 0)
                        DrawObjectWithTransform(e, ship, this.Size.Width, ship.loc.GetX(), ship.loc.GetY(), ship.dir.ToAngle(), PlayerDrawer);
                foreach (Projectile proj in theWorld.projectiles.Values)
                    DrawObjectWithTransform(e, proj, this.Size.Width, proj.loc.GetX(), proj.loc.GetY(), proj.dir.ToAngle(), ProjectileDrawer);
                foreach (Star star in theWorld.stars.Values)
                    DrawObjectWithTransform(e, star, this.Size.Width, star.loc.GetX(), star.loc.GetY(), 0, StarDrawer);
            }
            base.OnPaint(e);
        }
    } 
}