# Unity XR Hands sample hands

`RightHand.fbx` and `LeftHand.fbx` are copied unchanged from the Unity XR Hands package 1.7.3
(`com.unity.xr.hands`, `Samples~/HandVisualizer/Models`), copyright Unity Technologies, under the Unity Package
Distribution License (`LICENSE-XRHands.md`). That licence lets us use, run and ship them inside the game but not modify
them, so they stay as they are: the game only drives their bones (`MemoryHandSkin`) and draws them with its own
shaders (`Shaders/Hands`), which take what they need per hand at run time instead of from extra mesh channels.
