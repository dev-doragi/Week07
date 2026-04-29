#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class IncomeBlockPrefabGenerator
{
    private const string RootFolder = "Assets/02.Prefabs";
    private const string IncomeFolder = "Assets/02.Prefabs/Income";
    private const string BlockFolder = "Assets/02.Prefabs/Income/Blocks";
    private const string GeneratorSpritePath = "Assets/04.Art/Generator/Generator_1.png";

    [MenuItem("Tools/Income/Generate Income Block Prefabs")]
    public static void GenerateTetrominoPrefabs()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(IncomeFolder);
        EnsureFolder(BlockFolder);

        Sprite cellSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GeneratorSpritePath);

        Array types = Enum.GetValues(typeof(IncomeBlockType));
        for (int i = 0; i < types.Length; i++)
        {
            var type = (IncomeBlockType)types.GetValue(i);

            var go = new GameObject($"Income_{type}",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(IncomeBlockPiece));

            var piece = go.GetComponent<IncomeBlockPiece>();
            var serialized = new SerializedObject(piece);

            serialized.FindProperty("_blockType").enumValueIndex = (int)type;
            var spriteProp = serialized.FindProperty("_cellSprite");
            if (spriteProp != null)
                spriteProp.objectReferenceValue = cellSprite;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            string prefabPath = $"{BlockFolder}/Income_{type}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[IncomeBlockPrefabGenerator] Income block prefabs were generated under Assets/02.Prefabs/Income/Blocks");
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] split = folderPath.Split('/');
        string current = split[0];

        for (int i = 1; i < split.Length; i++)
        {
            string next = $"{current}/{split[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, split[i]);

            current = next;
        }
    }
}
#endif
