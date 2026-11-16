using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // Everything round the table once the sky is up, in one asset, so a whole look swaps by swapping the asset
    // (MemoryGame.Look; the presets are in PicoFinal/Looks, made and switched by MetaHackthone > Pico Final > Look):
    //   Sky        the dome's material and where its glow is (MemorySky).
    //   Lighting   the key light and ambient on the table-top (MemoryLighting); the background lights itself.
    //   Clouds     the cloud kit, its material and how its sea lies (MemoryClouds).
    //   Ruins      ruin pieces (our kit on its material, and/or the environment artist's models, which keep their own
    //              materials and borrow their bake from the environment scene) and how they are laid out (MemoryClouds).
    //   Environment  the environment artist's scene, which of its groups stay off, and its fog (MemoryEnvironment,
    //              MemoryFog).
    //   Dream      the dream kit (MemoryDream).
    // Empty parts are simply left out.
    [CreateAssetMenu(menuName="Shape of Memory/Look",fileName="Look")]
    public sealed class MemoryLook : ScriptableObject
    {
        [Header("Sky")]
        [Tooltip("The sky that fades in once the book is first opened (MemorySky); empty: the room stays.")]
        public Material Sky;
        [Tooltip("Where that sky's glow is in its own cubemap (degrees round from +Z toward +X, at _Rotation 0): WindwardEveningSky 50, MagicHourSky 120, FlowSky 30.")]
        public float GlowAzimuth=50;
        [Tooltip("Where the glow shows, in degrees right of the player's forward (negative: left); clouds and ruins are lit from there. The environment scene's bake, rim light and haze assume the Flow sky at -20.")]
        public float GlowFacing;

        [Header("Lighting (the table-top)")]
        public MemoryLighting.Setup Lighting=MemoryLighting.Setup.Game;

        [Header("Clouds")]
        [Tooltip("Stylised cumulus round the player (MemoryClouds): the meshes of Art/Clouds/CloudKit.fbx, each with its _LOD1 and _LOD2. Empty: none.")]
        public Mesh[] CloudPieces=Array.Empty<Mesh>();
        [Tooltip("Their material: ShapeOfMemory/Cloud (Materials/Memory cloud.mat, or a recoloured copy).")]
        public Material CloudMaterial;
        [Tooltip("Off: the cloud sea's tops are MemoryClouds.Drop metres under the floor (the table floats over it). On: just under the feet.")]
        public bool CloudsUnderFeet;
        public MemoryClouds.Shape CloudShape=MemoryClouds.Shape.Flat;

        [Header("Ruins")]
        [Tooltip("Our ruin kit: the meshes of Art/Ruins/RuinKit.fbx (Ruin_*), on RuinMaterial. Empty: none of ours.")]
        public Mesh[] RuinPieces=Array.Empty<Mesh>();
        [Tooltip("ShapeOfMemory/Cloud with a stone palette (Materials/Memory ruin.mat).")]
        public Material RuinMaterial;
        [Tooltip("The environment artist's ruin models (Art/Art Assets/Prefabs/SM_Ruin*), laid out with ours. Each keeps its own material and takes its light from an instance of the same model in the environment scene (its lightmap, turn and lean), so the environment scene must be set below. Empty: none.")]
        public GameObject[] RuinModels=Array.Empty<GameObject>();
        public MemoryClouds.RuinLayout Ruins=MemoryClouds.RuinLayout.Standing;

        [Header("Environment scene")]
        [Tooltip("The environment artist's sky scene, loaded additively and shown with the sky (MemoryEnvironment): its asset path, e.g. Assets/ShadowWorkshop/PicoFinal/Scenes/PicoFinal_Environment.unity (it must be in the DeployConfig's Scenes for a build). Its lighting assumes the Flow sky at GlowAzimuth 30, GlowFacing -20. Empty: none.")]
        public string EnvironmentScene="";
        [Tooltip("Groups under that scene's Environment/Art to leave off (Ruins_Near, Ruins_Far, Ruins_Overhead, Ruins_Below, Clouds_Mid, Clouds_Far, Clouds_Overhead, Clouds_Below, Outer_Clusters). All of them: the scene only lends its bake to RuinModels.")]
        public string[] EnvironmentHide=Array.Empty<string>();
        [Tooltip("Keep the scene's giants even in the groups left off: the pieces on its *_Giant materials, 450-800 m out (the gate, the three outer clusters of ruin and cloud, the far giant clouds), the far horizon.")]
        public bool EnvironmentGiants;
        [Tooltip("Another material for that scene's cloud sea (every renderer under Art/Cloud_Sea on MT_CloudSea), e.g. Materials/Env cloud sea (our fog).mat: its haze between the clouds as the game's own sea had it on 2026-11-12. Empty: the scene's own. (A group path such as Cloud_Sea/CloudSea_Floor in the list above leaves that one object off.)")]
        public Material EnvironmentSeaMaterial;
        [Tooltip("One fog for that scene's clouds and ruins alike, in place of each material's own haze (MemoryFog): distance, the mist between the clouds and how high it lies, and buildings fading into the cloud tops. Off: each material's own. It can be tuned here in Play mode.")]
        public MemoryFog Fog=new MemoryFog();

        [Header("Dream")]
        [Tooltip("Pieces of architecture drifting beyond the book (MemoryDream): the meshes of Art/Dream/DreamKit.fbx. Empty: none.")]
        public Mesh[] DreamPieces=Array.Empty<Mesh>();
        public Material DreamMaterial;

        public bool HasClouds=>CloudPieces!=null&&CloudPieces.Length>0&&CloudMaterial;
        public bool HasOurRuins=>RuinPieces!=null&&RuinPieces.Length>0&&RuinMaterial;
        public bool HasRuinModels=>RuinModels!=null&&RuinModels.Length>0;
        public bool HasEnvironment=>!string.IsNullOrEmpty(EnvironmentScene);
        public override string ToString()=>$"{name}: sky {(Sky?Sky.name:"none")} glow {GlowAzimuth}/{GlowFacing}, lighting {Lighting.Name}, "+
            $"{CloudPieces?.Length??0} cloud meshes on {(CloudMaterial?CloudMaterial.name:"none")}, {RuinPieces?.Length??0} ruin meshes + {RuinModels?.Length??0} ruin models, "+
            $"environment '{EnvironmentScene}' hiding [{string.Join(", ",EnvironmentHide??Array.Empty<string>())}]{(EnvironmentGiants?" but its giants":"")}"+
            (EnvironmentSeaMaterial?$", its sea on {EnvironmentSeaMaterial.name}":"")+(Fog!=null&&Fog.On?$", fog: {Fog}":"");
    }
}
