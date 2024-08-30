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

        // �θ��� ��ȯ ����� �̸� ������ ��
        Matrix4x4 parentTransform = transform.worldToLocalMatrix;

        // �� �޽��� �ؽ�ó�� ������ �迭�� ����
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            Material originalMaterial = meshRenderers[i].sharedMaterial;
            if (originalMaterial.HasProperty("_MainTex"))
            {
                textures[i] = (Texture2D)originalMaterial.mainTexture;
            }
            else
            {
                textures[i] = Texture2D.whiteTexture; // �ؽ�ó�� ���� ��� �⺻ ��� �ؽ�ó ���
            }
        }

        // �ؽ�ó ��Ʋ�� ����
        Texture2D atlas = new Texture2D(8192, 8192);
        Rect[] uvs = atlas.PackTextures(textures, 0, 8192);

        Debug.Log("Texture Atlas created with size: " + atlas.width + "x" + atlas.height);

        for (int i = 0; i < meshFilters.Length; i++)
        {
            combine[i].mesh = meshFilters[i].sharedMesh;

            // �ڽ� ������Ʈ�� ���� ��ǥ�� �����ϱ� ���� �θ��� ��ȯ�� ����
            combine[i].transform = parentTransform * meshFilters[i].transform.localToWorldMatrix;

            // ���ο� UV ������ ���� �迭 ����
            Vector2[] uvsOriginal = meshFilters[i].sharedMesh.uv;
            Vector2[] uvsNew = new Vector2[uvsOriginal.Length];

            // ���� UV�� ���ο� ��Ʋ�� UV�� ��ȯ
            for (int j = 0; j < uvsOriginal.Length; j++)
            {
                uvsNew[j] = new Vector2(
                    Mathf.Lerp(uvs[i].xMin, uvs[i].xMax, uvsOriginal[j].x),
                    Mathf.Lerp(uvs[i].yMin, uvs[i].yMax, uvsOriginal[j].y)
                );
            }
            meshFilters[i].sharedMesh.uv = uvsNew; // ������ UV�� �ٽ� �޽��� ����
        }

        // ��� �޽��� �ϳ��� �޽��� ����
        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combine, true, true);

        // ���յ� �޽��� MeshFilter�� �Ҵ�
        MeshFilter meshFilter = output.AddComponent<MeshFilter>();
        meshFilter.mesh = combinedMesh;

        // �� MeshRenderer�� �߰��ϰ�, URP�� Lit ���̴��� ����Ͽ� ���� ������ ��Ƽ����� �ؽ�ó ��Ʋ�󽺸� ����
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

        // MeshCollider �߰�
        MeshCollider meshCollider = output.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = combinedMesh;
        Debug.Log("MeshCollider assigned to the combined mesh.");

        // ���� �ڽ� ������Ʈ ����(���� Ȯ���� ����, ����� ���Ű� �ƴ� ��Ȱ��ȭ�� ��)
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
        }

        // Combine ������� �ڽ����� �߰�
        output.transform.parent = transform;
        output.transform.SetSiblingIndex(0);
    }

}
