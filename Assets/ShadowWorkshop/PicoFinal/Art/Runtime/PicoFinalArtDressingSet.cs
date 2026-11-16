using UnityEngine;

namespace ShapeOfMemory.ArtDressing
{
    // The art models PicoFinalArtDressing mounts on the game's table, reading stand, lamps and closed book. One asset,
    // Art/Resources/ShapeOfMemoryArtDressing.asset, loaded by name at runtime (so it ships without the game referencing
    // it). Leave a slot empty to leave that part of the game as it is.
    [CreateAssetMenu(menuName = "Shape of Memory/Art Dressing Set", fileName = "ShapeOfMemoryArtDressing")]
    public sealed class PicoFinalArtDressingSet : ScriptableObject
    {
        [Tooltip("Art/Art Assets/Prefabs/SM_Table_01: on every 'Sculptor table'")]
        public GameObject Table;
        [Tooltip("Art/Art Assets/Prefabs/BookStand_01: on the 'Reading stand'")]
        public GameObject Stand;
        [Tooltip("Art/Art Assets/Prefabs/SM_Spotlight_01: on 'Lamp 0' and 'Lamp 1'")]
        public GameObject Lamp;
        [Tooltip("Art/Art Assets/Prefabs/SM_Book_01: round the game's book while its cover is shut")]
        public GameObject Book;
    }
}
