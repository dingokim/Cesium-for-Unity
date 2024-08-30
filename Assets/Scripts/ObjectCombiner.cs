using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using static UnityEditor.Rendering.CameraUI;

public class ObjectCombiner : MonoBehaviour
{
    public void CombineChildren() 
    {
        List<GameObject> childrenList = new List<GameObject>();
        for (int i = 0; i < transform.childCount; i++)
        {
            childrenList.Add(transform.GetChild(i).gameObject);
        }
        GameObject[] children = childrenList.ToArray();

        CombineObjects(children);
    }

    public void CombineObjects(GameObject[] objects)
    {
        GameObject output = new GameObject("Combined Object");

        MeshFilter[] meshFilters = new MeshFilter[objects.Length];
        MeshRenderer[] meshRenderers = new MeshRenderer[objects.Length];
        for (int i = 0; i < objects.Length; i++)
        {
            meshFilters[i] = objects[i].GetComponent<MeshFilter>();
            meshRenderers[i] = objects[i].GetComponent<MeshRenderer>();
        }

        CombineInstance[] combine = new CombineInstance[meshFilters.Length];
        Texture2D[] textures = new Texture2D[meshRenderers.Length];

        // 부모의 변환 행렬을 미리 저장해 둠
        Matrix4x4 parentTransform = transform.worldToLocalMatrix;

        // 각 메쉬의 텍스처를 가져와 배열에 저장
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            Material originalMaterial = meshRenderers[i].sharedMaterial;
            if (originalMaterial.HasProperty("_MainTex"))
            {
                textures[i] = (Texture2D)originalMaterial.mainTexture;
            }
            else
            {
                textures[i] = Texture2D.whiteTexture; // 텍스처가 없는 경우 기본 흰색 텍스처 사용
            }
        }

        // 텍스처 아틀라스 생성
        Texture2D atlas = new Texture2D(8192, 8192);
        Rect[] uvs = atlas.PackTextures(textures, 0, 8192);

        Debug.Log("Texture Atlas created with size: " + atlas.width + "x" + atlas.height);

        for (int i = 0; i < meshFilters.Length; i++)
        {
            combine[i].mesh = meshFilters[i].sharedMesh;

            // 자식 오브젝트의 로컬 좌표를 유지하기 위해 부모의 변환을 적용
            combine[i].transform = parentTransform * meshFilters[i].transform.localToWorldMatrix;

            // 새로운 UV 매핑을 위한 배열 생성
            Vector2[] uvsOriginal = meshFilters[i].sharedMesh.uv;
            Vector2[] uvsNew = new Vector2[uvsOriginal.Length];

            // 기존 UV를 새로운 아틀라스 UV로 변환
            for (int j = 0; j < uvsOriginal.Length; j++)
            {
                uvsNew[j] = new Vector2(
                    Mathf.Lerp(uvs[i].xMin, uvs[i].xMax, uvsOriginal[j].x),
                    Mathf.Lerp(uvs[i].yMin, uvs[i].yMax, uvsOriginal[j].y)
                );
            }
            meshFilters[i].sharedMesh.uv = uvsNew; // 수정된 UV를 다시 메쉬에 설정
        }

        // 모든 메쉬를 하나의 메쉬로 병합
        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combine, true, true);

        // 병합된 메쉬를 MeshFilter에 할당
        MeshFilter meshFilter = output.AddComponent<MeshFilter>();
        meshFilter.mesh = combinedMesh;

        // 새 MeshRenderer를 추가하고, URP의 Lit 셰이더를 사용하여 새로 생성된 머티리얼과 텍스처 아틀라스를 적용
        MeshRenderer meshRenderer = output.AddComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            Material newMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            newMaterial.mainTexture = atlas;

            Debug.Log("New material created with URP Lit shader: " + newMaterial.shader.name);
            Debug.Log("Texture assigned to material: " + (newMaterial.mainTexture != null ? "Yes" : "No"));

            meshRenderer.sharedMaterial = newMaterial;

            Debug.Log("Material assigned to MeshRenderer.");
        }

        // MeshCollider 추가
        MeshCollider meshCollider = output.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = combinedMesh;
        Debug.Log("MeshCollider assigned to the combined mesh.");

        // 원본 자식 오브젝트 제거(과정 확인을 위해, 현재는 제거가 아닌 비활성화로 둠)
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
        }

        // Combine 결과물을 자식으로 추가
        output.transform.parent = transform;
        output.transform.SetSiblingIndex(0);
    }

}
