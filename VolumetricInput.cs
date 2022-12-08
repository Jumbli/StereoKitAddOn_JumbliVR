using StereoKit;
using System;
using System.Collections.Generic;
using System.Text;

namespace JumbliVR
{

    // Presents a spherical menu and allows finger tip selection from the activating hand
    static class VolumetricInput
    {

        // The public arrangment allows you to further decorate rendered items if desired
        // Use this after the Draw() method is called.
        static public Dictionary<string, Arrangement> arrangement = new Dictionary<string, Arrangement>();
        public class Arrangement
        {
            public Pose pose;
            public Vec2 size;
        }

        //Configuration
        static public float lineThicknessMin = .002f;
        static public float lineThicknessMax = lineThicknessMin * 3f;
        static public float TextSizeMin = .3f;
        static public float TextSizeMax = .5f;

        static private int previousBestInx = -1;
        static private bool resetFingerRequired = false;

        static private string[] menuSet = {};

        static private Hand hand = Input.Hand(Handed.Right);
        static private Action<Pose, Color, string> ?customTextRenderer;

        // Menu items starting with a asterisk will call the customRenderer to allow icons to be drawn
        // Icons may be used for a backspace character, close symbol etc.
        static public void Activate(Hand usingHand, string[] menuItems, Action<Pose, Color, string> ?customRenderer)
        {
            hand = usingHand;
            menuSet = menuItems;
            customTextRenderer = customRenderer;
            arrangement.Clear();
            foreach (string item in menuItems)
            {
                arrangement.Add(item, new Arrangement() { size = Text.Size(item)});
            }
        }


        static private Pose itemPose = Pose.Identity;

        // Returns the name of the selected item or an empty string
        // Push a Hierarchy before calling to set your desired position
        static public string Draw(Color lineColor, Color textColor)
        {
            Hands.OverrideOpacity(.2f, hand.handed);

            Color linearLineColor = lineColor.ToLinear();
            Color linearLineColorFaded = linearLineColor;
            linearLineColorFaded.a = .8f;
            Color linearTextColor = textColor.ToLinear();
            Vec3 localHeadPosition = Hierarchy.ToLocal(Input.Head.position);

            string selected = "";

            int n = menuSet.Length;

            Vec3 tipLocal = Hierarchy.ToLocal(hand[FingerId.Index, JointId.Tip].position);

            Vec3 bestDir = Vec3.Zero;
            float bestDot = 0;
            int bestInx = -1;
            Vec3 directionToFinger = tipLocal.Normalized;
            float dist = Vec3.Distance(tipLocal, Vec3.Zero);

            float goldenRatio = (1 + (float)Math.Pow(5, 0.5)) / 2;

            for (int i = 0; i < n; i++)
            {

                // Spiral arrangement uses unoptimised code based on the following article
                // http://extremelearning.com.au/how-to-evenly-distribute-points-on-a-sphere-more-effectively-than-the-canonical-fibonacci-lattice/

                double theta = 2 * Math.PI * i / goldenRatio;
                double phi = Math.Acos(1 - 2 * (i + 0.5f) / n);
                double x = Math.Cos(theta) * Math.Sin(phi) * -1;
                double y = Math.Sin(theta) * Math.Sin(phi);
                double z = Math.Cos(phi) * -1;
                Vec3 directionToIcon = V.XYZ((float)x, (float)y, (float)z);

                string name = menuSet[i];
                itemPose.position = directionToIcon * .1f;
                itemPose.orientation = Quat.LookAt(itemPose.position, localHeadPosition);

                // Scale down distant text to exagerate depth
                float alpha = Math.Clamp((Vec3.Distance(localHeadPosition.Normalized * .1f,itemPose.position)) / .2f, 0, 1);
                float scale = SKMath.Lerp(TextSizeMax, TextSizeMin, alpha);

                if (name.StartsWith("*") && customTextRenderer != null && name.Length > 1)
                    customTextRenderer(itemPose, linearTextColor, name.Substring(1));
                else
                    Text.Add(name, itemPose.ToMatrix(scale), linearTextColor, TextAlign.Center, TextAlign.Center);

                arrangement[name].pose = itemPose;

                Lines.Add(directionToIcon * .05f, directionToIcon * .065f, linearLineColorFaded, lineThicknessMax);
                float dot = Vec3.Dot(directionToFinger, directionToIcon);

                if ((dot > .8f && dot > bestDot && resetFingerRequired == false) || (previousBestInx == i && resetFingerRequired))
                {
                    bestDot = dot;
                    bestDir = directionToIcon;
                    bestInx = i;
                }
            }

            if (resetFingerRequired && dist < .04f)
                resetFingerRequired = false; // Finger has been returned to the centre

            if (resetFingerRequired)
            {
                Mesh.Sphere.Draw(Material.Unlit, Matrix.S(.02f), linearLineColor);
                Lines.Add(bestDir * .01f, bestDir * .05f, linearLineColor, lineThicknessMax);
                Lines.Add(bestDir * .065f, bestDir * .085f, linearLineColor, lineThicknessMin);
                if (dist > .5f)
                    selected = "-1"; // Force a close if hand moved well away
            }
            else
            {
                Lines.Add(Vec3.Zero, bestDir * dist, linearLineColor, lineThicknessMax);
                if (dist < .05f)
                {
                    Lines.Add(bestDir * dist, bestDir * .05f, linearLineColor, lineThicknessMin);
                    Lines.Add(bestDir * .065f, bestDir * .085f, linearLineColor, lineThicknessMin);
                }

                if (dist < .03f) // We are near the centre position
                {
                    previousBestInx = -1;
                }
                else
                {
                    previousBestInx = bestInx;
                    if (dist >= .05f)
                    {
                        resetFingerRequired = true;
                        if (bestInx >= 0) // We have selected an icon
                        {
                            Sound.Click.Play(itemPose.position);
                            selected = menuSet[bestInx];
                        }
                    }
                }
            }

            return selected;

        }
    }
}
