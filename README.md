# MelonLoader Hand Fingers Overrider

Mod that allows you to override finger tracking.

## Desktop mode
Enable tracking in mods settings, use **LCtrl/RCtrl + (F1/F2/F3/F4/F5)** to change finger state.
If you have Leap Motion controller you can use its data of finger tracking by enabling option in mods settings, desktop orientation will be used.

## VR mode
Currently, there is now way found to override fingers in VR mode because of complex update method for **HandGestureController** class.
If you can find solution for this problem, make a pull request.

Current solution: Avatars 3.0 parameters.
Your avatar has to be made with SDK3 and parameters:
* `_FingerValue(0-9)`: float value, represents value of finger in range of 0.0 to 1.0. Indexes are:
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
* `_HandPresent(0-1)`: boolean value, represents detection of hand from Leap Motion controller. Indexes are:
  * 0 - Left hand
  * 1 - Right hand
  
If Leap Motion controller is used, it changes tracking mode to HMD.

## Known bugs
* Thread lock if Leap Motion controller is disconnected, `LeapService` service is running and user disables Leap Motion tracking in mods settings. Solution: connect Leap Motion controller or stop `LeapService` service.