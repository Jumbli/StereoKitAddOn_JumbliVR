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
Supports analogue stick movement, you move the direction you point your left hand.
Displays a compass on the controller and the floor to orient you in game, while other markings orient you in your physical space.
Also provides a volumetric menu that allows you to choose a teleport destination.
Use the hand menu to initiate movement, the teleport menu or the settings pannel.


## Volumetric input
This shows a 3d volumetric menu and allows selection with your finger tip. It is used to select teleport destinations but can also be used as a 3d keyboard. I will add a sample for the 3d keyboard at a later date.

## Hands
Allow hands to be easily hidden or faded out when required. You need to call a method each frame to keep them faded / hidden so you don't have to worry about turning them back on.

## ModalWindow
A simple class to show a window with a choice of standard buttons.

## inititeFloor.hlsl
A shader to draw a floor that uses concentric circles and radial lines to guide you to centre stage. 

## Utils
A general class for small handy functions

# To Do
- Add an optional cage / dashboard as an additional comfort option.
- Add settings for height adjustment

