# StereoKitAddOn_JumbliVR
This code uses the SkereoKit open source OpenXR library
https://github.com/StereoKit/StereoKit

The project is a VSCode project, so see the following instructions on how to set up:
https://stereokit.net/Pages/Guides/Getting-Started-VS-Code.html

# Status
This is a first draft, so expect more features and refinements as I progress with my own project that makes use of this code.

# Classes

## Program
Creates a sample environment to test the classes below.

## Locomotion
Shows a controller that you can grab and drag using hand tracking to move the player around the scene. Supports walking / flying modes, smooth and snap turning.
Also provides a volumetric menu that allows you to choose a teleport destination.
Use the hand menu to initiate movement, the teleport menu or the settings pannel.

## Volumetric input
This shows a 3d volumetric menu and allows selection with your finger tip. It is used to select teleport destinations but can also be used as a 3d keyboard. I will add a sample of this at a later date.

## ModalWindow
A simple class to show a window with a choice of standard buttons.

# To Do
- Add an optional cage / dashboard as an additional comfort option.
- Add controller analogue joystick support.
- Add floor.
