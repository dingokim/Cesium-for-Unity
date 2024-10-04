using System.Collections;
using System.Collections.Generic;
using CesiumForUnity;
using UnityEngine;
using Unity.Mathematics;
using System.Text;
using UnityEngine.Networking;
using System;
using System.Net.Sockets;
using System.Globalization;
using System.Threading;

public class TileMaker : MonoBehaviour
{
    public GameObject PolygonManager;
    public GameObject OutputContainer;


    public float startX = 0;
    public float endX = 0;
    public float startZ = 0;
    public float endZ = 0;
    public int m = 1;
    public int n = 1;
    public float padding = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SplitAreaIntoTile() 
    {
        if (startX > endX || startZ > endZ) 
        {
            Debug.Log("잘못된 범위값 입력");
            return;
        }
        if (m < 1 || n < 1)
        {
            Debug.Log("잘못된 m,n값 입력");
            return;
        }
        if (padding < 0)
        {
            Debug.Log("padding은 음수값을 입력할 수 없습니다.");
            return;
        }

        float tileWidth = (endX - startX) / m;
        float tileHeight = (endZ - startZ) / n;

        for (int i = 0; i < m; i++) 
        {
            for (int j = 0; j < n; j++) 
            {
                float tileStartX = startX + i * tileWidth - padding;
                float tileEndX = tileStartX + padding + tileWidth + padding;

                float tileStartZ = startZ + j * tileHeight - padding;
                float tileEndZ = tileStartZ + padding + tileHeight + padding;

                List<Vector3> tilePolygonVertices = new List<Vector3>();
                tilePolygonVertices.Add(new Vector3(tileStartX, 0, tileStartZ));
                tilePolygonVertices.Add(new Vector3(tileStartX, 0, tileEndZ));
                tilePolygonVertices.Add(new Vector3(tileEndX, 0, tileEndZ));
                tilePolygonVertices.Add(new Vector3(tileEndX, 0, tileStartZ));

                PolygonManager.GetComponent<PolygonManager>().SetPolygon(tilePolygonVertices);
                PolygonManager.GetComponent<PolygonManager>().CutCookies();

                OutputContainer.GetComponent<ObjectCombiner>().CombineChildren();
                GameObject tile = OutputContainer.transform.GetChild(0).gameObject;
                tile.GetComponent<MeshCollider>().enabled = false;
                tile.transform.SetParent(this.transform);
            }
        }
    }
}
