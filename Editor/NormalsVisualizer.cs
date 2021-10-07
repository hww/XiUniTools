using UnityEditor;
using UnityEngine;

//[CustomEditor(typeof(MeshFilter))]
public class NormalsVisualizer : Editor
{
    public static float noramlLength = 0.5f;

    private Mesh mesh;

    void OnEnable()
    {
        MeshFilter mf = target as MeshFilter;
        if (mf != null)
        {
            mesh = mf.sharedMesh;
        }
    }

    void OnSceneGUI()
    {
        if (mesh == null)
            return;
        if (mesh.vertices == null || mesh.vertices.Length == 0)
            return;
        if (mesh.normals == null || mesh.normals.Length == 0)
            return;
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            Handles.matrix = (target as MeshFilter).transform.localToWorldMatrix;
            Handles.color = Color.yellow;
            Handles.DrawLine(
                mesh.vertices[i],
                mesh.vertices[i] + mesh.normals[i] * noramlLength);
        }
    }
}