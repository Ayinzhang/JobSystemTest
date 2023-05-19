using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ManagePrefabs))]
public class ManagePrefabsTool : Editor
{
    public override void OnInspectorGUI()
    {
        ManagePrefabs managePrefabs = target as ManagePrefabs;
        managePrefabs.currentPrefab = (ManagePrefabs.PrefabSelect)EditorGUILayout.EnumPopup("预制体种类", managePrefabs.currentPrefab);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("生成预制体")) managePrefabs.CreatePrefabs();
        if (GUILayout.Button("销毁预制体")) managePrefabs.DestroyPrefabs();
        GUILayout.EndHorizontal();
    }
}
