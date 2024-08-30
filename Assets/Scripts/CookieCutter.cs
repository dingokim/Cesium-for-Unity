using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.GraphicsBuffer;

public class CookieCutter : MonoBehaviour
{
    public GameObject polygonManager;

    private GameObject targetObject;

    float epsilon = 1e-6f;
    Vector3 epsilonVector = new Vector3(1e-3f, 1e-3f, 1e-3f);

    List<Vector3> originalVertices;
    List<int> originalTriangles;
    List<Vector2> originalUVs;

    List<Vector3> newVertices;
    List<int> newTriangles;
    List<Vector2> newUVs;

    Vector3[] polygon;

    Dictionary<int, int> ori_new;

    //�������� �� �������� y��������� RayCast�ؼ� ������ ������ ��ǥ, uv��
    Vector3[] surfaceIntersectionVertices;
    Vector2[] surfaceIntersectionUVs;
    //�������� �� �������� y��������� RayCast�ؼ� ������ ������ �����ϴ� mesh�ﰢ���� �ε���
    int[] triangleIndex;


    public void SetPolygon(Vector3[] newPolygon)
    {
        polygon = new Vector3[newPolygon.Length];
        Array.Copy(newPolygon, polygon, polygon.Length);
    }


    //��ŰĿ���� ������Ʈ�� �Ű������� �޾Ƽ� ���ϴ� ������ ����� �Լ��Դϴ�.
    public GameObject CookieCut(GameObject _targetObject, Vector3[] _polygon)
    {
        //�������� �������� üũ�մϴ�.
        polygon = _polygon;
        if (polygon == null || polygon.Length < 3)
        {
            Debug.Log("Polygon is not Ready");
            return null;
        }

        //Ÿ���� �������� üũ�մϴ�.
        targetObject = _targetObject;
        if (targetObject == null)
        {
            Debug.Log("Target is not Ready");
            return null;
        }


        //Ÿ���� �����մϴ�.
        GameObject output = Instantiate(targetObject);
        output.name = "Output";


        MeshFilter meshFilter = output.GetComponent<MeshFilter>();
        Mesh mesh = meshFilter.mesh;


        originalVertices = new List<Vector3>(mesh.vertices);
        originalTriangles = new List<int>(mesh.triangles);
        originalUVs = new List<Vector2>(mesh.uv);

        int oVL = originalVertices.Count;
        int pL = polygon.Length;

        ori_new = new Dictionary<int, int>();


        //������ ��ǥ���� ������ǥ��� ��ȯ�մϴ�. *������ �������� ��ġ���� ���ϱ����� epsilonVector�� ���մϴ�.
        for (int i = 0; i < oVL; i++) originalVertices[i] = output.transform.TransformPoint(originalVertices[i]) + epsilonVector;

        newVertices = new List<Vector3>();
        newTriangles = new List<int>();
        newUVs = new List<Vector2>();

        bool[] isInPolygon = new bool[oVL];


        //��� ������ ���Ͽ� polygon �������� ���θ� �迭�� �����մϴ�.
        for (int k = 0; k < oVL; k++)
        {
            Vector3 point = originalVertices[k];

            bool inside = false;

            for (int i = 0, j = pL - 1; i < pL; j = i++)
            {
                if ((polygon[i].z > point.z) != (polygon[j].z > point.z) &&
                    point.x < (polygon[j].x - polygon[i].x) * (point.z - polygon[i].z) / (polygon[j].z - polygon[i].z) + polygon[i].x)
                {
                    inside = !inside;
                }
            }

            isInPolygon[k] = inside;
        }





        // �ٰ��� ������ �� �������� y�� �������� RayCast�Ͽ� ������ �ﰢ���� ������ ��ǥ�� �����մϴ�.
        triangleIndex = new int[pL];
        surfaceIntersectionVertices = new Vector3[pL];
        surfaceIntersectionUVs = new Vector2[pL];

        Collider targetCollider = output.GetComponent<Collider>();

        for (int i = 0; i < pL; i++)
        {
            Vector3 currentVertex = polygon[i];
            Ray ray = new Ray(new Vector3(currentVertex.x, 1000, currentVertex.z), Vector3.down);
            RaycastHit hit;

            if (targetCollider.Raycast(ray, out hit, Mathf.Infinity))
            {
                // ��Ʈ�� ������ ��ǥ, uv���� ����
                surfaceIntersectionVertices[i] = hit.point;
                surfaceIntersectionUVs[i] = hit.textureCoord;

                MeshCollider meshCollider = hit.collider as MeshCollider;
                if (meshCollider != null)
                {
                    // ��Ʈ�� �ﰢ���� �ε����� ����()
                    triangleIndex[i] = hit.triangleIndex;
                }
            }
            else
            {
                triangleIndex[i] = -1;
                surfaceIntersectionVertices[i] = new Vector3(0, -1, 0);
                Debug.Log($"No hit point found at ({currentVertex.x}, ~, {currentVertex.z})");
            }
        }






        for (int i = 0; i < originalTriangles.Count; i += 3)//i/3�� ���� ó������ �ﰢ���� ��ȣ�� �ǹ�
        {
            int i1 = originalTriangles[i];
            int i2 = originalTriangles[i + 1];
            int i3 = originalTriangles[i + 2];

            Vector3 v1 = originalVertices[i1];
            Vector3 v2 = originalVertices[i2];
            Vector3 v3 = originalVertices[i3];

            bool v1Inside = isInPolygon[i1];
            bool v2Inside = isInPolygon[i2];
            bool v3Inside = isInPolygon[i3];

            // �� �� ��� ������ ���ο� ��ġ�� ���
            if (v1Inside && v2Inside && v3Inside)
            {
                TryAddVertexAndTriangle(i1);
                TryAddVertexAndTriangle(i2);
                TryAddVertexAndTriangle(i3);
            }
            // �� �� ��� ������ �ܺο� ��ġ�� ���
            else if (!v1Inside && !v2Inside && !v3Inside)
            {
                continue;
            }
            else
            {
                Vector3 p1, p2;
                int p1Index, p2Index;

                int p1q1_index, p1q2_index;
                int p2q1_index, p2q2_index;

                //���ܿ�
                if (v1Inside && !v2Inside && !v3Inside)
                {
                    GetPointAndIndex(i1, i2, out p1Index, out p1, out p1q1_index, out p1q2_index);
                    GetPointAndIndex(i1, i3, out p2Index, out p2, out p2q1_index, out p2q2_index);
                    if (p1q2_index != p2q1_index)
                    {
                        TryAddVertexAndTriangle(i1);
                        TryAddVertexAndTriangle(p1Index);
                        TryAddVertexAndTriangle(p2Index);
                    }
                    else if (p1q2_index == p2q1_index)
                    {
                        bool vertexInMesh_Case = false;
                        //��� �������� �������� ���� "�� ���������� y�� �������� �� ����"�� ���� ó������ �޽� �ﰢ�� ������ ���, ������ �߰��ϰ�, ������ p1, p2�� �̷���� �ﰢ���� �߰��մϴ�.
                        for (int j = 0; j < pL; j++)
                        {
                            if (triangleIndex[j] == -1) continue;

                            if (i == triangleIndex[j] * 3)
                            {
                                originalVertices.Add(surfaceIntersectionVertices[j]);
                                int newIndex = originalVertices.Count - 1;
                                originalUVs.Add(surfaceIntersectionUVs[j]);

                                TryAddVertexAndTriangle(i1);
                                TryAddVertexAndTriangle(newIndex);
                                TryAddVertexAndTriangle(p2Index);

                                TryAddVertexAndTriangle(i1);
                                TryAddVertexAndTriangle(p1Index);
                                TryAddVertexAndTriangle(newIndex);

                                vertexInMesh_Case = true;
                                break;
                            }
                        }

                        //���������� y�� �������� �� ������ ���� ó������ �ﰢ�� �ۿ� �����ϴ� ���, "�� �ﰢ���� ��������������� y��������� �� ������ ����", p3, p4�� ���ؼ� �簢���� �ﰢ���� �߰��մϴ�.
                        if (!vertexInMesh_Case)
                        {
                            int p3Index, p4Index;
                            Vector3 p3, p4;

                            GetIntersection(v2, v3, polygon[p1q1_index], polygon[p1q2_index], out p3);
                            GetIntersection(v2, v3, polygon[p2q1_index], polygon[p2q2_index], out p4);

                            originalVertices.Add(p3);
                            p3Index = originalVertices.Count - 1;
                            originalUVs.Add(CalculateUV(i2, i3, p3Index));

                            originalVertices.Add(p4);
                            p4Index = originalVertices.Count - 1;
                            originalUVs.Add(CalculateUV(i2, i3, p4Index));

                            TryAddVertexAndTriangle(i1);
                            TryAddVertexAndTriangle(p1Index);
                            TryAddVertexAndTriangle(p2Index);

                            TryAddVertexAndTriangle(p1Index);
                            TryAddVertexAndTriangle(p3Index);
                            TryAddVertexAndTriangle(p4Index);

                            TryAddVertexAndTriangle(p1Index);
                            TryAddVertexAndTriangle(p4Index);
                            TryAddVertexAndTriangle(p2Index);
                        }
                    }
                }
                //�ܳ���
                else if (!v1Inside && v2Inside && !v3Inside)
                {
                    GetPointAndIndex(i2, i3, out p1Index, out p1, out p1q1_index, out p1q2_index);
                    GetPointAndIndex(i2, i1, out p2Index, out p2, out p2q1_index, out p2q2_index);

                    if (p1q2_index != p2q1_index)
                    {
                        TryAddVertexAndTriangle(i2);
                        TryAddVertexAndTriangle(p1Index);
                        TryAddVertexAndTriangle(p2Index);
                    }
                    else if (p1q2_index == p2q1_index)
                    {
                        bool vertexInMesh_Case = false;

                        //��� �������� �������� ���� "Ư�� ���������� y�� �������� �� ����"�� ���� ó������ �޽� �ﰢ�� ������ ���, ������ �߰��ϰ�, ������ p1, p2�� �̷���� �ﰢ���� �߰��մϴ�.
                        for (int j = 0; j < pL; j++)
                        {
                            if (triangleIndex[j] == -1) continue;

                            if (i == triangleIndex[j] * 3)
                            {
                                originalVertices.Add(surfaceIntersectionVertices[j]);
                                int newIndex = originalVertices.Count - 1;
                                originalUVs.Add(surfaceIntersectionUVs[j]);

                                TryAddVertexAndTriangle(i2);
                                TryAddVertexAndTriangle(newIndex);
                                TryAddVertexAndTriangle(p2Index);

                                TryAddVertexAndTriangle(i2);
                                TryAddVertexAndTriangle(p1Index);
                                TryAddVertexAndTriangle(newIndex);

                                vertexInMesh_Case = true;
                                break;

                            }
                        }

                        //���������� y�� �������� �� ������ ���� ó������ �ﰢ�� �ۿ� �����ϴ� ���, "�� �ﰢ���� ��������������� y��������� �� ������ ����", p3, p4�� ���ؼ� �簢���� �ﰢ���� �߰��մϴ�.
                        if (!vertexInMesh_Case)
                        {
                            int p3Index, p4Index;
                            Vector3 p3, p4;

                            GetIntersection(v3, v1, polygon[p1q1_index], polygon[p1q2_index], out p3);
                            GetIntersection(v3, v1, polygon[p2q1_index], polygon[p2q2_index], out p4);

                            originalVertices.Add(p3);
                            p3Index = originalVertices.Count - 1;
                            originalUVs.Add(CalculateUV(i3, i1, p3Index));

                            originalVertices.Add(p4);
                            p4Index = originalVertices.Count - 1;
                            originalUVs.Add(CalculateUV(i3, i1, p4Index));

                            TryAddVertexAndTriangle(i2);
                            TryAddVertexAndTriangle(p1Index);
                            TryAddVertexAndTriangle(p2Index);

                            TryAddVertexAndTriangle(p1Index);
                            TryAddVertexAndTriangle(p3Index);
                            TryAddVertexAndTriangle(p4Index);

                            TryAddVertexAndTriangle(p1Index);
                            TryAddVertexAndTriangle(p4Index);
                            TryAddVertexAndTriangle(p2Index);
                        }
                    }
                }
                //�ܿܳ�
                else if (!v1Inside && !v2Inside && v3Inside)
                {
                    GetPointAndIndex(i3, i1, out p1Index, out p1, out p1q1_index, out p1q2_index);
                    GetPointAndIndex(i3, i2, out p2Index, out p2, out p2q1_index, out p2q2_index);

                    if (p1q2_index != p2q1_index)
                    {
                        TryAddVertexAndTriangle(i3);
                        TryAddVertexAndTriangle(p1Index);
                        TryAddVertexAndTriangle(p2Index);
                    }
                    else if (p1q2_index == p2q1_index)
                    {
                        bool vertexInMesh_Case = false;

                        //��� �������� �������� ���� "Ư�� ���������� y�� �������� �� ����"�� ���� ó������ �޽� �ﰢ�� ������ ���, ������ �߰��ϰ�, ������ p1, p2�� �̷���� �ﰢ���� �߰��մϴ�.
                        for (int j = 0; j < pL; j++)
                        {
                            if (triangleIndex[j] == -1) continue;

                            if (i == triangleIndex[j] * 3)
                            {
                                originalVertices.Add(surfaceIntersectionVertices[j]);
                                int newIndex = originalVertices.Count - 1;
                                originalUVs.Add(surfaceIntersectionUVs[j]);

                                TryAddVertexAndTriangle(i3);
                                TryAddVertexAndTriangle(newIndex);
                                TryAddVertexAndTriangle(p2Index);

                                TryAddVertexAndTriangle(i3);
                                TryAddVertexAndTriangle(p1Index);
                                TryAddVertexAndTriangle(newIndex);

                                vertexInMesh_Case = true;
                                break;
                            }
                        }

                        //���������� y�� �������� �� ������ ���� ó������ �ﰢ�� �ۿ� �����ϴ� ���, "�� �ﰢ���� ��������������� y��������� �� ������ ����", p3, p4�� ���ؼ� �簢���� �ﰢ���� �߰��մϴ�.
                        if (!vertexInMesh_Case)
                        {
                            int p3Index, p4Index;
                            Vector3 p3, p4;

                            GetIntersection(v1, v2, polygon[p1q1_index], polygon[p1q2_index], out p3);
                            GetIntersection(v1, v2, polygon[p2q1_index], polygon[p2q2_index], out p4);

                            originalVertices.Add(p3);
                            p3Index = originalVertices.Count - 1;
                            originalUVs.Add(CalculateUV(i1, i2, p3Index));

                            originalVertices.Add(p4);
                            p4Index = originalVertices.Count - 1;
                            originalUVs.Add(CalculateUV(i1, i2, p4Index));

                            TryAddVertexAndTriangle(i3);
                            TryAddVertexAndTriangle(p1Index);
                            TryAddVertexAndTriangle(p2Index);

                            TryAddVertexAndTriangle(p1Index);
                            TryAddVertexAndTriangle(p3Index);
                            TryAddVertexAndTriangle(p4Index);

                            TryAddVertexAndTriangle(p1Index);
                            TryAddVertexAndTriangle(p4Index);
                            TryAddVertexAndTriangle(p2Index);
                        }
                    }
                }
                //������
                else if (v1Inside && v2Inside && !v3Inside)
                {
                    GetPointAndIndex(i2, i3, out p1Index, out p1, out p1q1_index, out p1q2_index);
                    GetPointAndIndex(i1, i3, out p2Index, out p2, out p2q1_index, out p2q2_index);

                    if (p1q2_index != p2q1_index)
                    {
                        TryAddVertexAndTriangle(i1);
                        TryAddVertexAndTriangle(p1Index);
                        TryAddVertexAndTriangle(p2Index);

                        TryAddVertexAndTriangle(i1);
                        TryAddVertexAndTriangle(i2);
                        TryAddVertexAndTriangle(p1Index);
                    }
                    else
                    {
                        originalVertices.Add(surfaceIntersectionVertices[p1q2_index]);
                        int p3index = originalVertices.Count - 1;
                        originalUVs.Add(surfaceIntersectionUVs[p1q2_index]);

                        TryAddVertexAndTriangle(p3index);
                        TryAddVertexAndTriangle(i2);
                        TryAddVertexAndTriangle(p1Index);

                        TryAddVertexAndTriangle(p3index);
                        TryAddVertexAndTriangle(i1);
                        TryAddVertexAndTriangle(i2);

                        TryAddVertexAndTriangle(p3index);
                        TryAddVertexAndTriangle(p2Index);
                        TryAddVertexAndTriangle(i1);
                    }
                }
                //���ܳ�
                else if (v1Inside && !v2Inside && v3Inside)
                {
                    GetPointAndIndex(i1, i2, out p1Index, out p1, out p1q1_index, out p1q2_index);
                    GetPointAndIndex(i3, i2, out p2Index, out p2, out p2q1_index, out p2q2_index);

                    if (p1q2_index != p2q1_index)
                    {
                        TryAddVertexAndTriangle(i3);
                        TryAddVertexAndTriangle(p1Index);
                        TryAddVertexAndTriangle(p2Index);

                        TryAddVertexAndTriangle(i3);
                        TryAddVertexAndTriangle(i1);
                        TryAddVertexAndTriangle(p1Index);
                    }
                    else
                    {
                        originalVertices.Add(surfaceIntersectionVertices[p1q2_index]);
                        int p3index = originalVertices.Count - 1;
                        originalUVs.Add(surfaceIntersectionUVs[p1q2_index]);

                        TryAddVertexAndTriangle(p3index);
                        TryAddVertexAndTriangle(i1);
                        TryAddVertexAndTriangle(p1Index);

                        TryAddVertexAndTriangle(p3index);
                        TryAddVertexAndTriangle(i3);
                        TryAddVertexAndTriangle(i1);

                        TryAddVertexAndTriangle(p3index);
                        TryAddVertexAndTriangle(p2Index);
                        TryAddVertexAndTriangle(i3);
                    }
                }
                //�ܳ���
                else if (!v1Inside && v2Inside && v3Inside)
                {
                    GetPointAndIndex(i3, i1, out p1Index, out p1, out p1q1_index, out p1q2_index);
                    GetPointAndIndex(i2, i1, out p2Index, out p2, out p2q1_index, out p2q2_index);

                    if (p1q2_index != p2q1_index)
                    {
                        TryAddVertexAndTriangle(i2);
                        TryAddVertexAndTriangle(p1Index);
                        TryAddVertexAndTriangle(p2Index);

                        TryAddVertexAndTriangle(i2);
                        TryAddVertexAndTriangle(i3);
                        TryAddVertexAndTriangle(p1Index);
                    }
                    else
                    {
                        originalVertices.Add(surfaceIntersectionVertices[p1q2_index]);
                        int p3index = originalVertices.Count - 1;
                        originalUVs.Add(surfaceIntersectionUVs[p1q2_index]);

                        TryAddVertexAndTriangle(p3index);
                        TryAddVertexAndTriangle(i3);
                        TryAddVertexAndTriangle(p1Index);

                        TryAddVertexAndTriangle(p3index);
                        TryAddVertexAndTriangle(i2);
                        TryAddVertexAndTriangle(i3);

                        TryAddVertexAndTriangle(p3index);
                        TryAddVertexAndTriangle(p2Index);
                        TryAddVertexAndTriangle(i2);
                    }
                }
            }
        }

        mesh.Clear();

        int newVerticesLength = newVertices.Count;
        Vector3[] worldVertices = newVertices.ToArray();
        Vector3[] localVertices = new Vector3[newVerticesLength];
        for (int i = 0; i < newVerticesLength; i++) localVertices[i] = output.transform.InverseTransformPoint(worldVertices[i]) - epsilonVector;

        mesh.vertices = localVertices;

        mesh.triangles = newTriangles.ToArray();

        mesh.uv = newUVs.ToArray();

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;

        //MeshCollider�� �� mesh ����� �°� �ٲߴϴ�.
        MeshCollider collider = output.GetComponent<MeshCollider>();
        collider.sharedMesh = null;
        collider.sharedMesh = mesh;

        return output;
    }


    private void GetPointAndIndex(int index1, int index2, out int pIndex, out Vector3 p, out int q1_index, out int q2_index)
    {

        p = FindIntersection(index1, index2, out q1_index, out q2_index);

        //ã�� ������ originalVertices�� �߰�, �߰��� ���� ���� �ε��� ��ȯ
        originalVertices.Add(p);
        pIndex = originalVertices.Count - 1;

        //uv���� �����Ͽ�  originalUVs�� �߰�
        originalUVs.Add(CalculateUV(index1, index2, pIndex));

    }

    //������ �� ������ ���л��� ������ (originalVertices����)index�� �־����� �� ���л��� ������ uv���� ����ϴ� �Լ��Դϴ�.
    private Vector2 CalculateUV(int i1, int i2, int pi)
    {
        Vector3 v1 = originalVertices[i1];
        Vector3 v2 = originalVertices[i2];
        Vector2 uv1 = originalUVs[i1];
        Vector2 uv2 = originalUVs[i2];
        Vector3 p = originalVertices[pi];
        float t = Vector3.Distance(p, v1) / Vector3.Distance(v2, v1);
        Vector2 uvp = uv1 + t * (uv2 - uv1);
        return uvp;


    }


    //���� v1, v2�� �մ� ���а� ������ �������� ������ ������ ��, �� ������ ��ǥ�� ã�� �Լ��Դϴ�.
    private Vector3 FindIntersection(int i1, int i2, out int q1_index, out int q2_index)
    {
        Vector3 v1 = originalVertices[i1];
        Vector3 v2 = originalVertices[i2];
        Vector3 q1;
        Vector3 q2;
        Vector3 intersection;


        int pL = polygon.Length;
        for (int i = 0; i < pL; i++)
        {
            q1 = polygon[i];
            q2 = (i < pL - 1) ? polygon[i + 1] : polygon[0];

            if (GetIntersection(v1, v2, q1, q2, out intersection))
            {
                q1_index = i;
                q2_index = (i < pL - 1) ? (i + 1) : 0;
                return intersection;
            }
        }

        Debug.Log("!!! ���� : ������ ������ ���߽��ϴ�. �ӽ÷� v1�� ��� ����մϴ�. !!!");
        q1_index = -1;
        q2_index = -2;
        return v1;
    }

    private bool GetIntersection(Vector3 v1, Vector3 v2, Vector3 q1, Vector3 q2, out Vector3 intersection)
    {
        float A1, B1, C1, A2, B2, C2, delta, x, y, z;
        intersection = Vector3.zero;

        A1 = v2.z - v1.z;
        B1 = v1.x - v2.x;
        C1 = A1 * v1.x + B1 * v1.z;

        A2 = q2.z - q1.z;
        B2 = q1.x - q2.x;
        C2 = A2 * q1.x + B2 * q1.z;

        delta = A1 * B2 - A2 * B1;

        if (delta == 0)
            return false;

        x = (B2 * C1 - B1 * C2) / delta;
        z = (A1 * C2 - A2 * C1) / delta;
        if (Math.Abs(v1.x - x) < epsilon || Math.Abs(v1.x - v2.x) < epsilon)
            y = v1.y - (v1.z - z) * (v1.y - v2.y) / (v1.z - v2.z);
        else
            y = v1.y - (v1.x - x) * (v1.y - v2.y) / (v1.x - v2.x);
        intersection = new Vector3(x, y, z);


        if (IsBetween(v1, v2, intersection) && IsBetween(q1, q2, intersection))
            return true;
        else
            return false;
    }



    private bool IsBetween(Vector3 a, Vector3 b, Vector3 c)
    {
        return (Math.Min(a.x, b.x) - c.x <= epsilon && c.x - Math.Max(a.x, b.x) <= epsilon &&
                Math.Min(a.z, b.z) - c.z <= epsilon && c.z - Math.Max(a.z, b.z) <= epsilon);
    }


    public static Vector3 Cross(Vector3 a, Vector3 b)
    {
        return new Vector3(
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x
        );
    }
    public static float Dot(Vector3 a, Vector3 b)
    {
        return a.x * b.x + a.y * b.y + a.z * b.z;
    }



    //�������� �� ���������� y��� �����ϰ� �� ������ mesh�� �������� Ȯ���ϰ�, �Ű������� ���� ������ ��ǥ�� �����ϴ� �Լ��Դϴ�.
    public static bool IsLineIntersectingMesh(Vector3 linePoint, Vector3[] mesh, out Vector3 intersection)
    {
        if (mesh.Length != 3)
        {
            throw new ArgumentException("Polygon must be defined by exactly 3 vertices.");
        }

        Vector3 v0 = mesh[0];
        Vector3 v1 = mesh[1];
        Vector3 v2 = mesh[2];

        //����� ��ֺ��� ���
        Vector3 d0 = v1 - v0;
        Vector3 d1 = v2 - v0;
        Vector3 normal = Vector3.Cross(d0, d1);

        // ������ ����� ������ ���
        if (Math.Abs(normal.y) < float.Epsilon)
        {
            Debug.Log("������ �޽��� �����Ͽ� ������θ� �Ǵ��� �� �����ϴ�.");
            intersection = new Vector3();
            return false;
        }


        float d = Vector3.Dot(normal, v0);
        float t = (d - Vector3.Dot(normal, linePoint)) / normal.y;

        //���� �������� ���������κ��� �������� �� ������ ������ ���� ���
        intersection = new Vector3(linePoint.x, t, linePoint.z);

        // �� ���� mesh ���ο� �����ϴ��� Ȯ��
        if (IsPointInTriangle(intersection, v0, v1, v2))
        {
            Debug.Log($"�޽��� �������� �������� �����Ѵ�. ���� : {intersection}");
            return true;
        }
        else
        {
            return false;
        }
    }

    //������ ���� ������ ���ο� ��ġ�ϴ��� Ȯ���ϴ� �Լ��Դϴ�.
    private static bool IsPointInTriangle(Vector3 p, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        Vector3 v0 = p2 - p0;
        Vector3 v1 = p1 - p0;
        Vector3 v2 = p - p0;

        float dot00 = Vector3.Dot(v0, v0);
        float dot01 = Vector3.Dot(v0, v1);
        float dot02 = Vector3.Dot(v0, v2);
        float dot11 = Vector3.Dot(v1, v1);
        float dot12 = Vector3.Dot(v1, v2);

        float invDenom = 1 / (dot00 * dot11 - dot01 * dot01);
        float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

        return (u >= 0) && (v >= 0) && (u + v < 1);
    }

    //�� ���Ͱ� ������ ���մϴ�. (float���꿡�� �̼��� ������ �߻��ϴ��� ���ٰ� �Ǵ��ϱ� ���� epsilon�� ����Ͽ� ���մϴ�.)
    private bool isEqual(Vector3 v1, Vector3 v2)
    {
        if (MathF.Abs(v1.x - v2.x) < epsilon && MathF.Abs(v1.y - v2.y) < epsilon && MathF.Abs(v1.z - v2.z) < epsilon) return true;
        else return false;
    }

    private void TryAddVertexAndTriangle(int index)
    {
        if (ori_new.ContainsKey(index))
        {
            newTriangles.Add(ori_new[index]);
            return;
        }
        else
        {
            ori_new.Add(index, newVertices.Count);
            newTriangles.Add(newVertices.Count);
            newVertices.Add(originalVertices[index]);
            newUVs.Add(originalUVs[index]);
        }

    }

}
