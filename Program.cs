using StereoKit;
using JumbliVR;
using StereoKit.Framework;
using System.Collections;

internal class Program
{
    private static void Main(string[] args)
    {
        SK.Initialize();

        TextStyle iconTextStyle = TextStyle.Default; // Replace with your custom icon text style;

        // The hand menu is a good way to launch the locomotion controllers 
        // because the controller is created in the hand's current position
        HandMenuRadial handMenu = SK.AddStepper(new HandMenuRadial(
            new HandRadialLayer("Locomotion",
            // Launches the locomotion controller
            new HandMenuItem("Move", null, new Action(delegate () { Locomotion.ActivateController(Input.Hand(Handed.Right)); })),
            // Launches the volumetric teleport menu
            new HandMenuItem("Go to", null, new Action(delegate () { Locomotion.ActivateTeleport(Input.Hand(Handed.Right), CustomIconRenderer); })),
            new HandMenuItem("Settings", null, new Action(delegate () { Locomotion.ActiveSettingsWindow(); }))
            )));


        // Update this list of teleport destination as they become available or unavailable
        Locomotion.teleportDestinations = new Dictionary<string, Pose>
        {
            {"Home", Pose.Identity},
            {"West side", new Pose(V.XYZ(-4,0,0), Quat.FromAngles(0,90,0))},
            {"East side", new Pose(V.XYZ(4,0,0), Quat.FromAngles(0,270,0))},
            {"High", new Pose(V.XYZ(0,4,0), Quat.FromAngles(0,0,0))},
            {"South", new Pose(V.XYZ(0,0,4), Quat.FromAngles(0,180,0))}
        };

        // Create some stuff to move around
        Random r = new Random();
        Dictionary<Vec4, Color> env = new Dictionary<Vec4, Color>();
        BuildEnvironment();

        // Set up hands that can fade out when required
        Color handColor = Color.HSV(.8f, .5f, .5f);
        
        
        Material handMaterial = Hands.CreateMaterial();
        handMaterial.DepthTest = DepthTest.Always;
        handMaterial.DepthWrite = false;

        Hands.SetHandMaterial(handMaterial, handColor);

        SK.Run(() =>
        {
            // Draw some stuff
            foreach (KeyValuePair<Vec4, Color> kvp in env)
                Mesh.Sphere.Draw(Material.Default, Matrix.TS(kvp.Key.XYZ, SKMath.Lerp(.1f,.5f,kvp.Key.w)), kvp.Value);

            Text.Add("North",Matrix.TRS(Vec3.Forward * 5f,Quat.FromAngles(0,180,0),10f),Color.Black);
            Text.Add("South",Matrix.TRS(Vec3.Forward * -5f,Quat.FromAngles(0,0,0),10f),Color.Black);
            Text.Add("East",Matrix.TRS(Vec3.Right * 5f,Quat.FromAngles(0,90,0),10f),Color.Black);
            Text.Add("West",Matrix.TRS(Vec3.Right * -5f,Quat.FromAngles(0,270,0),10f),Color.Black);
            Text.Add("Home",Matrix.TRS(Vec3.Up * -1.6f,Quat.FromAngles(90,180,0),10f),Color.Black);

            // Handle locomotion
            if (Locomotion.Draw(handColor))
                Hands.HideHand(Locomotion.handBeingUsed.handed);
            // Show a window when required
            ModalWindow.Draw();
            // Fade in/out hands or make invisible as requested by other components
            Hands.DrawHands();
        });

        // The volumetric menu will callback to CustomIconRenderer to display any custom icons
        // To use a custom icon, pass the character for the icon prefixed with an asterisk, e.g. "*\xABCD";
        void CustomIconRenderer(Pose pose, Color color, string icon)
        {
            Text.Add(icon, pose.ToMatrix(), iconTextStyle, color, TextAlign.Center, TextAlign.Center);
        }

        void BuildEnvironment()
        {

            int count = 100;
            int envRange = 5;
            while (count > 0)
            {
                Vec4 v = new Vec4(r.NextInt64(envRange * -1, envRange), r.NextInt64(envRange * -1, envRange), r.NextInt64(envRange * -1, envRange), r.NextSingle());
                if (env.ContainsKey(v) == false)
                {
                    env.Add(v, Color.HSV(r.NextSingle(), .5f, .5f));
                    count--;
                }
            }            
        }
    }
}