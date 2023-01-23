using StereoKit;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace JumbliVR
{
    static class Locomotion
    {

        // Configurable parameters
        static public bool rotationEnabled = true;
        static public string snapTurnDegreesStr = "";
        static public int snapTurnDegrees = -1;  // -1 = smooth turning, 45 or 30 are common values
        static public bool flyingEnabled = true;
        static public bool showFlyingOptionsIUnSettings = true;
        static public float minMetersPerSecond = 1f;
        static public float maxMetersPerSecond = 10;
        static public float movementRangeMin = .05f;
        static public float movementRangeMax = .1f;
        static public float rotationRangeMin = .3f;
        static public float rotationRangeMax = .8f;
        static public bool showFloor = true;
        static public bool showCompass = true;
        static public Color BoundaryMarkerColour = Color.Black;
        static public float controllerDeadspot = .05f;
        static public bool invertFloor = true;
        static public float floorScale = 3;

        // Add teleport points to this list as they become available
        // Default to containing some test data
        static public Dictionary<string, Pose> teleportDestinations = new Dictionary<string, Pose>()
        {
            {"Digby", new Pose(-3,0,0,Quat.FromAngles(0,90,0)) },
            {"Home", new Pose(0,0,0) },
            {"Space", new Pose(0,20,1) },
            {"Secret", new Pose(0,-20,-21) },
        };

        // Useful public info
        static public Hand handBeingUsed = Input.Hand(Handed.Right);
        static public Pose stagePose = Pose.Identity;

        static private bool handTrackingReadyToSnapTurn = true;
        static private bool controllerReadyToSnapTurn = true;
        static private Vec3 localControllerPosition;
        static private LinePoint[]? controllerHome;
        static private LinePoint[]? controllerRotationGuideL;
        static private LinePoint[]? controllerRotationGuideR;
        static private LinePoint[]? controllerRotationNeedle;
        static private Color controllerColor;
        static private Color controllerColorLinear;
        static private Color lineColor;
        static private Color compassTextColor;
        static private Plane flatPlane = new Plane(Vec3.Zero, Vec3.Forward);


        static private bool isControllerGrabbed = false;
        static private Vec3 worldHandTrackedLocomotionDirection;
        static private Vec3 worldControllerLocomotionDirection;
        static private float newHandTrackingRotation;
        static private float newControllerRotation;
        static private Vec3 worldHandlePosition;

        // Constant configuration
        private const float controllerScale = .2f;
        private const float circleRadius = .04f;
        private const float homeCircleThickness = .005f;
        private const float rotationCircleThickness = .01f;


        static private LinePoint[] directionLine = {
            new LinePoint(Vec3.Zero, controllerColorLinear,.001f),
            new LinePoint(Vec3.Zero, controllerColorLinear,.01f)
        };

        static private void SetControllerColor(Color color)
        {
            controllerColor = color;
            controllerColorLinear = controllerColor.ToLinear();
            compassTextColor = new Color(
                (controllerColorLinear.r + .5f) % 1,
                (controllerColorLinear.g + .5f) % 1,
                (controllerColorLinear.b + .5f) % 1);

            lineColor = controllerColorLinear * 1.5f;
            lineColor.a = 1;
            controllerRotationNeedle = new LinePoint[]
            {
                new LinePoint(Vec3.Zero,lineColor,.005f),
                new LinePoint(Vec3.Zero,lineColor,.005f),
                new LinePoint(Vec3.Zero,lineColor,.0001f)
            };

            controllerHome = new LinePoint[24];
            
            List<LinePoint> controllerRotationGuideListL = new List<LinePoint>();
            List<LinePoint> controllerRotationGuideListR = new List<LinePoint>();
            

            float step = 180f / (controllerHome.Length - 1);

            Color homeColor = controllerColorLinear * .8f;
            homeColor.a = 1;


            for (int i = 0; i < controllerHome.Length; i++)
            {
                Vec3 dir = Vec3.AngleXZ(i * step);
                controllerHome[i] = new LinePoint(dir * circleRadius, homeColor, homeCircleThickness);
            }
            controllerHome[0].thickness = .001f;
            controllerHome[23].thickness = .001f;


            Color rotationGuideColor = controllerColorLinear * 1.25f;
            rotationGuideColor.a = 1;
            int steps = 20;
            step = 90f / (steps - 1);
            for (int i = 0; i < steps; i++)
            {
                Vec3 dir = Vec3.AngleXZ(i * step);

                dir = Vec3.AngleXY(i * step);
                float x = dir.x;
                float alpha = (x - rotationRangeMin) / (rotationRangeMax - rotationRangeMin);
                if (x >= rotationRangeMin && x <= rotationRangeMax)
                {
                    controllerRotationGuideListL.Add(new LinePoint(dir * circleRadius, rotationGuideColor, rotationCircleThickness * alpha));
                    controllerRotationGuideListR.Add(new LinePoint(dir * V.XYZ(-1, 1, 1) * circleRadius, rotationGuideColor, rotationCircleThickness * alpha));
                }
            }
            controllerRotationGuideL = controllerRotationGuideListL.ToArray();
            controllerRotationGuideR = controllerRotationGuideListR.ToArray();

            directionLine[0].color = controllerColorLinear;
            directionLine[0].color.a = 0;
            directionLine[1].color = controllerColorLinear * .8f;

            physicalDirectionIndicator1 = new LinePoint[] {
                new LinePoint(V.XYZ(0, floorLevel* .98f,-.8f), controllerColorLinear * .5f, .1f),
                new LinePoint(V.XYZ(0, floorLevel* .98f,-.9f), controllerColorLinear * .5f, .001f)
            };
            physicalDirectionIndicator2 = new LinePoint[] {
                new LinePoint(V.XYZ(.1f, floorLevel* .98f,.8f), controllerColorLinear * .5f, .015f),
                new LinePoint(V.XYZ(-.1f, floorLevel* .98f,.8f), controllerColorLinear * .5f, .015f)
            };

        }


        private static bool controllerActivated = false;

        // Activate the controller at the users hand position so they
        // can suitably position it for comfort.

        static public void ActivateController(Vec3 spawnLocation)
        {
            worldControllerLocation = spawnLocation;
            localControllerPosition = stagePose.ToMatrix().Inverse.Transform(new Pose(worldControllerLocation)).position;
            
            worldHandTrackedLocomotionDirection = Vec3.Zero;
            handlePose = Pose.Identity;
            controllerActivated = true;
            isControllerGrabbed = false;
            circleRotation = Quat.Identity;
            newHandTrackingRotation = 0;
            
            SetSnapTurnReady(true);
        }

        static public void DeactivateMenus()
        {
            controllerActivated = false;
            teleportStatus = TeleportStatus.None;
        }

        static private Bounds controllerBounds = new Bounds(Vec3.One * controllerScale * .5f);
        static private Pose handlePose;
        static private Quat circleRotation;

        static private Vec3 worldControllerLocation;

        static private Quat grabbedOrientation;
        static private Vec3 grabbedPlaneVector = Vec3.Zero;

        static public Material floorMaterial = Material.Default.Copy();
        static public bool initialised = false;
        static float floorLevel = float.MaxValue;

        static private LinePoint[]? physicalDirectionIndicator1;
        static private LinePoint[]? physicalDirectionIndicator2;
        static public void Init()
        {
            initialised = true;
            Shader s = Shader.FromFile("infiniteFloor.hlsl");
            floorMaterial = new Material(s);
            floorMaterial.Transparency = Transparency.Add;
            floorMaterial.DepthWrite = false;
            SetFloorStyle(invertFloor, floorScale);
            if (World.HasBounds) {
                if (floorLevel == float.MaxValue)
                    floorLevel = World.BoundsPose.position.y;
            }
            if (floorLevel == float.MaxValue)
                floorLevel = -1.5f;
        }
        static public void SetFloorStyle(bool invert = false, float scale = 1)
        {
            invertFloor = invert;
            floorScale = scale;
            if (floorMaterial != null)
            {
                floorMaterial.SetFloat("inverse", invertFloor ? 1 : 0);
                floorMaterial.SetFloat("fadeDistance", scale / 3);
            }

            }


        static public void SetPlayerHeight(float newHeight)
        {
            floorLevel = newHeight * -1;
            controllerColor = new Color(-1, -1, -1); // Force lines to be regenerated at correct height
        }

        // Call each frame
        static public void Draw(Color color)
        {

            if (color.Equals(controllerColor) == false)
                SetControllerColor(color);

            if (initialised == false)
                Init();

            if (Platform.FilePickerVisible == false)
            {

                if (teleportStatus != TeleportStatus.None)
                {
                    DrawTeleportSelector();
                    controllerActivated = false;
                }

                if (deferredTeleportRequested)
                {
                    SetCameraOrientation(deferredTeleport.orientation);
                    SetPlayerPosition(deferredTeleport.position);
                    deferredTeleportRequested = false;
                }

                if (controllerActivated)
                {
                    if (DrawController())
                        Hands.HideHand(true, Locomotion.handBeingUsed.handed);
                }
                AnalogueControls();
            }

            // Draw an infinite floor
            Hierarchy.Push(Renderer.CameraRoot.Pose.ToMatrix());
            if (showFloor)
            {
                Hierarchy.Push(Matrix.S(floorScale));
                floorMaterial.SetVector("stage", Hierarchy.ToLocal(Input.Head.position));
                Hierarchy.Pop();

                // The floor shader orients the lines with the stage.
                // As the player uses artificial rotatation, the floor rotates with them.
                // This consistency with the real world may help reduce nausea.
                Mesh.Quad.Draw(floorMaterial, Matrix.TRS(
                    Hierarchy.ToLocal(V.XYZ(Input.Head.position.x, Renderer.CameraRoot.Pose.position.y + floorLevel, Input.Head.position.z)), Quat.FromAngles(90, 0, 0), floorScale),
                    Color.Black);


                // The following lines and indicators move with the stage direction.
                // They allow the user to orient themselves in their physical playing area.
                Lines.Add(physicalDirectionIndicator1);
                Lines.Add(physicalDirectionIndicator2);
                
                Text.Add("FWD", Matrix.TRS(Vec3.Zero, Quat.FromAngles(90, 180, 0), 1.5f), controllerColorLinear * .5f, TextAlign.Center, TextAlign.Center,
                    0, .3f / 1.5f, floorLevel * -.99f / 1.5f);
            }
            Hierarchy.Pop();
            // The compass on the floor helps the user keep track of the in-game directions.
            // North is the forward facing direction configured in the headset.
            if (showCompass)
            {
                Text.Add("N", Matrix.TRS(Renderer.CameraRoot.Pose.position, Quat.FromAngles(90, 180, 0), 3), Color.Black, TextAlign.Center, TextAlign.Center, 0, .075f, floorLevel * -.33f);
                Text.Add("E", Matrix.TRS(Renderer.CameraRoot.Pose.position, Quat.FromAngles(90, 180, 0), 3), Color.Black, TextAlign.Center, TextAlign.Center, -.075f, 0, floorLevel * -.33f);
                Text.Add("S", Matrix.TRS(Renderer.CameraRoot.Pose.position, Quat.FromAngles(90, 180, 0), 3), Color.Black, TextAlign.Center, TextAlign.Center, 0, -.075f, floorLevel * -.33f);
                Text.Add("W", Matrix.TRS(Renderer.CameraRoot.Pose.position, Quat.FromAngles(90, 180, 0), 3), Color.Black, TextAlign.Center, TextAlign.Center, .075f, 0, floorLevel * -.33f);
            }



        }

        static private void AnalogueControls()
        {
            if (worldControllerLocomotionDirection.Length > .05f)
                GlidePlayer(worldControllerLocomotionDirection);

            float absRotation = Math.Abs(newControllerRotation);
            if (absRotation > rotationRangeMin)
            {
                if (snapTurnDegrees == -1) // Smooth rotation
                {
                    float speed = Math.Abs(newControllerRotation) - rotationRangeMin;
                    speed = 180 * Math.Min(rotationRangeMax, speed) * Math.Sign(newControllerRotation) * Time.Stepf;
                    RotatePlayer(Quat.FromAngles(V.XYZ(0, speed, 0)));
                }
                else
                {
                    if (controllerReadyToSnapTurn && absRotation > rotationRangeMin + .1f)
                    {
                        RotatePlayer(Quat.FromAngles(V.XYZ(0, snapTurnDegrees * Math.Sign(newControllerRotation), 0)));
                        controllerReadyToSnapTurn = false;
                    }
                }
            }
            else
                controllerReadyToSnapTurn = true;

            // Analogue stick operations
            Vec2 moveStick = Input.Controller(Handed.Left).stick;
            Vec2 rotateStick = Input.Controller(Handed.Right).stick;
            

            Vec3 indicator = Input.Hand(Handed.Left).palm.position + Input.Hand(Handed.Left).palm.Right * .1f;
            Hierarchy.Push(Input.Controller(Handed.Left).aim.ToMatrix());
            worldControllerLocomotionDirection = Hierarchy.ToWorldDirection(V.XYZ(0, 0, moveStick.y * -1));
            worldControllerLocomotionDirection += Vec3.Cross(Vec3.Up,(Input.Controller(Handed.Left).aim.Forward * V.XYZ(1, 0, 1)).Normalized) * moveStick.x;
            worldControllerLocomotionDirection *= .25f;
            Hierarchy.Pop();

            // Show a direction indicator above the hand
            if (worldControllerLocomotionDirection.Length > .05f)
            {

                Lines.Add(new LinePoint[]
                {
                    new LinePoint(indicator,controllerColorLinear,.02f),
                    new LinePoint(indicator + worldControllerLocomotionDirection.Normalized * .03f , controllerColorLinear, .0001f)
                });
            }
            newControllerRotation = Input.Controller(Handed.Right).stick.x;

        }
        static private bool DrawController()
        {

            // We move the player (based on previous frame calculations) first
            // This stops the controller visually juddering
            if (isControllerGrabbed && handBeingUsed.IsTracked)
            {

                if (worldHandTrackedLocomotionDirection.Length > .05f)
                    GlidePlayer(worldHandTrackedLocomotionDirection);

                float absRotation = Math.Abs(newHandTrackingRotation);
                if (absRotation > rotationRangeMin)
                {
                    if (snapTurnDegrees == -1) // Smooth rotation
                    {
                        float speed = Math.Abs(newHandTrackingRotation) - rotationRangeMin;
                        speed = 180 * Math.Min(rotationRangeMax, speed) * Math.Sign(newHandTrackingRotation) * Time.Stepf;
                        RotatePlayer(Quat.FromAngles(V.XYZ(0, speed , 0)));
                    }
                    else {
                        if (handTrackingReadyToSnapTurn && absRotation > rotationRangeMin + .1f)
                        {
                            RotatePlayer(Quat.FromAngles(V.XYZ(0, snapTurnDegrees * Math.Sign(newHandTrackingRotation), 0)));
                            SetSnapTurnReady(false);

                            // We will have just jumped to a new position and corrupted the handle position
                            // so returning here lets everything sync up again
                            Pose saveHandlePose = handlePose;
                            isControllerGrabbed = UI.Handle("locomotion", ref handlePose, controllerBounds, false, UIMove.Exact);
                            handlePose = saveHandlePose;
                            return isControllerGrabbed;
                        }
                    }
                    
                }
                else
                    SetSnapTurnReady(true);
            }
            else
            {
                float resetAlpha = Math.Min(1, Time.Stepf * 20);
                circleRotation = Quat.Slerp(circleRotation, Quat.Identity, resetAlpha);
                handlePose = Pose.Lerp(handlePose, Pose.Identity, resetAlpha);
                if (MathF.Min(
                    Vec3.Distance(Input.Hand(Handed.Right).palm.position, worldControllerLocation),
                    Vec3.Distance(Input.Hand(Handed.Left).palm.position, worldControllerLocation)
                    ) > 1f && worldControllerLocation.Equals(Vec3.Zero) == false)
                    controllerActivated = false;
            }

            newHandTrackingRotation = 0;


            // Positions are relative to the player's stage, which moves as a result of locomotion
            Hierarchy.Push(stagePose.ToMatrix());
            {  // Braces just used to more easily see hierarchy push and pops
                worldControllerLocation = Hierarchy.ToWorld(localControllerPosition);
                
                
                // Controller elements are drawn relative to where the controller spawned on the stage
                Hierarchy.Push(Matrix.T(localControllerPosition));
                {
                    // Draw a semicircular home element and look towards the controller handle as it's dragged around
                    if (isControllerGrabbed)
                        circleRotation = Quat.Slerp(circleRotation, Quat.LookAt(Vec3.Zero, Hierarchy.ToLocal(worldHandlePosition)), Time.Stepf * 10f);
                    Hierarchy.Push(Matrix.R(circleRotation));
                    Lines.Add(controllerHome);
                    Hierarchy.Pop();

                    // Add the controller handle
                   isControllerGrabbed = UI.Handle("locomotion", ref handlePose, controllerBounds, false, UIMove.Exact);
                    if (UI.LastElementActive.IsJustActive())
                    {
                        if (UI.LastElementHandUsed(Handed.Left) == BtnState.Active)
                            handBeingUsed = Input.Hand(Handed.Left);
                        else
                            handBeingUsed = Input.Hand(Handed.Right);

                        grabbedPlaneVector = Vec3.Zero;
                        grabbedOrientation = handBeingUsed.palm.orientation;
                    }

                    float letterScale = .35f;
                    float letterOffset = .026f;

                    Vec3 localHeadPosition = Hierarchy.ToLocal(Input.Head.position);
                    Text.Add("S",Matrix.TRS(handlePose.position + letterOffset * Hierarchy.ToLocalDirection(Vec3.Forward) * -1,
                        Quat.LookDir(Hierarchy.ToLocalDirection(Vec3.Forward) * -1f),letterScale),compassTextColor);
                    Text.Add("N",Matrix.TRS(handlePose.position + letterOffset * Hierarchy.ToLocalDirection(Vec3.Forward),
                        Quat.LookDir(Hierarchy.ToLocalDirection(Vec3.Forward)),letterScale),compassTextColor);
                    Text.Add("W",Matrix.TRS(handlePose.position + letterOffset * Hierarchy.ToLocalDirection(Vec3.Right) * -1,
                        Quat.LookDir(Hierarchy.ToLocalDirection(Vec3.Right) * -1f),letterScale),compassTextColor);
                    Text.Add("E",Matrix.TRS(handlePose.position + letterOffset * Hierarchy.ToLocalDirection(Vec3.Right),
                        Quat.LookDir(Hierarchy.ToLocalDirection(Vec3.Right)),letterScale),compassTextColor);

                    Hierarchy.Push(handlePose.ToMatrix()); // Scale down the handle sphere
                    Mesh.Sphere.Draw(Material.Default, Matrix.S(.05f), controllerColor, RenderLayer.Vfx);
                    Hierarchy.Pop();


                    // Draw a line from the handle to its home position
                    directionLine[1].pt = handlePose.position;
                    Lines.Add(directionLine);

                    // Store world position of handler so home element can face towards it
                    worldHandlePosition = Hierarchy.ToWorld(handlePose.position);

                    // Calc the world direction we are moving
                    worldHandTrackedLocomotionDirection = Hierarchy.ToWorldDirection(handlePose.position);

                    if (rotationEnabled)
                    {
                        // Draw the rotation guide
                        Quat rotationGuideOrientation = Quat.LookAt(handlePose.position, Hierarchy.ToLocal(Utils.CalcNeckPosition()));
                        
                        Hierarchy.Push(Matrix.TR(handlePose.position, rotationGuideOrientation));
                        {
                            Lines.Add(controllerRotationGuideL);
                            Lines.Add(controllerRotationGuideR);
                            

                            Vec3 rotationIndicator = Vec3.Up;
                            if (handBeingUsed != null)
                            {
                                if (isControllerGrabbed)
                                {
                                    if (grabbedPlaneVector.Equals(Vec3.Zero))
                                        grabbedPlaneVector = flatPlane.Closest(Hierarchy.ToLocalDirection(handBeingUsed.palm.Right).Normalized);

                                    // Calculate how far we are rotating our palm
                                    // This is overcomplicated but works at most angles, such as lying down
                                    // And while you are physically walking around
                                    Vec3 currentPlaneVector = flatPlane.Closest(Hierarchy.ToLocalDirection(handBeingUsed.palm.Right).Normalized);
                                    float angleBetweenVectors = Vec3.AngleBetween(grabbedPlaneVector, currentPlaneVector);
                                    float direction = (grabbedPlaneVector.y * currentPlaneVector.x > grabbedPlaneVector.x * currentPlaneVector.y) ? -1 : 1;

                                    // Direction for the rotation needle
                                    rotationIndicator = Quat.FromAngles(0, 0, angleBetweenVectors * direction) * Vec3.Up;
                                    
                                }
                                newHandTrackingRotation = rotationIndicator.x;
                            }
                            if (controllerRotationNeedle != null)
                            {
                                controllerRotationNeedle[1].pt = (rotationIndicator + V.XYZ(0, 0, -.1f)) * .04f;
                                controllerRotationNeedle[2].pt = (rotationIndicator + V.XYZ(0, 0, -.1f)) * .045f;
                            }
                            Lines.Add(controllerRotationNeedle);
                        }
                        Hierarchy.Pop();
                    }

                }
                Hierarchy.Pop();
            }
            Hierarchy.Pop();

            return isControllerGrabbed;

        }

        private enum TeleportStatus
        {
            None,
            Selecting,
            Complete,
            RequestedFullList
        }
        static TeleportStatus teleportStatus = TeleportStatus.None;

        static public void ActivateTeleport(Hand hand, Action<Pose, Color, string> ?customRenderer)
        {
            handBeingUsed = hand;
            localControllerPosition = stagePose.ToMatrix().Inverse.Transform(hand[FingerId.Index, JointId.Tip].position);
            VolumetricInput.Activate(hand, teleportDestinations.Keys.ToArray(), customRenderer);            
            teleportStatus = TeleportStatus.Selecting;
        }

        public delegate void Notify(string destinationId);
        static public event Notify ?Teleported;
        // Set teleport desitnations to this pose when you just want a
        // dummy entry, such as a close button.
        static public Pose NullTeleportPose = new Pose(.001f, .0012f, .0013f);

        // Return true while selection in progress
        static private void DrawTeleportSelector()
        {
            
            Hierarchy.Push(stagePose.ToMatrix());
            Hierarchy.Push(Matrix.T(localControllerPosition));
            //Hierarchy.Push(Matrix.R(Quat.FromAngles(0, 180, 0)));
            string selectedId = VolumetricInput.Draw(controllerColor, Color.White);

            Color linearColor =  controllerColorLinear;
            linearColor.a = .5f;

            // Add arrow to each label pointing towards its destination
            foreach (KeyValuePair<string, VolumetricInput.Arrangement> kvp in VolumetricInput.arrangement)
            {
                Pose p = teleportDestinations[kvp.Key];

                if (p.Equals(NullTeleportPose) == false)
                {
                    p = Hierarchy.ToLocal(p);
                    Vec3 direction = (p.position - kvp.Value.pose.position);
                    if (direction.Length > 1f)
                    {
                        direction.Normalize();
                        //float dX = Vec3.Dot(Vec3.Right, direction);
                        float dY = Vec3.Dot(Vec3.Up, direction);
                        Vec3 offset;
                        //if (MathF.Abs(dX) > MathF.Abs(dY))
                            //offset = ((kvp.Value.size.x * .5f + .01f) * MathF.Sign(dX) * kvp.Value.pose.Right);
                        //else
                            offset = ((kvp.Value.size.y * .5f +.01f) * MathF.Sign(dY) * kvp.Value.pose.Up);

                        Lines.Add(new LinePoint[]
                            {
                                new LinePoint(kvp.Value.pose.position + offset,linearColor,.01f),
                                new LinePoint(kvp.Value.pose.position + offset + (direction * .02f), linearColor, .0001f)
                            });
                    }
                }
            }
            //Hierarchy.Pop();
            Hierarchy.Pop();
            Hierarchy.Pop();            

            if (selectedId != "")
            {
                if (teleportDestinations.ContainsKey(selectedId)) {
                    Pose p = teleportDestinations[selectedId];
                    if (p.Equals(NullTeleportPose) == false)
                    {
                        SetCameraOrientation(p.orientation);
                        SetPlayerPosition(p.position);
                    }
                }
                Teleported?.Invoke(selectedId);
                teleportStatus = TeleportStatus.None;
            }
        }

        // Change color of rotation guide depending on ready state
        static private void SetSnapTurnReady(bool isReady)
        {
            handTrackingReadyToSnapTurn = isReady;
            Color newColor = controllerColorLinear * (handTrackingReadyToSnapTurn?1.25f:.8f);
            newColor.a = 1;
            if (controllerRotationGuideL != null)
            {
                for (int i = 0; i < controllerRotationGuideL.Length; i++) {
                    if (controllerRotationGuideR != null)
                        controllerRotationGuideR[i].color = controllerRotationGuideL[i].color = newColor;
                }
            }
        }

        // Set absolute angle for the camera
        static public void SetCameraOrientation(float angle)
        {
            RotatePlayer(Quat.Delta(stagePose.orientation, Quat.FromAngles(V.XYZ(0, angle, 0))));
        }

        // basedOnPlayerGaze = true makes sure that whichever direction the player is looking
        // they will end up facing towards the desired direction
        // basedOnPlayerGaze = false, means they will need to look forwards in their stage area to align themselves
        static public void SetCameraOrientation(Quat rotation, bool basedOnPlayerGaze = true)
        {
            Quat q;
            if (basedOnPlayerGaze)            
            {
                // Flatten head orientation
                q = Quat.LookDir((Input.Head.orientation * Vec3.Forward) * V.XYZ(1,0,1));
            }
            else
                q = stagePose.orientation;
            RotatePlayer(Quat.Delta(q, rotation));
        }

        // The player may not be stood at the stage centre
        // so we need to rotate around the head position to reduce nausea
        static public void RotatePlayer(Quat angle)
        {
            Hierarchy.Push(Matrix.T(Input.Head.position));
            stagePose = Hierarchy.ToLocal(stagePose);
            Hierarchy.Push(Matrix.R(angle));
            stagePose = Hierarchy.ToWorld(stagePose);
            Hierarchy.Pop();
            Hierarchy.Pop();
            UpdateStagePose(stagePose);
        }

        // Move the stage so the player is stood at the set position
        static public void SetPlayerPosition(Vec3 newPosition)
        {
            // The player may not be at the centre of the stage so we have to adjust for that
            Vec3 playerPositionInStage = stagePose.ToMatrix().Inverse.Transform(Input.Head.position);
            stagePose.position = newPosition - Quat.Rotate(stagePose.orientation, playerPositionInStage);
            UpdateStagePose(stagePose);
        }

        // Used to move the player in a certain direction
        // The direction vector includes the speed as its length
        static public void GlidePlayer(Vec3 locomotionDirection)
        {
            if (flyingEnabled == false)
                locomotionDirection *= V.XYZ(1, 0, 1);

            float speed = SKMath.Lerp(minMetersPerSecond, maxMetersPerSecond, MathF.Min(movementRangeMax,locomotionDirection.Length - movementRangeMin) / movementRangeMax);
            stagePose.position += locomotionDirection * Time.Stepf * speed;
            UpdateStagePose(stagePose);
        }

        static public void UpdateStagePose(Pose pose)
        {
            stagePose = pose;
            Renderer.CameraRoot = stagePose.ToMatrix();
        }

        
        static private Pose deferredTeleport;
        static private bool deferredTeleportRequested = false;

        // This is useful when you want to initiate a teleport but you are currently inside a Hierarchy
        static public void RequestDeferredTeleport(Pose newPose)
        {
            deferredTeleport = newPose;
            deferredTeleportRequested = true;
        }


        static public void ActiveSettingsWindow()
        {
            ModalWindow.ActivateWithCloseButton("Locomotion settings", null, DrawSettings);
        }

        
        static private void DrawSettings()
        {
            if (UI.Radio("Walking mode", flyingEnabled == false))
                flyingEnabled = false;
            UI.SameLine();
            if (UI.Radio("Flying mode", flyingEnabled))
                flyingEnabled = true;
                
            UI.HSeparator();

            UI.Label("Rotation");
            UI.SameLine();
            UI.Toggle(rotationEnabled ? "Enabled" : "Disabled", ref rotationEnabled);
            if (rotationEnabled == false)
                UI.PushEnabled(false);
            UI.Label("Rotation mode:");
            UI.SameLine();

            bool snapTurning = snapTurnDegrees >= 0;

            if (UI.Radio("Smooth turning", snapTurning == false))
                snapTurnDegrees = -1;
            UI.SameLine();
            if (UI.Radio("Snap turning", snapTurning))
            {
                snapTurnDegrees = 30;
                snapTurnDegreesStr = "" + snapTurnDegrees;
            }
            UI.SameLine();
            if (snapTurnDegrees == -1)
                UI.PushEnabled(false);
            if (UI.Input("snapTurnDegrees", ref snapTurnDegreesStr, V.XY(.03f, 0), TextContext.Number))
                    int.TryParse(snapTurnDegreesStr, out snapTurnDegrees);
            if (snapTurnDegrees == -1)
                UI.PopEnabled();


            if (rotationEnabled == false)
                UI.PopEnabled();

        }



    }
}
