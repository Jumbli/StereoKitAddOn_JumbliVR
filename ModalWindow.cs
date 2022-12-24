using StereoKit;
using System;
using System.Collections.Generic;
using System.Text;

namespace JumbliVR
{
    static class ModalWindow
    {
        static bool isActive = false;

        public delegate void Notify();
        static public event Notify ?Activated;
        static public event Notify ?Closed;
        static public Action ?modalConfirmAction;
        static public Action ?modalDraw;

        static private Pose pose = new Pose();
        static private string message = "";

        static private Object? textStyle;

        public enum ButtonsToAdd
        {
            None = 0,
            Close,
            ConfirmCancel

        }
        static private ButtonsToAdd buttonsToAdd;

        static public void ActivateWithConfirmCancelButtons(Action confirmAction, string messageToDisplay = "", Object ?textStyleForWindow = null, Action ?customDraw = null)
        {
            modalConfirmAction = confirmAction;
            Activate(messageToDisplay, textStyleForWindow, customDraw);
            buttonsToAdd = ButtonsToAdd.ConfirmCancel;
        }

        static public void ActivateWithCloseButton(string messageToDisplay = "", Object ?textStyleForWindow = null, Action ?customDraw = null)
        {            
            Activate(messageToDisplay, textStyleForWindow, customDraw);
            buttonsToAdd = ButtonsToAdd.Close;
        }

        static public void Activate(string messageToDisplay, Object ?textStyleForWindow = null, Action ?customDraw = null)
        {
            modalDraw = customDraw;
            Activated?.Invoke();
            message = messageToDisplay;
            buttonsToAdd = ButtonsToAdd.None;

            pose = Utils.GetPopupPose(Vec3.Zero);
            if (Vec3.Distance(pose.position, Input.Head.position) > 1 ||
                    Vec3.Dot((Input.Head.position - pose.position).Normalized, Input.Head.Forward) < .8f)
            {
                pose.position = Input.Head.position + Input.Head.Forward * .5f;
                pose.orientation = Quat.LookAt(pose.position, Input.Head.position);
            }

            textStyle = textStyleForWindow;

            isActive = true;
        }


        static public void Draw()
        {
            if (isActive == false)
                return;

            UI.WindowBegin("ModalWindow", ref pose, UIWin.Body, UIMove.Exact);
            
            UI.LayoutReserve(V.XY(.2f, .0001f));

            // Draw content
            if (textStyle != null)
                UI.PushTextStyle((TextStyle)textStyle);

            if (message != "")
                UI.Label(message);

            if (modalDraw != null)
                modalDraw?.Invoke();
            
            if (textStyle != null)
                UI.PopTextStyle();


            // Add standard buttons
            switch (buttonsToAdd)
            {
                case ButtonsToAdd.Close:
                    if (UI.Button("Close"))
                        Close();
                    break;

                case ButtonsToAdd.ConfirmCancel:
                    if (UI.Button("Cancel"))
                        Close();

                    UI.SameLine();
                    UI.Space(.01f);
                    if (UI.Button("Confirm"))
                    {
                        modalConfirmAction?.Invoke();
                        Close();
                    }
                    break;
            }

            UI.WindowEnd();

        }

        static public void Close()
        {
            isActive= false;
            Closed?.Invoke();
        }
    }
}
