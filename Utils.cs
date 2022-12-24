using StereoKit;
using System;
using System.Collections.Generic;
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
    }


}

