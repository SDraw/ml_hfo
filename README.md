# Leap Motion Extension
This mod allows you to use your Leap Motion controller for hands and fingers visual tracking.

[![](.github/img_01.png)](https://youtu.be/ALDBcI9yCyM)

# Installation
* Install [Orion (v4)](https://developer.leapmotion.com/sdk-leap-motion-controller) or [Gemini (v5)](https://developer.leapmotion.com/gemini-v5-preview).
* Install [MelonLoader 0.3.0-ALPHA](https://github.com/LavaGang/MelonLoader).
* Install [UIExpansionKit](https://github.com/knah/VRCMods) (highly recommended).
* Get [latest release DLL](../../releases/latest).
* Put `ml_lme.dll` in `Mods` folder of game.

# Usage
**Note:** In desktop mode be sure to disable gestures in "Action Menu (R) - Options - Gestures" to make avatar animation actions be in fixed state instead of on hold state.

Available settings in mods settings menu (or MelonLoader configuration file):
* **Enable Leap Motion extension:** enable/disable extension.
* **Enable HMD mode for Leap Motion:** force HMD mode for Leap Motion controller.
* **Send SDK3 parameters:** send Avatars 3.0 parameters. In this case avatar has to have specific parameters:
  * `_HandPresent(0-1)`: boolean value, represents detection of hand. Indexes are:
    * 0 - Left hand
    * 1 - Right hand
  * `_FingerBend(0-9)`: float value, represents value of finger bend in range of 0.0 to 1.0. Indexes are:
    * 0 - Left thumb
    * 1 - Left index
    * 2 - Left middle
    * 3 - Left ring
    * 4 - Left pinky
    * 5 - Right thumb
    * 6 - Right index
    * 7 - Right middle
    * 8 - Right ring
    * 9 - Right pinky
  * `_FingerSpread(0-9)`: float value, represents value of finger spread in range of 0.0 to 1.0. Indexes are same as for `_FingerBend(0-9)`.
* **Head root:** use player's head as root transformation point.
* **RootOffsetY/RootOffsetZ:** transformation root offset. Values are representing offset for avatar with height 1.0. After applied offsets transformation scales based on current avatar height.
* **FingersOnly:** apply only fingers tracking. Useful for finger tracking with real VR controllers.

# Notes
Usage of mods breaks ToS of VRChat and can lead to ban. Use at your own risk.

# Credits
* Thanks to [Magic3000](https://github.com/Magic3000) for patch to enable finger tracking in VR mode.
