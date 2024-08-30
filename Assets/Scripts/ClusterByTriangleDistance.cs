using System.Collections;
using System.Collections.Generic;
using CesiumForUnity;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR;

// ��� �鿡 ���� clustering & grouping�� �ϱ� ���� �ۼ��ߴ� �ڵ�
// ���� ����� �ٲ� ��üȭ�� ������ ������, ���� ����ȭ ������ ���� �� ���� �� ���� ���ܵ�.
public class ClusterByTriangleDistance : MonoBehaviour
{
    public CesiumGeoreference georeference;
    public GameObject targetObject;
    public Vector3 upVector;

    private int startTriangleIndex;
    private GameObject startGroup;
    public int degree = 15;
    public float epsilon = 0.1f; // DBSCAN���� ����� �Ÿ� ����
    public int minPts = 0; // DBSCAN���� ����� �ּ� ����Ʈ ��
    public float yWeight = 1;

    private List<int> clusteredTriangles = new List<int>();
    private List<Vector3> clusteredTrianglesCentroid = new List<Vector3>();

    private HashSet<GameObject> detectedTiles = new HashSet<GameObject>();

    private List<GameObject> totalGameObject = new List<GameObject>();


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                targetObject = hit.collider.gameObject;
                Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
                startTriangleIndex = hit.triangleIndex;

                // upVector�� ����Ѵ�.
                //upVector = CalculateUpVectorByECEFConvert(hit.point);

                /*
                ������ �𸣰����� CesiumGeoreference�� tile�� ������ normalVector�� ���غ��� �׻� (0,-1,0)�̴�.
                �ϴ� �������� upVector�� ������ �� �ְԲ� public���� �����صξ���.
                CesiumGeoreference ���� ������Ʈ���� �Ϲ����� ����Ƽ ������Ʈó�� (0,1,0)�� up �������� �����ϸ� �ȴ�.
                */



                // ��ֺ��Ͱ� upVector�� ����� �ﰢ������ clustering �Ѵ�. 
                // clusteredTriangles�� ���ŵȴ�.
                ExtractUp();


                // clusteredTriangles�� �ﰢ������ �ﰢ������ �Ÿ��� �������� groupȭ�Ѵ�.
                List<List<int>> groups = DBSCAN(clusteredTriangles);


                // �� �׷��� ������Ʈȭ �Ͽ� �ð�ȭ. (����Tile�� ��� startGroup�� ã�´�.)
                VisualizeCluster(groups);


                // startGroup�������� �浹üũ�� ���� ���� ��ǥ tile�� ã�� nextTiles�� �ִ´�.
                // �浹üũ�ϸ� Ȯ���ذ� �� �������� �ǴܵǴ� �׷��� �������� �ʴ´�.
                ;
                ;
                ;
                ;
                ;
            }


            // targetTiles�� nextTiles�� ����. nextTiles�� �ʱ�ȭ.
            // targetTiles�� �� ���ҿ� ���ؼ� ���ʴ�� 'Ŭ������ȭ->�׷�ȭ->�ð�ȭ->����Ÿ��ã��' �� �ݺ�
            // targetTiles�� ����ٸ� startGroup���� �浹üũ�� ���� Ȯ���ذ��� ���̻� �湮���� ���� Tile�� ã�� �� ���ٸ� ����

            ;
            ;
            ;
            ;
        }
    }

    void ExtractUp()
    {
        clusteredTriangles.Clear();
        clusteredTrianglesCentroid.Clear();

        Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
        Vector3[] localVertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Vector3[] vertices = new Vector3[localVertices.Length];

        // ���� ��ǥ�� ���� ��ǥ�� ��ȯ�Ͽ� ����
        for (int i = 0; i < localVertices.Length; i++)
        {
            vertices[i] = targetObject.transform.TransformPoint(localVertices[i]);
        }


        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i + 1]];
            Vector3 v2 = vertices[triangles[i + 2]];

            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            float angle = Vector3.Angle(normal, upVector);

            if (angle <= degree)
            {
                clusteredTriangles.Add(i / 3);
                clusteredTrianglesCentroid.Add((v0 + v1 + v2) / 3.0f);
            }
        }
    }


    List<List<int>> DBSCAN(List<int> clusteredTriangles)
    {
        List<List<int>> groups = new List<List<int>>();
        int[] labels = new int[clusteredTriangles.Count];
        int groupId = 0;

        for (int i = 0; i < clusteredTriangles.Count; i++)
        {
            if (labels[i] != 0) continue;

            List<int> neighbors = GetNeighbors(clusteredTriangles, i);
            if (neighbors.Count < minPts)
            {
                labels[i] = -1; // noise
                continue;
            }

            groupId++;
            List<int> group = new List<int>();
            ExpandCluster(clusteredTriangles, i, neighbors, groupId, labels, group);
            groups.Add(group);
        }

        // Ŭ�����Ϳ��� ���� �ﰢ�� �ε����� ��ȯ
        for (int i = 0; i < groups.Count; i++)
        {
            for (int j = 0; j < groups[i].Count; j++)
            {
                groups[i][j] = clusteredTriangles[groups[i][j]];
            }
        }

        return groups;
    }

    void ExpandCluster(List<int> clusteredTriangles, int index, List<int> neighbors, int groupId, int[] labels, List<int> group)
    {
        labels[index] = groupId;
        group.Add(index);

        for (int i = 0; i < neighbors.Count; i++)
        {
            int neighborIndex = neighbors[i];
            if (labels[neighborIndex] == -1)
            {
                labels[neighborIndex] = groupId;
                group.Add(neighborIndex);
            }
            else if (labels[neighborIndex] == 0)
            {
                labels[neighborIndex] = groupId;
                group.Add(neighborIndex);
                List<int> nextNeighbors = GetNeighbors(clusteredTriangles, neighborIndex);
                if (nextNeighbors.Count >= minPts)
                {
                    neighbors.AddRange(nextNeighbors);
                }
            }
        }
    }



    //DBSCAN�˰��򿡼� �ﰢ������ �Ÿ��� �������� �̿����� �Ǵ��ϴ� �Լ�
    List<int> GetNeighbors(List<int> clusteredTriangles, int index)
    {
        List<int> neighbors = new List<int>();
        Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;

        // ���� �ﰢ���� ���� ��ǥ
        Vector3[] indexTriangleVertices = new Vector3[3];
        int triangleIndex = clusteredTriangles[index];
        indexTriangleVertices[0] = vertices[triangles[triangleIndex * 3]];
        indexTriangleVertices[1] = vertices[triangles[triangleIndex * 3 + 1]];
        indexTriangleVertices[2] = vertices[triangles[triangleIndex * 3 + 2]];

        for (int i = 0; i < clusteredTriangles.Count; i++)
        {
            if (i == index) continue;

            Vector3[] currentTriangleVertices = new Vector3[3];
            int currentTriangleIndex = clusteredTriangles[i];
            currentTriangleVertices[0] = vertices[triangles[currentTriangleIndex * 3]];
            currentTriangleVertices[1] = vertices[triangles[currentTriangleIndex * 3 + 1]];
            currentTriangleVertices[2] = vertices[triangles[currentTriangleIndex * 3 + 2]];

            // �� �ﰢ������ �Ÿ��� epsilon���� �۴ٸ� �̿��ϴٰ� �Ǵ�
            float distance = TriangleDistance(indexTriangleVertices, currentTriangleVertices);
            if (distance <= epsilon)
            {
                neighbors.Add(i);
            }
        }
        
        return neighbors;
    }


    void VisualizeCluster_old(List<List<int>> groups)
    {
        GameObject[] groupObjects = new GameObject[groups.Count];
        float[] groupHeightAverages = new float[groups.Count];
        float minHeightAverage = float.MaxValue;
        float maxHeightAverage = float.MinValue;

        for (int k = 0; k < groups.Count; k++)
        {
            List<int> group = groups[k];

            List<Vector3> vertices = new List<Vector3>();
            List<int> newTriangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>(); // UV ��ǥ�� ������ ����Ʈ
            Dictionary<int, int> vertexMap = new Dictionary<int, int>();

            Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
            Vector3[] originalVertices = mesh.vertices;
            for (int i = 0; i < originalVertices.Length; i++)
            {
                // �� ������ ���� ��ǥ��� ��ȯ
                originalVertices[i] = targetObject.transform.TransformPoint(originalVertices[i]);
            }

            int[] originalTriangles = mesh.triangles;
            Vector2[] originalUVs = mesh.uv; // targetObject�� UV ��ǥ

            GameObject newObj = new GameObject("Cluster");

            foreach (int triangleIndex in group)
            {
                if (startGroup == null && triangleIndex == startTriangleIndex) //���� surface�� ã�� ����
                {
                    startGroup = newObj;
                    startGroup.name = "Start";
                }

                for (int i = 0; i < 3; i++)
                {
                    int originalIndex = originalTriangles[triangleIndex * 3 + i];

                    if (!vertexMap.ContainsKey(originalIndex))
                    {
                        vertexMap[originalIndex] = vertices.Count;
                        vertices.Add(originalVertices[originalIndex]); // ���� ��ǥ ���
                        uvs.Add(originalUVs[originalIndex]); // �ش� ������ UV ��ǥ�� �߰�
                    }
                    newTriangles.Add(vertexMap[originalIndex]);
                }
            }

            newObj.AddComponent<MeshFilter>();
            newObj.AddComponent<MeshRenderer>();

            Mesh newMesh = new Mesh();
            newMesh.vertices = vertices.ToArray();
            newMesh.triangles = newTriangles.ToArray();
            newMesh.uv = uvs.ToArray(); // �� �޽��� UV ���� ����
            newMesh.RecalculateNormals();

            newObj.GetComponent<MeshFilter>().mesh = newMesh;
            newObj.GetComponent<MeshRenderer>().material = targetObject.GetComponent<MeshRenderer>().material; // ���� ���͸����� ���
            newObj.transform.SetParent(this.transform);

            groupObjects[k] = newObj;

            // ������ ���̰� ��� ���
            float heightAverage = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                heightAverage += CalculateHeight(vertices[i], upVector);
            }
            heightAverage = heightAverage / vertices.Count;
            groupHeightAverages[k] = heightAverage;

            // ������ ���̰� ����� �ִ� �ּ� ����
            if (heightAverage < minHeightAverage)
            {
                minHeightAverage = heightAverage;
            }
            if (heightAverage > maxHeightAverage)
            {
                maxHeightAverage = heightAverage;
            }
        }

        // ����� õ���� ������ ���̰��� å��. (�Ʒ��� �ִ�~�ּҸ� 9:1�� �����ϴ� ���� �������� ��.)
        float heightThreshold = 0.1f * maxHeightAverage + 0.9f * minHeightAverage;

        for (int k = 0; k < groups.Count; k++)
        {
            //���� ����� ���ذ� �̸��� �׷쿡 ���ؼ�
            if (groupHeightAverages[k] < heightThreshold) 
            {
                //�̸��� Ground�� �����Ѵ�. 
                groupObjects[k].name = "Ground";
                groupObjects[k].SetActive(false);
            }

        }
    }
    // �� �׷츶�� ������Ʈ�� �����Ͽ� ����ȭ �ϴ� ���


    // �� �׷쿡 ���� ������ ����ְ�, �������� ground �ﰢ���� �����ϴ� ���
    void VisualizeCluster(List<List<int>> groups)
    {
        float[] groupHeightAverages = new float[groups.Count];
        float minHeightAverage = float.MaxValue;
        float maxHeightAverage = float.MinValue;

        Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
        int[] originalTriangles = mesh.triangles;
        Vector3[] originalVertices = mesh.vertices;
        for (int i = 0; i < originalVertices.Length; i++)
        {
            originalVertices[i] = targetObject.transform.TransformPoint(originalVertices[i]);
        }

        for (int k = 0; k < groups.Count; k++)
        {
            List<int> group = groups[k];

            HashSet<int> vertexIndices_Hash = new HashSet<int>();

            for (int i = 0; i < group.Count; i++) 
            {
                int triangleIndex = group[i];

                vertexIndices_Hash.Add(originalTriangles[triangleIndex * 3]);
                vertexIndices_Hash.Add(originalTriangles[triangleIndex * 3 + 1]);
                vertexIndices_Hash.Add(originalTriangles[triangleIndex * 3 + 2]);
            }

            List<int> vertexIndices = new List<int>(vertexIndices_Hash);

            // ������ ���̰� ��� ���
            float heightAverage = 0;
            for (int i = 0; i < vertexIndices_Hash.Count; i++)
            {
                heightAverage += CalculateHeight(originalVertices[vertexIndices[i]], upVector);
            }
            heightAverage = heightAverage / vertexIndices.Count;
            groupHeightAverages[k] = heightAverage;

            // ������ ���̰� ����� �ִ� �ּ� ����
            if (heightAverage < minHeightAverage)
            {
                minHeightAverage = heightAverage;
            }
            if (heightAverage > maxHeightAverage)
            {
                maxHeightAverage = heightAverage;
            }
        }


        // ����� õ���� ������ ���̰��� å��. (�Ʒ��� �ִ�~�ּҸ� 9:1�� �����ϴ� ���� �������� ��.)
        float heightThreshold = 0.1f * maxHeightAverage + 0.9f * minHeightAverage;


        // ���� ����� ���ذ� �̸��� �׷쿡 ���ؼ� �׷쿡 ���ԵǴ� �ﰢ������ ���� ������Ʈ���� ����
        List<int> trianglesToRemove = new List<int>();
        for (int k = 0; k < groups.Count; k++)
        {
            if (groupHeightAverages[k] < heightThreshold)
                trianglesToRemove.AddRange(groups[k]);
        }
        RemoveTrianglesByIndex(targetObject, trianglesToRemove);
    }


    public float CalculateHeight(Vector3 point, Vector3 upVector)
    {
        // upVector�� ���� ���ͷ� ����ȭ�մϴ�.
        Vector3 normalizedUpVector = upVector.normalized;

        // ���� ��ǥ�� upVector�� �����Ͽ� ���̸� ����մϴ�.
        float height = Vector3.Dot(point, normalizedUpVector);

        return height;
    }


    public void RemoveTrianglesByIndex(GameObject obj, List<int> group)
    {
        Mesh mesh = obj.GetComponent<MeshFilter>().mesh;

        // ���� �ﰢ�� �ε��� �迭�� �����ɴϴ�.
        List<int> triangles = new List<int>(mesh.triangles);

        // �ε����� ���ĵǾ� ���� ������ ������ �߻��� �� �����Ƿ�, �������� �����մϴ�.
        group.Sort((a, b) => b.CompareTo(a));

        // �ﰢ�� �ε������� group�� ���Ե� �ε����� �����մϴ�.
        foreach (int index in group)
        {
            // �ﰢ�� �ϳ��� 3���� �ε����� �����Ƿ�, 3���� �ε����� �����մϴ�.
            triangles.RemoveAt(index * 3);
            triangles.RemoveAt(index * 3);
            triangles.RemoveAt(index * 3);
        }

        // ���ŵ� �ﰢ�� �ε����� Mesh�� �ٽ� �����մϴ�.
        mesh.triangles = triangles.ToArray();

        // Mesh�� ������Ʈ�Ͽ� ��������� �����մϴ�.
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }












    public float TriangleDistance(Vector3[] tri1, Vector3[] tri2)
    {
        float minDistance = float.MaxValue;

        // Tri1�� ��
        Vector3[] edges1 = { tri1[1] - tri1[0], tri1[2] - tri1[1], tri1[0] - tri1[2] };

        // Tri2�� ��
        Vector3[] edges2 = { tri2[1] - tri2[0], tri2[2] - tri2[1], tri2[0] - tri2[2] };

        // �� ���� ����
        Vector3[] tri1Points = { tri1[0], tri1[1], tri1[2] };
        Vector3[] tri2Points = { tri2[0], tri2[1], tri2[2] };

        // ��� �� ���� �� �ִ� �Ÿ� ���
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                float distance = SegmentDistance(tri1Points[i], edges1[i], tri2Points[j], edges2[j]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }
        }

        return minDistance;
    }

    private static float SegmentDistance(Vector3 P, Vector3 d, Vector3 Q, Vector3 e)
    {
        Vector3 r = P - Q;
        float a = Vector3.Dot(d, d);
        float b = Vector3.Dot(d, e);
        float c = Vector3.Dot(e, e);
        float d1 = Vector3.Dot(d, r);
        float d2 = Vector3.Dot(e, r);

        float denominator = a * c - b * b;

        float s = (b * d2 - c * d1) / denominator;
        float t = (a * d2 - b * d1) / denominator;

        s = Mathf.Clamp01(s);
        t = Mathf.Clamp01(t);

        Vector3 closestPoint1 = P + s * d;
        Vector3 closestPoint2 = Q + t * e;

        return Vector3.Distance(closestPoint1, closestPoint2);
    }

    private Vector3 CalculateUpVectorByECEFConvert(Vector3 hitPoint) 
    {
        double3 worldPosition_d3 = new double3(hitPoint.x, hitPoint.y, hitPoint.z);

        // ���� ��ǥ�� ECEF ��ǥ�� ��ȯ
        double3 ecefPosition = georeference.TransformUnityPositionToEarthCenteredEarthFixed(worldPosition_d3);

        double3 upVector_d3 = new double3(-ecefPosition.x, ecefPosition.z, -ecefPosition.y);

        Vector3 upVector = new Vector3((float)upVector_d3.x, (float)upVector_d3.y, (float)upVector_d3.z);
        return upVector.normalized;
    }
}
