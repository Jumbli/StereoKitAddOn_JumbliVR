using StereoKit;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Text;

namespace JumbliVR
{
    // This class contains small features or workarounds that I find useful
    static class Utils
    {
        static public Pose GetPopupPose(Vec3 offset)
        {
            Pose pose = UI.PopupPose(offset);
            if (Vec3.Distance(pose.position, Input.Head.position) > 1 ||
                Vec3.Dot((Input.Head.position - pose.position).Normalized, Input.Head.Forward) < .8f)
            {
                pose.position = Input.Head.position + Input.Head.Forward * .5f;
                pose.orientation = Quat.LookAt(pose.position, Input.Head.position);
            }

            return pose;
        }

        static public Vec3 CalcNeckPosition()
        {
            return Input.Head.position + Input.Head.Up * -.15f;
        }

        static private Dictionary<string, float> alignments = new Dictionary<string, float>();

        static public void ResetAlignments()
        {
            alignments.Clear();
        }
        static public void AddLabelWithAlign(string commonId, string caption)
        {
            float startX = UI.LayoutAt.x;
            UI.Label(caption);
            UI.SameLine();
            float labelSize = Math.Abs(UI.LayoutAt.x - startX);
            float maxLabelSize = labelSize;
            if (alignments.ContainsKey(commonId))
            {
                maxLabelSize = Math.Max(alignments[commonId], maxLabelSize);

                float diff = maxLabelSize - labelSize;
                if (diff > 0)
                {
                    UI.Space(diff);
                }
            }
            alignments[commonId] = maxLabelSize;
        }

        static public Quat FlattenQuat(Quat q)
        {
            return Quat.LookDir((q * Vec3.Forward) * V.XYZ(1, 0, 1));
        }

        static public string UserXYZ(Vec3 v)
        {
            return
                "North: " + v.z.ToString("0.00") + "m, " +
                "East: " + v.x.ToString("0.00") + "m, " +
                "Height: " + v.y.ToString("0.00") + "m";
        }

        static public string GetTextWithMaxLength(string str, int maxCharaters, string noneLabel = "[none]", bool truncateText = false)
        {

            if (str.Length > maxCharaters)
            {
                if (truncateText)
                    return str.Substring(0, Math.Max(0, str.Length - (maxCharaters - 3))) + "...";
                else
                    return "..." + str.Substring(Math.Max(0, str.Length - (maxCharaters - 3)));
            }
            else
            {
                if (str == "")
                    return noneLabel;
                else
                    return str;
            }



        }
    }


}

