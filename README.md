# MelonLoader Hand Fingers Overrider

Mod that allows you to override finger tracking.

## Desktop mode
Enable tracking in mods settings, use **LCtrl/RCtrl + F1/F2/F3/F4/F5** to change finger state.
If you have Leap Motion controller you can use it for finger tracking by enabling option in mods settings.

## VR mode
Works only localy for now, but avatar can be made with SDK3 features using parameters:
* `_FingerValue(0-9)`: float value, represents value of finger squeeze in range of 0.0 to 1.0. Indexes are:
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
* `_HandPresent(0-1)`: boolean value, represents detection of hand. Indexes are:
  * 0 - Left hand
  * 1 - Right hand

## Known bugs
* Thread lock if Leap Motion controller is disconnected, `LeapService` service is running and user disables Leap Motion tracking in mods settings.
  * Solution: connect Leap Motion controller or stop `LeapService` service.