using UnityEngine;
using UnityEditor;

public class CreateRedCube
{
    [MenuItem("Tools/Create Red Cube")]
    static void Create()
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "RedCube";
        cube.transform.position = Vector3.zero;

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.red;

        string matPath = "Assets/Editor/RedCubeMaterial.mat";
        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();

        cube.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        Selection.activeGameObject = cube;
        Undo.RegisterCreatedObjectUndo(cube, "Create Red Cube");

        Debug.Log("Red Cube created at origin.");
    }
}
