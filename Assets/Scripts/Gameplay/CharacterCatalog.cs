using System;
using UnityEngine;

// The characters players can pick in the lobby, in roster order. A pick travels over
// the network as an index into this list, so every machine must use the same asset.
[CreateAssetMenu(fileName = "CharacterCatalog", menuName = "TagTheGame/Character Catalog")]
public class CharacterCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Shown in the lobby under the portrait.")]
        public string displayName;

        [Tooltip("A humanoid character prefab. It is copied into the player at spawn; the prefab itself is never changed.")]
        public GameObject model;

        [Tooltip("Head-and-shoulders picture for the lobby. Made by TagTheGame > Render Character Portraits.")]
        public Texture2D portrait;

        [Tooltip("Where the bomb sits, measured from the chest bone of this model.")]
        public Vector3 bombLocalPosition;

        [Tooltip("How the bomb is turned, relative to the chest bone of this model.")]
        public Vector3 bombLocalEulerAngles;
    }

    [Tooltip("Every character in the roster.")]
    public Entry[] characters = new Entry[0];

    public int Count => characters != null ? characters.Length : 0;

    public Entry Get(int index)
    {
        return CharacterRules.IsValid(index, Count) ? characters[index] : null;
    }
}
