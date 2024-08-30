using UnityEngine;
using CesiumForUnity;


public class CesiumSamplesTeleportationArea : MonoBehaviour
{
    void Start()
    {
        Cesium3DTileset tileset = GetComponent<Cesium3DTileset>();
        if (tileset != null)
        {
            tileset.OnTileGameObjectCreated += go =>
                go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>();
        }
    }
}