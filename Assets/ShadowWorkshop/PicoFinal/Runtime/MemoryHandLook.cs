using System;
using UnityEngine;

namespace ShapeOfMemory
{
    // How the player's hands look (MemoryGame.HandLook; presets in PicoFinal/Looks/Hands, made by MetaHackthone > Pico
    // Final > Hands > Make looks): our skinned hand mesh and the materials it is drawn with, in order, plus optional motes
    // drifting off the fingers. MemoryHandSkin draws it. Empty on the game: the joint-drawn bones (MemoryHands).
    //   Ink      a pen-drawn hand: a depth pass, a light then a dark outline that is redrawn a few times a second, and a
    //            see-through paper wash hatched on its shaded side (ShapeOfMemory/HandInkLine, HandInkWash).
    //   Lantern  a hand of warm light, brightest where it faces the eye, with motes (ShapeOfMemory/HandLantern, HandMote).
    // Both begin with ShapeOfMemory/HandDepth so their see-through passes shade only the hand's front surface, once.
    [CreateAssetMenu(menuName="Shape of Memory/Hand look",fileName="Hand look")]
    public sealed class MemoryHandLook : ScriptableObject
    {
        [Tooltip("The hand meshes (Art/Hands/MemoryHand_R and _L: Tools/ShadowArt/build_hand.py, then Pico Final > Hands > Import hand mesh).")]
        public Mesh Right,Left;
        [Tooltip("Instead of the meshes above: a rigged hand model as it comes (its skinned mesh and its bones' names, e.g. R_IndexProximal), used unchanged. Art/Hands/UnityXRHands: the Unity XR Hands sample hands (Unity Package Distribution License: use and ship, not modify).")]
        public GameObject RightModel,LeftModel;
        [Tooltip("Metres along the hand from the wrist joint over which the forearm stub fades in (0 at x, full by y): the stub's cut end must be gone by x. Ours runs 7.9 cm back; the Unity sample's 2.05 cm (Hands > Mesh menus set it).")]
        public Vector2 WristFade=new Vector2(-.042f,-.014f);
        [Tooltip("Drawn in this order on each hand (each its own pass over the one skinned mesh). The aqua index and thumb tips and the pinch glow come from the shaders' _TipIdle/_TipGrab/_Close.")]
        public Material[] Materials=Array.Empty<Material>();

        [Header("Motes")]
        public bool Motes;
        [Tooltip("ShapeOfMemory/HandMote.")]
        public Material MoteMaterial;
        [Tooltip("Motes a second, a hand.")]
        public float MoteRate=22;
        [Tooltip("Seconds a mote lasts (each varies from 60% to 110% of it).")]
        public float MoteLife=1.6f;
        [Tooltip("Mote size in metres, smallest to largest.")]
        public Vector2 MoteSize=new Vector2(.002f,.0055f);
        [Tooltip("How fast a mote rises, slowest to fastest (m/s).")]
        public Vector2 MoteRise=new Vector2(.012f,.04f);
        public Color MoteColor=new Color(1f,.82f,.55f,1f);

        public bool Valid=>MeshFor(false)&&MeshFor(true)&&Materials!=null&&Materials.Length>0&&Array.TrueForAll(Materials,m=>m);
        // The skinned mesh a hand is drawn with: the model's if there is one, else ours.
        public Mesh MeshFor(bool left)
        {
            var skin=SkinOf(left);
            return skin?skin.sharedMesh:left?Left:Right;
        }
        // The model's bone names in its mesh's bone order (null: our own mesh, one bone per joint in joint order).
        public string[] BonesFor(bool left)
        {
            var skin=SkinOf(left);
            if(!skin)return null;
            var bones=skin.bones;var names=new string[bones.Length];
            for(int i=0;i<bones.Length;i++)names[i]=bones[i]?bones[i].name:"";
            return names;
        }
        SkinnedMeshRenderer SkinOf(bool left)
        {
            var model=left?LeftModel:RightModel;
            return model?model.GetComponentInChildren<SkinnedMeshRenderer>(true):null;
        }
    }
}
