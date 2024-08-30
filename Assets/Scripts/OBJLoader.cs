using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class OBJLoader
{
    private Dictionary<string, Material> materials;

    public GameObject Load(string objPath)
    {
        string objDirectory = Path.GetDirectoryName(objPath);
        string[] objLines = File.ReadAllLines(objPath);

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();
        List<int> triangles = new List<int>();

        GameObject obj = new GameObject(Path.GetFileNameWithoutExtension(objPath));
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        materials = new Dictionary<string, Material>();

        foreach (string line in objLines)
        {
            if (line.StartsWith("mtllib "))
            {
                // .mtl 파일 로드
                string mtlFileName = line.Substring(7).Trim();
                LoadMTLFile(Path.Combine(objDirectory, mtlFileName));
            }
            else if (line.StartsWith("v "))
            {
                string[] parts = line.Split(' ');
                vertices.Add(new Vector3(
                    float.Parse(parts[1]),
                    float.Parse(parts[2]),
                    float.Parse(parts[3])
                ));
            }
            else if (line.StartsWith("vt "))
            {
                string[] parts = line.Split(' ');
                uvs.Add(new Vector2(
                    float.Parse(parts[1]),
                    float.Parse(parts[2])
                ));
            }
            else if (line.StartsWith("vn "))
            {
                string[] parts = line.Split(' ');
                normals.Add(new Vector3(
                    float.Parse(parts[1]),
                    float.Parse(parts[2]),
                    float.Parse(parts[3])
                ));
            }
            else if (line.StartsWith("usemtl "))
            {
                // 재질 설정
                string materialName = line.Substring(7).Trim();
                if (materials.ContainsKey(materialName))
                {
                    mr.material = materials[materialName];
                }
            }
            else if (line.StartsWith("f "))
            {
                string[] parts = line.Split(' ');
                for (int i = 1; i < 4; i++)
                {
                    string[] vertexData = parts[i].Split('/');
                    triangles.Add(int.Parse(vertexData[0]) - 1);
                }
            }
        }

        Mesh mesh = new Mesh
        {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray()
        };

        if (normals.Count == vertices.Count)
            mesh.normals = normals.ToArray();

        if (uvs.Count == vertices.Count)
            mesh.uv = uvs.ToArray();

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        mf.mesh = mesh;

        return obj;
    }

    private void LoadMTLFile(string mtlPath)
    {
        if (!File.Exists(mtlPath))
        {
            Debug.LogError("MTL 파일을 찾을 수 없습니다: " + mtlPath);
            return;
        }

        string[] mtlLines = File.ReadAllLines(mtlPath);
        Material currentMaterial = null;
        string currentMaterialName = "";

        foreach (string line in mtlLines)
        {
            if (line.StartsWith("newmtl "))
            {
                if (currentMaterial != null && !materials.ContainsKey(currentMaterialName))
                {
                    materials.Add(currentMaterialName, currentMaterial);
                }

                currentMaterialName = line.Substring(7).Trim();
                currentMaterial = new Material(Shader.Find("Standard"));
            }
            else if (line.StartsWith("map_Kd "))
            {
                if (currentMaterial != null)
                {
                    string texturePath = line.Substring(7).Trim();
                    string fullPath = Path.Combine(Path.GetDirectoryName(mtlPath), texturePath);
                    if (File.Exists(fullPath))
                    {
                        Texture2D texture = LoadTexture(fullPath);
                        currentMaterial.mainTexture = texture;
                    }
                }
            }
        }

        if (currentMaterial != null && !materials.ContainsKey(currentMaterialName))
        {
            materials.Add(currentMaterialName, currentMaterial);
        }
    }

    private Texture2D LoadTexture(string texturePath)
    {
        byte[] fileData = File.ReadAllBytes(texturePath);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(fileData);
        return texture;
    }
}
